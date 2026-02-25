using UnityEngine;

public static class WebGLLogConfigurator
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Configure()
    {
#if UNITY_WEBGL && !UNITY_EDITOR && !DEVELOPMENT_BUILD
        // Keep warnings/errors, suppress regular Debug.Log noise in production WebGL.
        Debug.unityLogger.filterLogType = LogType.Warning;

        // Trim warning stack traces to keep browser console readable.
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
#endif
    }
}
