using UnityEngine;
using UnityEngine.UI;
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
    private int MaxTargetIndex => TargetWords.Length - 1;
    private Button targetButton;

    /// <summary>
    /// Returns the current target word in lowercase, e.g. "coffee".
    /// </summary>
    public string CurrentTargetWord =>
        currentTargetIndex < TargetWords.Length ? TargetWords[currentTargetIndex] : "";

    void Awake()
    {
        targetButton = GetComponent<Button>();
    }

    void Start()
    {
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

        currentTargetIndex++;

        UpdateButtonLabel();
        UpdateButtonInteractivity();

        if (cueController != null)
        {
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
                buttonText.text = $"Final Target ({currentTargetIndex}/{MaxTargetIndex})";
            else
                buttonText.text = $"Next Target ({currentTargetIndex}/{MaxTargetIndex})";
        }
    }

    private void UpdateButtonInteractivity()
    {
        if (targetButton != null)
            targetButton.interactable = currentTargetIndex < MaxTargetIndex;
    }
}
