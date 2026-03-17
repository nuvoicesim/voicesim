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
        Thinking,
        SearchingWordFinding,
        AttentiveListening,
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

    [Header("Blink Pattern Shift")]
    [SerializeField, Range(0.1f, 5f)] float blinkPatternFrequency = 1.1f;
    [SerializeField, Range(0f, 100f)] float blinkPatternPulseWeight = 28f;
    [SerializeField, Range(0.05f, 0.45f)] float blinkPatternDutyCycle = 0.18f;

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
        ApplyBlinkPatternShift(blinkPatternShiftWeight);

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

        float pulseWeight = pulse * blinkPatternPulseWeight * Mathf.Clamp01(layerWeight);
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
                    BS("eyeWideLeft", 2f),
                    BS("eyeWideRight", 2f))
            },
            new BaseStatePreset
            {
                state = BaseState.Thinking,
                preset = Preset(1f,
                    BS("browInnerUp", 15f),
                    BS("browDownRight", 7f),
                    BS("eyeLookUpLeft", 7f),
                    BS("eyeLookUpRight", 7f),
                    BS("mouthPressLeft", 10f, 0.85f),
                    BS("mouthPressRight", 10f, 0.85f))
            },
            new BaseStatePreset
            {
                state = BaseState.SearchingWordFinding,
                preset = Preset(1f,
                    BS("browInnerUp", 24f),
                    BS("browDownLeft", 10f),
                    BS("browDownRight", 10f),
                    BS("eyeSquintLeft", 11f),
                    BS("eyeSquintRight", 11f),
                    BS("mouthStretchLeft", 14f, 0.8f),
                    BS("mouthStretchRight", 14f, 0.8f),
                    BS("jawOpen", 6f, 0.8f))
            },
            new BaseStatePreset
            {
                state = BaseState.AttentiveListening,
                preset = Preset(1f,
                    BS("browInnerUp", 8f),
                    BS("eyeWideLeft", 12f),
                    BS("eyeWideRight", 12f),
                    BS("mouthClose", 6f, 0.6f))
            },
            new BaseStatePreset
            {
                state = BaseState.MildHappy,
                preset = Preset(1f,
                    BS("mouthSmileLeft", 20f, 0.65f),
                    BS("mouthSmileRight", 20f, 0.65f),
                    BS("cheekSquintLeft", 8f),
                    BS("cheekSquintRight", 8f))
            },
            new BaseStatePreset
            {
                state = BaseState.Frustrated,
                preset = Preset(1f,
                    BS("browDownLeft", 20f),
                    BS("browDownRight", 20f),
                    BS("eyeSquintLeft", 14f),
                    BS("eyeSquintRight", 14f),
                    BS("noseSneerLeft", 10f),
                    BS("noseSneerRight", 10f),
                    BS("mouthFrownLeft", 22f, 0.6f),
                    BS("mouthFrownRight", 22f, 0.6f))
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
                    BS("mouthSmileLeft", 48f, 0.6f),
                    BS("mouthSmileRight", 48f, 0.6f),
                    BS("cheekSquintLeft", 26f),
                    BS("cheekSquintRight", 26f),
                    BS("eyeSquintLeft", 14f),
                    BS("eyeSquintRight", 14f))
            },
            new PeakReactionPreset
            {
                state = PeakReaction.CryingOverwhelmed,
                preset = Preset(1f,
                    BS("browInnerUp", 44f),
                    BS("browDownLeft", 18f),
                    BS("browDownRight", 18f),
                    BS("eyeSquintLeft", 24f),
                    BS("eyeSquintRight", 24f),
                    BS("mouthFrownLeft", 36f, 0.45f),
                    BS("mouthFrownRight", 36f, 0.45f),
                    BS("jawOpen", 18f, 0.75f))
            },
            new PeakReactionPreset
            {
                state = PeakReaction.Relief,
                preset = Preset(1f,
                    BS("browInnerUp", 9f),
                    BS("browOuterUpLeft", 8f),
                    BS("browOuterUpRight", 8f),
                    BS("mouthSmileLeft", 22f, 0.65f),
                    BS("mouthSmileRight", 22f, 0.65f),
                    BS("jawOpen", 10f, 0.75f))
            },
            new PeakReactionPreset
            {
                state = PeakReaction.Confused,
                preset = Preset(1f,
                    BS("browInnerUp", 22f),
                    BS("browDownLeft", 8f),
                    BS("browOuterUpRight", 14f),
                    BS("eyeSquintLeft", 9f),
                    BS("eyeWideRight", 8f),
                    BS("mouthPucker", 12f, 0.75f),
                    BS("mouthFrownLeft", 12f, 0.55f))
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
                    BS("browDownLeft", 16f),
                    BS("browDownRight", 16f),
                    BS("browInnerUp", 10f))
            },
            new OverlayPreset
            {
                state = Overlay.SoftSmile,
                preset = Preset(1f,
                    BS("mouthSmileLeft", 14f, 0.7f),
                    BS("mouthSmileRight", 14f, 0.7f),
                    BS("cheekSquintLeft", 6f),
                    BS("cheekSquintRight", 6f))
            },
            new OverlayPreset
            {
                state = Overlay.EyeTension,
                preset = Preset(1f,
                    BS("eyeSquintLeft", 18f),
                    BS("eyeSquintRight", 18f),
                    BS("eyeWideLeft", 6f),
                    BS("eyeWideRight", 6f))
            },
            new OverlayPreset
            {
                state = Overlay.BlinkPatternShift,
                preset = Preset(1f,
                    BS("eyeSquintLeft", 8f),
                    BS("eyeSquintRight", 8f),
                    BS("eyeBlinkLeft", 8f),
                    BS("eyeBlinkRight", 8f))
            }
        };
    }
}
