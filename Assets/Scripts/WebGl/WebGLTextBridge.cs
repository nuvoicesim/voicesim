using UnityEngine;
using Newtonsoft.Json;

public class WebGLTextBridge : MonoBehaviour
{
    public static WebGLTextBridge Instance { get; private set; }

    [Header("Speech Settings")]
    [SerializeField] private float defaultSpeechWpm = 0f;

    [System.Serializable]
    private class WebGLSpeechPayload
    {
        public string text;
        public string userSpeechStartAt;
        public string userSpeechEndAt;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[WebGLTextBridge] Ready.");
        }
        else
        {
            Destroy(this);
        }
    }

    // Called by JavaScript in WebGL template after browser speech recognition.
    public void InjectText(string text)
    {
        WebGLSpeechPayload payload = TryParsePayload(text);
        string resolvedText = payload != null && !string.IsNullOrWhiteSpace(payload.text)
            ? payload.text
            : text;
        string userSpeechStartAt = payload?.userSpeechStartAt;
        string userSpeechEndAt = payload?.userSpeechEndAt;

        if (string.IsNullOrWhiteSpace(resolvedText))
        {
            Debug.LogWarning("[WebGLTextBridge] Empty text from browser STT.");
            return;
        }

        if (OpenAIRequest.Instance != null)
        {
            resolvedText = resolvedText.Trim();
            Debug.Log($"[WebGLTextBridge] Forwarding transcript to OpenAIRequest: {resolvedText}");
            OpenAIRequest.Instance.ReceiveNurseTranscription(resolvedText, defaultSpeechWpm, userSpeechStartAt, userSpeechEndAt);
            return;
        }

        Debug.LogError("[WebGLTextBridge] OpenAIRequest.Instance is null; cannot process transcript.");
    }

    // Optional hook for JS status messages.
    public void UpdateVoiceStatus(string status)
    {
        Debug.Log("[WebGLTextBridge] Voice status: " + status);
    }

    private static WebGLSpeechPayload TryParsePayload(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue) || rawValue[0] != '{')
            return null;

        try
        {
            return JsonConvert.DeserializeObject<WebGLSpeechPayload>(rawValue);
        }
        catch
        {
            return null;
        }
    }
}
