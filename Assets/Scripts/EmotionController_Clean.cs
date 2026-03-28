using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EmotionController_Clean : MonoBehaviour
{
    // Kept because TTSManager still uses emotionController.animator as a motion fallback target.
    public Animator animator;

    [Header("Debug Settings")]
    public bool setEmotionCode = false;
    public bool setMotionCode = false;
    public int currentEmotionCode;
    public int currentMotionCode;

    [Header("Facial Expression Integration")]
    public FacialExpressionRuntimeBridge facialExpressionBridge;

    bool hasLoggedMissingFacialBridge;

    void Start()
    {
        TryResolveFacialBridge();
        Debug.Log($"[EmotionController_Clean] Startup: facial bridge {(facialExpressionBridge != null ? "found" : "not found")}");
    }

    bool TryResolveFacialBridge()
    {
        if (facialExpressionBridge != null)
        {
            return true;
        }

        facialExpressionBridge = GetComponent<FacialExpressionRuntimeBridge>();
        if (facialExpressionBridge == null)
        {
            facialExpressionBridge = GetComponentInChildren<FacialExpressionRuntimeBridge>(true);
        }
        if (facialExpressionBridge == null)
        {
            facialExpressionBridge = GetComponentInParent<FacialExpressionRuntimeBridge>();
        }

        return facialExpressionBridge != null;
    }

    // Kept for TTSManager compatibility. The current facial runtime does not use word timings here.
    public void SyncAnimationsWithWordTimings(List<TTSManager.WordTiming> timings)
    {
    }

    public void HandleEmotionCode(int emotionCode, int motionCode)
    {
        if (!setEmotionCode)
        {
            currentEmotionCode = emotionCode;
        }

        if (!setMotionCode)
        {
            currentMotionCode = motionCode;
        }

        if (TryResolveFacialBridge())
        {
            hasLoggedMissingFacialBridge = false;
            Debug.Log(
                $"[EmotionController_Clean] Forwarding to facial bridge: emotionCode={currentEmotionCode}, motionCode={currentMotionCode}");
            facialExpressionBridge.HandleEmotionAndMotion(currentEmotionCode, currentMotionCode);
            return;
        }

        if (!hasLoggedMissingFacialBridge)
        {
            Debug.LogWarning($"[EmotionController_Clean] Facial bridge not found on '{name}'.");
            hasLoggedMissingFacialBridge = true;
        }
    }
}
