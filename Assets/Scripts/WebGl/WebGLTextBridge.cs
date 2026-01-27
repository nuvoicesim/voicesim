using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class LlmResponseRoot
{
    public bool success;
    public LlmData data;
    public object error;
    public string timestamp;
}

[Serializable]
public class LlmData
{
    public string response;
    public string role;
    public int emotion_code;
    public int motion_code;
    public string clean_text;
}

[Serializable]
public class LlmRequestBody
{
    public string message;
    public string role;
}

public class WebGLTextBridge : MonoBehaviour
{
    [Header("Backend")]
    public string webglLlmUrl = "https://rkh0ga80p1.execute-api.us-east-2.amazonaws.com/webgl/llm";

    [Header("References")]
    public WebGLMockResponder mockResponder; // We will reuse this to trigger animations

    private void Awake()
    {
        if (mockResponder == null)
        {
            mockResponder = GetComponent<WebGLMockResponder>();
        }

        if (mockResponder == null)
        {
            Debug.LogWarning("[WebGLTextBridge] WebGLMockResponder not found on the same GameObject.");
        }
    }

    public void InjectText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            Debug.LogWarning("[WebGLTextBridge] Empty text.");
            return;
        }

#if UNITY_WEBGL
        Debug.Log("[WebGLTextBridge] WebGL -> calling backend /llm: " + text);
        StartCoroutine(CallLlmCoroutine(text));
#else
        // Non-WebGL: keep original pipeline
        if (OpenAIRequest.Instance != null)
        {
            Debug.Log("[WebGLTextBridge] Non-WebGL -> OpenAIRequest: " + text);
            OpenAIRequest.Instance.ReceiveNurseTranscription(text, 0f);
        }
        else
        {
            Debug.LogError("[WebGLTextBridge] OpenAIRequest.Instance is null.");
        }
#endif
    }

    private IEnumerator CallLlmCoroutine(string nurseText)
    {
        var req = new LlmRequestBody
        {
            message = nurseText,
            role = "nurse"
        };

        string json = JsonUtility.ToJson(req);

        using (var www = new UnityWebRequest(webglLlmUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[WebGLTextBridge] /llm request failed: " + www.error);
                Debug.LogError("[WebGLTextBridge] Response body: " + www.downloadHandler.text);
                yield break;
            }

            string respText = www.downloadHandler.text;
            Debug.Log("[WebGLTextBridge] /llm raw response: " + respText);

            LlmResponseRoot parsed = null;
            try
            {
                parsed = JsonUtility.FromJson<LlmResponseRoot>(respText);
            }
            catch (Exception e)
            {
                Debug.LogError("[WebGLTextBridge] JSON parse error: " + e.Message);
                yield break;
            }

            if (parsed == null || parsed.data == null)
            {
                Debug.LogError("[WebGLTextBridge] Parsed response is null or missing data.");
                yield break;
            }

            string patientReply = string.IsNullOrEmpty(parsed.data.clean_text) ? parsed.data.response : parsed.data.clean_text;
            Debug.Log("[WebGLTextBridge] Patient reply: " + patientReply);
            Debug.Log("[WebGLTextBridge] emotion=" + parsed.data.emotion_code + ", motion=" + parsed.data.motion_code);

            // Trigger animation using your existing responder
            if (mockResponder != null)
            {
                // Option A: temporarily reuse text matching behavior
                // mockResponder.OnNurseText(patientReply);

                // Option B: directly map emotion/motion code to triggers (recommended next)
                mockResponder.PlayByCodes(parsed.data.emotion_code, parsed.data.motion_code, patientReply);
            }
            else
            {
                Debug.LogWarning("[WebGLTextBridge] mockResponder is null, cannot trigger animations.");
            }
        }
    }
}