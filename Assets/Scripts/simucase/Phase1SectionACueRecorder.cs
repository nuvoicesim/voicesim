using UI.Cues;
using UnityEngine;

// Phase 1 Section A cue-evidence recorder.
//
// Subscribes to CueController.CuePressedEvent and writes cue evidence onto
// StudyActiveItemTracker (cueUsed = true and the strongest cueLevel reached
// since the active item began). StudyTaskResultBuffer.BuildItemResult then
// projects these into the finalized StudyItemResult, which flows through
// ScoringItemContext.cueUsed / cueLevel to the /llm-scoring request body.
//
// Drop this MonoBehaviour onto any scene-level GameObject in sectionA.unity.
// It is Section-A-only by attachment; Sections C, D, and Phase 2 scenes do not
// include this recorder, so their items continue to ship with cueUsed = null
// and cueLevel = null exactly as before.
//
// Safe failure modes:
//   - If CueController is missing in scene, the recorder logs once and disables itself.
//   - If no active item is tracked when a cue press fires, RecordCueEvent
//     short-circuits gracefully without throwing.
public class Phase1SectionACueRecorder : MonoBehaviour
{
    [SerializeField] private CueController cueController;

    private bool subscribed;

    private void Awake()
    {
        if (cueController == null)
            cueController = FindObjectOfType<CueController>();
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
            Debug.LogWarning("[Phase1SectionACueRecorder] No CueController found in scene; cue evidence will not be recorded.");
            return;
        }

        cueController.CuePressedEvent += HandleCuePressed;
        subscribed = true;
    }

    private void HandleCuePressed(CueLevel level)
    {
        if (level == CueLevel.None)
            return;

        StudyItemMetadata activeMetadata = StudyActiveItemTracker.ActiveItemMetadata;
        StudyActiveItemTracker.RecordCueEvent(activeMetadata, level.ToString());
    }
}
