using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Linq;
using System.Text.RegularExpressions;

public class OpenAIRequest : MonoBehaviour
{
    public static OpenAIRequest Instance; // Singleton instance
    public string apiUrl = "https://api.openai.com/v1/chat/completions";
    public string apiKey;
    public string CurrentUserId { get; private set; }
    [SerializeField] private string currentScenario = ""; // left empty until login sets it
    public string lostResponse = "umm... fast... uh... fast... ";
    public float maxSpeechSpeed = 200f;
    // Components
    private CharacterAnimationController animationController;
    private EmotionController emotionController;

    // Internal state
    private float currentSpeechSpeed;
    private string basePath;
    private List<Dictionary<string, string>> chatMessages;
    private string currentPatientResponse = "";

    // Precompiled regex for emotion/motion code extraction
    private static readonly Regex EmotionMotionRegex =
        new Regex(@"\[(\d+)\]\[(\d+)\]", RegexOptions.Compiled);

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // Uncomment if you want this object to persist across scenes
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // --- Development-only SSL/TLS relaxations (avoid in production) ---
#if UNITY_EDITOR && !UNITY_WEBGL
        try
        {
            // Completely bypass SSL certificate validation (DEV ONLY)
            System.Net.ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            // Support multiple security protocols
            System.Net.ServicePointManager.SecurityProtocol =
                (System.Net.SecurityProtocolType)3072 | // Tls12
                (System.Net.SecurityProtocolType)768  | // Tls11
                (System.Net.SecurityProtocolType)192;   // Tls10

            // Force HTTP/1.1 behavior tweaks
            System.Net.ServicePointManager.DefaultConnectionLimit = 10;
            System.Net.ServicePointManager.Expect100Continue = false;
            System.Net.ServicePointManager.UseNagleAlgorithm = false;

            Debug.Log("DEV SSL/TLS overrides configured");
            Debug.Log("✓ Enhanced SSL/TLS settings configured");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to configure SSL/TLS: {e.Message}");
        }
#endif

        // Load API key first, then run diagnostics
        LoadApiKey();
        StartCoroutine(NetworkDiagnostics());

        // Initialize components
        animationController = GetComponent<CharacterAnimationController>();
        emotionController = GetComponent<EmotionController>();
        if (emotionController == null)
            Debug.LogError("EmotionController component not found on the GameObject.");

        // Initialize prompts/chat only if a scenario is already set (e.g., via Inspector)
        if (!string.IsNullOrEmpty(currentScenario))
        {
            basePath = Path.Combine(Application.streamingAssetsPath, "Prompts", currentScenario);
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.Initialize(currentScenario);
            InitializeChat();
        }
        else
        {
            Debug.LogWarning("[OpenAIRequest] currentScenario is empty at Start; will initialize after login via ApplyLoginContext.");
        }
    }

    private void LoadApiKey()
    {
        Debug.Log("=== API KEY LOADING ===");

        // Method 1: Environment variable
        apiKey = EnvironmentLoader.GetEnvVariable("OPENAI_API_KEY");
        if (!string.IsNullOrEmpty(apiKey))
        {
            Debug.Log("✓ API key loaded from environment variables");
            return;
        }

        // Method 2: StreamingAssets config
        string configPath = Path.Combine(Application.streamingAssetsPath, "config.json");
        Debug.Log($"Looking for config file at: {configPath}");

        if (File.Exists(configPath))
        {
            try
            {
                string configContent = File.ReadAllText(configPath);
                var config = JsonConvert.DeserializeObject<Dictionary<string, string>>(configContent);

                if (config != null && config.ContainsKey("openai_api_key"))
                {
                    apiKey = config["openai_api_key"];
                    Debug.Log("✓ API key loaded from config file");
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error reading config file: {e.Message}");
            }
        }

        // Method 3: Inspector
        if (!string.IsNullOrEmpty(apiKey))
        {
            Debug.Log("✓ API key found in Inspector");
            return;
        }

        Debug.LogError("✗ No API key found! Please set it via environment variable, config file, or Inspector");
    }

    /// <summary>
    /// Called by LoginPanel after successful login. Sets user and switches scenario based on simulationLevel.
    /// Rebuilds basePath, re-creates the system prompt/chat, and reinitializes scoring for the scenario.
    /// </summary>
    public void ApplyLoginContext(string userId, int simulationLevel)
    {
        CurrentUserId = userId;
        Debug.Log($"[OpenAIRequest] Authenticated user: {CurrentUserId}");

        // Map level → scenario
        switch (simulationLevel)
        {
            case 1: currentScenario = "task1"; break;
            case 2: currentScenario = "task2"; break;
            case 3: currentScenario = "task3"; break;
            default:
                Debug.LogWarning($"[OpenAIRequest] Unknown simulationLevel {simulationLevel}, defaulting to task1");
                currentScenario = "task1";
                break;
        }

        // Rebuild base path + reset prompt/chat + re-init scoring
        basePath = Path.Combine(Application.streamingAssetsPath, "Prompts", currentScenario);
        InitializeChat();
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.Initialize(currentScenario);

        Debug.Log($"[OpenAIRequest] Scenario set to '{currentScenario}'. basePath: {basePath}");
    }

    private IEnumerator NetworkDiagnostics()
    {
        Debug.Log("=== NETWORK DIAGNOSTICS START ===");

        // Test 1: Basic connectivity
        Debug.Log("Testing basic internet connectivity...");
        UnityWebRequest testRequest = UnityWebRequest.Get("https://www.google.com");
        testRequest.timeout = 10;
        yield return testRequest.SendWebRequest();

        if (testRequest.result == UnityWebRequest.Result.Success)
            Debug.Log("✓ Basic internet connection: OK");
        else
        {
            Debug.LogError("✗ Basic internet connection: FAILED");
            Debug.LogError($"Error: {testRequest.error}");
            Debug.LogError($"Response Code: {testRequest.responseCode}");
        }

        // Test 2: HTTPS
        Debug.Log("Testing HTTPS connection...");
        UnityWebRequest httpsTest = UnityWebRequest.Get("https://httpbin.org/get");
        httpsTest.timeout = 10;
        yield return httpsTest.SendWebRequest();

        if (httpsTest.result == UnityWebRequest.Result.Success)
            Debug.Log("✓ HTTPS connection: OK");
        else
        {
            Debug.LogError("✗ HTTPS connection: FAILED");
            Debug.LogError($"Error: {httpsTest.error}");
            Debug.LogError($"Response Code: {httpsTest.responseCode}");
        }

        // Wait a moment to ensure API key load completed
        yield return new WaitForSeconds(1f);

        // Test 3: API key validity
        if (!string.IsNullOrEmpty(apiKey))
        {
            Debug.Log("Testing API key validity...");
            UnityWebRequest keyTest = UnityWebRequest.Get("https://api.openai.com/v1/models");
            keyTest.SetRequestHeader("Authorization", "Bearer " + apiKey.Trim());
            //keyTest.SetRequestHeader("Authorization", "Bearer " + apiKey);
            keyTest.timeout = 15;
            yield return keyTest.SendWebRequest();

            if (keyTest.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("✓ API key validation: OK");
                Debug.Log($"Available models response length: {keyTest.downloadHandler.text.Length}");
            }
            else
            {
                Debug.LogError("✗ API key validation: FAILED");
                Debug.LogError($"Error: {keyTest.error}");
                Debug.LogError($"Response Code: {keyTest.responseCode}");
                Debug.LogError($"Response: {keyTest.downloadHandler.text}");
            }
        }
        else
        {
            Debug.LogError("✗ API key: NOT FOUND - Skipping API key validation");
        }

        Debug.Log("=== NETWORK DIAGNOSTICS END ===");
    }

    private string LoadPromptFromFile(string fileName)
    {
        if (string.IsNullOrEmpty(basePath))
        {
            Debug.LogWarning("[OpenAIRequest] basePath is not set; cannot load prompts.");
            return "";
        }

        string filePath = Path.Combine(basePath, fileName);
        if (!File.Exists(filePath))
        {
            Debug.LogError("Prompt file not found: " + filePath);
            return "";
        }
        return File.ReadAllText(filePath);
    }

    private void InitializeChat()
    {
        // If basePath is missing, fall back to a minimal system prompt to avoid null chat
        if (string.IsNullOrEmpty(basePath))
        {
            Debug.LogWarning("[OpenAIRequest] basePath not set; using minimal system prompt.");
            chatMessages = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string> { { "role", "system" }, { "content", "You are a patient. Keep replies concise. End with [0][0]." } }
            };
            return;
        }

        string baseInstructions = LoadPromptFromFile("baseInstructions.txt");
        if (string.IsNullOrEmpty(baseInstructions))
        {
            Debug.LogError("[OpenAIRequest] baseInstructions is empty; creating minimal system prompt.");
            chatMessages = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string> { { "role", "system" }, { "content", "You are a patient. Keep replies concise. End with [0][0]." } }
            };
            return;
        }

        string emotionInstructions = @"
            IMPORTANT: You will analysis your emotion based on the conversation. Then end EVERY response with corresponding emotion codes:
            - Use [0] for neutral responses or statements
            - Use [1] for responses involving minor pain or discomfort
            - Use [2] for positive responses, gratitude, or when feeling better
            - Use [3] for pain
            - Use [4] for sad
            - Use [5] for anger
            - Use [6] for frustration due to speech block or not being understood
            - Use [7] for thinking or processing information
            - Use [8] for an apologetic grimace
            - Use [9] for crying in frustration when unable to communicate";

        string motionInstructions = @"
            IMPORTANT: You will use the following animations based on the conversation. Then end EVERY response with corresponding motion codes after the emotion code:
            - [0] for neutral responses or statements
            - [1] when unable to answer or feeling lost after doctor’s question
            - [2] for strong affirmative or emphatic agreement
            - [3] for agreement with an attempt to add clarification
            - [4] for actively listening or confirming understanding
            - [5] for passive or minimal acknowledgment
            - [6] for disagreement, denial, or inability to answer
            - [7] for intense frustration when unable to express words
            - [8] for expressing impatience, agitation, or urging the doctor during conversation
            - [9] for struggling to recall words or thinking of a response";

        string systemPrompt = $"{baseInstructions}\n{emotionInstructions}\n{motionInstructions}";

        chatMessages = new List<Dictionary<string, string>>
        {
            new Dictionary<string, string> { { "role", "system" }, { "content", systemPrompt } }
        };

        Debug.Log("System: " + systemPrompt);
    }

    public void ReceiveNurseTranscription(string transcribedText, float speechWpm)
    {
        NurseResponds(transcribedText, speechWpm);
    }

    private void NurseResponds(string nurseMessage, float speechWpm)
    {
        if (chatMessages == null || chatMessages.Count == 0)
            InitializeChat();

        chatMessages.Add(new Dictionary<string, string> { { "role", "user" }, { "content", nurseMessage } });
        PrintChatMessage(chatMessages);
        currentSpeechSpeed = speechWpm;
        Debug.Log("speech speed:" + currentSpeechSpeed);

        StartCoroutine(PostRequest());

        // Evaluate nurse's response
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.RecordTurn(currentPatientResponse, nurseMessage);
    }

    IEnumerator PostRequest()
    {
        Debug.Log("=== STARTING API REQUEST ===");
        Debug.Log($"API URL: {apiUrl}");
        Debug.Log($"API Key exists: {!string.IsNullOrEmpty(apiKey)}");

        if (!string.IsNullOrEmpty(apiKey))
            Debug.Log($"API Key preview: {apiKey.Substring(0, Math.Min(10, apiKey.Length))}...");

        if (currentSpeechSpeed > maxSpeechSpeed)
        {
            Debug.Log($"Speech too fast ({currentSpeechSpeed} > {maxSpeechSpeed}), using lost response");
            HandlePatientResponse(lostResponse, 5, 1);
            yield break;
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("Cannot make API request: No API key available");
            yield break;
        }

        string requestBody = BuildRequestBody();
        Debug.Log($"Request Body Length: {requestBody.Length}");

        // Use UnityWebRequest.PostWwwForm to create a POST, then replace the body with JSON
        var request = UnityWebRequest.PostWwwForm(apiUrl, "");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(requestBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();

        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + apiKey.Trim());

        request.timeout = 45;

        Debug.Log("Sending request to OpenAI...");
        yield return request.SendWebRequest();

        Debug.Log("=== API RESPONSE ===");
        Debug.Log($"Response Code: {request.responseCode}");
        Debug.Log($"Request Result: {request.result}");
        Debug.Log($"Error: {request.error ?? "None"}");
        Debug.Log($"Response Body Length: {request.downloadHandler?.text?.Length ?? 0}");

        if (request.result == UnityWebRequest.Result.ConnectionError ||
            request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError("=== API REQUEST FAILED ===");
            Debug.LogError($"Error Type: {request.result}");
            Debug.LogError($"Error Message: {request.error}");
            Debug.LogError($"Response Code: {request.responseCode}");
            Debug.LogError($"Response Body: {request.downloadHandler.text}");

            // If 421 (misdirected request), try the alternative method
            if (request.responseCode == 421)
            {
                Debug.LogWarning("Got 421 error, trying alternative request...");
                yield return StartCoroutine(TryAlternativeRequest(requestBody));
                yield break;
            }

            ShowDetailedError(request);
        }
        else if (request.responseCode == 200)
        {
            Debug.Log("=== API REQUEST SUCCESS ===");
            try
            {
                var jsonResponse = JObject.Parse(request.downloadHandler.text);
                var messageContent = jsonResponse["choices"][0]["message"]["content"].ToString();
                Debug.Log($"Received message: {messageContent.Substring(0, Math.Min(100, messageContent.Length))}...");
            
                var match = EmotionMotionRegex.Match(messageContent);
                string ttsText = messageContent;
                
                int emotionCode;
                int motionCode;

                if (emotionController == null)
                {
                    Debug.LogError("EmotionController is null");
                    yield break;
                }
                if (!match.Success)
                {
                    Debug.LogWarning("No emotion/motion codes found in alternative response, using defaults");
                    emotionCode = 0;
                    motionCode = 0;
                }
                else
                {
                    emotionCode = int.Parse(match.Groups[1].Value);
                    motionCode = int.Parse(match.Groups[2].Value);
                    Debug.Log($"Extracted emotion code: {emotionCode}, motion code: {motionCode}");
                    ttsText = messageContent.Substring(0, messageContent.Length - 6).Trim();
                    Debug.Log($"TTS Text: {ttsText}");
                }
                
                HandlePatientResponse(ttsText, emotionCode, motionCode);
                
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing API response: {e.Message}");
                Debug.LogError($"Response was: {request.downloadHandler.text}");
            }
        }
        else
        {
            Debug.LogError($"Unexpected response code: {request.responseCode}");
            Debug.LogError($"Response: {request.downloadHandler.text}");
        }
    }

    // Alternative POST method for certain edge-case HTTP errors
    private IEnumerator TryAlternativeRequest(string requestBody)
    {
        Debug.Log("=== TRYING ALTERNATIVE REQUEST METHOD ===");

        var form = new WWWForm();
        form.AddField("data", requestBody);

        var request = UnityWebRequest.Post(apiUrl, form);

        // Replace body with JSON
        request.uploadHandler.Dispose();
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(requestBody));

        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + apiKey.Trim());
        request.SetRequestHeader("User-Agent", "UnityPlayer");

        request.timeout = 45;

        yield return request.SendWebRequest();

        Debug.Log($"Alternative request result: {request.result}");
        Debug.Log($"Alternative response code: {request.responseCode}");

        if (request.result == UnityWebRequest.Result.Success && request.responseCode == 200)
        {
            Debug.Log("=== ALTERNATIVE REQUEST SUCCESS ===");
            try
            {
                var jsonResponse = JObject.Parse(request.downloadHandler.text);
                var messageContent = jsonResponse["choices"][0]["message"]["content"].ToString();

                var match = EmotionMotionRegex.Match(messageContent);
                string ttsText = messageContent;
                
                int emotionCode;
                int motionCode;

                if (emotionController == null)
                {
                    Debug.LogError("EmotionController is null");
                    yield break;
                }
                if (!match.Success)
                {
                    Debug.LogWarning("No emotion/motion codes found in alternative response, using defaults");
                    emotionCode = 0;
                    motionCode = 0;
                }
                else
                {
                    emotionCode = int.Parse(match.Groups[1].Value);
                    motionCode = int.Parse(match.Groups[2].Value);
                    Debug.Log($"Extracted emotion code: {emotionCode}, motion code: {motionCode}");
                    ttsText = messageContent.Substring(0, messageContent.Length - 6).Trim();
                    Debug.Log($"TTS Text: {ttsText}");
                }
                
                HandlePatientResponse(ttsText, emotionCode, motionCode);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing alternative response: {e.Message}");
            }
        }
        else
        {
            Debug.LogError("Alternative request also failed");
            Debug.LogError($"Error: {request.error}");
            Debug.LogError($"Response: {request.downloadHandler.text}");
        }
    }

    private void ShowDetailedError(UnityWebRequest request)
    {
        string errorDetails = "=== DETAILED ERROR ANALYSIS ===\n";

        switch (request.result)
        {
            case UnityWebRequest.Result.ConnectionError:
                errorDetails += "CONNECTION ERROR - Possible causes:\n";
                errorDetails += "• No internet connection\n";
                errorDetails += "• Firewall blocking the application\n";
                errorDetails += "• Antivirus software blocking network access\n";
                errorDetails += "• DNS resolution issues\n";
                break;

            case UnityWebRequest.Result.ProtocolError:
                errorDetails += "PROTOCOL ERROR - Possible causes:\n";
                if (request.responseCode == 401)
                    errorDetails += "• Invalid or expired API key\n";
                else if (request.responseCode == 403)
                    errorDetails += "• API access forbidden (check billing/limits)\n";
                else if (request.responseCode == 429)
                    errorDetails += "• Rate limit exceeded\n";
                else if (request.responseCode >= 500)
                    errorDetails += "• Server error (try again later)\n";
                else
                    errorDetails += $"• HTTP {request.responseCode} error\n";
                break;
        }

        errorDetails += "\nTROUBLESHOOTING STEPS:\n";
        errorDetails += "1. Check your internet connection\n";
        errorDetails += "2. Verify your API key is correct\n";
        errorDetails += "3. Try running the application as administrator\n";
        errorDetails += "4. Check Windows Firewall/antivirus settings\n";
        errorDetails += "5. Try again in a few minutes\n";

        Debug.LogError(errorDetails);
    }

    private void HandlePatientResponse(string responseText, int emotionCode, int motionCode)
    {
        currentPatientResponse = responseText; // for scoring

        chatMessages.Add(new Dictionary<string, string> { { "role", "assistant" }, { "content", responseText } });
        PrintChatMessage(chatMessages);

        if (TTSManager.Instance != null)
            TTSManager.Instance.ConvertTextToSpeech(responseText);
        else
            Debug.LogError("TTSManager instance not found.");

        if (emotionController != null)
            emotionController.HandleEmotionCode(emotionCode, motionCode);
    }

    private string BuildRequestBody()
    {
        if (chatMessages == null || chatMessages.Count == 0)
            InitializeChat();

        var requestObject = new
        {
            model = "gpt-4o-2024-08-06",
            messages = chatMessages,
            temperature = 0.8f
        };
        return JsonConvert.SerializeObject(requestObject, Formatting.Indented);
    }

    public static void PrintChatMessage(List<Dictionary<string, string>> messages)
    {
        if (messages.Count == 0) return;

        var latestMessage = messages[messages.Count - 1];
        string role = latestMessage["role"];
        string content = latestMessage["content"];

        // Extract emotion/motion codes if present
        string emotionCode = "";
        string motionCode = "";
        var match = EmotionMotionRegex.Match(content);
        if (match.Success)
        {
            emotionCode = $" (Emotion: {match.Groups[1].Value})";
            motionCode = $" (Motion: {match.Groups[2].Value})";
        }

        Debug.Log($"[{role.ToUpper()}]{emotionCode}{motionCode}\n{content}\n");
    }
       

    // =================
    // AWS集成相关的公共方法
    // =================

    // 获取聊天消息的公共方法
    public List<Dictionary<string, string>> GetChatMessages()
    {
        return chatMessages;
    }

    // 保存当前对话到AWS的方法（现在不做任何操作，等待report生成时一起发送）
    public void SaveConversationToAWS()
    {
        // 不再立即保存，等待evaluation report生成时一起发送
        Debug.Log("🔄 Conversation will be saved with evaluation report");
    }

    // =================
    // 手动触发保存的方法（用于测试）
    // =================

    [ContextMenu("Debug Chat Messages")]
    public void DebugChatMessages()
    {
        Debug.Log($"=== CHAT MESSAGES DEBUG ({chatMessages?.Count ?? 0} messages) ===");
        if (chatMessages != null)
        {
            for (int i = 0; i < chatMessages.Count; i++)
            {
                var msg = chatMessages[i];
                Debug.Log($"{i}: [{msg["role"]}] {msg["content"]}");
            }
        }
        else
        {
            Debug.Log("Chat messages is null");
        }
    }

    [ContextMenu("Test Save Current Conversation")]
    public void TestSaveCurrentConversation()
    {
        if (AWSAPIConnector.Instance != null && chatMessages != null && chatMessages.Count > 0)
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
