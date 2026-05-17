using System;
using UI.Cues;
using UnityEngine.SceneManagement;

public static class Phase1StudyItemMetadataResolver
{
    private static readonly SectionDefinition[] SupportedSections =
    {
        new SectionDefinition("A", "phase1-section-a", "object_naming", "sectionA", 21, 25),
        new SectionDefinition("B", "phase1-section-b", "word_fluency", "sectionB", 26, 26),
        new SectionDefinition("C", "phase1-section-c", "sentence_completion", "sectionC", 11, 15),
        new SectionDefinition("D", "phase1-section-d", "responsive_speech", "sectionD", 16, 20)
    };

    public static bool TryResolveCurrentItem(
        SimuCaseTargetButtonUI targetButton,
        CueController cueController,
        out StudyItemMetadata metadata)
    {
        metadata = null;
        if (targetButton == null)
            return false;

        return TryResolve(
            targetButton.CurrentScriptNumber,
            targetButton.CurrentTargetIndex,
            targetButton.CurrentTargetWord,
            cueController != null ? cueController.currentScript : null,
            SceneManager.GetActiveScene().name,
            out metadata);
    }

    public static bool TryResolve(
        int scriptNumber,
        int zeroBasedItemIndex,
        string fallbackTargetAnswer,
        ScriptEntry currentScript,
        string sceneName,
        out StudyItemMetadata metadata)
    {
        metadata = null;
        SectionDefinition section = ResolveSection(scriptNumber, sceneName);
        if (section == null)
            return false;

        int itemOrdinal = section.ResolveItemOrdinal(scriptNumber, zeroBasedItemIndex);
        bool hasMatchingScript = currentScript != null && currentScript.script_number == scriptNumber;

        metadata = new StudyItemMetadata
        {
            phaseId = StudyDataDefaults.Phase1,
            itemId = section.BuildItemId(itemOrdinal),
            taskId = section.taskId,
            taskType = section.sectionType,
            sectionId = section.sectionId,
            sectionType = section.sectionType,
            scriptNumber = scriptNumber,
            promptText = hasMatchingScript ? TrimToNull(currentScript.title) : null,
            targetAnswer = hasMatchingScript
                ? StudyDataDefaults.FirstNonEmpty(currentScript.target_word, fallbackTargetAnswer)
                : TrimToNull(fallbackTargetAnswer),
            alternateTarget = hasMatchingScript ? TrimToNull(currentScript.alternate_target) : null,
            activityType = StudyDataDefaults.ActivityAssessment,
            scoringMode = StudyDataDefaults.ScoringPhase1Rubric
        };

        return true;
    }

    private static SectionDefinition ResolveSection(int scriptNumber, string sceneName)
    {
        foreach (SectionDefinition section in SupportedSections)
        {
            if (section.ContainsScript(scriptNumber))
                return section;
        }

        foreach (SectionDefinition section in SupportedSections)
        {
            if (section.MatchesScene(sceneName))
                return section;
        }

        return null;
    }

    private static string TrimToNull(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed class SectionDefinition
    {
        public readonly string sectionId;
        public readonly string taskId;
        public readonly string sectionType;
        private readonly string sceneName;
        private readonly int firstScriptNumber;
        private readonly int lastScriptNumber;

        public SectionDefinition(
            string sectionId,
            string taskId,
            string sectionType,
            string sceneName,
            int firstScriptNumber,
            int lastScriptNumber)
        {
            this.sectionId = sectionId;
            this.taskId = taskId;
            this.sectionType = sectionType;
            this.sceneName = sceneName;
            this.firstScriptNumber = firstScriptNumber;
            this.lastScriptNumber = lastScriptNumber;
        }

        public bool ContainsScript(int scriptNumber)
        {
            return scriptNumber >= firstScriptNumber && scriptNumber <= lastScriptNumber;
        }

        public bool MatchesScene(string activeSceneName)
        {
            return !string.IsNullOrWhiteSpace(activeSceneName) &&
                   string.Equals(activeSceneName.Trim(), sceneName, StringComparison.OrdinalIgnoreCase);
        }

        public int ResolveItemOrdinal(int scriptNumber, int zeroBasedItemIndex)
        {
            if (ContainsScript(scriptNumber))
                return scriptNumber - firstScriptNumber + 1;

            return Math.Max(1, zeroBasedItemIndex + 1);
        }

        public string BuildItemId(int itemOrdinal)
        {
            return $"{sectionId}-{Math.Max(1, itemOrdinal):00}";
        }
    }
}
