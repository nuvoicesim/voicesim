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

        // 确保一开始加载的是 Target 1
        if (cueController != null)
        {
            cueController.SetCurrentScriptByNumber(currentTargetIndex);
        }
        else
        {
            Debug.LogError("TargetButtonUI: cueController not assigned.");
        }
    }

    public int GetCurrentTargetIndex()
    {
        return currentTargetIndex;
    }

    public void OnTargetClicked()
    {
        // 先推进到下一个 target
        currentTargetIndex++;
        if (currentTargetIndex > MaxTargetIndex)
            currentTargetIndex = 1;

        UpdateButtonLabel();

        // 切换到新的 target word
        if (cueController != null)
        {
            cueController.SetCurrentScriptByNumber(currentTargetIndex);
            cueController.ResetCueing();
        }
        else
        {
            Debug.LogError("TargetButtonUI: cueController not assigned.");
        }

        // warning system 立即恢复正常
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