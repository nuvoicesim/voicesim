using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UI.Cues;
using UI.Cues.WarningSystem;

// Drop-in replacement for TargetButtonUI in the SimuCase scene.
// Script numbers map to target_words.json entries 11-15 (green→december).
public class SimuCaseTargetButtonUI : MonoBehaviour
{
    [SerializeField] private CueSkipGuard cueSkipGuard;
    [SerializeField] private TMP_Text buttonText;
    [SerializeField] private CueController cueController;

    [Header("Scene Config")]
    [SerializeField] private int startingScriptNumber = 11;
    [SerializeField] private string[] targetWords = { "green", "sweet", "blue", "dogs", "december" };

    private int currentTargetIndex = 0;
    private int ScriptNumOffset => startingScriptNumber;
    private int MaxTargetIndex => targetWords.Length - 1;
    private Button targetButton;

    public string CurrentTargetWord =>
        currentTargetIndex < targetWords.Length ? targetWords[currentTargetIndex] : "";

    private void Awake()
    {
        targetButton = GetComponent<Button>();
    }

    private void Start()
    {
        if (targetButton != null)
            targetButton.onClick.AddListener(OnTargetClicked);

        UpdateButtonLabel();
        UpdateButtonInteractivity();
    }

    public void OnTargetClicked()
    {
        if (currentTargetIndex >= MaxTargetIndex)
        {
            Debug.Log($"Already at final target: {currentTargetIndex} ({CurrentTargetWord})");
            return;
        }

        currentTargetIndex++;
        UpdateButtonLabel();
        UpdateButtonInteractivity();

        if (cueController != null)
        {
            cueController.SetCurrentScript(currentTargetIndex + ScriptNumOffset);
            cueController.ResetCueing();
        }
        else
        {
            Debug.LogError("SimuCaseTargetButtonUI: cueController not assigned.");
        }

        if (cueSkipGuard != null)
        {
            cueSkipGuard.ResetCueing();
            cueSkipGuard.PauseWarnings(0f);
        }

        GetComponent<MarkCorrectUI>()?.OnMarkCorrectClicked();

        Debug.Log($"Advanced to: {currentTargetIndex} ({CurrentTargetWord})");
    }

    private void UpdateButtonLabel()
    {
        if (buttonText == null) return;
        buttonText.text = currentTargetIndex >= MaxTargetIndex
            ? $"Final Target ({currentTargetIndex + 1}/{targetWords.Length})"
            : $"Next Target ({currentTargetIndex + 1}/{targetWords.Length})";
    }

    private void UpdateButtonInteractivity()
    {
        if (targetButton != null)
            targetButton.interactable = currentTargetIndex < MaxTargetIndex;
    }
}
