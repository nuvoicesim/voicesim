using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EmotionController : MonoBehaviour
{
    public Animator animator;

    [Header("Debug Settings")]
    public bool setEmotionCode = false;
    public bool setMotionCode = false;
    // Preserved for prefab compatibility with EmotionController.
    public bool disableMotion = false;
    public int currentEmotionCode;
    public int currentMotionCode;
    // Preserved for prefab compatibility with EmotionController.
    public string[] emotionNames = { "Neutral", "Discomfort", "Happy", "Pain", "Sad", "Anger", "Frustrated", "Thinking", "Apologetic", "Cry" };
    // Preserved for prefab compatibility with EmotionController.
    public string[] motionNames = { "Neutral", "Confused", "Nod 1", "Nod 2", "Nod 3", "Nod 4", "Head Shake 1", "Head Shake 2", "Tap Table", "Struggling" };

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

    // Compatibility stub for callers that still pass word timings.
    public void SyncAnimationsWithWordTimings(List<TTSManager.WordTiming> timings)
    {
        _ = timings;
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
