using UnityEngine;

public class WebGLTextBridge : MonoBehaviour
{
    public static WebGLTextBridge Instance { get; private set; }

    [Header("Speech Settings")]
    [SerializeField] private float defaultSpeechWpm = 0f;

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
        if (string.IsNullOrWhiteSpace(text))
        {
            Debug.LogWarning("[WebGLTextBridge] Empty text from browser STT.");
            return;
        }

        if (OpenAIRequest.Instance != null)
        {
            Debug.Log($"[WebGLTextBridge] Forwarding transcript to OpenAIRequest: {text}");
            OpenAIRequest.Instance.ReceiveNurseTranscription(text, defaultSpeechWpm);
            return;
        }

        Debug.LogError("[WebGLTextBridge] OpenAIRequest.Instance is null; cannot process transcript.");
    }

    // Optional hook for JS status messages.
    public void UpdateVoiceStatus(string status)
    {
        Debug.Log("[WebGLTextBridge] Voice status: " + status);
    }
}
