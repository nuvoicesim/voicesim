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
    [Header("LLM Scoring Backend")]
    [SerializeField] private string fallbackScoringUrl = "https://f0kk74qeyf.execute-api.us-west-2.amazonaws.com/dev/llm-scoring";
    [SerializeField] private int scoringTimeoutSeconds = 60;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        if (evaluationCanvas != null)
            evaluationCanvas.gameObject.SetActive(false);

        if (closeButton != null)
            closeButton.onClick.AddListener(HideEvaluationPanel);
    }

    public void Initialize(string scenario)
    {
        currentScenario = scenario;
        Debug.Log($"[ScoreManager] Initialized for scenario={currentScenario}. Backend owns scoring prompt.");
    }

    public void RecordTurn(string patientResponse, string nurseResponse)
    {
        conversationTurns.Add(new ConversationTurn
        {
            Patient = patientResponse,
            Nurse = nurseResponse
        });
        Debug.Log($"Turn {conversationTurns.Count} recorded.");
    }

    public void SubmitEvaluation()
    {
        if (conversationTurns.Count == 0)
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

    private void CreateNoConversationReport()
    {
        if (progressBarUI != null)
            progressBarUI.HideProgressBar();

        if (evaluationCanvas != null)
            evaluationCanvas.gameObject.SetActive(true);

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

        string scoringUrl = fallbackScoringUrl;
        if (OpenAIRequest.Instance != null)
        {
            scoringUrl = OpenAIRequest.Instance.GetScoringEndpointUrl();
        }

        string userId = "anonymous-user";
        int simulationLevel = 1;
        if (OpenAIRequest.Instance != null)
        {
            if (!string.IsNullOrWhiteSpace(OpenAIRequest.Instance.CurrentUserId))
                userId = OpenAIRequest.Instance.CurrentUserId;
            simulationLevel = Mathf.Clamp(OpenAIRequest.Instance.CurrentSimulationLevel, 1, 3);
        }

        var requestBody = new ScoringRequestPayload
        {
            userID = userId,
            simulationLevel = simulationLevel,
            conversationTurns = conversationTurns
                .ConvertAll(turn => new ScoringTurn { patient = turn.Patient ?? "", nurse = turn.Nurse ?? "" }),
            metadata = new ScoringMetadata
            {
                sessionId = $"{userId}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                turnIndex = conversationTurns.Count,
                client = "unity"
            }
        };

        string jsonBody = JsonConvert.SerializeObject(requestBody);

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
            Debug.LogError("OpenAI Request Error: " + request.error);
            if (progressBarUI != null)
                progressBarUI.HideProgressBar();
            yield break;
        }

        if (progressBarUI != null)
            progressBarUI.UpdateProgress(0.9f, "Compiling assessment report...");
        yield return new WaitForSeconds(0.3f);

        yield return StartCoroutine(ProcessAIResponse(request.downloadHandler.text));
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
            if (reportToken == null)
                throw new Exception("Missing `report` in scoring response.");

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

        MedicalReportFormatter formatter = reportText.GetComponent<MedicalReportFormatter>();
        if (formatter != null)
        {
            formatter.ApplyFormattedReport(evaluation, conversationTurns.Count);
            Debug.Log("使用格式化器显示报告");
        }
        else
        {
            Debug.LogError("未找到MedicalReportFormatter组件！");
            DisplayOriginalFormat(evaluation);
        }

        if (AWSAPIConnector.Instance != null)
        {
            Debug.Log("Saving evaluation to AWS database...");
            AWSAPIConnector.Instance.SaveEvaluationFromScoreManager(evaluation);
        }
        else
        {
            Debug.LogWarning("AWSAPIConnector instance not found - evaluation not saved to database");
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
}

[Serializable]
public class ScoringMetadata
{
    public string sessionId;
    public int turnIndex;
    public string client;
}

[Serializable]
public class ScoringRequestPayload
{
    public string userID;
    public int simulationLevel;
    public List<ScoringTurn> conversationTurns;
    public ScoringMetadata metadata;
}

[Serializable]
public class DynamicEvaluationResult
{
    public List<CriterionScore> criteria;
    public int totalScore;
    public string performanceLevel;
    public string overallExplanation;
}

[Serializable]
public class CriterionScore
{
    public string name;
    public int score;
    public int maxScore;
    public string explanation;
}
