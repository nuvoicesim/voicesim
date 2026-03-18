using System;
using UnityEngine;

[DisallowMultipleComponent]
public class FacialExpressionRuntimeBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] FacialExpressionTestController facialController;
    [SerializeField] EmotionController emotionController;
    [SerializeField] bool autoResolveReferences = true;
    [SerializeField] bool registerWithEmotionController = true;

    [Header("Default Trigger Intensities")]
    [SerializeField, Range(0f, 1f)] float happyOverlayIntensity = 0.55f;
    [SerializeField, Range(0f, 1f)] float stressOverlayIntensity = 0.6f;
    [SerializeField, Range(0f, 1f)] float confusedPeakIntensity = 0.75f;
    [SerializeField, Range(0f, 1f)] float happyPeakIntensity = 0.65f;
    [SerializeField, Range(0f, 1f)] float cryPeakIntensity = 0.95f;
    [SerializeField, Range(0f, 1f)] float reliefPeakIntensity = 0.6f;
    [SerializeField] bool resetOverlaysBeforeApply = true;

    void Awake()
    {
        ResolveReferences();
    }

    void OnValidate()
    {
        if (autoResolveReferences)
        {
            ResolveReferences();
        }
    }

    public void HandleEmotionAndMotion(int emotionCode, int motionCode)
    {
        if (!EnsureController()) return;

        ApplyBaseFromEmotionCode(emotionCode);
        ApplyOverlayFromEmotionCode(emotionCode);
        ApplyPeakFromEmotionAndMotion(emotionCode, motionCode);
    }

    public void TriggerBaseExpression(FacialExpressionTestController.BaseState state, float intensity = 1f)
    {
        if (!EnsureController()) return;
        facialController.SetBaseState(state, intensity);
    }

    public void TriggerBaseExpression(string baseExpressionName, float intensity = 1f)
    {
        if (!EnsureController()) return;
        facialController.SetBaseState(baseExpressionName, intensity);
    }

    // 0=Neutral, 1=MildHappy, 2=Frustrated
    public void TriggerBaseExpression(int baseExpressionCode, float intensity = 1f)
    {
        switch (baseExpressionCode)
        {
            case 1:
                TriggerBaseExpression(FacialExpressionTestController.BaseState.MildHappy, intensity);
                return;
            case 2:
                TriggerBaseExpression(FacialExpressionTestController.BaseState.Frustrated, intensity);
                return;
            default:
                TriggerBaseExpression(FacialExpressionTestController.BaseState.Neutral, intensity);
                return;
        }
    }

    public void TriggerPeakReaction(FacialExpressionTestController.PeakReaction reaction, float intensity = 1f, bool autoFade = true)
    {
        if (!EnsureController()) return;
        facialController.PlayPeakReaction(reaction, intensity, autoFade);
    }

    public void TriggerPeakReaction(string peakReactionName, float intensity = 1f, bool autoFade = true)
    {
        if (!EnsureController()) return;

        FacialExpressionTestController.PeakReaction parsed;
        if (!Enum.TryParse(peakReactionName, true, out parsed))
        {
            return;
        }

        TriggerPeakReaction(parsed, intensity, autoFade);
    }

    // 1=StrongHappy, 2=CryingOverwhelmed, 3=Relief, 4=Confused
    public void TriggerPeakReaction(int peakReactionCode, float intensity = 1f, bool autoFade = true)
    {
        switch (peakReactionCode)
        {
            case 1:
                TriggerPeakReaction(FacialExpressionTestController.PeakReaction.StrongHappy, intensity, autoFade);
                return;
            case 2:
                TriggerPeakReaction(FacialExpressionTestController.PeakReaction.CryingOverwhelmed, intensity, autoFade);
                return;
            case 3:
                TriggerPeakReaction(FacialExpressionTestController.PeakReaction.Relief, intensity, autoFade);
                return;
            case 4:
                TriggerPeakReaction(FacialExpressionTestController.PeakReaction.Confused, intensity, autoFade);
                return;
            default:
                return;
        }
    }

    public void TriggerOverlay(FacialExpressionTestController.Overlay overlay, float intensity)
    {
        if (!EnsureController()) return;
        facialController.SetOverlayWeight(overlay, intensity);
    }

    public void TriggerOverlay(string overlayName, float intensity)
    {
        if (!EnsureController()) return;

        FacialExpressionTestController.Overlay parsed;
        if (!Enum.TryParse(overlayName, true, out parsed))
        {
            return;
        }

        TriggerOverlay(parsed, intensity);
    }

    // 0=BrowTension, 1=SoftSmile, 2=EyeTension, 3=BlinkPatternShift
    public void TriggerOverlay(int overlayCode, float intensity)
    {
        switch (overlayCode)
        {
            case 0:
                TriggerOverlay(FacialExpressionTestController.Overlay.BrowTension, intensity);
                return;
            case 1:
                TriggerOverlay(FacialExpressionTestController.Overlay.SoftSmile, intensity);
                return;
            case 2:
                TriggerOverlay(FacialExpressionTestController.Overlay.EyeTension, intensity);
                return;
            case 3:
                TriggerOverlay(FacialExpressionTestController.Overlay.BlinkPatternShift, intensity);
                return;
            default:
                return;
        }
    }

    bool EnsureController()
    {
        if (facialController != null)
        {
            return true;
        }

        ResolveReferences();
        return facialController != null;
    }

    void ResolveReferences()
    {
        if (!autoResolveReferences && facialController != null && emotionController != null)
        {
            return;
        }

        if (facialController == null)
        {
            facialController = GetComponent<FacialExpressionTestController>();
        }

        if (emotionController == null)
        {
            emotionController = GetComponent<EmotionController>();
        }

        if (registerWithEmotionController && emotionController != null && emotionController.facialExpressionBridge == null)
        {
            emotionController.facialExpressionBridge = this;
        }
    }

    void ApplyBaseFromEmotionCode(int emotionCode)
    {
        switch (emotionCode)
        {
            case 2:
                TriggerBaseExpression(FacialExpressionTestController.BaseState.MildHappy);
                break;
            case 3:
            case 4:
            case 5:
            case 6:
            case 9:
                TriggerBaseExpression(FacialExpressionTestController.BaseState.Frustrated);
                break;
            default:
                TriggerBaseExpression(FacialExpressionTestController.BaseState.Neutral);
                break;
        }
    }

    void ApplyOverlayFromEmotionCode(int emotionCode)
    {
        if (resetOverlaysBeforeApply)
        {
            TriggerOverlay(FacialExpressionTestController.Overlay.BrowTension, 0f);
            TriggerOverlay(FacialExpressionTestController.Overlay.SoftSmile, 0f);
            TriggerOverlay(FacialExpressionTestController.Overlay.EyeTension, 0f);
        }

        switch (emotionCode)
        {
            case 2:
                TriggerOverlay(FacialExpressionTestController.Overlay.SoftSmile, happyOverlayIntensity);
                break;
            case 3:
            case 4:
            case 5:
            case 6:
            case 9:
                TriggerOverlay(FacialExpressionTestController.Overlay.BrowTension, stressOverlayIntensity);
                TriggerOverlay(FacialExpressionTestController.Overlay.EyeTension, stressOverlayIntensity);
                break;
        }
    }

    void ApplyPeakFromEmotionAndMotion(int emotionCode, int motionCode)
    {
        if (motionCode == 1)
        {
            TriggerPeakReaction(FacialExpressionTestController.PeakReaction.Confused, confusedPeakIntensity, true);
            return;
        }

        switch (emotionCode)
        {
            case 2:
                TriggerPeakReaction(FacialExpressionTestController.PeakReaction.StrongHappy, happyPeakIntensity, true);
                break;
            case 8:
                TriggerPeakReaction(FacialExpressionTestController.PeakReaction.Relief, reliefPeakIntensity, true);
                break;
            case 9:
                TriggerPeakReaction(FacialExpressionTestController.PeakReaction.CryingOverwhelmed, cryPeakIntensity, true);
                break;
        }
    }
}
