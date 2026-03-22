using System;
using System.Collections.Generic;
using UnityEngine;
using uLipSync;

[DisallowMultipleComponent]
public class FacialExpressionTestController : MonoBehaviour
{
    public enum BaseState
    {
        Neutral,
        MildHappy,
        Frustrated
    }

    public enum PeakReaction
    {
        None,
        StrongHappy,
        CryingOverwhelmed,
        Relief,
        Confused
    }

    public enum Overlay
    {
        BrowTension,
        SoftSmile,
        EyeTension,
        BlinkPatternShift
    }

    [Serializable]
    public class BlendShapeInfluence
    {
        public string blendShape = string.Empty;
        [Range(0f, 100f)] public float weight = 0f;
        [Range(0f, 1f)] public float mouthReduceDuringSpeech = 0f;

        public BlendShapeInfluence()
        {
        }

        public BlendShapeInfluence(string blendShapeName, float blendWeight, float speechReduction = 0f)
        {
            blendShape = blendShapeName;
            weight = blendWeight;
            mouthReduceDuringSpeech = speechReduction;
        }
    }

    [Serializable]
    public class ExpressionPreset
    {
        [Range(0f, 1f)] public float intensity = 1f;
        public List<BlendShapeInfluence> blendShapes = new List<BlendShapeInfluence>();
    }

    [Serializable]
    public class BaseStatePreset
    {
        public BaseState state;
        public ExpressionPreset preset = new ExpressionPreset();
    }

    [Serializable]
    public class PeakReactionPreset
    {
        public PeakReaction state;
        public ExpressionPreset preset = new ExpressionPreset();
    }

    [Serializable]
    public class OverlayPreset
    {
        public Overlay state;
        public ExpressionPreset preset = new ExpressionPreset();
    }

    struct ResolvedInfluence
    {
        public int index;
        public float weight;
        public float mouthReduceDuringSpeech;
    }

    sealed class ResolvedPreset
    {
        public float intensity = 1f;
        public readonly List<ResolvedInfluence> influences = new List<ResolvedInfluence>();
    }

    [Header("Target References")]
    [SerializeField] SkinnedMeshRenderer faceRenderer;
    [SerializeField] uLipSync.uLipSync lipSync;
    [SerializeField] bool autoFindReferences = true;

    [Header("Base State")]
    [SerializeField] BaseState activeBaseState = BaseState.Neutral;
    [SerializeField, Range(0f, 1f)] float baseStateWeight = 1f;

    [Header("Peak Reaction")]
    [SerializeField] PeakReaction activePeakReaction = PeakReaction.None;
    [SerializeField, Range(0f, 1f)] float peakReactionWeight = 0f;
    [SerializeField] bool fadePeakReaction = true;
    [SerializeField, Min(0f)] float peakFadeSpeed = 0.7f;

    [Header("Overlays")]
    [SerializeField, Range(0f, 1f)] float browTensionWeight = 0f;
    [SerializeField, Range(0f, 1f)] float softSmileWeight = 0f;
    [SerializeField, Range(0f, 1f)] float eyeTensionWeight = 0f;
    [SerializeField, Range(0f, 1f)] float blinkPatternShiftWeight = 0f;
    [SerializeField, Range(0f, 1f)] float idleBlinkPatternShiftWeight = 0.25f;

    [Header("Blink Pattern Shift")]
    [SerializeField, Range(0.1f, 5f)] float blinkPatternFrequency = 1.25f;
    [SerializeField, Range(0f, 100f)] float blinkPatternPulseWeight = 40f;
    [SerializeField, Range(0.05f, 0.45f)] float blinkPatternDutyCycle = 0.16f;
    [SerializeField, Range(0f, 1f)] float baselineBlinkPulseScale = 0.58f;

    [Header("Speech-Aware Mouth Reduction")]
    [SerializeField, Range(0f, 1f)] float speechReductionStart = 0.1f;
    [SerializeField, Range(0f, 1f)] float speechReductionFull = 0.45f;
    [SerializeField, Min(0f)] float speechSmoothingSpeed = 8f;

    [Header("Blend")]
    [SerializeField, Min(1f)] float expressionBlendSpeed = 220f;

    [Header("Preset Library (Inspector-Tunable)")]
    [SerializeField] List<BaseStatePreset> baseStatePresets = CreateDefaultBaseStatePresets();
    [SerializeField] List<PeakReactionPreset> peakReactionPresets = CreateDefaultPeakReactionPresets();
    [SerializeField] List<OverlayPreset> overlayPresets = CreateDefaultOverlayPresets();

    readonly Dictionary<BaseState, ResolvedPreset> _resolvedBaseStatePresets = new Dictionary<BaseState, ResolvedPreset>();
    readonly Dictionary<PeakReaction, ResolvedPreset> _resolvedPeakReactionPresets = new Dictionary<PeakReaction, ResolvedPreset>();
    readonly Dictionary<Overlay, ResolvedPreset> _resolvedOverlayPresets = new Dictionary<Overlay, ResolvedPreset>();
    readonly Dictionary<string, int> _blendShapeIndexByName = new Dictionary<string, int>(StringComparer.Ordinal);
    readonly List<int> _controlledIndices = new List<int>();

    float[] _targetWeights;
    float[] _appliedWeights;
    float _speechAmount = 0f;
    int _eyeBlinkLeftIndex = -1;
    int _eyeBlinkRightIndex = -1;
    bool _cacheBuilt = false;
    bool _hasLoggedMissingRenderer = false;

    void Awake()
    {
        EnsurePresetLists();
        TryAutoFindReferences();
        RebuildCache();
    }

    void OnValidate()
    {
        EnsurePresetLists();
        if (autoFindReferences)
        {
            TryAutoFindReferences();
        }
        _cacheBuilt = false;
    }

    void LateUpdate()
    {
        if (!EnsureReady())
        {
            return;
        }

        UpdateSpeechAmount();
        UpdatePeakReactionWeight();
        EvaluateAndApplyExpressions();
    }

    public void SetBaseState(BaseState state, float intensity = 1f)
    {
        activeBaseState = state;
        baseStateWeight = Mathf.Clamp01(intensity);
    }

    public void SetBaseState(string stateName, float intensity = 1f)
    {
        BaseState parsed;
        if (Enum.TryParse(stateName, true, out parsed))
        {
            SetBaseState(parsed, intensity);
            return;
        }

        // Graceful fallback for any legacy calls that still send removed base state names.
        if (string.Equals(stateName, "Thinking", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(stateName, "SearchingWordFinding", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(stateName, "AttentiveListening", StringComparison.OrdinalIgnoreCase))
        {
            SetBaseState(BaseState.Neutral, intensity);
        }
    }

    public void PlayPeakReaction(PeakReaction reaction, float intensity = 1f, bool autoFade = true)
    {
        activePeakReaction = reaction;
        peakReactionWeight = Mathf.Clamp01(intensity);
        fadePeakReaction = autoFade;
    }

    public void ClearPeakReaction()
    {
        activePeakReaction = PeakReaction.None;
        peakReactionWeight = 0f;
    }

    public void SetOverlayWeight(Overlay overlay, float intensity)
    {
        float clamped = Mathf.Clamp01(intensity);
        switch (overlay)
        {
            case Overlay.BrowTension:
                browTensionWeight = clamped;
                break;
            case Overlay.SoftSmile:
                softSmileWeight = clamped;
                break;
            case Overlay.EyeTension:
                eyeTensionWeight = clamped;
                break;
            case Overlay.BlinkPatternShift:
                blinkPatternShiftWeight = clamped;
                break;
        }
    }

    public void RebuildPresetCache()
    {
        _cacheBuilt = false;
        RebuildCache();
    }

    bool EnsureReady()
    {
        if (!faceRenderer || !faceRenderer.sharedMesh)
        {
            if (autoFindReferences)
            {
                TryAutoFindReferences();
            }

            if (!faceRenderer || !faceRenderer.sharedMesh)
            {
                if (!_hasLoggedMissingRenderer)
                {
                    Debug.LogWarning("FacialExpressionTestController: Could not find a face renderer with blendshapes.");
                    _hasLoggedMissingRenderer = true;
                }
                return false;
            }
        }

        if (!_cacheBuilt)
        {
            RebuildCache();
        }

        return _cacheBuilt && _targetWeights != null && _appliedWeights != null;
    }

    void EnsurePresetLists()
    {
        if (baseStatePresets == null || baseStatePresets.Count == 0)
        {
            baseStatePresets = CreateDefaultBaseStatePresets();
        }

        if (peakReactionPresets == null || peakReactionPresets.Count == 0)
        {
            peakReactionPresets = CreateDefaultPeakReactionPresets();
        }

        if (overlayPresets == null || overlayPresets.Count == 0)
        {
            overlayPresets = CreateDefaultOverlayPresets();
        }
    }

    void TryAutoFindReferences()
    {
        if (!lipSync)
        {
            lipSync = GetComponent<uLipSync.uLipSync>();
        }

        if (!faceRenderer)
        {
            uLipSyncBlendShape blendShapeDriver = GetComponent<uLipSyncBlendShape>();
            if (blendShapeDriver && blendShapeDriver.skinnedMeshRenderer)
            {
                faceRenderer = blendShapeDriver.skinnedMeshRenderer;
            }
        }

        if (!faceRenderer)
        {
            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; ++i)
            {
                if (!string.Equals(transforms[i].name, "Head.001", StringComparison.Ordinal))
                {
                    continue;
                }

                SkinnedMeshRenderer renderer = transforms[i].GetComponent<SkinnedMeshRenderer>();
                if (renderer && renderer.sharedMesh && renderer.sharedMesh.blendShapeCount > 0)
                {
                    faceRenderer = renderer;
                    break;
                }
            }
        }

        if (!faceRenderer)
        {
            SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < renderers.Length; ++i)
            {
                if (!renderers[i] || !renderers[i].sharedMesh)
                {
                    continue;
                }

                if (renderers[i].sharedMesh.blendShapeCount > 0)
                {
                    faceRenderer = renderers[i];
                    break;
                }
            }
        }
    }

    void RebuildCache()
    {
        _resolvedBaseStatePresets.Clear();
        _resolvedPeakReactionPresets.Clear();
        _resolvedOverlayPresets.Clear();
        _blendShapeIndexByName.Clear();
        _controlledIndices.Clear();
        _eyeBlinkLeftIndex = -1;
        _eyeBlinkRightIndex = -1;
        _cacheBuilt = false;

        if (!faceRenderer || !faceRenderer.sharedMesh)
        {
            return;
        }

        Mesh mesh = faceRenderer.sharedMesh;
        int blendShapeCount = mesh.blendShapeCount;
        if (blendShapeCount <= 0)
        {
            return;
        }

        for (int i = 0; i < blendShapeCount; ++i)
        {
            string blendShapeName = mesh.GetBlendShapeName(i);
            if (!_blendShapeIndexByName.ContainsKey(blendShapeName))
            {
                _blendShapeIndexByName.Add(blendShapeName, i);
            }
        }

        _targetWeights = new float[blendShapeCount];
        _appliedWeights = new float[blendShapeCount];

        HashSet<int> controlledSet = new HashSet<int>();
        ResolveBaseStatePresets(mesh, controlledSet);
        ResolvePeakReactionPresets(mesh, controlledSet);
        ResolveOverlayPresets(mesh, controlledSet);

        TryGetBlendShapeIndex(mesh, "eyeBlinkLeft", out _eyeBlinkLeftIndex);
        TryGetBlendShapeIndex(mesh, "eyeBlinkRight", out _eyeBlinkRightIndex);

        foreach (int index in controlledSet)
        {
            _controlledIndices.Add(index);
            _appliedWeights[index] = faceRenderer.GetBlendShapeWeight(index);
        }

        _cacheBuilt = true;
    }

    void ResolveBaseStatePresets(Mesh mesh, HashSet<int> controlledSet)
    {
        for (int i = 0; i < baseStatePresets.Count; ++i)
        {
            BaseStatePreset statePreset = baseStatePresets[i];
            _resolvedBaseStatePresets[statePreset.state] = ResolvePreset(mesh, statePreset.preset, controlledSet);
        }
    }

    void ResolvePeakReactionPresets(Mesh mesh, HashSet<int> controlledSet)
    {
        for (int i = 0; i < peakReactionPresets.Count; ++i)
        {
            PeakReactionPreset statePreset = peakReactionPresets[i];
            _resolvedPeakReactionPresets[statePreset.state] = ResolvePreset(mesh, statePreset.preset, controlledSet);
        }
    }

    void ResolveOverlayPresets(Mesh mesh, HashSet<int> controlledSet)
    {
        for (int i = 0; i < overlayPresets.Count; ++i)
        {
            OverlayPreset statePreset = overlayPresets[i];
            _resolvedOverlayPresets[statePreset.state] = ResolvePreset(mesh, statePreset.preset, controlledSet);
        }
    }

    ResolvedPreset ResolvePreset(Mesh mesh, ExpressionPreset preset, HashSet<int> controlledSet)
    {
        ResolvedPreset resolved = new ResolvedPreset();
        if (preset == null)
        {
            return resolved;
        }

        resolved.intensity = Mathf.Clamp01(preset.intensity);
        if (preset.blendShapes == null)
        {
            return resolved;
        }

        for (int i = 0; i < preset.blendShapes.Count; ++i)
        {
            BlendShapeInfluence influence = preset.blendShapes[i];
            if (influence == null || string.IsNullOrEmpty(influence.blendShape))
            {
                continue;
            }

            int index;
            if (!TryGetBlendShapeIndex(mesh, influence.blendShape, out index))
            {
                continue;
            }

            string actualName = mesh.GetBlendShapeName(index);
            if (IsLipSyncBlendShape(actualName))
            {
                continue;
            }

            ResolvedInfluence resolvedInfluence = new ResolvedInfluence
            {
                index = index,
                weight = Mathf.Clamp(influence.weight, 0f, 100f),
                mouthReduceDuringSpeech = Mathf.Clamp01(influence.mouthReduceDuringSpeech)
            };
            resolved.influences.Add(resolvedInfluence);
            controlledSet.Add(index);
        }

        return resolved;
    }

    bool TryGetBlendShapeIndex(Mesh mesh, string blendShapeName, out int index)
    {
        if (_blendShapeIndexByName.TryGetValue(blendShapeName, out index))
        {
            return true;
        }

        if (string.IsNullOrEmpty(blendShapeName))
        {
            index = -1;
            return false;
        }

        string lowerFirst = char.ToLowerInvariant(blendShapeName[0]) + blendShapeName.Substring(1);
        if (_blendShapeIndexByName.TryGetValue(lowerFirst, out index))
        {
            return true;
        }

        string upperFirst = char.ToUpperInvariant(blendShapeName[0]) + blendShapeName.Substring(1);
        if (_blendShapeIndexByName.TryGetValue(upperFirst, out index))
        {
            return true;
        }

        index = -1;
        return false;
    }

    void UpdateSpeechAmount()
    {
        float target = 0f;
        if (lipSync != null)
        {
            float denom = Mathf.Max(speechReductionFull - speechReductionStart, 0.001f);
            target = Mathf.Clamp01((lipSync.result.volume - speechReductionStart) / denom);
        }

        if (speechSmoothingSpeed <= 0f)
        {
            _speechAmount = target;
        }
        else
        {
            _speechAmount = Mathf.MoveTowards(_speechAmount, target, speechSmoothingSpeed * Time.deltaTime);
        }
    }

    void UpdatePeakReactionWeight()
    {
        if (!fadePeakReaction || activePeakReaction == PeakReaction.None || peakReactionWeight <= 0f)
        {
            return;
        }

        peakReactionWeight = Mathf.MoveTowards(peakReactionWeight, 0f, peakFadeSpeed * Time.deltaTime);
        if (peakReactionWeight <= 0.0001f)
        {
            peakReactionWeight = 0f;
            activePeakReaction = PeakReaction.None;
        }
    }

    void EvaluateAndApplyExpressions()
    {
        Array.Clear(_targetWeights, 0, _targetWeights.Length);

        ResolvedPreset basePreset;
        if (_resolvedBaseStatePresets.TryGetValue(activeBaseState, out basePreset))
        {
            ApplyPreset(basePreset, baseStateWeight);
        }

        if (activePeakReaction != PeakReaction.None && peakReactionWeight > 0f)
        {
            ResolvedPreset peakPreset;
            if (_resolvedPeakReactionPresets.TryGetValue(activePeakReaction, out peakPreset))
            {
                ApplyPreset(peakPreset, peakReactionWeight);
            }
        }

        ApplyOverlayPreset(Overlay.BrowTension, browTensionWeight);
        ApplyOverlayPreset(Overlay.SoftSmile, softSmileWeight);
        ApplyOverlayPreset(Overlay.EyeTension, eyeTensionWeight);
        float effectiveBlinkPatternShiftWeight = Mathf.Max(blinkPatternShiftWeight, idleBlinkPatternShiftWeight);
        ApplyBlinkPatternShift(effectiveBlinkPatternShiftWeight);

        float blendStep = expressionBlendSpeed * Time.deltaTime;
        for (int i = 0; i < _controlledIndices.Count; ++i)
        {
            int index = _controlledIndices[i];
            float target = Mathf.Clamp(_targetWeights[index], 0f, 100f);
            float next = Mathf.MoveTowards(_appliedWeights[index], target, blendStep);
            _appliedWeights[index] = next;
            faceRenderer.SetBlendShapeWeight(index, next);
        }
    }

    void ApplyOverlayPreset(Overlay overlay, float layerWeight)
    {
        if (layerWeight <= 0f)
        {
            return;
        }

        ResolvedPreset preset;
        if (!_resolvedOverlayPresets.TryGetValue(overlay, out preset))
        {
            return;
        }

        ApplyPreset(preset, layerWeight);
    }

    void ApplyBlinkPatternShift(float layerWeight)
    {
        if (layerWeight <= 0f)
        {
            return;
        }

        ApplyOverlayPreset(Overlay.BlinkPatternShift, layerWeight);

        if (_eyeBlinkLeftIndex < 0 && _eyeBlinkRightIndex < 0)
        {
            return;
        }

        float duty = Mathf.Clamp(blinkPatternDutyCycle, 0.05f, 0.45f);
        float cycle = Mathf.Repeat(Time.time * Mathf.Max(0.1f, blinkPatternFrequency), 1f);

        float pulse = 0f;
        if (cycle < duty)
        {
            float normalized = cycle / duty;
            pulse = 1f - Mathf.Abs((normalized * 2f) - 1f);
        }

        float speechSuppression = Mathf.Lerp(1f, 0.6f, _speechAmount);
        float effectivePulseScale = Mathf.Max(Mathf.Clamp01(layerWeight), baselineBlinkPulseScale);
        float pulseWeight = pulse * blinkPatternPulseWeight * effectivePulseScale * speechSuppression;
        if (_eyeBlinkLeftIndex >= 0)
        {
            _targetWeights[_eyeBlinkLeftIndex] += pulseWeight;
        }
        if (_eyeBlinkRightIndex >= 0)
        {
            _targetWeights[_eyeBlinkRightIndex] += pulseWeight;
        }
    }

    void ApplyPreset(ResolvedPreset preset, float layerWeight)
    {
        if (preset == null || layerWeight <= 0f)
        {
            return;
        }

        float scalar = Mathf.Clamp01(layerWeight) * preset.intensity;
        if (scalar <= 0f)
        {
            return;
        }

        for (int i = 0; i < preset.influences.Count; ++i)
        {
            ResolvedInfluence influence = preset.influences[i];
            float weight = influence.weight;
            if (influence.mouthReduceDuringSpeech > 0f && _speechAmount > 0f)
            {
                float keep = 1f - (_speechAmount * influence.mouthReduceDuringSpeech);
                weight *= Mathf.Clamp01(keep);
            }

            _targetWeights[influence.index] += weight * scalar;
        }
    }

    static bool IsLipSyncBlendShape(string blendShapeName)
    {
        return !string.IsNullOrEmpty(blendShapeName) &&
               blendShapeName.StartsWith("MTH.", StringComparison.OrdinalIgnoreCase);
    }

    static ExpressionPreset Preset(float intensity, params BlendShapeInfluence[] influences)
    {
        ExpressionPreset preset = new ExpressionPreset();
        preset.intensity = Mathf.Clamp01(intensity);
        preset.blendShapes = new List<BlendShapeInfluence>(influences);
        return preset;
    }

    static BlendShapeInfluence BS(string blendShapeName, float weight, float speechReduction = 0f)
    {
        return new BlendShapeInfluence(blendShapeName, weight, speechReduction);
    }

    static List<BaseStatePreset> CreateDefaultBaseStatePresets()
    {
        return new List<BaseStatePreset>
        {
            new BaseStatePreset
            {
                state = BaseState.Neutral,
                preset = Preset(1f,
                    BS("browInnerUp", 2f),
                    BS("eyeWideLeft", 3f),
                    BS("eyeWideRight", 3f),
                    BS("mouthClose", 2f, 0.8f))
            },
            new BaseStatePreset
            {
                state = BaseState.MildHappy,
                preset = Preset(1f,
                    BS("browOuterUpLeft", 9f),
                    BS("browOuterUpRight", 9f),
                    BS("eyeSquintLeft", 8f),
                    BS("eyeSquintRight", 8f),
                    BS("cheekSquintLeft", 16f),
                    BS("cheekSquintRight", 16f),
                    BS("mouthSmileLeft", 28f, 0.65f),
                    BS("mouthSmileRight", 28f, 0.65f))
            },
            new BaseStatePreset
            {
                state = BaseState.Frustrated,
                preset = Preset(1f,
                    BS("browDownLeft", 32f),
                    BS("browDownRight", 32f),
                    BS("browInnerUp", 12f),
                    BS("eyeSquintLeft", 24f),
                    BS("eyeSquintRight", 24f),
                    BS("cheekSquintLeft", 14f),
                    BS("cheekSquintRight", 14f),
                    BS("noseSneerLeft", 18f),
                    BS("noseSneerRight", 18f),
                    BS("mouthFrownLeft", 22f, 0.55f),
                    BS("mouthFrownRight", 22f, 0.55f),
                    BS("jawForward", 5f, 0.5f))
            }
        };
    }

    static List<PeakReactionPreset> CreateDefaultPeakReactionPresets()
    {
        return new List<PeakReactionPreset>
        {
            new PeakReactionPreset
            {
                state = PeakReaction.StrongHappy,
                preset = Preset(1f,
                    BS("browOuterUpLeft", 14f),
                    BS("browOuterUpRight", 14f),
                    BS("eyeSquintLeft", 22f),
                    BS("eyeSquintRight", 22f),
                    BS("cheekSquintLeft", 34f),
                    BS("cheekSquintRight", 34f),
                    BS("mouthDimpleLeft", 20f, 0.55f),
                    BS("mouthDimpleRight", 20f, 0.55f),
                    BS("mouthSmileLeft", 58f, 0.6f),
                    BS("mouthSmileRight", 58f, 0.6f))
            },
            new PeakReactionPreset
            {
                state = PeakReaction.CryingOverwhelmed,
                preset = Preset(1f,
                    BS("browInnerUp", 58f),
                    BS("browDownLeft", 24f),
                    BS("browDownRight", 24f),
                    BS("eyeWideLeft", 14f),
                    BS("eyeWideRight", 14f),
                    BS("eyeSquintLeft", 32f),
                    BS("eyeSquintRight", 32f),
                    BS("cheekSquintLeft", 20f),
                    BS("cheekSquintRight", 20f),
                    BS("mouthShrugUpper", 8f, 0.65f),
                    BS("mouthFrownLeft", 34f, 0.4f),
                    BS("mouthFrownRight", 34f, 0.4f),
                    BS("jawOpen", 15f, 0.75f))
            },
            new PeakReactionPreset
            {
                state = PeakReaction.Relief,
                preset = Preset(1f,
                    BS("browInnerUp", 16f),
                    BS("browOuterUpLeft", 16f),
                    BS("browOuterUpRight", 16f),
                    BS("eyeSquintLeft", 10f),
                    BS("eyeSquintRight", 10f),
                    BS("cheekSquintLeft", 16f),
                    BS("cheekSquintRight", 16f),
                    BS("mouthSmileLeft", 32f, 0.65f),
                    BS("mouthSmileRight", 32f, 0.65f),
                    BS("jawOpen", 14f, 0.8f))
            },
            new PeakReactionPreset
            {
                state = PeakReaction.Confused,
                preset = Preset(1f,
                    BS("browInnerUp", 14f),
                    BS("browDownLeft", 18f),
                    BS("browOuterUpRight", 26f),
                    BS("eyeLookInLeft", 12f),
                    BS("eyeLookOutRight", 14f),
                    BS("eyeSquintLeft", 14f),
                    BS("eyeWideRight", 18f),
                    BS("noseSneerLeft", 8f),
                    BS("jawLeft", 6f, 0.65f),
                    BS("mouthPucker", 11f, 0.72f),
                    BS("mouthFrownLeft", 13f, 0.5f))
            }
        };
    }

    static List<OverlayPreset> CreateDefaultOverlayPresets()
    {
        return new List<OverlayPreset>
        {
            new OverlayPreset
            {
                state = Overlay.BrowTension,
                preset = Preset(1f,
                    BS("browDownLeft", 28f),
                    BS("browDownRight", 28f),
                    BS("browInnerUp", 16f),
                    BS("browOuterUpLeft", 8f),
                    BS("browOuterUpRight", 8f))
            },
            new OverlayPreset
            {
                state = Overlay.SoftSmile,
                preset = Preset(1f,
                    BS("eyeSquintLeft", 6f),
                    BS("eyeSquintRight", 6f),
                    BS("cheekSquintLeft", 14f),
                    BS("cheekSquintRight", 14f),
                    BS("mouthSmileLeft", 24f, 0.7f),
                    BS("mouthSmileRight", 24f, 0.7f))
            },
            new OverlayPreset
            {
                state = Overlay.EyeTension,
                preset = Preset(1f,
                    BS("eyeLookInLeft", 8f),
                    BS("eyeLookInRight", 8f),
                    BS("eyeSquintLeft", 30f),
                    BS("eyeSquintRight", 30f),
                    BS("cheekSquintLeft", 14f),
                    BS("cheekSquintRight", 14f))
            },
            new OverlayPreset
            {
                state = Overlay.BlinkPatternShift,
                preset = Preset(1f,
                    BS("eyeSquintLeft", 12f),
                    BS("eyeSquintRight", 12f),
                    BS("eyeBlinkLeft", 14f),
                    BS("eyeBlinkRight", 14f))
            }
        };
    }
}
