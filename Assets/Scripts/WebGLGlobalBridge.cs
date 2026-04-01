using UnityEngine;

/// <summary>
/// Global bridge that JavaScript can call, which then forwards to the actual WebGLTextBridge instance
/// This solves the nested GameObject problem
/// </summary>
public class WebGLGlobalBridge : MonoBehaviour
{
    public static WebGLGlobalBridge Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[WebGLGlobalBridge] Global bridge created. JavaScript should call this GameObject: " + gameObject.name);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Called from JavaScript - forwards to WebGLTextBridge.Instance
    public void InjectText(string text)
    {
        Debug.Log("[WebGLGlobalBridge] InjectText called, forwarding to WebGLTextBridge.Instance");
        
        if (WebGLTextBridge.Instance != null)
        {
            WebGLTextBridge.Instance.InjectText(text);
        }
        else
        {
            Debug.LogError("[WebGLGlobalBridge] WebGLTextBridge.Instance is null! Make sure WebGLTextBridge component exists in scene.");
        }
    }

    // Called from JavaScript - forwards to WebGLTextBridge.Instance
    public void UpdateVoiceStatus(string status)
    {
        if (WebGLTextBridge.Instance != null)
        {
            WebGLTextBridge.Instance.UpdateVoiceStatus(status);
        }
    }

    // Called from JavaScript - injects host-owned runtime session context.
    public void ApplyRuntimeContext(string json)
    {
        Debug.Log("[WebGLGlobalBridge] ApplyRuntimeContext called.");
        RuntimeSessionContext.ApplyJson(json);
    }

    public void ClearRuntimeContext()
    {
        Debug.Log("[WebGLGlobalBridge] ClearRuntimeContext called.");
        RuntimeSessionContext.Clear();
    }
}
