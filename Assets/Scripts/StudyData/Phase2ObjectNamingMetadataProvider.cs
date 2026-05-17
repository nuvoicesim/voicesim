using System.Collections;
using System.Collections.Generic;
using UI.Cues;
using UnityEngine;

public class Phase2ObjectNamingMetadataProvider : MonoBehaviour, IStudyItemMetadataProvider
{
    [SerializeField] private string groupId = Phase2TrainingDefaults.GroupBen;
    [SerializeField] private int startingScriptNumber = 1;
    [SerializeField] private int itemCount = 5;
    [SerializeField] private string taskId = "phase2-ben-object-naming";
    [SerializeField] private string sectionId = "phase2-ben-object-naming";

    [Header("Phase 2 Scene Wiring")]
    [SerializeField] private CueController cueController;
    [SerializeField] private TargetButtonUI targetButtonUI;
    [SerializeField] private StudyCompletionChecklistManager completionChecklistManager;

    private static readonly ObjectNamingFallbackItem[] FallbackItems =
    {
        new ObjectNamingFallbackItem(1, "Morning Routine", "Coffee", ""),
        new ObjectNamingFallbackItem(2, "Family Members", "Daughter", ""),
        new ObjectNamingFallbackItem(3, "Daily Activities", "Shower", ""),
        new ObjectNamingFallbackItem(4, "Meals", "Sandwich", ""),
        new ObjectNamingFallbackItem(5, "Transportation", "Car", ""),
        new ObjectNamingFallbackItem(6, "Clothing", "Shirt", ""),
        new ObjectNamingFallbackItem(7, "Weather", "Sunny", ""),
        new ObjectNamingFallbackItem(8, "Household Items", "Television", "TV"),
        new ObjectNamingFallbackItem(9, "Health/Medical", "Pill", "Pills"),
        new ObjectNamingFallbackItem(10, "Emotions/Feelings", "Tired", "")
    };

    public string GroupId => groupId;
    public int StartingScriptNumber => startingScriptNumber;
    public int ItemCount => itemCount;
    public int EndingScriptNumber => startingScriptNumber + Mathf.Max(0, itemCount - 1);

    private void Awake()
    {
        ResolveSceneReferences();
        ApplyTargetRange();
    }

    private IEnumerator Start()
    {
        ResolveSceneReferences();
        ApplyTargetRange();

        if (targetButtonUI != null)
            targetButtonUI.BeforeTargetAdvanced += OnBeforeTargetAdvanced;

        yield return WaitForCueScripts();

        EnsureCueControllerMatchesCurrentTarget();
        ConfigureCompletionChecklist();
    }

    private void OnDestroy()
    {
        if (targetButtonUI != null)
            targetButtonUI.BeforeTargetAdvanced -= OnBeforeTargetAdvanced;
    }

    public bool TryGetCurrentStudyItemMetadata(out StudyItemMetadata metadata)
    {
        metadata = null;
        int scriptNumber = ResolveCurrentScriptNumber();
        if (!IsScriptInRange(scriptNumber))
            return false;

        metadata = BuildMetadata(scriptNumber);
        return metadata != null;
    }

    public List<StudyItemMetadata> GetActiveStudyItemMetadataList()
    {
        List<StudyItemMetadata> metadataItems = new List<StudyItemMetadata>(Mathf.Max(0, itemCount));
        for (int scriptNumber = startingScriptNumber; scriptNumber <= EndingScriptNumber; scriptNumber++)
        {
            StudyItemMetadata metadata = BuildMetadata(scriptNumber);
            if (metadata != null)
                metadataItems.Add(metadata);
        }

        return metadataItems;
    }

    public List<string> GetChecklistLabels()
    {
        List<string> labels = new List<string>(Mathf.Max(0, itemCount));
        for (int scriptNumber = startingScriptNumber; scriptNumber <= EndingScriptNumber; scriptNumber++)
        {
            ObjectNamingFallbackItem item = ResolveFallbackItem(scriptNumber);
            TryResolveScriptEntry(scriptNumber, out ScriptEntry scriptEntry);

            string target = StudyDataDefaults.FirstNonEmpty(scriptEntry?.target_word, item?.targetWord);
            string alternate = StudyDataDefaults.FirstNonEmpty(scriptEntry?.alternate_target, item?.alternateTarget);
            string displayTarget = string.IsNullOrWhiteSpace(alternate) ? target : target + " / " + alternate;
            labels.Add("Practice Target: " + displayTarget);
        }

        return labels;
    }

    private void OnBeforeTargetAdvanced(int scriptNumber)
    {
        if (!IsScriptInRange(scriptNumber))
            return;

        StudyItemMetadata metadata = BuildMetadata(scriptNumber);
        if (metadata != null)
            StudyTaskResultBuffer.TryFinalizeCurrentItem(metadata, null, null, out _);
    }

    private void ConfigureCompletionChecklist()
    {
        ResolveSceneReferences();
        if (completionChecklistManager == null)
            return;

        completionChecklistManager.ConfigureCompletionItems(
            GetActiveStudyItemMetadataList(),
            GetChecklistLabels());
    }

    private void ApplyTargetRange()
    {
        if (targetButtonUI == null)
            return;

        targetButtonUI.ConfigureTargetRange(startingScriptNumber, EndingScriptNumber);
    }

    private void EnsureCueControllerMatchesCurrentTarget()
    {
        if (cueController == null)
            return;

        int scriptNumber = ResolveCurrentScriptNumber();
        cueController.scriptNum = scriptNumber;
        if (cueController.TryGetScriptEntry(scriptNumber, out _))
        {
            cueController.SetCurrentScript(scriptNumber);
            cueController.ResetCueing();
        }
    }

    private IEnumerator WaitForCueScripts()
    {
        if (cueController == null)
            yield break;

        const float timeoutSeconds = 3f;
        float elapsed = 0f;
        while (!cueController.TryGetScriptEntry(startingScriptNumber, out _) && elapsed < timeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private int ResolveCurrentScriptNumber()
    {
        if (targetButtonUI != null)
            return Mathf.Clamp(targetButtonUI.CurrentTargetIndex, startingScriptNumber, EndingScriptNumber);

        if (cueController != null)
            return Mathf.Clamp(cueController.scriptNum, startingScriptNumber, EndingScriptNumber);

        return startingScriptNumber;
    }

    private StudyItemMetadata BuildMetadata(int scriptNumber)
    {
        ObjectNamingFallbackItem fallback = ResolveFallbackItem(scriptNumber);
        TryResolveScriptEntry(scriptNumber, out ScriptEntry scriptEntry);

        string normalizedGroupId = string.IsNullOrWhiteSpace(groupId) ? Phase2TrainingDefaults.GroupBen : groupId.Trim();
        string resolvedTaskId = StudyDataDefaults.FirstNonEmpty(taskId, "phase2-" + normalizedGroupId + "-object-naming");
        string resolvedSectionId = StudyDataDefaults.FirstNonEmpty(sectionId, resolvedTaskId);

        return new StudyItemMetadata
        {
            phaseId = StudyDataDefaults.Phase2,
            itemId = "phase2-object-naming-" + scriptNumber.ToString("00"),
            taskId = resolvedTaskId,
            taskType = StudyDataDefaults.TaskTypeObjectNamingWithCueing,
            sectionId = resolvedSectionId,
            sectionType = StudyDataDefaults.TaskTypeObjectNamingWithCueing,
            scriptNumber = scriptNumber,
            promptText = StudyDataDefaults.FirstNonEmpty(scriptEntry?.title, fallback?.title),
            stimulusRef = "target_words.json#script-" + scriptNumber,
            targetAnswer = StudyDataDefaults.FirstNonEmpty(scriptEntry?.target_word, fallback?.targetWord),
            alternateTarget = StudyDataDefaults.FirstNonEmpty(scriptEntry?.alternate_target, fallback?.alternateTarget),
            activityType = StudyDataDefaults.ActivityTraining,
            scoringMode = StudyDataDefaults.ScoringNone
        };
    }

    private bool TryResolveScriptEntry(int scriptNumber, out ScriptEntry scriptEntry)
    {
        scriptEntry = null;
        return cueController != null && cueController.TryGetScriptEntry(scriptNumber, out scriptEntry);
    }

    private bool IsScriptInRange(int scriptNumber)
    {
        return scriptNumber >= startingScriptNumber && scriptNumber <= EndingScriptNumber;
    }

    private static ObjectNamingFallbackItem ResolveFallbackItem(int scriptNumber)
    {
        foreach (ObjectNamingFallbackItem item in FallbackItems)
        {
            if (item != null && item.scriptNumber == scriptNumber)
                return item;
        }

        return null;
    }

    private void ResolveSceneReferences()
    {
        if (cueController == null)
            cueController = FindObjectOfType<CueController>();

        if (targetButtonUI == null)
            targetButtonUI = FindObjectOfType<TargetButtonUI>();

        if (completionChecklistManager == null)
            completionChecklistManager = FindObjectOfType<StudyCompletionChecklistManager>();
    }

    private class ObjectNamingFallbackItem
    {
        public readonly int scriptNumber;
        public readonly string title;
        public readonly string targetWord;
        public readonly string alternateTarget;

        public ObjectNamingFallbackItem(int scriptNumber, string title, string targetWord, string alternateTarget)
        {
            this.scriptNumber = scriptNumber;
            this.title = title;
            this.targetWord = targetWord;
            this.alternateTarget = alternateTarget;
        }
    }
}
