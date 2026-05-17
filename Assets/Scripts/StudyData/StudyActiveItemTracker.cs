using System;
using System.Collections.Generic;

[Serializable]
public class StudyActiveItemSnapshot
{
    public StudyItemMetadata activeItemMetadata;
    public string patientFinalResponse;
    public string startedAt;
    public List<StudyTranscriptTurn> transcriptTurns = new List<StudyTranscriptTurn>();
    public List<StudyInteractionEvent> interactionEvents = new List<StudyInteractionEvent>();
    public bool cueUsed;
    public string cueLevelReached;
}

public static class StudyActiveItemTracker
{
    public static event Action Changed;

    private const string SpeakerStudent = "student";
    private const string SpeakerPatient = "patient";
    private const string EventItemTrackingStarted = "item_tracking_started";
    private const string EventStudentUtteranceRecorded = "student_utterance_recorded";
    private const string EventPatientUtteranceRecorded = "patient_utterance_recorded";
    private const string EventCuePressed = "cue_pressed";

    private static StudyItemMetadata activeItemMetadata;
    private static readonly List<StudyTranscriptTurn> transcriptTurns = new List<StudyTranscriptTurn>();
    private static readonly List<StudyInteractionEvent> interactionEvents = new List<StudyInteractionEvent>();
    private static string patientFinalResponse;
    private static string startedAt;
    private static bool cueUsed;
    private static string cueLevelReached;

    public static bool HasActiveItem => activeItemMetadata != null;
    public static StudyItemMetadata ActiveItemMetadata => activeItemMetadata;
    public static string PatientFinalResponse => patientFinalResponse;
    public static string StartedAt => startedAt;
    public static int TranscriptTurnCount => transcriptTurns.Count;

    public static void BeginItem(StudyItemMetadata metadata)
    {
        if (metadata == null)
            return;

        activeItemMetadata = CloneMetadata(metadata);
        transcriptTurns.Clear();
        interactionEvents.Clear();
        patientFinalResponse = "";
        startedAt = StudyDataDefaults.NowIso();
        cueUsed = false;
        cueLevelReached = null;

        AddEvent(EventItemTrackingStarted, null);
        Changed?.Invoke();
    }

    public static bool BeginOrUpdateItem(StudyItemMetadata metadata)
    {
        if (metadata == null)
            return false;

        if (!IsSameItem(activeItemMetadata, metadata))
        {
            BeginItem(metadata);
            return true;
        }

        activeItemMetadata = CloneMetadata(metadata);
        Changed?.Invoke();
        return true;
    }

    public static bool RecordStudentUtterance(
        StudyItemMetadata metadata,
        string text,
        int? turnIndex = null,
        string userSpeechStartAt = null,
        string userSpeechEndAt = null)
    {
        if (!BeginOrUpdateItem(metadata))
            return false;

        bool recorded = AppendTranscriptTurn(
            SpeakerStudent,
            text,
            turnIndex,
            userSpeechStartAt,
            userSpeechEndAt,
            null,
            null);

        if (recorded)
            AddEvent(EventStudentUtteranceRecorded, text);

        return recorded;
    }

    public static bool RecordPatientResponse(
        StudyItemMetadata metadata,
        string text,
        int? turnIndex = null,
        string patientSpeechStartAt = null,
        string patientSpeechEndAt = null)
    {
        if (!BeginOrUpdateItem(metadata))
            return false;

        bool recorded = AppendTranscriptTurn(
            SpeakerPatient,
            text,
            turnIndex,
            null,
            null,
            patientSpeechStartAt,
            patientSpeechEndAt);

        if (recorded)
        {
            patientFinalResponse = text.Trim();
            AddEvent(EventPatientUtteranceRecorded, text);
        }

        return recorded;
    }

    public static bool AppendTranscriptTurn(
        string speaker,
        string text,
        int? turnIndex = null,
        string userSpeechStartAt = null,
        string userSpeechEndAt = null,
        string patientSpeechStartAt = null,
        string patientSpeechEndAt = null)
    {
        if (activeItemMetadata == null || string.IsNullOrWhiteSpace(text))
            return false;

        transcriptTurns.Add(new StudyTranscriptTurn
        {
            turnId = Guid.NewGuid().ToString("N"),
            sessionId = StudyDataDefaults.FirstNonEmpty(StudyRuntimeContext.SessionContext?.sessionId, RuntimeSessionContext.SessionId),
            taskId = activeItemMetadata.taskId,
            sectionId = activeItemMetadata.sectionId,
            itemId = activeItemMetadata.itemId,
            turnIndex = turnIndex,
            speaker = string.IsNullOrWhiteSpace(speaker) ? "unknown" : speaker.Trim(),
            text = text.Trim(),
            timestamp = StudyDataDefaults.NowIso(),
            userSpeechStartAt = userSpeechStartAt,
            userSpeechEndAt = userSpeechEndAt,
            patientSpeechStartAt = patientSpeechStartAt,
            patientSpeechEndAt = patientSpeechEndAt
        });

        Changed?.Invoke();
        return true;
    }

    public static StudyActiveItemSnapshot GetSnapshot()
    {
        return new StudyActiveItemSnapshot
        {
            activeItemMetadata = CloneMetadata(activeItemMetadata),
            patientFinalResponse = patientFinalResponse,
            startedAt = startedAt,
            transcriptTurns = CloneTranscriptTurns(),
            interactionEvents = CloneInteractionEvents(),
            cueUsed = cueUsed,
            cueLevelReached = cueLevelReached
        };
    }

    public static void Clear()
    {
        activeItemMetadata = null;
        transcriptTurns.Clear();
        interactionEvents.Clear();
        patientFinalResponse = "";
        startedAt = "";
        cueUsed = false;
        cueLevelReached = null;
        Changed?.Invoke();
    }

    public static bool RecordCueEvent(StudyItemMetadata metadata, string cueLevelName)
    {
        if (string.IsNullOrWhiteSpace(cueLevelName))
            return false;
        if (metadata != null)
            BeginOrUpdateItem(metadata);
        if (activeItemMetadata == null)
            return false;

        string normalized = cueLevelName.Trim();
        cueUsed = true;
        if (CueLevelStrength(normalized) > CueLevelStrength(cueLevelReached))
            cueLevelReached = normalized;

        interactionEvents.Add(new StudyInteractionEvent
        {
            eventId = Guid.NewGuid().ToString("N"),
            sessionId = StudyDataDefaults.FirstNonEmpty(StudyRuntimeContext.SessionContext?.sessionId, RuntimeSessionContext.SessionId),
            taskId = activeItemMetadata.taskId,
            sectionId = activeItemMetadata.sectionId,
            itemId = activeItemMetadata.itemId,
            eventType = EventCuePressed,
            timestamp = StudyDataDefaults.NowIso(),
            cueLevel = normalized,
            message = normalized
        });

        Changed?.Invoke();
        return true;
    }

    private static int CueLevelStrength(string level)
    {
        if (string.IsNullOrWhiteSpace(level))
            return 0;
        switch (level.Trim().ToLowerInvariant())
        {
            case "semantic": return 1;
            case "phonemic": return 2;
            case "model":    return 3;
            default:         return 0;
        }
    }

    private static void AddEvent(string eventType, string message)
    {
        if (activeItemMetadata == null)
            return;

        interactionEvents.Add(new StudyInteractionEvent
        {
            eventId = Guid.NewGuid().ToString("N"),
            sessionId = StudyDataDefaults.FirstNonEmpty(StudyRuntimeContext.SessionContext?.sessionId, RuntimeSessionContext.SessionId),
            taskId = activeItemMetadata.taskId,
            sectionId = activeItemMetadata.sectionId,
            itemId = activeItemMetadata.itemId,
            eventType = eventType,
            timestamp = StudyDataDefaults.NowIso(),
            message = message
        });
    }

    private static bool IsSameItem(StudyItemMetadata current, StudyItemMetadata next)
    {
        if (current == null || next == null)
            return false;

        return string.Equals(current.itemId, next.itemId, StringComparison.Ordinal) &&
               string.Equals(current.taskId, next.taskId, StringComparison.Ordinal) &&
               string.Equals(current.sectionId, next.sectionId, StringComparison.Ordinal);
    }

    private static StudyItemMetadata CloneMetadata(StudyItemMetadata source)
    {
        if (source == null)
            return null;

        return new StudyItemMetadata
        {
            phaseId = source.phaseId,
            itemId = source.itemId,
            taskId = source.taskId,
            taskType = source.taskType,
            sectionId = source.sectionId,
            sectionType = source.sectionType,
            scriptNumber = source.scriptNumber,
            promptText = source.promptText,
            stimulusRef = source.stimulusRef,
            targetAnswer = source.targetAnswer,
            alternateTarget = source.alternateTarget,
            activityType = source.activityType,
            scoringMode = source.scoringMode
        };
    }

    private static List<StudyTranscriptTurn> CloneTranscriptTurns()
    {
        List<StudyTranscriptTurn> snapshot = new List<StudyTranscriptTurn>(transcriptTurns.Count);
        foreach (StudyTranscriptTurn turn in transcriptTurns)
        {
            snapshot.Add(new StudyTranscriptTurn
            {
                turnId = turn.turnId,
                sessionId = turn.sessionId,
                taskId = turn.taskId,
                sectionId = turn.sectionId,
                itemId = turn.itemId,
                turnIndex = turn.turnIndex,
                speaker = turn.speaker,
                text = turn.text,
                timestamp = turn.timestamp,
                userSpeechStartAt = turn.userSpeechStartAt,
                userSpeechEndAt = turn.userSpeechEndAt,
                patientSpeechStartAt = turn.patientSpeechStartAt,
                patientSpeechEndAt = turn.patientSpeechEndAt,
                metadataJson = turn.metadataJson
            });
        }

        return snapshot;
    }

    private static List<StudyInteractionEvent> CloneInteractionEvents()
    {
        List<StudyInteractionEvent> snapshot = new List<StudyInteractionEvent>(interactionEvents.Count);
        foreach (StudyInteractionEvent interactionEvent in interactionEvents)
        {
            snapshot.Add(new StudyInteractionEvent
            {
                eventId = interactionEvent.eventId,
                sessionId = interactionEvent.sessionId,
                taskId = interactionEvent.taskId,
                sectionId = interactionEvent.sectionId,
                itemId = interactionEvent.itemId,
                eventType = interactionEvent.eventType,
                timestamp = interactionEvent.timestamp,
                cueLevel = interactionEvent.cueLevel,
                scoreValue = interactionEvent.scoreValue,
                message = interactionEvent.message,
                metadataJson = interactionEvent.metadataJson
            });
        }

        return snapshot;
    }
}
