using System;
using UnityEngine;

[DisallowMultipleComponent]
public class FacialExpressionRuntimeBridge : MonoBehaviour
{
    struct FacialMapResult
    {
        public FacialExpressionTestController.BaseState baseState;
        public float baseIntensity;
        public FacialExpressionTestController.PeakReaction peakReaction;
        public float peakIntensity;
        public bool triggerPeak;
        public float browTension;
        public float softSmile;
        public float eyeTension;
        public float blinkPatternShift;
    }

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

    [Header("Debug")]
    [SerializeField] bool enableBridgeLogs = true;

    int _lastLoggedEmotionCode = int.MinValue;
    int _lastLoggedMotionCode = int.MinValue;
    bool _hasLoggedMissingController = false;

    void Awake()
    {
        ResolveReferences();

        if (enableBridgeLogs)
        {
            string bridgeBinding = (emotionController != null && emotionController.facialExpressionBridge == this) ? "registered" : "not-registered";
            Debug.Log(
                $"[FacialBridge] Startup: initialized on '{name}'. " +
                $"controller={(facialController != null ? "found" : "missing")}, " +
                $"emotionController={(emotionController != null ? "found" : "missing")}, " +
                $"binding={bridgeBinding}");
        }
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

        FacialMapResult mapping = BuildMapping(emotionCode, motionCode);
        bool shouldLog = ShouldLogTransition(emotionCode, motionCode);

        if (shouldLog)
        {
            Debug.Log($"[FacialBridge] Received emotionCode={emotionCode}, motionCode={motionCode}");
            LogMapping(mapping);
        }

        ApplyMapping(mapping);

        if (shouldLog)
        {
            Debug.Log($"[FacialApply] Applied mapping on '{name}': base={mapping.baseState}({mapping.baseIntensity:0.00}), peak={GetPeakLogToken(mapping)}");
            _lastLoggedEmotionCode = emotionCode;
            _lastLoggedMotionCode = motionCode;
        }
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
            _hasLoggedMissingController = false;
            return true;
        }

        ResolveReferences();
        if (facialController != null)
        {
            _hasLoggedMissingController = false;
            return true;
        }

        if (enableBridgeLogs && !_hasLoggedMissingController)
        {
            Debug.LogWarning($"[FacialBridge] Missing FacialExpressionTestController on '{name}'. Skipping facial apply.");
            _hasLoggedMissingController = true;
        }

        return false;
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

    bool ShouldLogTransition(int emotionCode, int motionCode)
    {
        if (!enableBridgeLogs)
        {
            return false;
        }

        return emotionCode != _lastLoggedEmotionCode || motionCode != _lastLoggedMotionCode;
    }

    FacialMapResult BuildMapping(int emotionCode, int motionCode)
    {
        FacialMapResult map = new FacialMapResult
        {
            baseState = FacialExpressionTestController.BaseState.Neutral,
            baseIntensity = 1f,
            peakReaction = FacialExpressionTestController.PeakReaction.None,
            peakIntensity = 0f,
            triggerPeak = false,
            browTension = 0f,
            softSmile = 0f,
            eyeTension = 0f,
            blinkPatternShift = 0f
        };

        float strongStress = Mathf.Clamp01(stressOverlayIntensity * 1.2f);
        float mediumStress = Mathf.Clamp01(stressOverlayIntensity * 0.8f);
        float strongSmile = Mathf.Clamp01(happyOverlayIntensity * 1.2f);
        float mediumSmile = Mathf.Clamp01(happyOverlayIntensity * 0.8f);

        switch (emotionCode)
        {
            case 1: // Discomfort
                map.baseState = FacialExpressionTestController.BaseState.Frustrated;
                map.baseIntensity = 0.85f;
                map.browTension = mediumStress;
                map.eyeTension = Mathf.Clamp01(mediumStress * 0.9f);
                map.blinkPatternShift = 0.12f;
                break;
            case 2:
                map.baseState = FacialExpressionTestController.BaseState.MildHappy;
                map.baseIntensity = 1f;
                map.softSmile = strongSmile;
                SetPeak(ref map, FacialExpressionTestController.PeakReaction.StrongHappy, happyPeakIntensity);
                break;
            case 3:
                map.baseState = FacialExpressionTestController.BaseState.Frustrated;
                map.baseIntensity = 1f;
                map.browTension = strongStress;
                map.eyeTension = strongStress;
                map.blinkPatternShift = 0.2f;
                SetPeak(ref map, FacialExpressionTestController.PeakReaction.Confused, Mathf.Max(confusedPeakIntensity, 0.82f));
                break;
            case 4:
                map.baseState = FacialExpressionTestController.BaseState.Frustrated;
                map.baseIntensity = 1f;
                map.browTension = stressOverlayIntensity;
                map.eyeTension = strongStress;
                map.blinkPatternShift = 0.18f;
                SetPeak(ref map, FacialExpressionTestController.PeakReaction.CryingOverwhelmed, Mathf.Clamp01(cryPeakIntensity * 0.72f));
                break;
            case 5:
                map.baseState = FacialExpressionTestController.BaseState.Frustrated;
                map.baseIntensity = 1f;
                map.browTension = Mathf.Clamp01(stressOverlayIntensity * 1.15f);
                map.eyeTension = Mathf.Clamp01(stressOverlayIntensity * 0.95f);
                map.blinkPatternShift = 0.12f;
                SetPeak(ref map, FacialExpressionTestController.PeakReaction.Confused, Mathf.Clamp01(confusedPeakIntensity * 0.65f));
                break;
            case 6:
                map.baseState = FacialExpressionTestController.BaseState.Frustrated;
                map.baseIntensity = 1f;
                map.browTension = strongStress;
                map.eyeTension = Mathf.Clamp01(stressOverlayIntensity * 1.05f);
                map.blinkPatternShift = 0.2f;
                SetPeak(ref map, FacialExpressionTestController.PeakReaction.Confused, Mathf.Clamp01(confusedPeakIntensity * 0.82f));
                break;
            case 7: // Thinking
                map.baseState = FacialExpressionTestController.BaseState.Neutral;
                map.baseIntensity = 1f;
                map.browTension = Mathf.Clamp01(stressOverlayIntensity * 0.4f);
                map.eyeTension = Mathf.Clamp01(stressOverlayIntensity * 0.5f);
                map.blinkPatternShift = 0.28f;
                SetPeak(ref map, FacialExpressionTestController.PeakReaction.Confused, Mathf.Clamp01(confusedPeakIntensity * 0.55f));
                break;
            case 8: // Apologetic
                map.baseState = FacialExpressionTestController.BaseState.MildHappy;
                map.baseIntensity = 0.85f;
                map.softSmile = mediumSmile;
                map.eyeTension = Mathf.Clamp01(stressOverlayIntensity * 0.35f);
                SetPeak(ref map, FacialExpressionTestController.PeakReaction.Relief, Mathf.Max(reliefPeakIntensity, 0.72f));
                break;
            case 9:
                map.baseState = FacialExpressionTestController.BaseState.Frustrated;
                map.baseIntensity = 1f;
                map.browTension = strongStress;
                map.eyeTension = strongStress;
                map.blinkPatternShift = 0.25f;
                SetPeak(ref map, FacialExpressionTestController.PeakReaction.CryingOverwhelmed, cryPeakIntensity);
                break;
        }

        ApplyMotionInfluence(ref map, emotionCode, motionCode);
        return map;
    }

    void ApplyMotionInfluence(ref FacialMapResult map, int emotionCode, int motionCode)
    {
        switch (motionCode)
        {
            case 1: // Confused
                map.blinkPatternShift = Mathf.Max(map.blinkPatternShift, 0.35f);
                if (emotionCode != 2 && emotionCode != 8 && emotionCode != 9)
                {
                    SetPeak(ref map, FacialExpressionTestController.PeakReaction.Confused, Mathf.Max(map.peakIntensity, confusedPeakIntensity));
                }
                break;
            case 2:
            case 3:
            case 4:
            case 5: // Nod variants
                if (emotionCode == 2)
                {
                    map.softSmile = Mathf.Clamp01(map.softSmile + 0.12f);
                    SetPeak(ref map, FacialExpressionTestController.PeakReaction.StrongHappy, Mathf.Max(map.peakIntensity, happyPeakIntensity));
                }
                else if (emotionCode == 8)
                {
                    map.softSmile = Mathf.Clamp01(map.softSmile + 0.08f);
                    SetPeak(ref map, FacialExpressionTestController.PeakReaction.Relief, Mathf.Max(map.peakIntensity, reliefPeakIntensity));
                }
                break;
            case 6:
            case 7: // Head shake variants
                map.browTension = Mathf.Clamp01(map.browTension + 0.15f);
                map.eyeTension = Mathf.Clamp01(map.eyeTension + 0.1f);
                map.blinkPatternShift = Mathf.Max(map.blinkPatternShift, 0.22f);
                if (!map.triggerPeak)
                {
                    SetPeak(ref map, FacialExpressionTestController.PeakReaction.Confused, Mathf.Clamp01(confusedPeakIntensity * 0.85f));
                }
                break;
            case 8: // Tap table
                map.browTension = Mathf.Clamp01(map.browTension + 0.12f);
                map.eyeTension = Mathf.Clamp01(map.eyeTension + 0.12f);
                map.blinkPatternShift = Mathf.Max(map.blinkPatternShift, 0.2f);
                break;
            case 9: // Struggling
                map.browTension = Mathf.Clamp01(map.browTension + 0.18f);
                map.eyeTension = Mathf.Clamp01(map.eyeTension + 0.2f);
                map.blinkPatternShift = Mathf.Max(map.blinkPatternShift, 0.32f);
                if (emotionCode == 9)
                {
                    SetPeak(ref map, FacialExpressionTestController.PeakReaction.CryingOverwhelmed, Mathf.Max(map.peakIntensity, cryPeakIntensity));
                }
                else
                {
                    SetPeak(ref map, FacialExpressionTestController.PeakReaction.Confused, Mathf.Max(map.peakIntensity, confusedPeakIntensity * 0.95f));
                }
                break;
        }
    }

    void SetPeak(ref FacialMapResult map, FacialExpressionTestController.PeakReaction reaction, float intensity)
    {
        map.triggerPeak = true;
        map.peakReaction = reaction;
        map.peakIntensity = Mathf.Clamp01(intensity);
    }

    void ApplyMapping(FacialMapResult map)
    {
        TriggerBaseExpression(map.baseState, map.baseIntensity);

        if (resetOverlaysBeforeApply)
        {
            TriggerOverlay(FacialExpressionTestController.Overlay.BrowTension, 0f);
            TriggerOverlay(FacialExpressionTestController.Overlay.SoftSmile, 0f);
            TriggerOverlay(FacialExpressionTestController.Overlay.EyeTension, 0f);
            TriggerOverlay(FacialExpressionTestController.Overlay.BlinkPatternShift, 0f);
        }

        if (map.browTension > 0f)
        {
            TriggerOverlay(FacialExpressionTestController.Overlay.BrowTension, map.browTension);
        }

        if (map.softSmile > 0f)
        {
            TriggerOverlay(FacialExpressionTestController.Overlay.SoftSmile, map.softSmile);
        }

        if (map.eyeTension > 0f)
        {
            TriggerOverlay(FacialExpressionTestController.Overlay.EyeTension, map.eyeTension);
        }

        if (map.blinkPatternShift > 0f)
        {
            TriggerOverlay(FacialExpressionTestController.Overlay.BlinkPatternShift, map.blinkPatternShift);
        }

        if (map.triggerPeak)
        {
            TriggerPeakReaction(map.peakReaction, map.peakIntensity, true);
        }
    }

    void LogMapping(FacialMapResult map)
    {
        Debug.Log(
            "[FacialMap] " +
            $"base={map.baseState}({map.baseIntensity:0.00}), " +
            $"peak={GetPeakLogToken(map)}, " +
            "overlays=" +
            $"BrowTension({map.browTension:0.00}), " +
            $"SoftSmile({map.softSmile:0.00}), " +
            $"EyeTension({map.eyeTension:0.00}), " +
            $"BlinkPatternShift({map.blinkPatternShift:0.00})");
    }

    string GetPeakLogToken(FacialMapResult map)
    {
        if (!map.triggerPeak)
        {
            return "None";
        }

        return $"{map.peakReaction}({map.peakIntensity:0.00})";
    }
}
