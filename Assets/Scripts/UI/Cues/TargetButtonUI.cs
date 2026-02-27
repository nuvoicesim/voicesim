using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UI.Cues.WarningSystem;

public class TargetButtonUI : MonoBehaviour
{
    [SerializeField] private CueSkipGuard cueSkipGuard;
    [SerializeField] private TMP_Text buttonText;
    [SerializeField] private Image buttonImage;

    [SerializeField] private Color normalColor = new Color(0.85f, 0.85f, 0.85f);
    [SerializeField] private Color confirmedColor = new Color(0.4f, 0.8f, 0.4f);

    private bool confirmed = false;

    void Start()
    {
        SetNormalState();
    }

    public void OnTargetClicked()
    {
        if (!confirmed)
        {
            // ===== 切换到 Confirmed =====
            confirmed = true;

            if (buttonText != null)
                buttonText.text = "Target Confirmed";

            if (buttonImage != null)
                buttonImage.color = confirmedColor;

            if (cueSkipGuard != null)
                cueSkipGuard.OnTargetSuccess();

            Debug.Log("Target confirmed. Warnings paused.");
        }
        else
        {
            // ===== 切换回 Normal =====
            confirmed = false;

            if (buttonText != null)
                buttonText.text = "Target";

            if (buttonImage != null)
                buttonImage.color = normalColor;

            if (cueSkipGuard != null)
            {
                cueSkipGuard.ResetCueing();
                cueSkipGuard.PauseWarnings(0f); // 立刻恢复
            }

            Debug.Log("Target reset. Warnings resumed.");
        }
    }

    private void SetNormalState()
    {
        confirmed = false;

        if (buttonText != null)
            buttonText.text = "Target";

        if (buttonImage != null)
            buttonImage.color = normalColor;
    }
}