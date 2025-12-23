using UnityEngine;

public class WebGLTextBridge : MonoBehaviour
{
    private WebGLMockResponder mock;

    private void Awake()
    {
        mock = GetComponent<WebGLMockResponder>();
        if (mock == null)
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
        // WebGL: bypass OpenAI, use mock responder to trigger visible reaction
        if (mock != null)
        {
            Debug.Log("[WebGLTextBridge] InjectText -> Mock: " + text);
            mock.OnNurseText(text);
        }
        else
        {
            Debug.LogError("[WebGLTextBridge] Mock responder is missing. Add WebGLMockResponder to the same GameObject.");
        }
#else
        // Non-WebGL: keep original pipeline
        if (OpenAIRequest.Instance != null)
        {
            Debug.Log("[WebGLTextBridge] InjectText -> OpenAIRequest: " + text);
            OpenAIRequest.Instance.ReceiveNurseTranscription(text, 0f);
        }
        else
        {
            Debug.LogError("[WebGLTextBridge] OpenAIRequest.Instance is null.");
        }
#endif
    }
}