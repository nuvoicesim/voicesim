using System;
using UI.Cues;
using UnityEngine;
using UnityEngine.SceneManagement;

// Generic cue evidence recorder.
//
// Listens for cue button presses (CueController.CuePressedEvent) and records
// them onto StudyActiveItemTracker as cueUsed / cueLevel + a `cue_pressed`
// StudyInteractionEvent. Replaces the Phase-1-Section-A-only role of
// Phase1SectionACueRecorder with a generic, multi-phase recorder used by:
//
//   * UI_V8_simucase.prefab → Phase 1 sectionA/B/C/D
//   * UI_V8_secC.prefab    → Phase 2 Ben/Maria Sentence Completion
//   * Cues_V3.prefab       → Phase 2 Ben/Maria Object Naming (via nested
//                            UI_V8.prefab)
//
// Metadata-resolution priority chain (cue-event attribution):
//   1) Active IStudyItemMetadataProvider in the scene.
//      Implementers today: SimuCaseTargetButtonUI (Phase 1 A/C/D),
//      Phase2ObjectNamingMetadataProvider, Phase2SentenceCompletionMetadataProvider.
//   2) Phase 1 Section B synthetic fallback, used ONLY when the active scene
//      is sectionB. Promoted ABOVE the StudyActiveItemTracker.ActiveItemMetadata
//      fallback so any stale tracker state lingering from a previously played
//      section (where Clear() may not have run for every item) cannot
//      misattribute Section B cue presses to the previous section's itemId.
//      Reuses the existing Phase1StudyItemMetadataResolver.TryResolve with the
//      same synthetic script number Phase1SectionBChecklistManager already
//      uses, so the cue event attaches to the same synthetic Section B
//      task-level item that Section B's StudyTaskResultPayload already carries.
//      No new itemId scheme is introduced.
//   3) StudyActiveItemTracker.ActiveItemMetadata — set as a side-effect of
//      RecordStudentUtterance / RecordPatientResponse on dialogue-bearing
//      flows. Used only for non-sectionB scenes when (1) is absent.
//
// Strictly passive: never advances items, never calls /llm-scoring, never
// triggers MarkTaskComplete, never changes scoring/completion/progress/unlock,
// never modifies studentSelectedScore / patientFinalResponse / transcript /
// dialogue behavior. Exceptions inside HandleCuePressed are caught and logged
// so a recorder bug can never block the student flow.
public class CueEvidenceRecorder : MonoBehaviour
{
    // Matches Phase1SectionBChecklistManager.SyntheticScriptNumber so the
    // recorder's Section B fallback resolves the exact same synthetic item.
    private const int Phase1SectionBSyntheticScriptNumber = 26;
    private const string Phase1SectionBSceneName = "sectionB";

    [SerializeField] private CueController cueController;
    [SerializeField] private MonoBehaviour studyItemMetadataProviderBehaviour;

    private IStudyItemMetadataProvider studyItemMetadataProvider;
    private bool subscribed;

    private void Awake()
    {
        if (cueController == null)
            cueController = FindObjectOfType<CueController>();

        if (studyItemMetadataProvider == null &&
            studyItemMetadataProviderBehaviour is IStudyItemMetadataProvider configured)
        {
            studyItemMetadataProvider = configured;
        }
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        if (cueController != null && subscribed)
        {
            cueController.CuePressedEvent -= HandleCuePressed;
            subscribed = false;
        }
    }

    private void TrySubscribe()
    {
        if (subscribed)
            return;
        if (cueController == null)
            cueController = FindObjectOfType<CueController>();
        if (cueController == null)
        {
            Debug.LogWarning("[CueEvidenceRecorder] No CueController found in scene; cue evidence will not be recorded.");
            return;
        }

        cueController.CuePressedEvent += HandleCuePressed;
        subscribed = true;
    }

    private void HandleCuePressed(CueLevel level)
    {
        try
        {
            if (level == CueLevel.None)
                return;

            // Section B records cue evidence directly in
            // CueController.OnCueButtonPressed (Section B-only bridge) because
            // Section B has no IStudyItemMetadataProvider and no dialogue, so
            // the click site itself is the most reliable recording point.
            // Skip here to guarantee exactly one cue_pressed event per inner
            // cue click in Section B. A/C/D and Phase 2 continue through the
            // normal resolution chain below.
            //
            // Guard mirrors CueController.OnCueButtonPressed's bridge guard:
            // scriptNum == 26 is set by sectionB.unity's PrefabInstance
            // override at scene-deserialization time and is provably unique
            // to Section B across every scene/prefab/JSON content/Phase 2
            // runtime range. Using scriptNum keeps this guard symmetric with
            // the bridge and independent of Unity's active-scene-name
            // reporting.
            if (cueController != null &&
                cueController.scriptNum == Phase1SectionBSyntheticScriptNumber)
            {
                return;
            }

            StudyItemMetadata metadata = ResolveMetadata();
            if (metadata == null)
            {
                Debug.LogWarning(
                    $"[CueEvidenceRecorder] Cue '{level}' pressed but no current item/task metadata could be resolved; skipping record.");
                return;
            }

            StudyActiveItemTracker.RecordCueEvent(metadata, level.ToString());
        }
        catch (Exception ex)
        {
            // Safe-fail: never let a recorder bug propagate to the student UI.
            Debug.LogWarning($"[CueEvidenceRecorder] Failed to record cue press: {ex.Message}");
        }
    }

    // Returns the StudyItemMetadata to attribute the cue event to, or null if
    // resolution fails. Resolution order matches the class header comment.
    private StudyItemMetadata ResolveMetadata()
    {
        IStudyItemMetadataProvider provider = TryResolveProvider();
        if (provider != null &&
            provider.TryGetCurrentStudyItemMetadata(out StudyItemMetadata fromProvider) &&
            fromProvider != null)
        {
            return fromProvider;
        }

        // Phase 1 Section B synthetic fallback. Only fires when the current
        // scene is sectionB. Reuses Phase1StudyItemMetadataResolver.TryResolve
        // with the same synthetic script number Phase1SectionBChecklistManager
        // already uses, so the cue event attaches to the same synthetic
        // Section B task-level item.
        //
        // Promoted ABOVE the StudyActiveItemTracker.ActiveItemMetadata fallback
        // so that stale tracker state carried over from a previously played
        // section cannot misattribute Section B cue presses (Section B does
        // not advance the tracker via dialogue, because SimuCaseTargetButtonUI
        // is m_Enabled:0 in sectionB.unity and no other IStudyItemMetadataProvider
        // is active in B). Without this promotion, a Section A → Section B
        // session can leave ActiveItemMetadata == A-NN, the recorder picks
        // that, the cue event gets itemId="A-NN", and StudyTaskResultBuffer.
        // BuildItemResult's IsSnapshotForItem(snapshot, "B-01") fails, dropping
        // cueUsed/cueLevel/cue_pressed from the Section B payload.
        string sceneName = SceneManager.GetActiveScene().name;
        if (string.Equals(sceneName, Phase1SectionBSceneName, StringComparison.OrdinalIgnoreCase))
        {
            if (Phase1StudyItemMetadataResolver.TryResolve(
                    Phase1SectionBSyntheticScriptNumber,
                    zeroBasedItemIndex: 0,
                    fallbackTargetAnswer: null,
                    currentScript: null,
                    sceneName: sceneName,
                    out StudyItemMetadata synthetic))
            {
                return synthetic;
            }
        }

        StudyItemMetadata active = StudyActiveItemTracker.ActiveItemMetadata;
        if (active != null)
            return active;

        return null;
    }

    // Mirrors the IStudyItemMetadataProvider resolution pattern already used by
    // OpenAIRequest.TryResolveStudyItemMetadataProvider: prefer the configured
    // serialized behaviour, then fall back to scanning active MonoBehaviours.
    private IStudyItemMetadataProvider TryResolveProvider()
    {
        if (IsUsableProvider(studyItemMetadataProvider))
            return studyItemMetadataProvider;

        studyItemMetadataProvider = null;

        if (studyItemMetadataProviderBehaviour is IStudyItemMetadataProvider configured &&
            studyItemMetadataProviderBehaviour.isActiveAndEnabled)
        {
            studyItemMetadataProvider = configured;
            return studyItemMetadataProvider;
        }

        MonoBehaviour[] candidates =
            FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < candidates.Length; i++)
        {
            MonoBehaviour candidate = candidates[i];
            if (candidate == null || candidate == this)
                continue;
            if (candidate is IStudyItemMetadataProvider providerCandidate &&
                candidate.isActiveAndEnabled)
            {
                studyItemMetadataProviderBehaviour = candidate;
                studyItemMetadataProvider = providerCandidate;
                return studyItemMetadataProvider;
            }
        }

        return null;
    }

    private static bool IsUsableProvider(IStudyItemMetadataProvider provider)
    {
        if (provider == null)
            return false;
        MonoBehaviour behaviour = provider as MonoBehaviour;
        return behaviour == null || behaviour.isActiveAndEnabled;
    }
}
