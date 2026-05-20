using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

// Calls PUT /sessions/{sessionId}/task-progress/{progressKey}/complete after a
// successful study-flow /llm-scoring submission, so the backend can record
// task-level completion for the current internal task. Once every entry in
// the scene's SceneCatalog.requiredTaskKeys has a matching SessionTaskProgress
// row, the backend automatically completes the whole SimulationSession and
// mirrors completion into StudentItemProgress.
//
// CRITICAL CONTRACT — this client targets the TASK-LEVEL endpoint only:
//   PUT /sessions/{sessionId}/task-progress/{progressKey}/complete
// It must NOT be confused with the WHOLE-SESSION endpoint
//   PUT /sessions/{sessionId}/complete
// which is incorrect for multi-task WebGL builds. A prior experimental
// implementation that called the whole-session endpoint per task Finish was
// deliberately reverted; this helper replaces it with the correct per-task
// endpoint.
//
// Guards:
//   - missing sessionId / runtime token → log warning and skip (no UI disruption)
//   - missing phaseId or both taskId+sectionId → log warning and skip
//   - missing API config / bad URL build → log error and skip
//   - duplicate in-flight completion for the same (sessionId, progressKey) → ignore
//
// Failure-mode contract:
//   Task-progress PUT failure must NEVER block the already-successful
//   /llm-scoring Finish flow. The student already saw POST /llm-scoring
//   succeed and the rendered result (rubric block for Phase 1, or no-extra-UI
//   for Phase 2). A downstream synchronization hiccup is logged at
//   Warning/Log level and surfaced via the optional outcome callback.
//
// Auth + URL construction mirror ScoreManager's POST /llm-scoring convention:
//   - ApiConfigProvider.TryBuildBackendUrl(...) builds the URL.
//   - RuntimeSessionContext.ApplyAuthorization(...) applies the Bearer token.
public static class SessionTaskProgressClient
{
    public enum TaskProgressResult
    {
        Success,
        AlreadyInFlight,
        MissingSessionId,
        MissingRuntimeToken,
        MissingIdentity,
        ConfigError,
        HttpError,
    }

    public struct TaskProgressOutcome
    {
        public TaskProgressResult result;
        public long httpStatusCode;
        public string errorMessage;
        public string responseBody;

        public bool IsSuccess => result == TaskProgressResult.Success;
    }

    [Serializable]
    public class TaskProgressRequest
    {
        public string phaseId;
        public string taskId;
        public string sectionId;
        public string taskType;
    }

    private const string SessionsBasePath = "/sessions";
    private const string TaskProgressSegment = "/task-progress";
    private const string CompleteSuffix = "/complete";
    private const int DefaultTimeoutSeconds = 20;

    // De-dup key is (sessionId, progressKey), NOT sessionId alone, so two
    // distinct tasks completed in the same session may both be in flight
    // simultaneously (e.g. very fast double-Finish across tasks). The same
    // task double-clicked within one session is short-circuited.
    private static readonly HashSet<string> inFlightKeys = new HashSet<string>();

    private static string NonEmpty(string a, string b)
    {
        if (!string.IsNullOrWhiteSpace(a)) return a.Trim();
        if (!string.IsNullOrWhiteSpace(b)) return b.Trim();
        return null;
    }

    private static string ComposeInFlightKey(string sessionId, string progressKey)
    {
        return sessionId + "#" + progressKey;
    }

    /// <summary>
    /// Coroutine that issues
    ///   PUT /sessions/{sessionId}/task-progress/{progressKey}/complete
    /// and invokes the callback with the outcome. The host MonoBehaviour
    /// anchors the request lifecycle.
    /// </summary>
    /// <param name="host">MonoBehaviour to anchor any nested SendWebRequest behavior.</param>
    /// <param name="callerName">Used in log lines for source identification.</param>
    /// <param name="request">Identity fields used to build the URL and body.</param>
    /// <param name="onComplete">Invoked exactly once with the outcome. Optional.</param>
    public static IEnumerator MarkTaskComplete(
        MonoBehaviour host,
        string callerName,
        TaskProgressRequest request,
        Action<TaskProgressOutcome> onComplete)
    {
        string logger = string.IsNullOrWhiteSpace(callerName) ? "SessionTaskProgressClient" : callerName;

        string sessionId = RuntimeSessionContext.SessionId;
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            Debug.LogWarning($"[{logger}] SessionTaskProgressClient: missing RuntimeSessionContext.SessionId; skipping task-progress write.");
            onComplete?.Invoke(new TaskProgressOutcome { result = TaskProgressResult.MissingSessionId });
            yield break;
        }

        if (!RuntimeSessionContext.HasRuntimeToken)
        {
            Debug.LogWarning($"[{logger}] SessionTaskProgressClient: missing runtime token; skipping task-progress write for session {sessionId}.");
            onComplete?.Invoke(new TaskProgressOutcome { result = TaskProgressResult.MissingRuntimeToken });
            yield break;
        }

        if (request == null || string.IsNullOrWhiteSpace(request.phaseId))
        {
            Debug.LogWarning($"[{logger}] SessionTaskProgressClient: missing phaseId; skipping task-progress write.");
            onComplete?.Invoke(new TaskProgressOutcome { result = TaskProgressResult.MissingIdentity });
            yield break;
        }

        string taskOrSectionId = NonEmpty(request.taskId, request.sectionId);
        if (taskOrSectionId == null)
        {
            Debug.LogWarning($"[{logger}] SessionTaskProgressClient: missing taskId and sectionId; cannot derive progressKey.");
            onComplete?.Invoke(new TaskProgressOutcome { result = TaskProgressResult.MissingIdentity });
            yield break;
        }

        string phaseId = request.phaseId.Trim();
        string progressKey = phaseId + "#" + taskOrSectionId;

        string path = string.Concat(
            SessionsBasePath, "/", Uri.EscapeDataString(sessionId),
            TaskProgressSegment, "/", Uri.EscapeDataString(progressKey),
            CompleteSuffix);

        if (!ApiConfigProvider.TryBuildBackendUrl(path, out string completionUrl))
        {
            Debug.LogError($"[{logger}] SessionTaskProgressClient: cannot build task-progress URL (API config missing).");
            onComplete?.Invoke(new TaskProgressOutcome
            {
                result = TaskProgressResult.ConfigError,
                errorMessage = "API environment config is missing or incomplete.",
            });
            yield break;
        }

        // Build the JSON body. Use Newtonsoft NullValueHandling.Ignore to match
        // the existing ScoreManager convention so empty optional fields are
        // omitted rather than serialized as nulls.
        string jsonBody;
        try
        {
            jsonBody = JsonConvert.SerializeObject(
                new TaskProgressRequest
                {
                    phaseId = phaseId,
                    taskId = !string.IsNullOrWhiteSpace(request.taskId) ? request.taskId.Trim() : null,
                    sectionId = !string.IsNullOrWhiteSpace(request.sectionId) ? request.sectionId.Trim() : null,
                    taskType = !string.IsNullOrWhiteSpace(request.taskType) ? request.taskType.Trim() : null,
                },
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{logger}] SessionTaskProgressClient: failed to serialize body for {progressKey}: {ex.Message}");
            onComplete?.Invoke(new TaskProgressOutcome
            {
                result = TaskProgressResult.HttpError,
                errorMessage = ex.Message,
            });
            yield break;
        }

        // Build the request synchronously BEFORE registering in-flight so any
        // early-return path (auth failure, constructor throws) cannot leak a
        // dangling dedup entry into inFlightKeys.
        UnityWebRequest request_uwr = null;
        try
        {
            request_uwr = new UnityWebRequest(completionUrl, "PUT");
            byte[] body = Encoding.UTF8.GetBytes(jsonBody);
            request_uwr.uploadHandler = new UploadHandlerRaw(body);
            request_uwr.downloadHandler = new DownloadHandlerBuffer();
            request_uwr.SetRequestHeader("Content-Type", "application/json");
            request_uwr.SetRequestHeader("Accept", "application/json");
            request_uwr.timeout = DefaultTimeoutSeconds;

            if (!RuntimeSessionContext.ApplyAuthorization(request_uwr, logger))
            {
                request_uwr.Dispose();
                onComplete?.Invoke(new TaskProgressOutcome { result = TaskProgressResult.MissingRuntimeToken });
                yield break;
            }
        }
        catch (Exception ex)
        {
            request_uwr?.Dispose();
            Debug.LogError($"[{logger}] SessionTaskProgressClient: failed to build request for {progressKey}: {ex.Message}");
            onComplete?.Invoke(new TaskProgressOutcome
            {
                result = TaskProgressResult.HttpError,
                errorMessage = ex.Message,
            });
            yield break;
        }

        // Deduplication: check + register AFTER the request is fully built and
        // authorized. If a duplicate for the SAME (sessionId, progressKey) is
        // in flight, drop the freshly built request without ever touching the
        // dedup set.
        string inFlightKey = ComposeInFlightKey(sessionId, progressKey);
        if (inFlightKeys.Contains(inFlightKey))
        {
            Debug.Log($"[{logger}] SessionTaskProgressClient: task progress write already in flight for {inFlightKey}; ignoring duplicate request.");
            request_uwr.Dispose();
            onComplete?.Invoke(new TaskProgressOutcome { result = TaskProgressResult.AlreadyInFlight });
            yield break;
        }
        inFlightKeys.Add(inFlightKey);

        Debug.Log($"[{logger}] SessionTaskProgressClient: PUT {completionUrl}");

        // try/finally guarantees cleanup of both the in-flight registration
        // AND the UnityWebRequest disposal on every exit path (success, HTTP
        // failure, generator disposal). yield return is legal inside a try
        // block whose only handler is finally; do NOT add a catch here.
        try
        {
            UnityWebRequestAsyncOperation operation = request_uwr.SendWebRequest();
            while (!operation.isDone)
                yield return null;

            string responseBody = request_uwr.downloadHandler != null ? request_uwr.downloadHandler.text : string.Empty;
            long statusCode = request_uwr.responseCode;

            if (request_uwr.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning(
                    $"[{logger}] SessionTaskProgressClient: task-progress completion failed for {progressKey} " +
                    $"(HTTP {statusCode}, {request_uwr.error}). Body: {responseBody}");
                onComplete?.Invoke(new TaskProgressOutcome
                {
                    result = TaskProgressResult.HttpError,
                    httpStatusCode = statusCode,
                    errorMessage = request_uwr.error,
                    responseBody = responseBody,
                });
                yield break;
            }

            Debug.Log($"[{logger}] SessionTaskProgressClient: session {sessionId} task {progressKey} marked complete (HTTP {statusCode}).");
            onComplete?.Invoke(new TaskProgressOutcome
            {
                result = TaskProgressResult.Success,
                httpStatusCode = statusCode,
                responseBody = responseBody,
            });
        }
        finally
        {
            inFlightKeys.Remove(inFlightKey);
            request_uwr.Dispose();
        }
    }
}
