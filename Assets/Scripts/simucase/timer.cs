using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SimuCaseTimer : MonoBehaviour
{
    [SerializeField] private float countdownSeconds = 20f;
    [SerializeField] private bool autoStartOnSceneLoad;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private Button startButton;
    [SerializeField] private Button stopButton;

    private float remainingSeconds;
    private bool isRunning;
    private TMP_Text startButtonLabel;

    private void Awake()
    {
        remainingSeconds = Mathf.Max(0f, countdownSeconds);
        isRunning = false;
        UpdateTimerLabel();
    }

    private void Start()
    {
        if (startButton != null)
        {
            startButtonLabel = startButton.GetComponentInChildren<TMP_Text>(true);
            startButton.onClick.AddListener(OnStartButtonClicked);
        }

        if (stopButton != null)
            stopButton.onClick.AddListener(StopTimer);

        if (autoStartOnSceneLoad)
            StartTimer();
    }

    private void OnStartButtonClicked()
    {
        StartTimer();
        if (startButtonLabel != null)
            startButtonLabel.text = "Restart";
    }

    private void OnDestroy()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(OnStartButtonClicked);
        if (stopButton != null)
            stopButton.onClick.RemoveListener(StopTimer);
    }

    /// <summary>Resets to the full duration and begins counting down.</summary>
    public void StartTimer()
    {
        remainingSeconds = Mathf.Max(0f, countdownSeconds);
        isRunning = true;
        UpdateTimerLabel();
    }

    /// <summary>Pauses the countdown (display stays at the current value).</summary>
    public void StopTimer()
    {
        isRunning = false;
        UpdateTimerLabel();
    }

    private void Update()
    {
        if (!isRunning)
            return;

        remainingSeconds -= Time.unscaledDeltaTime;
        if (remainingSeconds <= 0f)
        {
            remainingSeconds = 0f;
            isRunning = false;
            UpdateTimerLabel();
            return;
        }

        UpdateTimerLabel();
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
