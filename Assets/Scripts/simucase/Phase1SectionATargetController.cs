using System.Collections;
using System.Collections.Generic;
using UI.Cues;
using UnityEngine;

// Phase 1 Section A target/image bootstrap.
//
// Section A's target words and image map are Section-A-specific (Book, Ball,
// Knife, Cup, Safety Pin). Rather than polluting Phase 2's target_words.json,
// this controller runtime-injects Section A's script entries into CueController
// (the same UseRuntimeScripts pattern Phase 2 Sentence Completion uses) and
// replaces CueController's image map with Section A's slots.
//
// Drop this MonoBehaviour onto a scene-level GameObject in sectionA.unity and
// drag the CueController reference (or leave empty for FindObjectOfType).
// Drop final Section A sprites onto the imageSlots list in the Inspector.
//
// Phase 1 Section A items metadata is provided by Phase1StudyItemMetadataResolver
// (scripts 21-25 → section "A" → "phase1-section-a" / "object_naming").
public class Phase1SectionATargetController : MonoBehaviour
{
    [Header("Cue Controller")]
    [SerializeField] private CueController cueController;

    [Header("Section A Image Slots")]
    [Tooltip("Drop final Section A sprites here (Book, Ball, Knife, Cup, Safety Pin). Empty entries render as blank image areas until sprites are added.")]
    [SerializeField] private List<TargetImageEntry> imageSlots = new List<TargetImageEntry>();

    [Header("Display Options")]
    [SerializeField] private string targetLabelPrefix = "Target:";
    [SerializeField] private bool includePromptInTargetText = false;

    private void Reset()
    {
        imageSlots = Phase1SectionAItemDefinitions.BuildDefaultImageSlots();
    }

    private void Awake()
    {
        if (imageSlots == null || imageSlots.Count == 0)
            imageSlots = Phase1SectionAItemDefinitions.BuildDefaultImageSlots();

        if (cueController == null)
            cueController = FindObjectOfType<CueController>();
    }

    private IEnumerator Start()
    {
        // Wait one frame so CueController.Start() can run its initial wiring before we override.
        yield return null;

        if (cueController == null)
        {
            Debug.LogWarning("[Phase1SectionATargetController] No CueController found in scene; Section A target injection skipped.");
            yield break;
        }

        ScriptEntry[] sectionAScripts = Phase1SectionAItemDefinitions.BuildScriptEntries();
        cueController.UseRuntimeScripts(sectionAScripts, Phase1SectionAItemDefinitions.ScriptStart);
        cueController.SetTargetImages(imageSlots);
        cueController.SetTargetLabelPrefix(targetLabelPrefix);
        cueController.SetIncludePromptInTargetText(includePromptInTargetText);
        cueController.ResetCueing();
    }
}
