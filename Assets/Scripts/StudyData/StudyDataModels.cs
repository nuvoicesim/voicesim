using System;
using System.Collections.Generic;

[Serializable]
public class StudySessionContext
{
    public string phaseId;
    public string activityType;
    public string scoringMode;
    public string platform;
    public string studentId;
    public string courseId;
    public string courseItemId;
    public string moduleItemId;
    public string assignmentId;
    public string sessionId;
    public string conditionId;
    public string assignedPath;
    public string orderGroup;
    public string patientId;
    public string scenarioId;
    public string startedAt;
    public string completedAt;

    public static StudySessionContext CreateDefault()
    {
        return new StudySessionContext
        {
            phaseId = StudyDataDefaults.PhaseUnknown,
            activityType = StudyDataDefaults.ActivityUnspecified,
            scoringMode = StudyDataDefaults.ScoringNone,
            platform = StudyDataDefaults.PlatformUnityWebGL,
            scenarioId = StudyDataDefaults.ScenarioUnknown,
            startedAt = StudyDataDefaults.NowIso()
        };
    }
}

[Serializable]
public class StudyTaskContext
{
    public string phaseId;
    public string activityType;
    public string scoringMode;
    public string patientId;
    public string scenarioId;
    public string taskId;
    public string taskType;
    public string sectionId;
    public string sectionType;
    public string startedAt;
    public string completedAt;
    public List<StudyItemMetadata> items = new List<StudyItemMetadata>();

    public string GetResolvedTaskId()
    {
        return StudyDataDefaults.FirstNonEmpty(taskId, sectionId);
    }

    public string GetResolvedTaskType()
    {
        return StudyDataDefaults.FirstNonEmpty(taskType, sectionType);
    }

    public static StudyTaskContext CreateDefault()
    {
        return new StudyTaskContext
        {
            phaseId = StudyDataDefaults.PhaseUnknown,
            activityType = StudyDataDefaults.ActivityUnspecified,
            scoringMode = StudyDataDefaults.ScoringNone,
            scenarioId = StudyDataDefaults.ScenarioUnknown,
            taskType = StudyDataDefaults.TaskTypeUnspecified,
            startedAt = StudyDataDefaults.NowIso()
        };
    }
}

[Serializable]
public class StudyItemMetadata
{
    public string phaseId;
    public string itemId;
    public string taskId;
    public string taskType;
    public string sectionId;
    public string sectionType;
    public int? scriptNumber;
    public string promptText;
    public string stimulusRef;
    public string targetAnswer;
    public string alternateTarget;
    public string activityType;
    public string scoringMode;
}

[Serializable]
public class StudyTranscriptTurn
{
    public string turnId;
    public string sessionId;
    public string taskId;
    public string sectionId;
    public string itemId;
    public int? turnIndex;
    public string speaker;
    public string text;
    public string timestamp;
    public string userSpeechStartAt;
    public string userSpeechEndAt;
    public string patientSpeechStartAt;
    public string patientSpeechEndAt;
    public string metadataJson;
}

[Serializable]
public class StudyInteractionEvent
{
    public string eventId;
    public string sessionId;
    public string taskId;
    public string sectionId;
    public string itemId;
    public string eventType;
    public string timestamp;
    public string cueLevel;
    public int? scoreValue;
    public string message;
    public string metadataJson;
}

[Serializable]
public class StudyItemResult
{
    public string itemId;
    public string taskId;
    public string taskType;
    public string sectionId;
    public string sectionType;
    public int? scriptNumber;
    public string promptText;
    public string stimulusRef;
    public string targetAnswer;
    public string alternateTarget;
    public string patientFinalResponse;
    public int? studentSelectedScore;
    public int? expectedScore;
    public bool? scoreMatchesExpected;
    public bool? completionChecked;
    public bool? cueUsed;
    public string cueLevel;
    public string startedAt;
    public string completedAt;
    public List<StudyTranscriptTurn> transcriptTurns = new List<StudyTranscriptTurn>();
    public List<StudyInteractionEvent> interactionEvents = new List<StudyInteractionEvent>();
}

[Serializable]
public class StudyTaskResultPayload
{
    public StudySessionContext sessionContext;
    public StudyTaskContext taskContext;
    public string status;
    public string startedAt;
    public string completedAt;
    public List<StudyItemResult> itemResults = new List<StudyItemResult>();
    public List<StudyTranscriptTurn> transcriptTurns = new List<StudyTranscriptTurn>();
    public List<StudyInteractionEvent> interactionEvents = new List<StudyInteractionEvent>();
}

public static class StudyDataDefaults
{
    public const string PhaseUnknown = "unknown";
    public const string Phase1 = "phase1";
    public const string Phase2 = "phase2";
    public const string ActivityUnspecified = "unspecified";
    public const string ActivityAssessment = "assessment";
    public const string ActivityTraining = "training";
    public const string ScoringNone = "none";
    public const string ScoringPhase1Rubric = "phase1-rubric";
    public const string PlatformUnityWebGL = "unity-webgl";
    public const string ScenarioUnknown = "unknown";
    public const string TaskTypeUnspecified = "unspecified";
    public const string TaskTypeObjectNamingWithCueing = "object_naming_with_cueing";
    public const string TaskTypeSentenceCompletionPractice = "sentence_completion_practice";
    public const string StatusNotStarted = "not_started";
    public const string StatusInProgress = "in_progress";
    public const string StatusCompleted = "completed";

    public static string NowIso()
    {
        return DateTime.UtcNow.ToString("o");
    }

    public static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return string.Empty;

        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return string.Empty;
    }
}
