using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using UI.Cues;
using UI.Cues.WarningSystem;

public class TargetButtonUI : MonoBehaviour
{
    [SerializeField] private CueSkipGuard cueSkipGuard;
    [SerializeField] private TMP_Text buttonText;
    [SerializeField] private CueController cueController;
    [SerializeField] private int startingTargetIndex = 1;
    [SerializeField] private int endingTargetIndex = -1;

    // Must match the order of scripts in CueController (1-indexed)
    private static readonly string[] TargetWords =
    {
        "",            // index 0 unused
        "coffee",      // 1
        "daughter",    // 2
        "shower",      // 3
        "sandwich",    // 4
        "car",         // 5
        "shirt",       // 6
        "sunny",       // 7
        "television",  // 8
        "pills",       // 9
        "tired",       // 10
    };

    private int currentTargetIndex = 1;
    private int MaxAvailableTargetIndex => TargetWords.Length - 1;
    private int MinTargetIndex => Mathf.Clamp(startingTargetIndex, 1, MaxAvailableTargetIndex);
    private int MaxTargetIndex => Mathf.Clamp(endingTargetIndex > 0 ? endingTargetIndex : MaxAvailableTargetIndex, MinTargetIndex, MaxAvailableTargetIndex);
    private Button targetButton;

    public event Action<int> BeforeTargetAdvanced;

    /// <summary>
    /// Returns the current target word in lowercase, e.g. "coffee".
    /// </summary>
    public string CurrentTargetWord =>
        currentTargetIndex < TargetWords.Length ? TargetWords[currentTargetIndex] : "";

    public int CurrentTargetIndex => currentTargetIndex;
    public int CurrentTargetOrdinal => Mathf.Max(1, currentTargetIndex - MinTargetIndex + 1);
    public int TargetCount => Mathf.Max(1, MaxTargetIndex - MinTargetIndex + 1);

    void Awake()
    {
        targetButton = GetComponent<Button>();
    }

    void Start()
    {
        currentTargetIndex = Mathf.Clamp(currentTargetIndex, MinTargetIndex, MaxTargetIndex);
        UpdateButtonLabel();
        UpdateButtonInteractivity();
    }

    public void ConfigureTargetRange(int startIndex, int endIndex)
    {
        startingTargetIndex = Mathf.Clamp(startIndex, 1, MaxAvailableTargetIndex);
        endingTargetIndex = Mathf.Clamp(endIndex, startingTargetIndex, MaxAvailableTargetIndex);
        currentTargetIndex = MinTargetIndex;

        if (cueController != null)
        {
            cueController.scriptNum = currentTargetIndex;
            if (cueController.TryGetScriptEntry(currentTargetIndex, out _))
            {
                cueController.SetCurrentScript(currentTargetIndex);
                cueController.ResetCueing();
            }
        }

        UpdateButtonLabel();
        UpdateButtonInteractivity();
    }

    public void OnTargetClicked()
    {
        if (currentTargetIndex >= MaxTargetIndex)
        {
            Debug.Log($"Already at final target: Target {currentTargetIndex} ({CurrentTargetWord})");
            return;
        }

        BeforeTargetAdvanced?.Invoke(currentTargetIndex);

        currentTargetIndex++;

        UpdateButtonLabel();
        UpdateButtonInteractivity();

        if (cueController != null)
        {
            cueController.scriptNum = currentTargetIndex;
            cueController.SetCurrentScript(currentTargetIndex);
            cueController.ResetCueing();
        }
        else
        {
            Debug.LogError("TargetButtonUI: cueController not assigned.");
        }

        if (cueSkipGuard != null)
        {
            cueSkipGuard.ResetCueing();
            cueSkipGuard.PauseWarnings(0f);
        }
        else
        {
            Debug.LogWarning("TargetButtonUI: cueSkipGuard not assigned.");
        }

        Debug.Log($"Advanced to next target: Target {currentTargetIndex} ({CurrentTargetWord})");
    }

    private void UpdateButtonLabel()
    {
        if (buttonText != null)
        {
            if (currentTargetIndex >= MaxTargetIndex)
                buttonText.text = $"Final Target ({CurrentTargetOrdinal}/{TargetCount})";
            else
                buttonText.text = $"Next Target ({CurrentTargetOrdinal}/{TargetCount})";
        }
    }

    private void UpdateButtonInteractivity()
    {
        if (targetButton != null)
            targetButton.interactable = currentTargetIndex < MaxTargetIndex;
    }
}
