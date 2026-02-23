using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class DialogueOuterResponse
{
    public string requestId;
    public Choice[] choices;
}

[Serializable]
public class Choice
{
    public DialogueMessage message;
}

[Serializable]
public class DialogueMessage
{
    public string role;     // "assistant"
    public string content;  // a JSON string: {"responseText":"...","emotionCode":0,"motionCode":0}
}

[Serializable]
public class DialogueInnerContent
{
    public string responseText;
    public int emotionCode;
    public int motionCode;
}

[Serializable]
public class ChatMessage
{
    public string role;     // "user" | "assistant"
    public string content;
}

[Serializable]
public class DialogueRequestBody
{
    public string userID;
    public int simulationLevel;          // 1,2,3
    public List<ChatMessage> messages;   // full history
}

public class WebGLTextBridge : MonoBehaviour
{
    [Header("Backend")]
    [Tooltip("Backend base url, e.g. https://.../dev")]
    public string backendBaseUrl = "https://f0kk74qeyf.execute-api.us-west-2.amazonaws.com/dev";

    [Tooltip("Dialogue route path")]
    public string dialoguePath = "/llm-dialogue";

    [Header("Dialogue Identity")]
    public string userID = "webgl-demo";
    [Range(1, 3)]
    public int simulationLevel = 1;

    [Header("History Control")]
    [Tooltip("Keep only last N turns (each turn = one user or assistant message). Suggested: 12 to 30.")]
    public int maxMessagesToKeep = 20;

    [Header("References")]
    public WebGLMockResponder mockResponder;
    public WebGLTTSPlayer ttsPlayer;

    [Header("Demo TTS (Placeholder)")]
    public string demoMp3Url = "http://localhost:3000/built/ElevenLabs_test.mp3";

    private readonly List<ChatMessage> _history = new List<ChatMessage>();
    private bool _isRequestInFlight = false;

    private void Awake()
    {
        if (mockResponder == null) mockResponder = GetComponent<WebGLMockResponder>();
        if (ttsPlayer == null) ttsPlayer = FindObjectOfType<WebGLTTSPlayer>();

        if (mockResponder == null) Debug.LogWarning("[WebGLTextBridge] WebGLMockResponder not found.");
        if (ttsPlayer == null) Debug.LogWarning("[WebGLTextBridge] WebGLTTSPlayer not found.");
    }

    // Call this when starting a new scenario / new patient session
    public void ResetConversation()
    {
        _history.Clear();
        Debug.Log("[WebGLTextBridge] Conversation history cleared.");
    }

    public void InjectText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            Debug.LogWarning("[WebGLTextBridge] Empty text.");
            return;
        }

#if UNITY_WEBGL
        if (_isRequestInFlight)
        {
            Debug.LogWarning("[WebGLTextBridge] Request in flight. Ignoring new input to avoid overlap.");
            return;
        }

        Debug.Log("[WebGLTextBridge] WebGL -> calling backend /llm-dialogue: " + text);

        // 1) Add nurse turn to history
        _history.Add(new ChatMessage { role = "user", content = text });
        TrimHistoryIfNeeded();

        // 2) Call backend with full history
        StartCoroutine(CallDialogueCoroutine());
#else
        // Non-WebGL fallback
        if (OpenAIRequest.Instance != null)
        {
            OpenAIRequest.Instance.ReceiveNurseTranscription(text, 0f);
        }
        else
        {
            Debug.LogError("[WebGLTextBridge] OpenAIRequest.Instance is null.");
        }
#endif
    }

    private void TrimHistoryIfNeeded()
    {
        if (maxMessagesToKeep <= 0) return;
        while (_history.Count > maxMessagesToKeep)
        {
            _history.RemoveAt(0);
        }
    }

    private IEnumerator CallDialogueCoroutine()
    {
        _isRequestInFlight = true;

        var reqBody = new DialogueRequestBody
        {
            userID = userID,
            simulationLevel = simulationLevel,
            messages = new List<ChatMessage>(_history) // copy for safety
        };

        string url = backendBaseUrl.TrimEnd('/') + dialoguePath;

        string json;
        try
        {
            json = JsonUtility.ToJson(reqBody);
        }
        catch (Exception e)
        {
            Debug.LogError("[WebGLTextBridge] Failed to serialize request: " + e.Message);
            _isRequestInFlight = false;
            yield break;
        }

        using (var www = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[WebGLTextBridge] /llm-dialogue request failed: " + www.error);
                Debug.LogError("[WebGLTextBridge] Response body: " + (www.downloadHandler != null ? www.downloadHandler.text : "(null)"));
                _isRequestInFlight = false;
                yield break;
            }

            string respText = www.downloadHandler.text;
            Debug.Log("[WebGLTextBridge] /llm-dialogue raw response: " + respText);

            DialogueOuterResponse outer;
            try
            {
                outer = JsonUtility.FromJson<DialogueOuterResponse>(respText);
            }
            catch (Exception e)
            {
                Debug.LogError("[WebGLTextBridge] Outer JSON parse error: " + e.Message);
                _isRequestInFlight = false;
                yield break;
            }

            if (outer == null || outer.choices == null || outer.choices.Length == 0 || outer.choices[0].message == null)
            {
                Debug.LogError("[WebGLTextBridge] Missing choices/message in response.");
                _isRequestInFlight = false;
                yield break;
            }

            string innerStr = outer.choices[0].message.content;
            Debug.Log("[WebGLTextBridge] Inner content string: " + innerStr);

            DialogueInnerContent inner;
            try
            {
                inner = JsonUtility.FromJson<DialogueInnerContent>(innerStr);
            }
            catch (Exception e)
            {
                Debug.LogError("[WebGLTextBridge] Inner JSON parse error: " + e.Message);
                _isRequestInFlight = false;
                yield break;
            }

            if (inner == null)
            {
                Debug.LogError("[WebGLTextBridge] Inner parsed object is null.");
                _isRequestInFlight = false;
                yield break;
            }

            string patientReply = inner.responseText ?? "";
            int emotion = inner.emotionCode;
            int motion = inner.motionCode;

            Debug.Log("[WebGLTextBridge] Patient reply: " + patientReply);
            Debug.Log("[WebGLTextBridge] emotion=" + emotion + ", motion=" + motion);

            // 3) Append assistant reply into history (this is the key for multi-turn)
            _history.Add(new ChatMessage { role = "assistant", content = patientReply });
            TrimHistoryIfNeeded();

            // 4) Trigger animation/gesture
            if (mockResponder != null)
            {
                mockResponder.PlayByCodes(emotion, motion, patientReply);
            }

            // 5) Demo TTS
            if (ttsPlayer != null && !string.IsNullOrEmpty(demoMp3Url))
            {
                Debug.Log("[WebGLTextBridge] Demo TTS -> Playing fixed mp3: " + demoMp3Url);
                ttsPlayer.PlayFromUrl(demoMp3Url);
            }
        }

        _isRequestInFlight = false;
    }
}