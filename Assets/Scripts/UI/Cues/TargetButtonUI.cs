using UnityEngine;
using TMPro;
using UI.Cues;
using UI.Cues.WarningSystem;

public class TargetButtonUI : MonoBehaviour
{
    [SerializeField] private CueSkipGuard cueSkipGuard;
    [SerializeField] private TMP_Text buttonText;
    [SerializeField] private CueController cueController;

    private int currentTargetIndex = 1;
    private const int MaxTargetIndex = 10;

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
            cueController.SetCurrentScriptByNumber(currentTargetIndex);
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

        Debug.Log("Advanced to next target: Target " + currentTargetIndex);
    }

    private void UpdateButtonLabel()
    {
        if (buttonText != null)
            buttonText.text = "Target " + currentTargetIndex;
    }
}