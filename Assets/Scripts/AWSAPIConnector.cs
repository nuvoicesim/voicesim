using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using Newtonsoft.Json;
using System;

public class AWSAPIConnector : MonoBehaviour
{
    public static AWSAPIConnector Instance;

    [Header("AWS API Configuration")]
    public string awsApiUrl = "https://dxa66vt2tl.execute-api.us-east-1.amazonaws.com/dev/chat-history";

    [Header("Test Configuration")]
    [Tooltip("Hard-coded user ID for testing")]
    public string testUserId = "test_user_12345";

    [Tooltip("Hard-coded simulation level for testing")]
    public int testSimulationLevel = 1;

    [Header("Debug Settings")]
    public bool enableDetailedLogging = true;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("AWS API Connector instance created");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        Debug.Log("=== AWS API CONNECTOR INITIALIZED ===");
        Debug.Log($"API Endpoint: {awsApiUrl}");
        Debug.Log($"Test User ID: {testUserId}");
        Debug.Log($"Test Simulation Level: {testSimulationLevel}");
    }

    public void ApplyLoginContext(string userId, int simLevel)
    {
        if (!string.IsNullOrWhiteSpace(userId)) testUserId = userId.Trim();
        if (simLevel > 0) testSimulationLevel = simLevel;

        if (enableDetailedLogging)
            Debug.Log($"[AWSAPIConnector] Using test fields -> userID='{testUserId}', simulationLevel={testSimulationLevel}");
    }

    // =================
    // 1. 保存聊天历史记录
    // =================
    public void SaveChatHistory(List<Dictionary<string, string>> chatMessages)
    {
        var chatHistoryData = new ChatHistoryPayload
        {
            userID = testUserId,
            simulationLevel = testSimulationLevel,
            chatHistory = chatMessages,
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            scenario = "Therapy",
            report = null  // chat history没有report数据
        };

        StartCoroutine(SendChatHistoryCoroutine(chatHistoryData));
    }

    // =================
    // 2. 保存评估报告和聊天历史（一次性发送）
    // =================
    public void SaveEvaluationReport(DynamicEvaluationResult evaluation)
    {
        // 获取当前的聊天历史
        var chatMessages = new List<Dictionary<string, string>>();
        if (OpenAIRequest.Instance != null)
        {
            chatMessages = OpenAIRequest.Instance.GetChatMessages() ?? new List<Dictionary<string, string>>();
        }

        // 创建report数据结构
        var reportData = new ReportData
        {
            totalScore = evaluation.totalScore,
            performanceLevel = evaluation.performanceLevel,
            overallExplanation = evaluation.overallExplanation,
            criteriaDetails = evaluation.criteria
        };

        var combinedPayload = new ChatHistoryPayload
        {
            userID = testUserId,
            simulationLevel = testSimulationLevel,
            chatHistory = chatMessages,  // 包含完整的聊天历史
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            scenario = "Therapy",
            report = reportData  // 同时包含report数据
        };

        Debug.Log($"💾 Saving both chat history ({chatMessages.Count} messages) and evaluation report together");
        StartCoroutine(SendCombinedDataCoroutine(combinedPayload));
    }

    // =================
    // 测试方法
    // =================

    [ContextMenu("Test Send Sample Chat History")]
    public void TestSendSampleChatHistory()
    {
        var sampleChatMessages = new List<Dictionary<string, string>>
        {
            new Dictionary<string, string> { { "role", "system" }, { "content", "You are a patient with communication difficulties" } },
            new Dictionary<string, string> { { "role", "user" }, { "content", "Hello, how are you feeling today?" } },
            new Dictionary<string, string> { { "role", "assistant" }, { "content", "I... um... not... feeling... good... [4]" } },
            new Dictionary<string, string> { { "role", "user" }, { "content", "Can you tell me more about what's bothering you?" } },
            new Dictionary<string, string> { { "role", "assistant" }, { "content", "My... head... hurts... um... since... stroke... [3]" } }
        };

        Debug.Log("🧪 Testing chat history upload to /chat-history endpoint...");
        SaveChatHistory(sampleChatMessages);
    }

    [ContextMenu("Test Send Sample Evaluation")]
    public void TestSendSampleEvaluation()
    {
        var sampleEvaluation = new DynamicEvaluationResult
        {
            totalScore = 85,
            performanceLevel = "Proficient",
            overallExplanation = "Student demonstrated good communication skills with appropriate empathy and therapeutic techniques.",
            criteria = new List<CriterionScore>
            {
                new CriterionScore { name = "Empathy", score = 9, maxScore = 10, explanation = "Showed excellent understanding of patient emotions" },
                new CriterionScore { name = "Communication", score = 8, maxScore = 10, explanation = "Clear and professional communication style" },
                new CriterionScore { name = "Assessment", score = 7, maxScore = 10, explanation = "Good assessment skills, could be more thorough" }
            }
        };

        Debug.Log("🧪 Testing evaluation report upload to /report endpoint...");
        SaveEvaluationReport(sampleEvaluation);
    }

    // =================
    // 私有协程方法
    // =================

    private IEnumerator SendChatHistoryCoroutine(ChatHistoryPayload payload)
    {
        string jsonData = JsonConvert.SerializeObject(payload, Formatting.Indented);

        if (enableDetailedLogging)
        {
            Debug.Log("=== SENDING CHAT HISTORY TO AWS /chat-history ===");
            Debug.Log($"Payload: {jsonData}");
        }

        yield return StartCoroutine(SendPostRequest(awsApiUrl, jsonData, "Chat History"));
    }

    private IEnumerator SendCombinedDataCoroutine(ChatHistoryPayload payload)
    {
        string jsonData = JsonConvert.SerializeObject(payload, Formatting.Indented);

        if (enableDetailedLogging)
        {
            Debug.Log("=== SENDING COMBINED CHAT HISTORY + EVALUATION REPORT TO AWS ===");
            Debug.Log($"Chat Messages Count: {payload.chatHistory?.Count ?? 0}");
            Debug.Log($"Report Data: {(payload.report != null ? "Present" : "Null")}");
            Debug.Log($"Payload: {jsonData}");
        }

        yield return StartCoroutine(SendPostRequest(awsApiUrl, jsonData, "Combined Chat History + Evaluation"));
    }

    // 通用POST请求方法
    private IEnumerator SendPostRequest(string url, string jsonData, string requestType)
    {
        var request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();

        // 设置请求头
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Accept", "application/json");

        // 如果你的AWS API需要特殊认证头，在这里添加：
        // request.SetRequestHeader("x-api-key", "your-aws-api-key");
        // request.SetRequestHeader("Authorization", "Bearer your-token");

        request.timeout = 30;

        Debug.Log($"📤 Sending {requestType} to AWS: {url}");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log($"✅ {requestType} sent successfully to AWS");
            Debug.Log($"Response: {request.downloadHandler.text}");
        }
        else
        {
            Debug.LogError($"❌ Failed to send {requestType} to AWS");
            Debug.LogError($"Error: {request.error}");
            Debug.LogError($"Response Code: {request.responseCode}");
            Debug.LogError($"Response Body: {request.downloadHandler.text}");
        }

        request.Dispose();
    }

    // =================
    // 与现有系统集成的方法
    // =================

    // 移除单独的聊天历史保存方法，现在只在evaluation时一起发送
    // public void SaveChatHistory() - 已移除，现在只通过SaveEvaluationReport保存

    // 在ScoreManager中调用此方法来保存评估和聊天历史
    public void SaveEvaluationFromScoreManager(DynamicEvaluationResult evaluation)
    {
        if (evaluation != null)
        {
            Debug.Log($"📊 Saving evaluation report + chat history to AWS (Score: {evaluation.totalScore})");
            SaveEvaluationReport(evaluation);  // 这现在会同时保存聊天历史和报告
        }
        else
        {
            Debug.LogWarning("No evaluation data available to save");
        }
    }
}

// =================
// 数据结构定义
// =================

[Serializable]
public class ChatHistoryPayload
{
    public string userID;                        // 注意：大写D
    public int simulationLevel;
    public List<Dictionary<string, string>> chatHistory;  // 完整的聊天历史
    public string timestamp;
    public string scenario;
    public ReportData report;  // 新增report字段
}

[Serializable]
public class ReportData
{
    public int totalScore;
    public string performanceLevel;
    public string overallExplanation;
    public List<CriterionScore> criteriaDetails;
}

// 保留原有的EvaluationReportPayload以防需要
[Serializable]
public class EvaluationReportPayload
{
    public string userID;                        // 注意：大写D
    public int simulationLevel;
    public int totalScore;
    public string performanceLevel;
    public string overallExplanation;
    public List<CriterionScore> criteriaDetails;
    public string timestamp;
    public string scenario;
}