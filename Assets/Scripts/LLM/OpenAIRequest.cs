using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UI.Cues;
using UI.Cues.WarningSystem;

public class OpenAIRequest : MonoBehaviour
{
    public static OpenAIRequest Instance;

    [Header("Legacy (Do Not Use)")]
    public string apiUrl = "";
    public string apiKey = "";

    [Header("LLM Backend")]
    [SerializeField, Range(1, 3)] private int maxRetries = 2;
    [SerializeField] private int requestTimeoutSeconds = 45;

    [Header("Conversation Settings")]
    [SerializeField] private string currentScenario = "";
    public string lostResponse = "umm... fast... uh... fast... ";
    public float maxSpeechSpeed = 200f;

    // Components
    private CharacterAnimationController animationController;
    private EmotionController emotionController;
    [SerializeField] private GameObject cueControllerObject;
    private CueController cueController;

    [Header("Cue Warning System")]
    [SerializeField] private CueSkipGuard cueSkipGuard;
    [SerializeField] private TargetButtonUI targetButtonUI;

    // Internal state
    private const string DialoguePath = "/llm-dialogue";
    private const string ScoringPath = "/llm-scoring";
    private float currentSpeechSpeed;
    private string basePath;
    public string CurrentUserId { get; private set; }

    private readonly List<Dictionary<string, string>> chatMessages = new List<Dictionary<string, string>>();
    private string currentPatientResponse = "";
    private string pendingNurseMessage = "";
    private string sessionId = "";
    private int turnIndex = 0;

    [Serializable]
    private class StructuredDialogueResponse
    {
        public string responseText;
        public int emotionCode;
        public int motionCode;
    }

    [Serializable]
    private class DialogueRequestPayload
    {
        public List<Dictionary<string, string>> messages;
        public DialogueMetadata metadata;
    }

    [Serializable]
    private class DialogueMetadata
    {
        public int turnIndex;
        public string client;
    }

    [Serializable]
    private class ErrorEnvelope
    {
        public string error;
        public string requestId;
        public bool retryable;
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        TryResolveEmotionController();

        if (cueControllerObject == null)
            Debug.LogWarning("OpenAIRequest: cueControllerObject is not assigned. Skipping cue UI setup.");

        cueController = cueControllerObject != null ? cueControllerObject.GetComponent<CueController>() : null;

        if (emotionController == null)
            Debug.LogError("OpenAIRequest: EmotionController not found on this object, children, or parent.");

        if (cueController == null)
            Debug.LogError("CueController component not found on the UI GameObject.");

        if (cueSkipGuard == null)
            Debug.LogWarning("OpenAIRequest: cueSkipGuard not assigned. Cue order warnings will not fire.");

        if (targetButtonUI == null)
            Debug.LogWarning("OpenAIRequest: targetButtonUI not assigned. Target word will be empty.");

        RuntimeSessionContext.Changed += HandleRuntimeContextChanged;
        ApplyRuntimeContextFromHost();

        if (!string.IsNullOrEmpty(currentScenario))
            InitializeChat();
        else
            Debug.LogWarning("[OpenAIRequest] Waiting for runtime session context from host app.");
    }

    private void OnDestroy()
    {
        RuntimeSessionContext.Changed -= HandleRuntimeContextChanged;
    }

    private bool TryResolveEmotionController()
    {
        if (emotionController != null)
            return true;

        emotionController = GetComponent<EmotionController>();
        if (emotionController == null)
            emotionController = GetComponentInChildren<EmotionController>(true);
        if (emotionController == null)
            emotionController = GetComponentInParent<EmotionController>();

        return emotionController != null;
    }

    public void ApplyLoginContext(string userId, int simulationLevel)
    {
        CurrentUserId = userId;
        sessionId = $"{CurrentUserId}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        currentScenario = RuntimeSessionContext.HasContext ? RuntimeSessionContext.ContextLabel : currentScenario;
        if (string.IsNullOrWhiteSpace(currentScenario))
            currentScenario = "assignment-runtime";
        turnIndex = 0;
        InitializeChat();

        if (ScoreManager.Instance != null)
            ScoreManager.Instance.Initialize(currentScenario);

        Debug.Log($"[OpenAIRequest] Legacy local context applied. userID={CurrentUserId}, contextLabel={currentScenario}");
    }

    private void InitializeChat()
    {
        chatMessages.Clear();
        currentPatientResponse = "";
        Debug.Log("[OpenAIRequest] Chat initialized without system message. Backend owns system prompt.");
    }

    public void ReceiveNurseTranscription(string transcribedText, float speechWpm)
    {
        Debug.LogError($"[OpenAIRequest] ReceiveNurseTranscription text=\"{transcribedText}\" wpm={speechWpm:0.##}");

        // Check cue order before sending to GPT
        if (cueSkipGuard != null)
        {
            string targetWord = targetButtonUI != null ? targetButtonUI.CurrentTargetWord : "";
            cueSkipGuard.CheckStudentUtterance(transcribedText, targetWord);
            Debug.Log($"[OpenAIRequest] CueSkipGuard checked: text=\"{transcribedText}\" target=\"{targetWord}\"");
        }

        NurseResponds(transcribedText, speechWpm);
    }

    private void NurseResponds(string nurseMessage, float speechWpm)
    {
        if (string.IsNullOrWhiteSpace(nurseMessage))
        {
            Debug.LogWarning("[OpenAIRequest] Empty nurse message ignored.");
            return;
        }

        if (!RuntimeSessionContext.HasRuntimeToken)
        {
            Debug.LogError("[OpenAIRequest] Cannot send dialogue request without a runtime token.");
            HandlePatientResponse("I... I am not sure...", 0, 0);
            return;
        }

        Debug.LogError($"[OpenAIRequest] NurseResponds accepted message=\"{nurseMessage.Trim()}\" chatCountBefore={chatMessages.Count}");

        if (chatMessages.Count == 0 && !string.IsNullOrEmpty(currentScenario))
            InitializeChat();

        chatMessages.Add(new Dictionary<string, string>
        {
            { "role", "user" },
            { "content", nurseMessage.Trim() }
        });
        PrintChatMessage(chatMessages);
        pendingNurseMessage = nurseMessage.Trim();
        Debug.LogError($"[OpenAIRequest] pendingNurseMessage set. turnIndex(before increment)={turnIndex}");

        turnIndex++;
        Debug.LogError($"[OpenAIRequest] turnIndex incremented to {turnIndex}");

        if (speechWpm > maxSpeechSpeed)
        {
            Debug.Log($"[OpenAIRequest] Speech too fast ({speechWpm} > {maxSpeechSpeed}). Using fallback text.");
            HandlePatientResponse(lostResponse, 5, 1);
        }
        else
        {
            StartCoroutine(PostDialogueRequest());
        }

    }

    private IEnumerator PostDialogueRequest()
    {
        string requestBody = BuildDialogueRequestBody();
        if (!ApiConfigProvider.TryBuildBackendUrl(DialoguePath, out string requestUrl))
        {
            Debug.LogError("[OpenAIRequest] API environment config is missing or incomplete.");
            HandlePatientResponse("I... I am not sure...", 0, 0);
            yield break;
        }

        int attempts = Mathf.Max(1, maxRetries);

        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            using (var request = new UnityWebRequest(requestUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(requestBody));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = requestTimeoutSeconds;
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accept", "application/json");
                request.SetRequestHeader("X-Request-ID", $"{BuildRequestIdPrefix()}-turn-{turnIndex}-try-{attempt}");

                if (!RuntimeSessionContext.ApplyAuthorization(request, "OpenAIRequest"))
                {
                    HandlePatientResponse("I... I am not sure...", 0, 0);
                    yield break;
                }

                yield return request.SendWebRequest();

                bool success = request.result == UnityWebRequest.Result.Success && request.responseCode == 200;
                if (success)
                {
                    if (TryExtractDialogueResponse(request.downloadHandler.text, out string ttsText, out int emotionCode, out int motionCode))
                    {
                        HandlePatientResponse(ttsText, emotionCode, motionCode);
                    }
                    else
                    {
                        Debug.LogError("[OpenAIRequest] Could not parse dialogue response. Using neutral fallback.");
                        HandlePatientResponse("I... I am not sure...", 0, 0);
                    }
                    yield break;
                }

                bool retryable = IsRetryableResponse(request.responseCode, request.downloadHandler.text);
                Debug.LogError($"[OpenAIRequest] Dialogue request failed (attempt {attempt}/{attempts}). code={request.responseCode}, error={request.error}, retryable={retryable}, body={request.downloadHandler.text}");

                if (!retryable || attempt >= attempts)
                {
                    HandlePatientResponse("I... I am not sure...", 0, 0);
                    yield break;
                }

                yield return new WaitForSeconds(0.35f * attempt);
            }
        }
    }

    private bool IsRetryableResponse(long responseCode, string responseBody)
    {
        if (responseCode == 408 || responseCode == 425 || responseCode == 429)
            return true;

        if (responseCode >= 500)
            return true;

        if (string.IsNullOrWhiteSpace(responseBody))
            return false;

        try
        {
            var error = JsonConvert.DeserializeObject<ErrorEnvelope>(responseBody);
            return error != null && error.retryable;
        }
        catch
        {
            return false;
        }
    }

    private string BuildDialogueRequestBody()
    {
        var filteredMessages = chatMessages
            .Where(m =>
                m.ContainsKey("role") &&
                m.ContainsKey("content") &&
                (m["role"] == "user" || m["role"] == "assistant") &&
                !string.IsNullOrWhiteSpace(m["content"]))
            .Select(m => new Dictionary<string, string>
            {
                { "role", m["role"] },
                { "content", m["content"] }
            })
            .ToList();

        var payload = new DialogueRequestPayload
        {
            messages = filteredMessages,
            metadata = new DialogueMetadata
            {
                turnIndex = turnIndex,
                client = "unity-webgl"
            }
        };

        return JsonConvert.SerializeObject(payload);
    }

    private bool TryExtractDialogueResponse(string responseBody, out string responseText, out int emotionCode, out int motionCode)
    {
        responseText = "";
        emotionCode = 0;
        motionCode = 0;

        try
        {
            var jsonResponse = JObject.Parse(responseBody);
            string messageContent = jsonResponse["choices"]?[0]?["message"]?["content"]?.ToString();
            if (string.IsNullOrWhiteSpace(messageContent))
            {
                Debug.LogError("[OpenAIRequest] Missing choices[0].message.content");
                return false;
            }

            var parsed = JsonConvert.DeserializeObject<StructuredDialogueResponse>(messageContent);
            if (parsed == null || string.IsNullOrWhiteSpace(parsed.responseText))
            {
                Debug.LogError("[OpenAIRequest] Structured dialogue payload is missing responseText.");
                return false;
            }

            responseText = parsed.responseText.Trim();
            emotionCode = Mathf.Clamp(parsed.emotionCode, 0, 9);
            motionCode = Mathf.Clamp(parsed.motionCode, 0, 9);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[OpenAIRequest] Exception while parsing dialogue response: {ex.Message}");
            return false;
        }
    }

    private void HandlePatientResponse(string responseText, int emotionCode, int motionCode)
    {
        currentPatientResponse = responseText;

        var assistantPayload = new StructuredDialogueResponse
        {
            responseText = responseText,
            emotionCode = Mathf.Clamp(emotionCode, 0, 9),
            motionCode = Mathf.Clamp(motionCode, 0, 9)
        };

        chatMessages.Add(new Dictionary<string, string>
        {
            { "role", "assistant" },
            { "content", JsonConvert.SerializeObject(assistantPayload) }
        });
        PrintChatMessage(chatMessages);

        if (TTSManager.Instance != null)
            TTSManager.Instance.ConvertTextToSpeech(responseText, motionCode);
        else
            Debug.LogError("TTSManager instance not found.");

        if (emotionController == null && !TryResolveEmotionController())
        {
            Debug.LogError("[OpenAIRequest] EmotionController not found; skipping HandleEmotionCode.");
        }
        else
        {
            emotionController.HandleEmotionCode(emotionCode, motionCode);
        }

        if (cueController != null)
            cueController.HandleResponse(responseText);

        if (ScoreManager.Instance != null && !string.IsNullOrWhiteSpace(pendingNurseMessage))
        {
            ScoreManager.Instance.RecordTurn(currentPatientResponse, pendingNurseMessage);
            pendingNurseMessage = "";
        }
    }

    public static void PrintChatMessage(List<Dictionary<string, string>> messages)
    {
        if (messages == null || messages.Count == 0)
            return;

        var latestMessage = messages[messages.Count - 1];
        string role = latestMessage.ContainsKey("role") ? latestMessage["role"] : "unknown";
        string content = latestMessage.ContainsKey("content") ? latestMessage["content"] : "";
        Debug.Log($"[{role.ToUpper()}]\n{content}\n");
    }

    public List<Dictionary<string, string>> GetChatMessages()
    {
        return chatMessages;
    }

    public string GetScoringEndpointUrl()
    {
        if (ApiConfigProvider.TryBuildBackendUrl(ScoringPath, out string scoringUrl))
            return scoringUrl;

        return null;
    }

    public void SaveConversationToAWS()
    {
        Debug.Log("[OpenAIRequest] Session turns and evaluation are persisted by the current backend flow. No separate chat-history call is required.");
    }

    [ContextMenu("Debug Chat Messages")]
    public void DebugChatMessages()
    {
        Debug.Log($"=== CHAT MESSAGES DEBUG ({chatMessages.Count} messages) ===");
        for (int i = 0; i < chatMessages.Count; i++)
        {
            var msg = chatMessages[i];
            Debug.Log($"{i}: [{msg["role"]}] {msg["content"]}");
        }
    }

    [ContextMenu("Test Save Current Conversation")]
    public void TestSaveCurrentConversation()
    {
        Debug.LogWarning("[OpenAIRequest] Legacy chat-history save helper is deprecated for the current embedded WebGL contract.");
    }

    private void HandleRuntimeContextChanged()
    {
        ApplyRuntimeContextFromHost();
    }

    private void ApplyRuntimeContextFromHost()
    {
        if (!RuntimeSessionContext.HasContext)
            return;

        string previousScenario = currentScenario;
        string previousSessionId = sessionId;
        string nextUserId = string.IsNullOrWhiteSpace(RuntimeSessionContext.UserId) ? CurrentUserId : RuntimeSessionContext.UserId;
        string nextScenario = RuntimeSessionContext.ContextLabel;
        string nextSessionId = RuntimeSessionContext.SessionId;
        bool isNewSession = !string.IsNullOrWhiteSpace(nextSessionId) && !string.Equals(previousSessionId, nextSessionId, StringComparison.Ordinal);
        bool needsInitialization = isNewSession || chatMessages.Count == 0 || string.IsNullOrWhiteSpace(previousScenario);

        CurrentUserId = nextUserId;
        currentScenario = string.IsNullOrWhiteSpace(nextScenario)
            ? "assignment-runtime"
            : nextScenario;

        if (!string.IsNullOrWhiteSpace(nextSessionId))
            sessionId = nextSessionId;

        if (needsInitialization)
        {
            turnIndex = 0;
            InitializeChat();
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.Initialize(currentScenario);
        }

        Debug.Log($"[OpenAIRequest] Runtime context applied. sessionId={sessionId}, assignmentId={RuntimeSessionContext.AssignmentId}, sceneId={RuntimeSessionContext.SceneId}, contextLabel={currentScenario}");
    }

    private string BuildRequestIdPrefix()
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
            return sessionId;

        return string.IsNullOrWhiteSpace(CurrentUserId) ? "unity-webgl" : CurrentUserId;
    }
}
