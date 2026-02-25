using UnityEngine;
using TMPro;

public class MarkCorrectUI : MonoBehaviour
{
    [SerializeField] private CueSkipGuard cueSkipGuard;
    [SerializeField] private TMP_Text statusText; // 可选：显示提示
    [SerializeField] private float pauseSeconds = 10f;

    public void OnMarkCorrectClicked()
    {
        if (cueSkipGuard == null)
        {
            Debug.LogError("MarkCorrectUI: cueSkipGuard not assigned.");
            return;
        }

        cueSkipGuard.ResetToSemantic();
        cueSkipGuard.PauseWarnings(pauseSeconds);

        if (statusText != null)
            statusText.text = $"Marked correct ✅ (warnings paused {pauseSeconds:0}s)";
        
        Debug.Log("Marked correct: reset to Semantic + paused warnings.");
    }
}