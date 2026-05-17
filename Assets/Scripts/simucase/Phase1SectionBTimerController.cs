using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

// Phase 1 Section B (Word Fluency) timer controller.
//
// Behavior:
//   - On Start(), begins a 60-second unscaled countdown.
//   - Updates the assigned TMP_Text every frame as "MM:SS" (or "Ss" if you
//     prefer; the format method is the obvious place to tweak later).
//   - At 0 seconds, fires SessionEnded events ONCE:
//       * IsSessionActive becomes false (controller exposes a public flag for
//         other components to gate further interaction).
//       * sessionEndedBanner is set active (a pre-authored "Time is up /
//         Session ended" panel in the scene).
//       * OnSessionEnded UnityEvent is invoked (wire it in the Inspector to
//         open the checklist panel and/or disable speaking input).
//
// Safest minimal session-end strategy: this controller does not tear down
// components. It exposes a flag and an event; downstream components (speaking
// input, checklist) consume those signals and decide their own behavior.
public class Phase1SectionBTimerController : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private float sessionSeconds = 60f;
    [SerializeField] private bool autoStartOnSceneLoad = true;

    [Header("Display")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private GameObject sessionEndedBanner;

    [Header("Auto-Open Checklist")]
    [Tooltip("Optional. When set, the checklist manager's ShowPanel() is called directly at session end so auto-open works without UnityEvent wiring.")]
    [SerializeField] private Phase1SectionBChecklistManager checklistManager;

    [Header("Speech Input Gating")]
    [Tooltip("When true, calls Phase1SessionInputGate.Disable() at session end so STT controllers reject new R-key recording starts. In-flight recordings settle naturally.")]
    [SerializeField] private bool disableSpeechInputOnSessionEnd = true;

    [Header("On Session End")]
    public UnityEvent onSessionEnded;

    private float remainingSeconds;
    private bool sessionActive;
    private bool sessionEndedFired;

    public bool IsSessionActive => sessionActive;
    public float RemainingSeconds => remainingSeconds;
    public event Action SessionEnded;

    private void Awake()
    {
        remainingSeconds = Mathf.Max(0f, sessionSeconds);
        sessionActive = false;
        sessionEndedFired = false;

        if (sessionEndedBanner != null)
            sessionEndedBanner.SetActive(false);

        UpdateTimerLabel();
    }

    private void Start()
    {
        if (autoStartOnSceneLoad)
            BeginSession();
    }

    public void BeginSession()
    {
        if (sessionActive || sessionEndedFired)
            return;

        remainingSeconds = Mathf.Max(0f, sessionSeconds);
        sessionActive = true;
        sessionEndedFired = false;

        if (sessionEndedBanner != null)
            sessionEndedBanner.SetActive(false);

        UpdateTimerLabel();
    }

    private void Update()
    {
        if (!sessionActive)
            return;

        remainingSeconds -= Time.unscaledDeltaTime;
        if (remainingSeconds <= 0f)
        {
            remainingSeconds = 0f;
            UpdateTimerLabel();
            EndSession();
            return;
        }

        UpdateTimerLabel();
    }

    private void EndSession()
    {
        if (sessionEndedFired)
            return;
        sessionEndedFired = true;
        sessionActive = false;

        if (sessionEndedBanner != null)
            sessionEndedBanner.SetActive(true);

        if (disableSpeechInputOnSessionEnd)
            Phase1SessionInputGate.Disable();

        if (checklistManager == null)
            checklistManager = FindObjectOfType<Phase1SectionBChecklistManager>();
        if (checklistManager != null)
            checklistManager.ShowPanel();

        SessionEnded?.Invoke();
        onSessionEnded?.Invoke();
    }

    private void UpdateTimerLabel()
    {
        if (timerText == null)
            return;
        int total = Mathf.CeilToInt(remainingSeconds);
        int minutes = total / 60;
        int seconds = total % 60;
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}
