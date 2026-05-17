using System;
using Newtonsoft.Json;
using UnityEngine;

[Serializable]
public class StudyRuntimeContextPayload
{
    public StudySessionContext sessionContext;
    public StudyTaskContext taskContext;
}

public static class StudyRuntimeContext
{
    public static event Action Changed;

    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
        NullValueHandling = NullValueHandling.Ignore
    };

    private static StudySessionContext sessionContext = StudySessionContext.CreateDefault();
    private static StudyTaskContext taskContext = StudyTaskContext.CreateDefault();

    public static StudySessionContext SessionContext => sessionContext;
    public static StudyTaskContext TaskContext => taskContext;
    public static string PhaseId => StudyDataDefaults.FirstNonEmpty(taskContext.phaseId, sessionContext.phaseId, StudyDataDefaults.PhaseUnknown);
    public static string PatientId => StudyDataDefaults.FirstNonEmpty(taskContext.patientId, sessionContext.patientId);
    public static string ScenarioId => StudyDataDefaults.FirstNonEmpty(taskContext.scenarioId, sessionContext.scenarioId, StudyDataDefaults.ScenarioUnknown);
    public static string ActivityType => StudyDataDefaults.FirstNonEmpty(taskContext.activityType, sessionContext.activityType, StudyDataDefaults.ActivityUnspecified);
    public static string ScoringMode => StudyDataDefaults.FirstNonEmpty(taskContext.scoringMode, sessionContext.scoringMode, StudyDataDefaults.ScoringNone);
    public static string TaskId => taskContext.GetResolvedTaskId();
    public static string TaskType => taskContext.GetResolvedTaskType();
    public static string SectionId => taskContext.sectionId;
    public static string SectionType => taskContext.sectionType;

    public static bool HasExplicitSessionContext =>
        sessionContext != null &&
        (!string.IsNullOrWhiteSpace(sessionContext.sessionId) ||
         !string.IsNullOrWhiteSpace(sessionContext.assignmentId) ||
         !string.IsNullOrWhiteSpace(sessionContext.patientId) ||
         IsNonDefault(sessionContext.phaseId, StudyDataDefaults.PhaseUnknown) ||
         IsNonDefault(sessionContext.scenarioId, StudyDataDefaults.ScenarioUnknown));

    public static bool HasExplicitTaskContext =>
        taskContext != null &&
        (!string.IsNullOrWhiteSpace(taskContext.taskId) ||
         !string.IsNullOrWhiteSpace(taskContext.sectionId));

    public static void Apply(StudyRuntimeContextPayload payload)
    {
        if (payload == null)
        {
            Debug.LogWarning("[StudyRuntimeContext] Ignoring null study runtime context payload.");
            return;
        }

        Apply(payload.sessionContext, payload.taskContext);
    }

    public static void Apply(StudySessionContext nextSessionContext, StudyTaskContext nextTaskContext)
    {
        sessionContext = NormalizeSessionContext(nextSessionContext);
        taskContext = NormalizeTaskContext(nextTaskContext, sessionContext);
        Changed?.Invoke();
    }

    public static void ApplySessionContext(StudySessionContext nextSessionContext)
    {
        sessionContext = NormalizeSessionContext(nextSessionContext);
        taskContext = NormalizeTaskContext(taskContext, sessionContext);
        Changed?.Invoke();
    }

    public static void ApplyTaskContext(StudyTaskContext nextTaskContext)
    {
        taskContext = NormalizeTaskContext(nextTaskContext, sessionContext);
        Changed?.Invoke();
    }

    public static void ApplyJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning("[StudyRuntimeContext] Ignoring empty study runtime context JSON.");
            return;
        }

        try
        {
            Apply(JsonConvert.DeserializeObject<StudyRuntimeContextPayload>(json));
        }
        catch (Exception ex)
        {
            Debug.LogError($"[StudyRuntimeContext] Failed to parse study runtime context JSON: {ex.Message}");
        }
    }

    public static void Clear()
    {
        sessionContext = StudySessionContext.CreateDefault();
        taskContext = StudyTaskContext.CreateDefault();
        Changed?.Invoke();
    }

    public static StudyTaskResultPayload CreateEmptyTaskResultPayload(string status = StudyDataDefaults.StatusInProgress)
    {
        return new StudyTaskResultPayload
        {
            sessionContext = sessionContext,
            taskContext = taskContext,
            status = string.IsNullOrWhiteSpace(status) ? StudyDataDefaults.StatusInProgress : status.Trim(),
            startedAt = taskContext?.startedAt,
            completedAt = taskContext?.completedAt
        };
    }

    public static string ToJson(object payload)
    {
        return JsonConvert.SerializeObject(payload, JsonSettings);
    }

    public static StudySessionContext CreateSessionContextFromRuntimeSession()
    {
        StudySessionContext context = StudySessionContext.CreateDefault();

        if (!RuntimeSessionContext.HasContext)
            return context;

        context.sessionId = RuntimeSessionContext.SessionId;
        context.assignmentId = RuntimeSessionContext.AssignmentId;
        context.studentId = RuntimeSessionContext.UserId;
        context.scenarioId = RuntimeSessionContext.ContextLabel;
        return NormalizeSessionContext(context);
    }

    private static StudySessionContext NormalizeSessionContext(StudySessionContext context)
    {
        StudySessionContext normalized = context ?? StudySessionContext.CreateDefault();
        normalized.phaseId = StudyDataDefaults.FirstNonEmpty(normalized.phaseId, StudyDataDefaults.PhaseUnknown);
        normalized.activityType = StudyDataDefaults.FirstNonEmpty(normalized.activityType, StudyDataDefaults.ActivityUnspecified);
        normalized.scoringMode = StudyDataDefaults.FirstNonEmpty(normalized.scoringMode, StudyDataDefaults.ScoringNone);
        normalized.platform = StudyDataDefaults.FirstNonEmpty(normalized.platform, StudyDataDefaults.PlatformUnityWebGL);
        normalized.scenarioId = StudyDataDefaults.FirstNonEmpty(normalized.scenarioId, StudyDataDefaults.ScenarioUnknown);
        normalized.startedAt = StudyDataDefaults.FirstNonEmpty(normalized.startedAt, StudyDataDefaults.NowIso());
        return normalized;
    }

    private static StudyTaskContext NormalizeTaskContext(StudyTaskContext context, StudySessionContext fallbackSessionContext)
    {
        StudyTaskContext normalized = context ?? StudyTaskContext.CreateDefault();
        normalized.phaseId = StudyDataDefaults.FirstNonEmpty(normalized.phaseId, fallbackSessionContext?.phaseId, StudyDataDefaults.PhaseUnknown);
        normalized.activityType = StudyDataDefaults.FirstNonEmpty(normalized.activityType, fallbackSessionContext?.activityType, StudyDataDefaults.ActivityUnspecified);
        normalized.scoringMode = StudyDataDefaults.FirstNonEmpty(normalized.scoringMode, fallbackSessionContext?.scoringMode, StudyDataDefaults.ScoringNone);
        normalized.patientId = StudyDataDefaults.FirstNonEmpty(normalized.patientId, fallbackSessionContext?.patientId);
        normalized.scenarioId = StudyDataDefaults.FirstNonEmpty(normalized.scenarioId, fallbackSessionContext?.scenarioId, StudyDataDefaults.ScenarioUnknown);
        normalized.taskType = StudyDataDefaults.FirstNonEmpty(normalized.taskType, normalized.sectionType, StudyDataDefaults.TaskTypeUnspecified);
        normalized.startedAt = StudyDataDefaults.FirstNonEmpty(normalized.startedAt, StudyDataDefaults.NowIso());
        if (normalized.items == null)
            normalized.items = new System.Collections.Generic.List<StudyItemMetadata>();
        return normalized;
    }

    private static bool IsNonDefault(string value, string defaultValue)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               !string.Equals(value.Trim(), defaultValue, StringComparison.OrdinalIgnoreCase);
    }
}
