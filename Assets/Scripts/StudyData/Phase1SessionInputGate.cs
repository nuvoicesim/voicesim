using UnityEngine;
using UnityEngine.SceneManagement;

// Phase 1 session-scoped speech-input gate.
//
// Phase 1 Section B (Word Fluency) needs to block new speaking interactions
// once the 60-second timer ends, while letting any in-flight recording / TTS
// complete naturally. Rather than wiring every input owner via Inspector
// references per-scene, a global static gate is used:
//
//   - STT controllers check `Phase1SessionInputGate.IsEnabled` before starting
//     a new recording. When false, new R-key starts are silently ignored.
//
//   - Phase1SectionBTimerController calls `Disable()` at session end.
//
//   - The gate auto-resets to enabled on every scene load via the
//     RuntimeInitializeOnLoadMethod-registered handler, so Section B's timeout
//     never leaks into another scene's input behavior.
//
// Default value is `true` (enabled). No scene wiring required for any other
// scene to keep working — the gate is invisible unless something flips it.
public static class Phase1SessionInputGate
{
    public static bool IsEnabled { get; private set; } = true;

    public static void Disable()
    {
        IsEnabled = false;
    }

    public static void Enable()
    {
        IsEnabled = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void RegisterSceneResetHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        IsEnabled = true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        IsEnabled = true;
    }
}
