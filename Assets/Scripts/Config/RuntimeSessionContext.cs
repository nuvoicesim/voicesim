using System;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class RuntimeSessionContextPayload
{
    public string tokenType;
    public string runtimeToken;
    public string expiresAt;
    public string refreshAfter;
    public string sessionId;
    public string assignmentId;
    public string sceneId;
    public string unityBuildFolder;
    public string userId;
    public string userRole;
    public string userEmail;
}

public static class RuntimeSessionContext
{
    public static event Action Changed;

    private static RuntimeSessionContextPayload current;

    public static RuntimeSessionContextPayload Current => current;
    public static bool HasContext => current != null;
    public static string TokenType => current?.tokenType?.Trim();
    public static bool HasRuntimeToken => !string.IsNullOrWhiteSpace(current?.runtimeToken);
    public static string RuntimeToken => current?.runtimeToken?.Trim();
    public static string ExpiresAt => current?.expiresAt?.Trim();
    public static string RefreshAfter => current?.refreshAfter?.Trim();
    public static string SessionId => current?.sessionId?.Trim();
    public static string AssignmentId => current?.assignmentId?.Trim();
    public static string SceneId => current?.sceneId?.Trim();
    public static string UnityBuildFolder => current?.unityBuildFolder?.Trim();
    public static string UserId => current?.userId?.Trim();

    public static string ContextLabel
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(SceneId))
                return SceneId;

            if (!string.IsNullOrWhiteSpace(UnityBuildFolder))
                return UnityBuildFolder;

            if (!string.IsNullOrWhiteSpace(AssignmentId))
                return AssignmentId;

            return "assignment-runtime";
        }
    }

    public static void ApplyJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning("[RuntimeSessionContext] Ignoring empty runtime context payload.");
            return;
        }

        try
        {
            var payload = JsonConvert.DeserializeObject<RuntimeSessionContextPayload>(json);
            if (payload == null)
            {
                Debug.LogWarning("[RuntimeSessionContext] Runtime context payload deserialized to null.");
                return;
            }

            Apply(payload);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RuntimeSessionContext] Failed to parse runtime context JSON: {ex.Message}");
        }
    }

    public static void Apply(RuntimeSessionContextPayload payload)
    {
        if (payload == null)
        {
            Debug.LogWarning("[RuntimeSessionContext] Ignoring null runtime context payload.");
            return;
        }

        current = Normalize(payload);

        Debug.Log(
            $"[RuntimeSessionContext] Applied context: sessionId='{current.sessionId}', " +
            $"assignmentId='{current.assignmentId}', contextLabel='{ContextLabel}', " +
            $"hasToken={HasRuntimeToken}");

        Changed?.Invoke();
    }

    public static void Clear()
    {
        current = null;
        Debug.Log("[RuntimeSessionContext] Cleared runtime context.");
        Changed?.Invoke();
    }

    public static bool ApplyAuthorization(UnityWebRequest request, string callerName)
    {
        if (request == null)
            return false;

        if (!HasRuntimeToken)
        {
            Debug.LogError($"[{callerName}] Missing runtime token. The host app must inject runtime context before calling backend APIs.");
            return false;
        }

        request.SetRequestHeader("Authorization", $"Bearer {RuntimeToken}");
        return true;
    }

    private static RuntimeSessionContextPayload Normalize(RuntimeSessionContextPayload payload)
    {
        return new RuntimeSessionContextPayload
        {
            tokenType = payload.tokenType?.Trim(),
            runtimeToken = payload.runtimeToken?.Trim(),
            expiresAt = payload.expiresAt?.Trim(),
            refreshAfter = payload.refreshAfter?.Trim(),
            sessionId = payload.sessionId?.Trim(),
            assignmentId = payload.assignmentId?.Trim(),
            sceneId = payload.sceneId?.Trim(),
            unityBuildFolder = payload.unityBuildFolder?.Trim(),
            userId = payload.userId?.Trim(),
            userRole = payload.userRole?.Trim(),
            userEmail = payload.userEmail?.Trim()
        };
    }
}
