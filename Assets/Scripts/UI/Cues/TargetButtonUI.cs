using UnityEngine;
using TMPro;
using UI.Cues;
using UI.Cues.WarningSystem;

public class TargetButtonUI : MonoBehaviour
{
    [SerializeField] private CueSkipGuard cueSkipGuard;
    [SerializeField] private TMP_Text buttonText;
    [SerializeField] private CueController cueController;

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
    private const int MaxTargetIndex = 10;

    /// <summary>
    /// Returns the current target word in lowercase, e.g. "coffee".
    /// </summary>
    public string CurrentTargetWord =>
        currentTargetIndex < TargetWords.Length ? TargetWords[currentTargetIndex] : "";

    void Start()
    {
        UpdateButtonLabel();
    }

    public void OnTargetClicked()
    {
        currentTargetIndex++;
        if (currentTargetIndex > MaxTargetIndex)
            currentTargetIndex = 1;

        UpdateButtonLabel();

        if (cueController != null)
        {
            //cueController.SetCurrentScriptByNumber(currentTargetIndex);
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
            buttonText.text = "Target " + currentTargetIndex;
    }
}