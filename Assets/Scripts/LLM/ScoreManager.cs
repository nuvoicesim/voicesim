using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    [Header("UI References")]
    public Canvas evaluationCanvas;
    public TextMeshProUGUI reportText;
    public Button closeButton;

    [Header("Progress Bar")]
    public MedicalProgressBarUI progressBarUI;

    private string currentScenario = "";
    private List<ConversationTurn> conversationTurns = new List<ConversationTurn>();
    private const string ScoringPath = "/llm-scoring";
    [SerializeField] private int scoringTimeoutSeconds = 60;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // Clear the static singleton when this instance is destroyed (most
        // commonly during scene unload). Without this, Instance retains a
        // reference to a destroyed Unity object across scene transitions.
        // The C# null-conditional operator (?.) performs a CLR null check
        // and does NOT invoke Unity's overloaded ==, so callers using
        // `ScoreManager.Instance?.Method()` would otherwise dereference a
        // destroyed wrapper and throw MissingReferenceException. Guard with
        // `Instance == this` so a later-loaded instance that already
        // reassigned the singleton is not accidentally cleared.
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Start()
    {
        // The outer ReportPanel's initial hidden state is already owned by
        // scene setup (m_IsActive: 0 on the panel itself) and by
        // CameraClipboardController.Start(), which always runs at scene load
        // because that controller sits on an always-active GameObject.
        // ScoreManager, by contrast, lives under ReportUIController which is
        // m_IsActive: 0 in the section scenes, so ScoreManager.Start() runs
        // LATE — specifically, one frame after the Finish coroutine in
        // CameraClipboardController calls EnsureGameObjectHierarchyActive(...)
        // and activates this subsystem. A redundant SetActive(false) on
        // evaluationCanvas here would therefore re-hide the ReportPanel that
        // SwitchToClipboard just activated, breaking the restored Phase 1
        // clipboard/report flow. Initial hide responsibility stays with the
        // scene and CameraClipboardController; this Start() only wires the
        // close-button listener.

        if (closeButton != null)
            closeButton.onClick.AddListener(HideEvaluationPanel);
    }

    public void Initialize(string scenario)
    {
        currentScenario = scenario;
        conversationTurns.Clear();
        Debug.Log($"[ScoreManager] Initialized for scenario={currentScenario}. Backend owns scoring prompt.");
    }

    public void RecordTurn(string patientResponse, string nurseResponse)
    {
        conversationTurns.Add(new ConversationTurn
        {
            Patient = patientResponse,
            Nurse = nurseResponse
        });
    }

    public void SubmitEvaluation()
    {
        // Phase 2 evidence persistence is valid with zero conversation turns
        // (e.g. Phase 2 Object Naming has no SLP-student dialogue). The legacy
        // CreateNoConversationReport() activates evaluationCanvas and returns
        // before POST /llm-scoring fires, which would block Phase 2 evidence
        // from ever reaching the backend. Detect Phase 2 study flow from the
        // finalized study payload BEFORE the zero-turn guard so Phase 2
        // continues into EvaluateFullConversationCoroutine() even with zero
        // turns. Non-Phase-2 zero-turn callers (Phase 1 rubric in C/D, legacy
        // non-study paths) keep the existing placeholder behavior unchanged.
        bool isPhase2StudyFlow = IsPhase2StudyFlow();
        if (conversationTurns.Count == 0 && !isPhase2StudyFlow)
        {
            Debug.LogWarning("No conversation turns recorded. Creating a placeholder report.");
            CreateNoConversationReport();
            return;
        }

        Debug.Log($"开始生成评估报告，共有 {conversationTurns.Count} 轮对话");

        if (progressBarUI != null)
            progressBarUI.ShowProgressBar();

        StartCoroutine(EvaluateFullConversationCoroutine());
    }

    private static bool IsPhase2StudyFlow()
    {
        StudyTaskResultPayload payload = TryBuildStudyTaskPayload();
        return string.Equals(
            payload?.taskContext?.phaseId,
            StudyDataDefaults.Phase2,
            System.StringComparison.OrdinalIgnoreCase);
    }

    // Phase 1 (May 19): when the active study flow is Phase 1, the report
    // panel must NOT show rubric / item-level / narrative content. The
    // panel is kept visible (the existing X-close + completion + course
    // unlock chain depend on it), but its content is replaced with a
    // neutral processing/saved-data message. See the call sites in
    // CreateNoConversationReport, ProcessEvaluationResult (rubric branch),
    // DisplayEvaluationToUI (legacy `report` wrapper), and
    // DisplayErrorReport.
    //
    // `internal` so CameraClipboardController.PrepareStudyFeedbackView can
    // skip the legacy "Rubric-Based Assessment Feedback" /
    // "AI Interaction Feedback" waiting-state UI for Phase 1 and show the
    // processing message from the moment the panel becomes visible.
    internal static bool IsPhase1StudyFlow()
    {
        StudyTaskResultPayload payload = TryBuildStudyTaskPayload();
        return string.Equals(
            payload?.taskContext?.phaseId,
            StudyDataDefaults.Phase1,
            System.StringComparison.OrdinalIgnoreCase);
    }

    // Title shown above the body in every Phase 1 report-panel state.
    // `internal` so CameraClipboardController can paint the same title
    // during the pre-/llm-scoring waiting state.
    internal const string Phase1ProcessingTitle = "AI is Processing Your Interaction";

    // Body shown when /llm-scoring runs (normal interaction + legacy
    // `report` wrapper + HTTP error). Wording acknowledges the AI
    // processing because /llm-scoring has fired by the time this body
    // renders. Also reused by CameraClipboardController for the
    // pre-/llm-scoring waiting state so students never see the legacy
    // rubric waiting placeholder.
    internal const string Phase1ProcessingBodyNormal =
        "Thank you for completing this VOICE activity and supporting the virtual patient interaction.\n\n"
        + "Your responses and session data have been saved. Our AI system is processing the interaction in the background to support the study workflow.\n\n"
        + "You may close this window when you are ready to continue.";

    // Body shown when the zero-turn guard in SubmitEvaluation
    // short-circuits BEFORE /llm-scoring runs (Phase 1 only — Phase 2 is
    // filtered out by IsPhase2StudyFlow upstream). Wording deliberately
    // avoids "AI is processing the interaction" because no /llm-scoring
    // request was sent on this path.
    private const string Phase1ProcessingBodyNoConversation =
        "No conversation was recorded during this activity.\n\n"
        + "Your session data has been saved for the study workflow.\n\n"
        + "You may close this window when you are ready to continue.";

    // Routes the Phase 1 message to every StudyFeedbackPresenter in the
    // scene. Mirrors the FindObjectsByType pattern already used by
    // ApplyRubricAssessment.
    private static void ShowPhase1ProcessingMessage(string title, string body)
    {
        StudyFeedbackPresenter[] presenters = FindObjectsByType<StudyFeedbackPresenter>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (StudyFeedbackPresenter presenter in presenters)
        {
            if (presenter == null)
                continue;
            presenter.ShowAiProcessingMessage(title, body);
        }
    }

    // Returns true when the active flow is a Phase 1 or Phase 2 study task.
    // Used to gate the post-/llm-scoring task-progress PUT so legacy non-study
    // callers do not trigger task-progress completion side effects.
    private static bool IsStudyFlow()
    {
        StudyTaskResultPayload payload = TryBuildStudyTaskPayload();
        string phaseId = payload?.taskContext?.phaseId;
        return string.Equals(phaseId, StudyDataDefaults.Phase1, System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(phaseId, StudyDataDefaults.Phase2, System.StringComparison.OrdinalIgnoreCase);
    }

    // Builds the task-progress request body from the current finalized study
    // payload's taskContext. Returns null when the required identity fields
    // (phaseId, taskId-or-sectionId) are not present, which lets the caller
    // skip the task-progress PUT cleanly without invoking the client.
    private static SessionTaskProgressClient.TaskProgressRequest
        BuildTaskProgressRequestFromCurrentStudyContext()
    {
        StudyTaskResultPayload payload = TryBuildStudyTaskPayload();
        StudyTaskContext taskContext = payload?.taskContext;
        if (taskContext == null) return null;
        if (string.IsNullOrWhiteSpace(taskContext.phaseId)) return null;

        bool hasTaskOrSection =
            !string.IsNullOrWhiteSpace(taskContext.taskId) ||
            !string.IsNullOrWhiteSpace(taskContext.sectionId);
        if (!hasTaskOrSection) return null;

        return new SessionTaskProgressClient.TaskProgressRequest
        {
            phaseId = taskContext.phaseId,
            taskId = taskContext.taskId,
            sectionId = taskContext.sectionId,
            taskType = taskContext.taskType,
        };
    }

    private void CreateNoConversationReport()
    {
        if (progressBarUI != null)
            progressBarUI.HideProgressBar();

        if (evaluationCanvas != null)
            evaluationCanvas.gameObject.SetActive(true);

        // Phase 1: hide the legacy "Evaluation Report / No Conversation
        // Recorded / start a new conversation" copy and show the neutral
        // saved-data message instead. Uses the no-conversation body
        // variant because /llm-scoring has NOT been called on this path
        // (the zero-turn guard in SubmitEvaluation returns early).
        if (IsPhase1StudyFlow())
        {
            ShowPhase1ProcessingMessage(Phase1ProcessingTitle, Phase1ProcessingBodyNoConversation);
            StartCoroutine(RefreshScrollViewLayout());
            Debug.Log("[ScoreManager] Phase 1 no-conversation: showing AI processing message.");
            return;
        }

        StringBuilder reportContent = new StringBuilder();
        reportContent.AppendLine("Evaluation Report\n");
        reportContent.AppendLine("No Conversation Recorded\n");
        reportContent.AppendLine("It appears that no conversation turns have been recorded during this session.\n");
        reportContent.AppendLine("To receive a proper evaluation, please:");
        reportContent.AppendLine("• Engage in conversation with the patient");
        reportContent.AppendLine("• Complete at least a few dialogue exchanges");
        reportContent.AppendLine("• Then click the Finish button to generate your assessment\n");
        reportContent.AppendLine("Please start a new conversation and try again.");

        if (reportText != null)
        {
            reportText.text = reportContent.ToString();
            StartCoroutine(RefreshScrollViewLayout());
        }

        Debug.Log("显示无对话记录提示报告");
    }

    private void DisplayErrorReport(string rawContent, string errorMessage)
    {
        Debug.LogError($"[ScoreManager] Report generation issue: {errorMessage}");
        string contentPreview;
        if (string.IsNullOrEmpty(rawContent))
        {
            contentPreview = "<empty>";
        }
        else
        {
            int previewLen = Mathf.Min(500, rawContent.Length);
            contentPreview = rawContent.Substring(0, previewLen) + (rawContent.Length > previewLen ? "..." : "");
        }
        Debug.Log($"[AI Raw Content Preview] {contentPreview}");

        if (progressBarUI != null)
            progressBarUI.HideProgressBar();

        if (evaluationCanvas != null)
            evaluationCanvas.gameObject.SetActive(true);

        // Phase 1: never surface "Evaluation Report" or technical error
        // wording to students. Show the same neutral processing copy as
        // the success path. The backend error has already been logged
        // above; backend retry / network logic is untouched.
        if (IsPhase1StudyFlow())
        {
            ShowPhase1ProcessingMessage(Phase1ProcessingTitle, Phase1ProcessingBodyNormal);
            StartCoroutine(RefreshScrollViewLayout());
            return;
        }

        if (reportText != null)
        {
            reportText.text =
                "Evaluation Report\n\n" +
                "Your report is being generated. This may take a moment. You can view your results on our website.";
            StartCoroutine(RefreshScrollViewLayout());
        }
    }

    private IEnumerator EvaluateFullConversationCoroutine()
    {
        if (progressBarUI != null)
            progressBarUI.UpdateProgress(0.1f, "Analyzing patient conversation...");
        yield return new WaitForSeconds(0.5f);

        if (progressBarUI != null)
            progressBarUI.UpdateProgress(0.3f, "Preparing clinical assessment...");
        yield return new WaitForSeconds(0.3f);

        if (!ApiConfigProvider.TryBuildBackendUrl(ScoringPath, out string scoringUrl))
        {
            DisplayErrorReport(string.Empty, "API environment config is missing or incomplete.");
            yield break;
        }

        StudyTaskResultPayload studyPayload = TryBuildStudyTaskPayload();

        var requestBody = new ScoringRequestPayload
        {
            conversationTurns = conversationTurns
                .ConvertAll(turn => new ScoringTurn
                {
                    patient = turn.Patient ?? "",
                    nurse = turn.Nurse ?? "",
                    slpStudent = turn.Nurse ?? ""
                }),
            metadata = new ScoringMetadata
            {
                turnIndex = conversationTurns.Count,
                client = "unity-webgl"
            },
            taskContext = BuildScoringTaskContext(studyPayload),
            studyTaskContext = BuildScoringStudyTaskContext(studyPayload)
        };

        string jsonBody = JsonConvert.SerializeObject(
            requestBody,
            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

        if (progressBarUI != null)
            progressBarUI.UpdateProgress(0.5f, "Consulting evaluation system...");
        yield return new WaitForSeconds(0.2f);

        var request = new UnityWebRequest(scoringUrl, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Accept", "application/json");
        request.timeout = scoringTimeoutSeconds;

        if (!RuntimeSessionContext.ApplyAuthorization(request, "ScoreManager"))
        {
            if (progressBarUI != null)
                progressBarUI.HideProgressBar();
            yield break;
        }

        Debug.Log($"[ScoreManager] Submitting conversation to scoring endpoint: {scoringUrl}");

        var operation = request.SendWebRequest();

        float requestStartTime = Time.time;
        while (!operation.isDone)
        {
            float elapsedTime = Time.time - requestStartTime;
            float progressValue = Mathf.Lerp(0.5f, 0.8f, Mathf.Clamp01(elapsedTime / 10f));
            if (progressBarUI != null)
                progressBarUI.UpdateProgress(progressValue, "Generating clinical feedback...");
            yield return null;
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            string responseBody = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            Debug.LogError($"OpenAI Request Error: {request.error} (HTTP {request.responseCode})");
            Debug.LogError($"[ScoreManager] Scoring response body: {responseBody}");
            DisplayErrorReport(responseBody, $"HTTP {request.responseCode}: {request.error}");
            yield break;
        }

        if (progressBarUI != null)
            progressBarUI.UpdateProgress(0.9f, "Compiling assessment report...");
        yield return new WaitForSeconds(0.3f);

        yield return StartCoroutine(ProcessAIResponse(request.downloadHandler.text));

        // After /llm-scoring success and ProcessAIResponse has returned
        // cleanly, record TASK-LEVEL completion for this internal task via
        // PUT /sessions/{sessionId}/task-progress/{progressKey}/complete.
        //
        // CRITICAL: this is the TASK-level endpoint, NOT the whole-session
        // endpoint. A prior experiment that called
        // PUT /sessions/{sessionId}/complete after each task Finish was
        // intentionally reverted; do not re-introduce that here.
        //
        // Gated by IsStudyFlow() so only Phase 1 (rubric) and Phase 2
        // (training evidence) submissions trigger task-progress; legacy
        // non-study callers are unaffected. Failure here logs at Warning
        // level and never disturbs the already-accepted Finish flow.
        if (IsStudyFlow())
        {
            SessionTaskProgressClient.TaskProgressRequest taskProgressRequest =
                BuildTaskProgressRequestFromCurrentStudyContext();
            if (taskProgressRequest != null)
            {
                yield return StartCoroutine(
                    SessionTaskProgressClient.MarkTaskComplete(
                        this,
                        "ScoreManager",
                        taskProgressRequest,
                        outcome =>
                        {
                            if (!outcome.IsSuccess)
                            {
                                Debug.LogWarning(
                                    $"[ScoreManager] Task-progress completion did not succeed: result={outcome.result} httpStatus={outcome.httpStatusCode}");
                            }
                        }));
            }
            else
            {
                Debug.LogWarning("[ScoreManager] Skipping task-progress PUT: current study context is missing phaseId or taskId/sectionId.");
            }
        }
    }

    private IEnumerator ProcessAIResponse(string responseText)
    {
        Debug.Log("=== OpenAI原始响应 ===");
        Debug.Log($"完整响应长度: {responseText.Length} 字符");
        Debug.Log($"完整响应内容: {responseText}");
        Debug.Log("==================");

        if (progressBarUI != null)
            progressBarUI.UpdateProgress(1.0f, "Finalizing evaluation...");
        yield return new WaitForSeconds(0.5f);

        ProcessEvaluationResult(responseText);
    }

    private void ProcessEvaluationResult(string responseText)
    {
        try
        {
            var jsonResponse = JObject.Parse(responseText);
            var reportToken = jsonResponse["report"];

            // Backend compatibility (May 18):
            //   - Phase 1 rubric branch returns a flat envelope centered on
            //     rubricAssessment, no `report` wrapper.
            //   - Phase 2 evidence branch returns a lightweight success envelope,
            //     also no `report` wrapper.
            // Treat absence of `report` as accepted HTTP 2xx and skip the legacy
            // narrative rendering path. For the Phase 1 envelope, extract the
            // top-level rubricAssessment and route it through the existing
            // ApplyRubricAssessment helper so StudyFeedbackPresenter renders
            // the returned itemFeedback / taskFeedback in place of its
            // waiting-state placeholder. Phase 2 envelopes have no
            // rubricAssessment field; the token resolves to null and the
            // rubric apply step is skipped, preserving the prior behavior.
            if (reportToken == null)
            {
                // Phase 1 returns the flat rubricAssessment envelope but
                // we no longer render rubric/item-level detail to
                // students. Skip ApplyRubricAssessment for Phase 1 and
                // show the neutral processing message. Phase 2 keeps the
                // existing no-op behavior (its envelope has neither
                // `report` nor `rubricAssessment`).
                bool isPhase1 = IsPhase1StudyFlow();
                JToken rubricToken = jsonResponse["rubricAssessment"];
                if (rubricToken != null && !isPhase1)
                {
                    RubricAssessmentResult rubricAssessment = rubricToken.ToObject<RubricAssessmentResult>();
                    if (rubricAssessment != null)
                    {
                        ApplyRubricAssessment(new DynamicEvaluationResult
                        {
                            rubricAssessment = rubricAssessment
                        }, hideAiInteractionBlock: true);
                    }
                }

                if (isPhase1)
                {
                    ShowPhase1ProcessingMessage(Phase1ProcessingTitle, Phase1ProcessingBodyNormal);
                }

                Debug.Log("[ScoreManager] /llm-scoring response did not include a legacy `report` wrapper; treating as accepted (Phase 1 rubric / Phase 2 evidence envelope). Skipping legacy narrative rendering.");
                if (progressBarUI != null)
                    progressBarUI.HideProgressBar();
                return;
            }

            var evaluation = reportToken.ToObject<DynamicEvaluationResult>();
            if (evaluation == null)
                throw new Exception("Failed to deserialize scoring report.");

            if (progressBarUI != null)
                progressBarUI.HideProgressBar();

            DisplayEvaluationToConsole(evaluation);
            DisplayEvaluationToUI(evaluation);
        }
        catch (Exception ex)
        {
            Debug.LogError("JSON解析失败: " + ex.Message);
            Debug.LogError($"尝试解析的内容: {responseText}");

            DisplayErrorReport(responseText, ex.Message);
        }
    }

    private void DisplayEvaluationToConsole(DynamicEvaluationResult evaluation)
    {
        Debug.Log("===== FINAL EVALUATION =====");
        foreach (var criterion in evaluation.criteria)
        {
            Debug.Log($"[{criterion.name}] Score: {criterion.score}/{criterion.maxScore} — {criterion.explanation}");
        }
        Debug.Log($"Total Score: {evaluation.totalScore}");
        Debug.Log($"Performance Level: {evaluation.performanceLevel}");
        Debug.Log($"Overall Summary: {evaluation.overallExplanation}");
    }

    private void DisplayEvaluationToUI(DynamicEvaluationResult evaluation)
    {
        if (progressBarUI != null)
            progressBarUI.HideProgressBar();

        if (evaluationCanvas != null)
            evaluationCanvas.gameObject.SetActive(true);

        // Defensive Phase 1 short-circuit. The current sandbox backend
        // returns the flat rubricAssessment envelope (no `report`
        // wrapper) for Phase 1, so this branch is not reached for
        // Phase 1 today. If a future backend change reintroduces the
        // legacy `report` wrapper, Phase 1 students must still see only
        // the processing message — never the formatter's narrative or
        // the rubric detail.
        if (IsPhase1StudyFlow())
        {
            ShowPhase1ProcessingMessage(Phase1ProcessingTitle, Phase1ProcessingBodyNormal);
            StartCoroutine(RefreshScrollViewLayout());
            return;
        }

        MedicalReportFormatter formatter = reportText.GetComponent<MedicalReportFormatter>();
        if (formatter != null)
        {
            formatter.ApplyFormattedReport(evaluation, conversationTurns.Count);
            ApplyRubricAssessment(evaluation);
            Debug.Log("使用格式化器显示报告");
        }
        else
        {
            Debug.LogError("未找到MedicalReportFormatter组件！");
            DisplayOriginalFormat(evaluation);
        }

        StartCoroutine(RefreshScrollViewLayout());
    }

    private void DisplayOriginalFormat(DynamicEvaluationResult evaluation)
    {
        StringBuilder reportContent = new StringBuilder();
        reportContent.AppendLine("Evaluation Report\n");
        reportContent.AppendLine($"Conversation Summary: {conversationTurns.Count} dialogue turns completed\n");
        reportContent.AppendLine("Assessment Criteria Details\n");

        foreach (var criterion in evaluation.criteria)
        {
            reportContent.AppendLine($"• {criterion.name}");
            reportContent.AppendLine($"  Score: {criterion.score}/{criterion.maxScore}");
            reportContent.AppendLine($"  Explanation: {criterion.explanation}\n");
        }

        reportContent.AppendLine("Overall Assessment");
        reportContent.AppendLine($"Total Score: {evaluation.totalScore}");
        reportContent.AppendLine($"Performance Level: {evaluation.performanceLevel}\n");
        reportContent.AppendLine("Overall Summary");
        reportContent.AppendLine(evaluation.overallExplanation);

        if (reportText != null)
        {
            reportText.text = reportContent.ToString();
        }
    }

    private IEnumerator RefreshScrollViewLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        var contentSizeFitter = reportText.GetComponent<UnityEngine.UI.ContentSizeFitter>();
        if (contentSizeFitter != null)
        {
            contentSizeFitter.SetLayoutVertical();
        }

        Transform contentParent = reportText.transform.parent;
        if (contentParent != null)
        {
            var parentContentSizeFitter = contentParent.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            if (parentContentSizeFitter != null)
            {
                parentContentSizeFitter.SetLayoutVertical();
            }
        }

        Canvas.ForceUpdateCanvases();

        ScrollRect scrollRect = reportText.GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            Debug.Log($"ScrollRect找到了！Content高度: {scrollRect.content.rect.height}");
            Debug.Log($"Viewport高度: {scrollRect.viewport.rect.height}");
            Debug.Log($"可滚动: {scrollRect.content.rect.height > scrollRect.viewport.rect.height}");
        }
        else
        {
            Debug.Log("没有找到ScrollRect组件，但这可能是正常的");
        }
    }

    public void HideEvaluationPanel()
    {
        if (evaluationCanvas != null)
        {
            evaluationCanvas.gameObject.SetActive(false);
            Debug.Log("评估报告已关闭");
        }

        if (progressBarUI != null)
            progressBarUI.HideProgressBar();
    }

    public int GetConversationCount()
    {
        return conversationTurns.Count;
    }

    public bool CanViewReport()
    {
        return true;
    }

    public List<ConversationTurn> GetConversationTurns()
    {
        return conversationTurns;
    }

    private static StudyTaskResultPayload TryBuildStudyTaskPayload()
    {
        try
        {
            return StudyTaskResultBuffer.BuildPayload(StudyDataDefaults.StatusCompleted);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[ScoreManager] Could not build study task payload for /llm-scoring context: " + ex.Message);
            return null;
        }
    }

    private static ScoringTaskContext BuildScoringTaskContext(StudyTaskResultPayload payload)
    {
        string phaseId = ResolveContextString(
            payload?.taskContext?.phaseId,
            StudyRuntimeContext.PhaseId,
            StudyDataDefaults.PhaseUnknown);

        string activityType = ResolveContextString(
            payload?.taskContext?.activityType,
            StudyRuntimeContext.ActivityType,
            StudyDataDefaults.ActivityUnspecified);

        string taskType = ResolveContextString(
            payload?.taskContext?.taskType,
            StudyRuntimeContext.TaskType,
            StudyDataDefaults.TaskTypeUnspecified);

        string scoringMode = ResolveContextString(
            payload?.taskContext?.scoringMode,
            StudyRuntimeContext.ScoringMode,
            StudyDataDefaults.ScoringNone);

        string patientProfile = ResolveContextString(
            payload?.taskContext?.patientId,
            StudyRuntimeContext.PatientId,
            "");

        string sessionRef = ResolveContextString(
            payload?.sessionContext?.sessionId,
            StudyRuntimeContext.SessionContext?.sessionId,
            "");

        string feedbackUse = DeriveFeedbackUse(phaseId);

        bool hasAnyContext =
            !string.IsNullOrEmpty(feedbackUse) ||
            !string.IsNullOrEmpty(phaseId) ||
            !string.IsNullOrEmpty(activityType) ||
            !string.IsNullOrEmpty(taskType) ||
            !string.IsNullOrEmpty(patientProfile);

        if (!hasAnyContext)
            return null;

        return new ScoringTaskContext
        {
            feedbackUse = string.IsNullOrEmpty(feedbackUse) ? null : feedbackUse,
            phaseId = string.IsNullOrEmpty(phaseId) ? null : phaseId,
            activityType = string.IsNullOrEmpty(activityType) ? null : activityType,
            taskType = string.IsNullOrEmpty(taskType) ? null : taskType,
            patientProfile = string.IsNullOrEmpty(patientProfile) ? null : patientProfile,
            scoringMode = string.IsNullOrEmpty(scoringMode) ? null : scoringMode,
            sessionRef = string.IsNullOrEmpty(sessionRef) ? null : sessionRef
        };
    }

    private static ScoringStudyTaskContext BuildScoringStudyTaskContext(StudyTaskResultPayload payload)
    {
        if (payload == null)
            return null;

        bool hasItems = payload.itemResults != null && payload.itemResults.Count > 0;
        if (!hasItems)
            return null;

        ScoringStudyTaskContext ctx = new ScoringStudyTaskContext
        {
            status = string.IsNullOrEmpty(payload.status) ? null : payload.status,
            startedAt = string.IsNullOrEmpty(payload.startedAt) ? null : payload.startedAt,
            completedAt = string.IsNullOrEmpty(payload.completedAt) ? null : payload.completedAt,
            items = new List<ScoringItemContext>(payload.itemResults.Count)
        };

        List<ScoringTranscriptTurn> aggregatedTurns = new List<ScoringTranscriptTurn>();
        List<ScoringInteractionEvent> aggregatedEvents = new List<ScoringInteractionEvent>();

        for (int i = 0; i < payload.itemResults.Count; i++)
        {
            StudyItemResult item = payload.itemResults[i];
            if (item == null)
                continue;

            ctx.items.Add(ProjectScoringItem(item));

            if (item.transcriptTurns != null)
            {
                int turnFallbackIndex = aggregatedTurns.Count;
                foreach (StudyTranscriptTurn t in item.transcriptTurns)
                {
                    if (t == null)
                        continue;
                    aggregatedTurns.Add(ProjectScoringTranscriptTurn(t, turnFallbackIndex++));
                }
            }

            if (item.interactionEvents != null)
            {
                foreach (StudyInteractionEvent e in item.interactionEvents)
                {
                    if (e == null)
                        continue;
                    aggregatedEvents.Add(ProjectScoringInteractionEvent(e));
                }
            }
        }

        ctx.transcriptTurns = aggregatedTurns.Count > 0 ? aggregatedTurns : null;
        ctx.interactionEvents = aggregatedEvents.Count > 0 ? aggregatedEvents : null;
        return ctx;
    }

    private static ScoringItemContext ProjectScoringItem(StudyItemResult item)
    {
        return new ScoringItemContext
        {
            itemId = string.IsNullOrEmpty(item.itemId) ? null : item.itemId,
            taskId = string.IsNullOrEmpty(item.taskId) ? null : item.taskId,
            taskType = string.IsNullOrEmpty(item.taskType) ? null : item.taskType,
            sectionId = string.IsNullOrEmpty(item.sectionId) ? null : item.sectionId,
            sectionType = string.IsNullOrEmpty(item.sectionType) ? null : item.sectionType,
            scriptNumber = item.scriptNumber,
            promptText = string.IsNullOrEmpty(item.promptText) ? null : item.promptText,
            stimulusRef = string.IsNullOrEmpty(item.stimulusRef) ? null : item.stimulusRef,
            targetAnswer = string.IsNullOrEmpty(item.targetAnswer) ? null : item.targetAnswer,
            alternateTarget = string.IsNullOrEmpty(item.alternateTarget) ? null : item.alternateTarget,
            patientFinalResponse = string.IsNullOrEmpty(item.patientFinalResponse) ? null : item.patientFinalResponse,
            studentSelectedScore = item.studentSelectedScore,
            expectedScore = item.expectedScore,
            scoreMatchesExpected = item.scoreMatchesExpected,
            completionChecked = item.completionChecked,
            cueUsed = item.cueUsed,
            cueLevel = string.IsNullOrEmpty(item.cueLevel) ? null : item.cueLevel,
            startedAt = string.IsNullOrEmpty(item.startedAt) ? null : item.startedAt,
            completedAt = string.IsNullOrEmpty(item.completedAt) ? null : item.completedAt
        };
    }

    private static ScoringTranscriptTurn ProjectScoringTranscriptTurn(StudyTranscriptTurn turn, int fallbackTurnIndex)
    {
        return new ScoringTranscriptTurn
        {
            turnIndex = turn.turnIndex ?? fallbackTurnIndex,
            speakerRole = string.IsNullOrEmpty(turn.speaker) ? null : turn.speaker,
            text = string.IsNullOrEmpty(turn.text) ? null : turn.text,
            cueLevel = null,
            itemId = string.IsNullOrEmpty(turn.itemId) ? null : turn.itemId
        };
    }

    private static ScoringInteractionEvent ProjectScoringInteractionEvent(StudyInteractionEvent ev)
    {
        return new ScoringInteractionEvent
        {
            eventType = string.IsNullOrEmpty(ev.eventType) ? null : ev.eventType,
            itemId = string.IsNullOrEmpty(ev.itemId) ? null : ev.itemId,
            cueLevel = string.IsNullOrEmpty(ev.cueLevel) ? null : ev.cueLevel,
            message = string.IsNullOrEmpty(ev.message) ? null : ev.message,
            occurredAt = string.IsNullOrEmpty(ev.timestamp) ? null : ev.timestamp
        };
    }

    private static string DeriveFeedbackUse(string phaseId)
    {
        if (string.Equals(phaseId, StudyDataDefaults.Phase1, StringComparison.OrdinalIgnoreCase))
            return "phase1_ai_interaction";
        if (string.Equals(phaseId, StudyDataDefaults.Phase2, StringComparison.OrdinalIgnoreCase))
            return "phase2_training";
        return null;
    }

    private static string ResolveContextString(string primary, string fallback, string treatAsEmpty)
    {
        if (!IsBlankOrSentinel(primary, treatAsEmpty))
            return primary.Trim();
        if (!IsBlankOrSentinel(fallback, treatAsEmpty))
            return fallback.Trim();
        return "";
    }

    private static bool IsBlankOrSentinel(string value, string sentinel)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;
        if (string.IsNullOrEmpty(sentinel))
            return false;
        return string.Equals(value.Trim(), sentinel, StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyRubricAssessment(DynamicEvaluationResult evaluation, bool hideAiInteractionBlock = false)
    {
        if (evaluation == null || evaluation.rubricAssessment == null)
            return;

        StudyRubricFeedbackBlock block = BuildRubricFeedbackBlockFromAssessment(evaluation.rubricAssessment);
        if (block == null)
            return;

        StudyFeedbackPresenter[] presenters = FindObjectsByType<StudyFeedbackPresenter>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (StudyFeedbackPresenter presenter in presenters)
        {
            if (presenter == null)
                continue;
            if (hideAiInteractionBlock)
                presenter.SetAiInteractionBlockVisible(false);
            presenter.SetRubricFeedback(block);
        }
    }

    private static StudyRubricFeedbackBlock BuildRubricFeedbackBlockFromAssessment(RubricAssessmentResult assessment)
    {
        if (assessment == null)
            return null;

        string granularity = NormalizeAssessmentGranularity(assessment);
        if (string.IsNullOrEmpty(granularity))
            return null;

        StudyRubricFeedbackBlock block = new StudyRubricFeedbackBlock
        {
            taskId = string.IsNullOrEmpty(assessment.sectionId) ? null : assessment.sectionId,
            taskSummary = string.IsNullOrEmpty(assessment.taskSummary) ? null : assessment.taskSummary,
            assessmentGranularity = granularity,
            itemFeedback = null,
            taskFeedback = null
        };

        if (string.Equals(granularity, "task_level", StringComparison.OrdinalIgnoreCase))
        {
            if (assessment.taskFeedback == null)
                return null;
            block.taskFeedback = ProjectTaskFeedback(assessment.taskFeedback);
            if (block.taskFeedback == null)
                return null;
            return block;
        }

        // item_level
        if (assessment.itemFeedback == null || assessment.itemFeedback.Count == 0)
            return null;

        List<StudyRubricFeedbackItem> items = new List<StudyRubricFeedbackItem>(assessment.itemFeedback.Count);
        foreach (RubricAssessmentItem src in assessment.itemFeedback)
        {
            if (src == null)
                continue;
            items.Add(ProjectItemFeedback(src));
        }

        if (items.Count == 0)
            return null;

        block.itemFeedback = items;
        return block;
    }

    private static string NormalizeAssessmentGranularity(RubricAssessmentResult assessment)
    {
        string declared = assessment.assessmentGranularity;
        if (!string.IsNullOrWhiteSpace(declared))
        {
            string normalized = declared.Trim().ToLowerInvariant();
            if (normalized == "item_level" || normalized == "task_level")
                return normalized;
            // Unknown declared value — fall through to inference.
        }

        bool hasItems = assessment.itemFeedback != null && assessment.itemFeedback.Count > 0;
        bool hasTask = assessment.taskFeedback != null;
        if (hasItems && hasTask)
            return "item_level"; // Safe deterministic default: prefer item-level when both are inadvertently present.
        if (hasItems)
            return "item_level";
        if (hasTask)
            return "task_level";
        return null;
    }

    private static StudyRubricFeedbackItem ProjectItemFeedback(RubricAssessmentItem src)
    {
        return new StudyRubricFeedbackItem
        {
            itemId = string.IsNullOrEmpty(src.itemId) ? null : src.itemId,
            studentSelectedScore = src.studentSelectedScore,
            expectedScore = src.expectedScore,
            scoreMatchesExpected = src.scoreMatchesExpected,
            rubricReason = string.IsNullOrEmpty(src.rubricReason) ? null : src.rubricReason
        };
    }

    private static StudyRubricFeedbackTask ProjectTaskFeedback(RubricAssessmentTask src)
    {
        StudyRubricFeedbackTask task = new StudyRubricFeedbackTask
        {
            taskId = string.IsNullOrEmpty(src.taskId) ? null : src.taskId,
            studentSelectedScore = src.studentSelectedScore,
            expectedScore = src.expectedScore,
            scoreMatchesExpected = src.scoreMatchesExpected,
            rubricReason = string.IsNullOrEmpty(src.rubricReason) ? null : src.rubricReason
        };

        if (src.scoringDetail != null)
        {
            task.validUniqueResponseCount = src.scoringDetail.validUniqueResponseCount;
            task.excludedExampleCount = src.scoringDetail.excludedExampleCount;
            task.repeatedResponseCount = src.scoringDetail.repeatedResponseCount;
            task.offCategoryCount = src.scoringDetail.offCategoryCount;
        }

        return task;
    }
}

[Serializable]
public class ConversationTurn
{
    public string Patient;
    public string Nurse;
}

[Serializable]
public class ScoringTurn
{
    public string patient;
    public string nurse;
    public string slpStudent;
}

[Serializable]
public class ScoringMetadata
{
    public int turnIndex;
    public string client;
}

[Serializable]
public class ScoringRequestPayload
{
    public List<ScoringTurn> conversationTurns;
    public ScoringMetadata metadata;
    public ScoringTaskContext taskContext;
    public ScoringStudyTaskContext studyTaskContext;
}

[Serializable]
public class ScoringTaskContext
{
    public string feedbackUse;
    public string phaseId;
    public string activityType;
    public string taskType;
    public string patientProfile;
    public string scoringMode;
    public string sessionRef;
}

[Serializable]
public class ScoringStudyTaskContext
{
    public string status;
    public string startedAt;
    public string completedAt;
    public List<ScoringItemContext> items;
    public List<ScoringTranscriptTurn> transcriptTurns;
    public List<ScoringInteractionEvent> interactionEvents;
}

[Serializable]
public class ScoringItemContext
{
    public string itemId;
    public string taskId;
    public string taskType;
    public string sectionId;
    public string sectionType;
    public int? scriptNumber;
    public string promptText;
    public string stimulusRef;
    public string targetAnswer;
    public string alternateTarget;
    public string patientFinalResponse;
    public int? studentSelectedScore;
    public int? expectedScore;
    public bool? scoreMatchesExpected;
    public bool? completionChecked;
    public bool? cueUsed;
    public string cueLevel;
    public string startedAt;
    public string completedAt;
}

[Serializable]
public class ScoringTranscriptTurn
{
    public int? turnIndex;
    public string speakerRole;
    public string text;
    public string cueLevel;
    public string itemId;
}

[Serializable]
public class ScoringInteractionEvent
{
    public string eventType;
    public string itemId;
    public string cueLevel;
    public string message;
    public string occurredAt;
}

[Serializable]
public class DynamicEvaluationResult
{
    public List<CriterionScore> criteria;
    public int totalScore;
    public string performanceLevel;
    public string overallExplanation;

    public string feedbackUse;
    public string feedbackSource;
    public List<NarrativeFeedbackSection> feedbackSections;
    public RubricAssessmentResult rubricAssessment;
    public FeedbackReportMetadata metadata;
}

[Serializable]
public class CriterionScore
{
    public string name;
    public int score;
    public int maxScore;
    public string explanation;
}

[Serializable]
public class NarrativeFeedbackSection
{
    public string sectionId;
    public string title;
    public string body;
}

[Serializable]
public class RubricAssessmentResult
{
    public string assessmentGranularity;
    public string sectionId;
    public string taskSummary;
    public List<RubricAssessmentItem> itemFeedback;
    public RubricAssessmentTask taskFeedback;
}

[Serializable]
public class RubricAssessmentItem
{
    public string itemId;
    public int? studentSelectedScore;
    public int? expectedScore;
    public bool? scoreMatchesExpected;
    public string rubricReason;
    public bool? cueUsed;
    public string cueType;
}

[Serializable]
public class RubricAssessmentTask
{
    public string taskId;
    public int? studentSelectedScore;
    public int? expectedScore;
    public bool? scoreMatchesExpected;
    public string rubricReason;
    public RubricAssessmentTaskScoringDetail scoringDetail;
}

[Serializable]
public class RubricAssessmentTaskScoringDetail
{
    public int? validUniqueResponseCount;
    public int? excludedExampleCount;
    public int? repeatedResponseCount;
    public int? offCategoryCount;
}

[Serializable]
public class FeedbackReportMetadata
{
    public string phaseId;
    public string taskType;
    public string patientProfile;
    public string sessionRef;
    public string facultyId;
    public string comparisonGroupId;
}
