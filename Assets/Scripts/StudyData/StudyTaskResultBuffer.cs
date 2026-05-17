using System;
using System.Collections.Generic;

public static class StudyTaskResultBuffer
{
    private static readonly List<StudyItemResult> finalizedItemResults = new List<StudyItemResult>();
    private static readonly HashSet<string> finalizedItemIds = new HashSet<string>();
    private static StudyTaskContext taskContext;
    private static string startedAt;
    private static string completedAt;

    public static int FinalizedItemCount => finalizedItemResults.Count;
    public static StudyTaskContext TaskContext => taskContext;

    public static void ResetForTask(StudyItemMetadata metadata)
    {
        finalizedItemResults.Clear();
        finalizedItemIds.Clear();
        taskContext = BuildTaskContext(metadata);
        startedAt = StudyDataDefaults.NowIso();
        completedAt = null;
    }

    public static bool HasFinalizedItem(string itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId) && finalizedItemIds.Contains(itemId.Trim());
    }

    public static IReadOnlyList<StudyItemResult> GetFinalizedResults()
    {
        return finalizedItemResults.AsReadOnly();
    }

    public static bool TryUpdateCompletionChecked(string itemId, bool? completionChecked)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        string normalizedItemId = itemId.Trim();
        foreach (StudyItemResult result in finalizedItemResults)
        {
            if (result != null && string.Equals(result.itemId, normalizedItemId, StringComparison.Ordinal))
            {
                result.completionChecked = completionChecked;
                return true;
            }
        }

        return false;
    }

    public static bool TryFinalizeCurrentItem(
        StudyItemMetadata metadata,
        int? selectedScore,
        out StudyItemResult result)
    {
        return TryFinalizeCurrentItem(metadata, selectedScore, null, out result);
    }

    public static bool TryFinalizeCurrentItem(
        StudyItemMetadata metadata,
        out StudyItemResult result)
    {
        return TryFinalizeCurrentItem(metadata, null, null, out result);
    }

    public static bool TryFinalizeCurrentItem(
        StudyItemMetadata metadata,
        int? selectedScore,
        bool? completionChecked,
        out StudyItemResult result)
    {
        result = null;
        if (metadata == null || string.IsNullOrWhiteSpace(metadata.itemId))
            return false;

        EnsureTask(metadata);

        string itemId = metadata.itemId.Trim();
        if (finalizedItemIds.Contains(itemId))
        {
            UnityEngine.Debug.Log($"[StudyTaskResultBuffer] Item '{itemId}' is already finalized. Skipping duplicate result.");
            return false;
        }

        StudyActiveItemSnapshot snapshot = StudyActiveItemTracker.GetSnapshot();
        StudyItemMetadata resultMetadata = metadata;
        if (IsSnapshotForItem(snapshot, itemId))
            resultMetadata = snapshot.activeItemMetadata ?? metadata;

        result = BuildItemResult(resultMetadata, selectedScore, completionChecked, snapshot);
        finalizedItemResults.Add(result);
        finalizedItemIds.Add(itemId);
        StudyActiveItemTracker.Clear();
        return true;
    }

    public static void RefreshSelectedScores(SimuCaseChecklistManager checklistManager)
    {
        if (checklistManager == null)
            return;

        foreach (StudyItemResult result in finalizedItemResults)
        {
            int rowIndex = ResolveRowIndex(result);
            if (rowIndex < 0)
                continue;

            if (checklistManager.TryGetSelectedScore(rowIndex, out int score))
                result.studentSelectedScore = score;
        }
    }

    public static StudyTaskResultPayload BuildPayload(string status = StudyDataDefaults.StatusCompleted)
    {
        completedAt = StudyDataDefaults.NowIso();
        StudyTaskContext payloadTaskContext = CloneTaskContext(taskContext) ?? BuildTaskContext(null);
        if (payloadTaskContext != null)
            payloadTaskContext.completedAt = completedAt;

        return new StudyTaskResultPayload
        {
            sessionContext = BuildSessionContext(payloadTaskContext),
            taskContext = payloadTaskContext,
            status = string.IsNullOrWhiteSpace(status) ? StudyDataDefaults.StatusCompleted : status.Trim(),
            startedAt = startedAt,
            completedAt = completedAt,
            itemResults = CloneItemResults()
        };
    }

    private static void EnsureTask(StudyItemMetadata metadata)
    {
        if (metadata == null)
            return;

        string incomingTaskId = StudyDataDefaults.FirstNonEmpty(metadata.taskId, metadata.sectionId);
        string activeTaskId = taskContext != null ? taskContext.GetResolvedTaskId() : "";

        if (taskContext == null || !string.Equals(incomingTaskId, activeTaskId, StringComparison.Ordinal))
            ResetForTask(metadata);
    }

    private static StudyTaskContext BuildTaskContext(StudyItemMetadata metadata)
    {
        StudyTaskContext runtimeTaskContext = StudyRuntimeContext.TaskContext;
        StudySessionContext runtimeSessionContext = StudyRuntimeContext.SessionContext;

        return new StudyTaskContext
        {
            phaseId = ResolvePhase(metadata?.phaseId, runtimeTaskContext?.phaseId, runtimeSessionContext?.phaseId, StudyDataDefaults.PhaseUnknown),
            activityType = ResolveActivity(metadata?.activityType, runtimeTaskContext?.activityType, runtimeSessionContext?.activityType, StudyDataDefaults.ActivityUnspecified),
            scoringMode = ResolveScoring(metadata?.scoringMode, runtimeTaskContext?.scoringMode, runtimeSessionContext?.scoringMode, StudyDataDefaults.ScoringNone),
            patientId = StudyDataDefaults.FirstNonEmpty(runtimeTaskContext?.patientId, runtimeSessionContext?.patientId, StudyRuntimeContext.PatientId),
            scenarioId = ResolveScenario(runtimeTaskContext?.scenarioId, runtimeSessionContext?.scenarioId, StudyRuntimeContext.ScenarioId, StudyDataDefaults.ScenarioUnknown),
            taskId = StudyDataDefaults.FirstNonEmpty(metadata?.taskId, runtimeTaskContext?.taskId),
            taskType = ResolveTaskType(metadata?.taskType, metadata?.sectionType, runtimeTaskContext?.taskType, runtimeTaskContext?.sectionType, StudyDataDefaults.TaskTypeUnspecified),
            sectionId = StudyDataDefaults.FirstNonEmpty(metadata?.sectionId, runtimeTaskContext?.sectionId),
            sectionType = StudyDataDefaults.FirstNonEmpty(metadata?.sectionType, runtimeTaskContext?.sectionType),
            startedAt = StudyDataDefaults.NowIso()
        };
    }

    private static StudySessionContext BuildSessionContext(StudyTaskContext payloadTaskContext)
    {
        StudySessionContext hostContext = StudyRuntimeContext.CreateSessionContextFromRuntimeSession();
        StudySessionContext runtimeContext = StudyRuntimeContext.SessionContext;
        StudySessionContext context = StudySessionContext.CreateDefault();

        context.phaseId = ResolvePhase(payloadTaskContext?.phaseId, runtimeContext?.phaseId, hostContext?.phaseId, StudyDataDefaults.PhaseUnknown);
        context.activityType = ResolveActivity(payloadTaskContext?.activityType, runtimeContext?.activityType, hostContext?.activityType, StudyDataDefaults.ActivityUnspecified);
        context.scoringMode = ResolveScoring(payloadTaskContext?.scoringMode, runtimeContext?.scoringMode, hostContext?.scoringMode, StudyDataDefaults.ScoringNone);
        context.platform = StudyDataDefaults.FirstNonEmpty(runtimeContext?.platform, hostContext?.platform, StudyDataDefaults.PlatformUnityWebGL);
        context.studentId = StudyDataDefaults.FirstNonEmpty(runtimeContext?.studentId, hostContext?.studentId, RuntimeSessionContext.UserId);
        context.courseId = StudyDataDefaults.FirstNonEmpty(runtimeContext?.courseId, hostContext?.courseId);
        context.courseItemId = StudyDataDefaults.FirstNonEmpty(runtimeContext?.courseItemId, hostContext?.courseItemId);
        context.moduleItemId = StudyDataDefaults.FirstNonEmpty(runtimeContext?.moduleItemId, hostContext?.moduleItemId);
        context.assignmentId = StudyDataDefaults.FirstNonEmpty(runtimeContext?.assignmentId, hostContext?.assignmentId, RuntimeSessionContext.AssignmentId);
        context.sessionId = StudyDataDefaults.FirstNonEmpty(runtimeContext?.sessionId, hostContext?.sessionId, RuntimeSessionContext.SessionId);
        context.conditionId = StudyDataDefaults.FirstNonEmpty(runtimeContext?.conditionId, hostContext?.conditionId);
        context.assignedPath = StudyDataDefaults.FirstNonEmpty(runtimeContext?.assignedPath, hostContext?.assignedPath);
        context.orderGroup = StudyDataDefaults.FirstNonEmpty(runtimeContext?.orderGroup, hostContext?.orderGroup);
        context.patientId = StudyDataDefaults.FirstNonEmpty(payloadTaskContext?.patientId, runtimeContext?.patientId, hostContext?.patientId, StudyRuntimeContext.PatientId);
        context.scenarioId = ResolveScenario(payloadTaskContext?.scenarioId, runtimeContext?.scenarioId, hostContext?.scenarioId, StudyRuntimeContext.ScenarioId, StudyDataDefaults.ScenarioUnknown);
        context.startedAt = StudyDataDefaults.FirstNonEmpty(runtimeContext?.startedAt, hostContext?.startedAt, payloadTaskContext?.startedAt, startedAt, StudyDataDefaults.NowIso());
        context.completedAt = completedAt;
        return context;
    }

    private static string ResolvePhase(params string[] values)
    {
        return ResolveIgnoringSentinel(StudyDataDefaults.PhaseUnknown, StudyDataDefaults.PhaseUnknown, values);
    }

    private static string ResolveActivity(params string[] values)
    {
        return ResolveIgnoringSentinel(StudyDataDefaults.ActivityUnspecified, StudyDataDefaults.ActivityUnspecified, values);
    }

    private static string ResolveScoring(params string[] values)
    {
        if (values != null)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }
        }

        return StudyDataDefaults.ScoringNone;
    }

    private static string ResolveScenario(params string[] values)
    {
        return ResolveIgnoringSentinel(StudyDataDefaults.ScenarioUnknown, StudyDataDefaults.ScenarioUnknown, values);
    }

    private static string ResolveTaskType(params string[] values)
    {
        return ResolveIgnoringSentinel(StudyDataDefaults.TaskTypeUnspecified, StudyDataDefaults.TaskTypeUnspecified, values);
    }

    private static string ResolveIgnoringSentinel(string sentinel, string defaultValue, params string[] values)
    {
        if (values != null)
        {
            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                string trimmed = value.Trim();
                if (!string.IsNullOrWhiteSpace(sentinel) &&
                    string.Equals(trimmed, sentinel, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return trimmed;
            }
        }

        return defaultValue;
    }

    private static bool IsSnapshotForItem(StudyActiveItemSnapshot snapshot, string itemId)
    {
        return snapshot != null &&
               snapshot.activeItemMetadata != null &&
               string.Equals(snapshot.activeItemMetadata.itemId, itemId, StringComparison.Ordinal);
    }

    private static StudyItemResult BuildItemResult(
        StudyItemMetadata metadata,
        int? selectedScore,
        bool? completionChecked,
        StudyActiveItemSnapshot snapshot)
    {
        bool snapshotMatches = IsSnapshotForItem(snapshot, metadata.itemId);
        bool? snapshotCueUsed = snapshotMatches && snapshot.cueUsed ? (bool?)true : null;
        string snapshotCueLevel = snapshotMatches && !string.IsNullOrWhiteSpace(snapshot.cueLevelReached)
            ? snapshot.cueLevelReached
            : null;

        return new StudyItemResult
        {
            itemId = metadata.itemId,
            taskId = metadata.taskId,
            taskType = metadata.taskType,
            sectionId = metadata.sectionId,
            sectionType = metadata.sectionType,
            scriptNumber = metadata.scriptNumber,
            promptText = metadata.promptText,
            stimulusRef = metadata.stimulusRef,
            targetAnswer = metadata.targetAnswer,
            alternateTarget = metadata.alternateTarget,
            patientFinalResponse = snapshotMatches ? snapshot.patientFinalResponse : null,
            studentSelectedScore = selectedScore,
            expectedScore = null,
            scoreMatchesExpected = null,
            completionChecked = completionChecked,
            cueUsed = snapshotCueUsed,
            cueLevel = snapshotCueLevel,
            startedAt = snapshotMatches ? snapshot.startedAt : null,
            completedAt = StudyDataDefaults.NowIso(),
            transcriptTurns = snapshotMatches ? snapshot.transcriptTurns : new List<StudyTranscriptTurn>(),
            interactionEvents = snapshotMatches ? snapshot.interactionEvents : new List<StudyInteractionEvent>()
        };
    }

    private static int ResolveRowIndex(StudyItemResult result)
    {
        if (result == null || string.IsNullOrWhiteSpace(result.itemId))
            return -1;

        int dashIndex = result.itemId.LastIndexOf('-');
        if (dashIndex < 0 || dashIndex >= result.itemId.Length - 1)
            return -1;

        string ordinalText = result.itemId.Substring(dashIndex + 1);
        return int.TryParse(ordinalText, out int ordinal) ? Math.Max(0, ordinal - 1) : -1;
    }

    private static StudyTaskContext CloneTaskContext(StudyTaskContext source)
    {
        if (source == null)
            return null;

        return new StudyTaskContext
        {
            phaseId = source.phaseId,
            activityType = source.activityType,
            scoringMode = source.scoringMode,
            patientId = source.patientId,
            scenarioId = source.scenarioId,
            taskId = source.taskId,
            taskType = source.taskType,
            sectionId = source.sectionId,
            sectionType = source.sectionType,
            startedAt = source.startedAt,
            completedAt = source.completedAt,
            items = source.items != null ? new List<StudyItemMetadata>(source.items) : new List<StudyItemMetadata>()
        };
    }

    private static List<StudyItemResult> CloneItemResults()
    {
        List<StudyItemResult> snapshot = new List<StudyItemResult>(finalizedItemResults.Count);
        foreach (StudyItemResult result in finalizedItemResults)
        {
            snapshot.Add(new StudyItemResult
            {
                itemId = result.itemId,
                taskId = result.taskId,
                taskType = result.taskType,
                sectionId = result.sectionId,
                sectionType = result.sectionType,
                scriptNumber = result.scriptNumber,
                promptText = result.promptText,
                stimulusRef = result.stimulusRef,
                targetAnswer = result.targetAnswer,
                alternateTarget = result.alternateTarget,
                patientFinalResponse = result.patientFinalResponse,
                studentSelectedScore = result.studentSelectedScore,
                expectedScore = result.expectedScore,
                scoreMatchesExpected = result.scoreMatchesExpected,
                completionChecked = result.completionChecked,
                cueUsed = result.cueUsed,
                cueLevel = result.cueLevel,
                startedAt = result.startedAt,
                completedAt = result.completedAt,
                transcriptTurns = result.transcriptTurns != null ? new List<StudyTranscriptTurn>(result.transcriptTurns) : new List<StudyTranscriptTurn>(),
                interactionEvents = result.interactionEvents != null ? new List<StudyInteractionEvent>(result.interactionEvents) : new List<StudyInteractionEvent>()
            });
        }

        return snapshot;
    }
}
