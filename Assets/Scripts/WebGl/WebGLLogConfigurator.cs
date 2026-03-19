using System;
using UnityEngine;

public static class WebGLLogConfigurator
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Configure()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        bool forceVerboseLogs = Application.absoluteURL.IndexOf("unityLogs=1", StringComparison.OrdinalIgnoreCase) >= 0;

        if (!Debug.isDebugBuild && !forceVerboseLogs)
        {
            Debug.LogWarning("[WebGLLog] Non-development build: Debug.Log is suppressed (Warning/Error only). Use Development Build or append ?unityLogs=1 for runtime logs.");

            // Keep warnings/errors, suppress regular Debug.Log noise in production WebGL.
            Debug.unityLogger.filterLogType = LogType.Warning;

            // Trim warning stack traces to keep browser console readable.
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
            return;
        }

        if (forceVerboseLogs && !Debug.isDebugBuild)
        {
            Debug.LogWarning("[WebGLLog] Verbose runtime logging enabled by URL flag (?unityLogs=1).");
        }
#endif
    }
}
