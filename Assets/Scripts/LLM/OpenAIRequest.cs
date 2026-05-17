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
    private TTSManager ttsManager;
    [SerializeField] private GameObject cueControllerObject;
    private CueController cueController;

    [Header("Cue Warning System")]
    [SerializeField] private CueSkipGuard cueSkipGuard;
    [SerializeField] private TargetButtonUI targetButtonUI;
    [SerializeField] private SimuCaseTargetButtonUI simuCaseTargetButtonUI;

    [Header("Study Data")]
    [SerializeField] private MonoBehaviour studyItemMetadataProviderBehaviour;

    // Internal state
    private const string DialoguePath = "/llm-dialogue";
    private const string ScoringPath = "/llm-scoring";
    private const string SessionTurnPathFormat = "/sessions/{0}/turns/{1}";
    private float currentSpeechSpeed;
    private string basePath;
    public string CurrentUserId { get; private set; }

    private readonly List<Dictionary<string, string>> chatMessages = new List<Dictionary<string, string>>();
    private string currentPatientResponse = "";
    private string pendingNurseMessage = "";
    private string activeUserSpeechStartAt = "";
    private string activeUserSpeechEndAt = "";
    private bool hasReportedUserSpeechEndForCurrentTurn;
    private string sessionId = "";
    private int turnIndex = 0;
    private ScoreManager cachedScoreManager;
    private IStudyItemMetadataProvider studyItemMetadataProvider;

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
        public string userSpeechStartAt;
    }

    [Serializable]
    private class ErrorEnvelope
    {
        public string error;
        public string requestId;
        public bool retryable;
    }

    [Serializable]
    private class TurnTimingUpdatePayload
    {
        public string userSpeechStartAt;
        public string userSpeechEndAt;
        public string patientSpeechStartAt;
        public string patientSpeechEndAt;
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
        TryResolveTTSManager();
        TryResolveEmotionController();

        if (cueControllerObject == null)
            Debug.LogWarning("OpenAIRequest: cueControllerObject is not assigned. Skipping cue UI setup.");

        cueController = cueControllerObject != null ? cueControllerObject.GetComponent<CueController>() : null;

        if (emotionController == null)
            Debug.LogError("OpenAIRequest: EmotionController not found on this object, children, or parent.");

        if (ttsManager == null)
            Debug.LogError("OpenAIRequest: TTSManager not found under this root or in scene.");

        if (cueController == null)
            Debug.LogError("CueController component not found on the UI GameObject.");

        if (cueSkipGuard == null)
            Debug.LogWarning("OpenAIRequest: cueSkipGuard not assigned. Cue order warnings will not fire.");

        bool hasSimuCaseTargetProvider = TryResolveSimuCaseTargetButtonUI() != null;
        bool hasLegacyTargetProvider = TryResolveLegacyTargetButtonUI() != null;
        bool hasStudyItemMetadataProvider = TryResolveStudyItemMetadataProvider() != null;
        if (!hasSimuCaseTargetProvider && !hasLegacyTargetProvider && !hasStudyItemMetadataProvider)
            Debug.LogWarning("OpenAIRequest: no supported target button provider is assigned. Target word will be empty.");

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
        if (emotionController != null && emotionController.isActiveAndEnabled)
            return true;

        emotionController = SelectBestEmotionController(GetComponentsInChildren<EmotionController>(true));
        return emotionController != null;
    }

    private FacialExpressionRuntimeBridge TryResolveFacialExpressionBridge()
    {
        if (!TryResolveEmotionController() || emotionController == null)
            return null;

        if (emotionController.facialExpressionBridge != null)
            return emotionController.facialExpressionBridge;

        FacialExpressionRuntimeBridge bridge = emotionController.GetComponent<FacialExpressionRuntimeBridge>();
        if (bridge == null)
            bridge = emotionController.GetComponentInChildren<FacialExpressionRuntimeBridge>(true);
        if (bridge == null)
            bridge = emotionController.GetComponentInParent<FacialExpressionRuntimeBridge>();

        if (bridge != null && emotionController.facialExpressionBridge == null)
            emotionController.facialExpressionBridge = bridge;

        return bridge;
    }

    private void BeginProcessingPresentation()
    {
        FacialExpressionRuntimeBridge bridge = TryResolveFacialExpressionBridge();
        if (bridge != null)
        {
            bridge.BeginProcessingPresentation();
            return;
        }

        Debug.LogWarning("[OpenAIRequest] Facial bridge not found; skipping processing presentation.");
    }

    private bool TryResolveTTSManager()
    {
        if (ttsManager != null && ttsManager.isActiveAndEnabled)
            return true;

        ttsManager = SelectBestComponent(GetComponentsInChildren<TTSManager>(true));
        return ttsManager != null;
    }

    private static EmotionController SelectBestEmotionController(EmotionController[] candidates)
    {
        return SelectBestComponent(candidates);
    }

    private static T SelectBestComponent<T>(T[] candidates) where T : Behaviour
    {
        if (candidates == null || candidates.Length == 0)
            return null;

        T activeInHierarchy = null;
        T fallback = null;

        for (int i = 0; i < candidates.Length; i++)
        {
            T candidate = candidates[i];
            if (candidate == null)
                continue;

            if (fallback == null)
                fallback = candidate;

            if (candidate.isActiveAndEnabled)
                return candidate;

            if (activeInHierarchy == null && candidate.gameObject.activeInHierarchy)
                activeInHierarchy = candidate;
        }

        return activeInHierarchy ?? fallback;
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

        ScoreManager scoreManager = TryResolveScoreManager(includeInactive: true);
        if (scoreManager != null)
            scoreManager.Initialize(currentScenario);

        Debug.Log($"[OpenAIRequest] Legacy local context applied. userID={CurrentUserId}, contextLabel={currentScenario}");
    }

    private void InitializeChat()
    {
        chatMessages.Clear();
        currentPatientResponse = "";
        activeUserSpeechStartAt = "";
        activeUserSpeechEndAt = "";
        hasReportedUserSpeechEndForCurrentTurn = false;
        Debug.Log("[OpenAIRequest] Chat initialized without system message. Backend owns system prompt.");
    }

    public void ReceiveNurseTranscription(string transcribedText, float speechWpm, string userSpeechStartAt = null, string userSpeechEndAt = null)
    {
        Debug.LogError($"[OpenAIRequest] ReceiveNurseTranscription text=\"{transcribedText}\" wpm={speechWpm:0.##}");

        // Check cue order before sending to GPT
        if (cueSkipGuard != null)
        {
            string targetWord;
            if (!TryResolveCurrentTargetWord(out targetWord))
                targetWord = "";
            cueSkipGuard.CheckStudentUtterance(transcribedText, targetWord);
            Debug.Log($"[OpenAIRequest] CueSkipGuard checked: text=\"{transcribedText}\" target=\"{targetWord}\"");
        }

        NurseResponds(transcribedText, speechWpm, userSpeechStartAt, userSpeechEndAt);
    }

    private void NurseResponds(string nurseMessage, float speechWpm, string userSpeechStartAt, string userSpeechEndAt)
    {
        if (string.IsNullOrWhiteSpace(nurseMessage))
        {
            Debug.LogWarning("[OpenAIRequest] Empty nurse message ignored.");
            return;
        }

        string trimmedNurseMessage = nurseMessage.Trim();
        string normalizedUserSpeechStartAt = NormalizeOptionalTimestamp(userSpeechStartAt);
        string normalizedUserSpeechEndAt = NormalizeOptionalTimestamp(userSpeechEndAt);
        TryRecordStudentStudyTurn(trimmedNurseMessage, normalizedUserSpeechStartAt, normalizedUserSpeechEndAt, turnIndex + 1);

        if (!RuntimeSessionContext.HasRuntimeToken)
        {
            Debug.LogError("[OpenAIRequest] Cannot send dialogue request without a runtime token.");
            HandlePatientResponse("I... I am not sure...", 0, 0);
            return;
        }

        Debug.LogError($"[OpenAIRequest] NurseResponds accepted message=\"{trimmedNurseMessage}\" chatCountBefore={chatMessages.Count}");

        if (chatMessages.Count == 0 && !string.IsNullOrEmpty(currentScenario))
            InitializeChat();

        chatMessages.Add(new Dictionary<string, string>
        {
            { "role", "user" },
            { "content", trimmedNurseMessage }
        });
        PrintChatMessage(chatMessages);
        pendingNurseMessage = trimmedNurseMessage;
        activeUserSpeechStartAt = normalizedUserSpeechStartAt;
        activeUserSpeechEndAt = normalizedUserSpeechEndAt;
        hasReportedUserSpeechEndForCurrentTurn = false;
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
            BeginProcessingPresentation();
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
                        TryReportPendingUserSpeechEnd();
                        HandlePatientResponse(ttsText, emotionCode, motionCode);
                    }
                    else
                    {
                        TryReportPendingUserSpeechEnd();
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
                client = "unity-webgl",
                userSpeechStartAt = activeUserSpeechStartAt
            }
        };

        return JsonConvert.SerializeObject(
            payload,
            new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
    }

    private bool TryExtractDialogueResponse(string responseBody, out string responseText, out int emotionCode, out int motionCode)
    {
        responseText = "";
        emotionCode = 0;
        motionCode = 0;

        try
        {
            var jsonResponse = JObject.Parse(responseBody);
            TryApplyResolvedTurnIndex(jsonResponse["metadata"]?["turnIndex"]);

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
        TryRecordPatientStudyTurn(responseText, turnIndex > 0 ? turnIndex : (int?)null);

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

        if (TryResolveTTSManager())
            ttsManager.ConvertTextToSpeech(
                responseText,
                motionCode,
                turnIndex);
        else
            Debug.LogError("TTSManager instance not found.");

        if (!TryResolveEmotionController())
        {
            Debug.LogError("[OpenAIRequest] EmotionController not found; skipping HandleEmotionCode.");
        }
        else
        {
            emotionController.HandleEmotionCode(emotionCode, motionCode);
        }

        if (cueController != null)
            cueController.HandleResponse(responseText);

        ScoreManager scoreManager = TryResolveScoreManager(includeInactive: true);
        if (scoreManager != null && !string.IsNullOrWhiteSpace(pendingNurseMessage))
        {
            scoreManager.RecordTurn(currentPatientResponse, pendingNurseMessage);
            pendingNurseMessage = "";
        }
    }

    private ScoreManager TryResolveScoreManager(bool includeInactive)
    {
        if (cachedScoreManager != null)
            return cachedScoreManager;

        if (ScoreManager.Instance != null)
        {
            cachedScoreManager = ScoreManager.Instance;
            return cachedScoreManager;
        }

        cachedScoreManager = FindObjectOfType<ScoreManager>();
        if (cachedScoreManager != null || !includeInactive)
            return cachedScoreManager;

        ScoreManager[] candidates = FindObjectsByType<ScoreManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < candidates.Length; i++)
        {
            ScoreManager candidate = candidates[i];
            if (candidate == null)
                continue;

            if (!candidate.gameObject.scene.IsValid())
                continue;

            cachedScoreManager = candidate;
            return cachedScoreManager;
        }

        return null;
    }

    private void TryRecordStudentStudyTurn(string text, string userSpeechStartAt, string userSpeechEndAt, int? trackingTurnIndex)
    {
        if (!TryResolveCurrentStudyItemMetadata(out StudyItemMetadata metadata))
            return;

        StudyActiveItemTracker.RecordStudentUtterance(
            metadata,
            text,
            trackingTurnIndex,
            userSpeechStartAt,
            userSpeechEndAt);
    }

    private void TryRecordPatientStudyTurn(string text, int? trackingTurnIndex)
    {
        if (!TryResolveCurrentStudyItemMetadata(out StudyItemMetadata metadata))
            return;

        StudyActiveItemTracker.RecordPatientResponse(
            metadata,
            text,
            trackingTurnIndex);
    }

    private bool TryResolveCurrentStudyItemMetadata(out StudyItemMetadata metadata)
    {
        metadata = null;
        IStudyItemMetadataProvider provider = TryResolveStudyItemMetadataProvider();
        return provider != null && provider.TryGetCurrentStudyItemMetadata(out metadata);
    }

    private IStudyItemMetadataProvider TryResolveStudyItemMetadataProvider()
    {
        if (IsUsableStudyItemMetadataProvider(studyItemMetadataProvider))
            return studyItemMetadataProvider;

        studyItemMetadataProvider = null;

        if (studyItemMetadataProviderBehaviour is IStudyItemMetadataProvider configuredProvider &&
            studyItemMetadataProviderBehaviour.isActiveAndEnabled)
        {
            studyItemMetadataProvider = configuredProvider;
            return studyItemMetadataProvider;
        }

        SimuCaseTargetButtonUI simuCaseTargetButton = TryResolveSimuCaseTargetButtonUI();
        if (simuCaseTargetButton != null)
        {
            studyItemMetadataProviderBehaviour = simuCaseTargetButton;
            studyItemMetadataProvider = simuCaseTargetButton;
            return studyItemMetadataProvider;
        }

        MonoBehaviour[] candidates = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < candidates.Length; i++)
        {
            MonoBehaviour candidate = candidates[i];
            if (candidate == null || candidate == this)
                continue;

            if (candidate is IStudyItemMetadataProvider provider && candidate.isActiveAndEnabled)
            {
                studyItemMetadataProviderBehaviour = candidate;
                studyItemMetadataProvider = provider;
                return studyItemMetadataProvider;
            }
        }

        return null;
    }

    private static bool IsUsableStudyItemMetadataProvider(IStudyItemMetadataProvider provider)
    {
        if (provider == null)
            return false;

        MonoBehaviour behaviour = provider as MonoBehaviour;
        return behaviour == null || behaviour.isActiveAndEnabled;
    }

    private bool TryResolveCurrentTargetWord(out string targetWord)
    {
        targetWord = string.Empty;

        if (TryResolveCurrentStudyItemMetadata(out StudyItemMetadata metadata) &&
            !string.IsNullOrWhiteSpace(metadata.targetAnswer))
        {
            targetWord = metadata.targetAnswer;
            return true;
        }

        SimuCaseTargetButtonUI simuCaseTargetButton = TryResolveSimuCaseTargetButtonUI();
        if (simuCaseTargetButton != null)
        {
            targetWord = simuCaseTargetButton.CurrentTargetWord ?? string.Empty;
            return true;
        }

        TargetButtonUI legacyTargetButton = TryResolveLegacyTargetButtonUI();
        if (legacyTargetButton != null)
        {
            targetWord = legacyTargetButton.CurrentTargetWord ?? string.Empty;
            return true;
        }

        return false;
    }

    private SimuCaseTargetButtonUI TryResolveSimuCaseTargetButtonUI()
    {
        if (simuCaseTargetButtonUI != null && simuCaseTargetButtonUI.isActiveAndEnabled)
            return simuCaseTargetButtonUI;

        simuCaseTargetButtonUI = FindObjectOfType<SimuCaseTargetButtonUI>();
        return simuCaseTargetButtonUI;
    }

    private TargetButtonUI TryResolveLegacyTargetButtonUI()
    {
        if (targetButtonUI != null && targetButtonUI.isActiveAndEnabled)
            return targetButtonUI;

        targetButtonUI = FindObjectOfType<TargetButtonUI>();
        return targetButtonUI;
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
            ScoreManager scoreManager = TryResolveScoreManager(includeInactive: true);
            if (scoreManager != null)
                scoreManager.Initialize(currentScenario);
        }

        Debug.Log($"[OpenAIRequest] Runtime context applied. sessionId={sessionId}, assignmentId={RuntimeSessionContext.AssignmentId}, sceneId={RuntimeSessionContext.SceneId}, contextLabel={currentScenario}");
    }

    private string BuildRequestIdPrefix()
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
            return sessionId;

        return string.IsNullOrWhiteSpace(CurrentUserId) ? "unity-webgl" : CurrentUserId;
    }

    private void TryApplyResolvedTurnIndex(JToken turnIndexToken)
    {
        if (turnIndexToken == null)
            return;

        int resolvedTurnIndex;
        if (!int.TryParse(turnIndexToken.ToString(), out resolvedTurnIndex) || resolvedTurnIndex <= 0)
            return;

        if (resolvedTurnIndex != turnIndex)
        {
            Debug.Log($"[OpenAIRequest] Backend resolved turnIndex={resolvedTurnIndex} (client had {turnIndex}). Reusing backend value.");
        }

        turnIndex = resolvedTurnIndex;
    }

    private static string NormalizeOptionalTimestamp(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }

    public void ReportPatientSpeechStart(int resolvedTurnIndex)
    {
        StartCoroutine(ReportTurnTimingUpdate(
            resolvedTurnIndex,
            patientSpeechStartAt: GetUtcIsoTimestamp()));
    }

    public void ReportPatientSpeechEnd(int resolvedTurnIndex)
    {
        StartCoroutine(ReportTurnTimingUpdate(
            resolvedTurnIndex,
            patientSpeechEndAt: GetUtcIsoTimestamp()));
    }

    private void TryReportPendingUserSpeechEnd()
    {
        if (hasReportedUserSpeechEndForCurrentTurn || string.IsNullOrWhiteSpace(activeUserSpeechEndAt))
            return;

        hasReportedUserSpeechEndForCurrentTurn = true;
        StartCoroutine(ReportTurnTimingUpdate(
            turnIndex,
            userSpeechEndAt: activeUserSpeechEndAt));
    }

    private IEnumerator ReportTurnTimingUpdate(
        int resolvedTurnIndex,
        string userSpeechStartAt = null,
        string userSpeechEndAt = null,
        string patientSpeechStartAt = null,
        string patientSpeechEndAt = null)
    {
        if (resolvedTurnIndex <= 0)
            yield break;

        var payload = new TurnTimingUpdatePayload
        {
            userSpeechStartAt = NormalizeOptionalTimestamp(userSpeechStartAt),
            userSpeechEndAt = NormalizeOptionalTimestamp(userSpeechEndAt),
            patientSpeechStartAt = NormalizeOptionalTimestamp(patientSpeechStartAt),
            patientSpeechEndAt = NormalizeOptionalTimestamp(patientSpeechEndAt)
        };

        if (string.IsNullOrWhiteSpace(payload.userSpeechStartAt) &&
            string.IsNullOrWhiteSpace(payload.userSpeechEndAt) &&
            string.IsNullOrWhiteSpace(payload.patientSpeechStartAt) &&
            string.IsNullOrWhiteSpace(payload.patientSpeechEndAt))
        {
            yield break;
        }

        string activeSessionId = !string.IsNullOrWhiteSpace(RuntimeSessionContext.SessionId)
            ? RuntimeSessionContext.SessionId
            : sessionId;

        if (string.IsNullOrWhiteSpace(activeSessionId))
        {
            Debug.LogWarning("[OpenAIRequest] Skipping turn timing update because sessionId is missing.");
            yield break;
        }

        string sessionPath = string.Format(
            SessionTurnPathFormat,
            Uri.EscapeDataString(activeSessionId),
            resolvedTurnIndex);

        if (!ApiConfigProvider.TryBuildBackendUrl(sessionPath, out string endpoint))
        {
            Debug.LogError("[OpenAIRequest] Could not build turn timing endpoint.");
            yield break;
        }

        string jsonContent = JsonConvert.SerializeObject(
            payload,
            new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });

        using (var request = new UnityWebRequest(endpoint, "PUT"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonContent);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = requestTimeoutSeconds;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");

            if (!RuntimeSessionContext.ApplyAuthorization(request, "OpenAIRequest"))
                yield break;

            yield return request.SendWebRequest();

            bool success = request.result == UnityWebRequest.Result.Success &&
                           request.responseCode >= 200 &&
                           request.responseCode < 300;

            if (!success)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                string responseBody = request.downloadHandler?.text ?? string.Empty;
                Debug.LogWarning(
                    $"[OpenAIRequest] Turn timing update failed for sessionId={activeSessionId}, turnIndex={resolvedTurnIndex}. " +
                    $"status={(int)request.responseCode}, transport={request.error}, body={responseBody}");
#endif
            }
        }
    }

    private static string GetUtcIsoTimestamp()
    {
        return DateTimeOffset.UtcNow.UtcDateTime.ToString("o");
    }
}
