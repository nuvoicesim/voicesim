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

public class OpenAIRequest : MonoBehaviour
{
    public static OpenAIRequest Instance;

    [Header("Legacy (Do Not Use)")]
    public string apiUrl = "";
    public string apiKey = "";

    [Header("LLM Backend")]
    [SerializeField] private string backendBaseUrl = "https://f0kk74qeyf.execute-api.us-west-2.amazonaws.com/dev";
    [SerializeField] private string dialoguePath = "/llm-dialogue";
    [SerializeField, Range(1, 3)] private int maxRetries = 2;
    [SerializeField] private int requestTimeoutSeconds = 45;

    [Header("Conversation Settings")]
    [SerializeField] private string currentScenario = "";
    public string lostResponse = "umm... fast... uh... fast... ";
    public float maxSpeechSpeed = 200f;

    public string CurrentUserId { get; private set; }
    public int CurrentSimulationLevel { get; private set; } = 1;

    private EmotionController emotionController;
    [SerializeField] private GameObject cueControllerObject;
    private CueController cueController;
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
    private class DialogueMetadata
    {
        public string sessionId;
        public int turnIndex;
        public string client;
    }

    [Serializable]
    private class DialogueOptions
    {
        public float temperature;
        public int maxOutputTokens;
    }

    [Serializable]
    private class DialogueRequestPayload
    {
        public string userID;
        public int simulationLevel;
        public List<Dictionary<string, string>> messages;
        public DialogueMetadata metadata;
        public DialogueOptions options;
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
        emotionController = GetComponent<EmotionController>();

        if (cueControllerObject == null)
{
    Debug.LogWarning("OpenAIRequest: cueControllerObject is not assigned. Skipping cue UI setup.");
    return;
}

        cueController = cueControllerObject.GetComponent<CueController>();

        if (emotionController == null)
            Debug.LogError("EmotionController component not found on the GameObject.");

        if (cueController == null)
            Debug.LogError("CueController component not found on the UI GameObject.");

        if (!string.IsNullOrEmpty(currentScenario))
            InitializeChat();
        else
            Debug.LogWarning("[OpenAIRequest] currentScenario is empty at Start; will initialize after login via ApplyLoginContext.");
    }

    public void ApplyLoginContext(string userId, int simulationLevel)
    {
        CurrentUserId = userId;
        CurrentSimulationLevel = Mathf.Clamp(simulationLevel, 1, 3);
        sessionId = $"{CurrentUserId}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        turnIndex = 0;

        switch (CurrentSimulationLevel)
        {
            case 1: currentScenario = "task1"; break;
            case 2: currentScenario = "task2"; break;
            case 3: currentScenario = "task3"; break;
            default: currentScenario = "task1"; break;
        }

        InitializeChat();
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.Initialize(currentScenario);

        Debug.Log($"[OpenAIRequest] Login context applied. userID={CurrentUserId}, simulationLevel={CurrentSimulationLevel}, scenario={currentScenario}");
    }

    private void InitializeChat()
    {
        chatMessages.Clear();
        currentPatientResponse = "";
        Debug.Log("[OpenAIRequest] Chat initialized without system message. Backend owns system prompt.");
    }

    public void ReceiveNurseTranscription(string transcribedText, float speechWpm)
    {
        NurseResponds(transcribedText, speechWpm);
    }

    private void NurseResponds(string nurseMessage, float speechWpm)
    {
        if (string.IsNullOrWhiteSpace(nurseMessage))
        {
            Debug.LogWarning("[OpenAIRequest] Empty nurse message ignored.");
            return;
        }

        if (chatMessages.Count == 0 && !string.IsNullOrEmpty(currentScenario))
            InitializeChat();

        chatMessages.Add(new Dictionary<string, string>
        {
            { "role", "user" },
            { "content", nurseMessage.Trim() }
        });
        PrintChatMessage(chatMessages);
        pendingNurseMessage = nurseMessage.Trim();

        turnIndex++;

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
        string requestUrl = $"{backendBaseUrl.TrimEnd('/')}{dialoguePath}";
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
                request.SetRequestHeader("X-Request-ID", $"{sessionId}-turn-{turnIndex}-try-{attempt}");

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
            userID = string.IsNullOrWhiteSpace(CurrentUserId) ? "anonymous-user" : CurrentUserId,
            simulationLevel = Mathf.Clamp(CurrentSimulationLevel, 1, 3),
            messages = filteredMessages,
            metadata = new DialogueMetadata
            {
                sessionId = sessionId,
                turnIndex = turnIndex,
                client = "unity"
            },
            options = new DialogueOptions
            {
                temperature = 0.2f,
                maxOutputTokens = 220
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

        if (emotionController != null)
            emotionController.HandleEmotionCode(emotionCode, motionCode);

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
        return $"{backendBaseUrl.TrimEnd('/')}/llm-scoring";
    }

    public void SaveConversationToAWS()
    {
        Debug.Log("🔄 Conversation will be saved with evaluation report");
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
        if (AWSAPIConnector.Instance != null && chatMessages.Count > 0)
        {
            Debug.Log("🧪 Testing immediate chat history save...");
            AWSAPIConnector.Instance.SaveChatHistory(chatMessages);
        }
        else
        {
            Debug.LogWarning("Cannot test save: missing components or no chat messages");
        }
    }
}
