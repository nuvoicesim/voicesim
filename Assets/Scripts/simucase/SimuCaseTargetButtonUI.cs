using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UI.Cues;
using UI.Cues.WarningSystem;

// Drop-in replacement for TargetButtonUI in the SimuCase scene.
// Script numbers map to target_words.json entries 11-15 (green→december).
public class SimuCaseTargetButtonUI : MonoBehaviour, IStudyItemMetadataProvider
{
    [SerializeField] private CueSkipGuard cueSkipGuard;
    [SerializeField] private TMP_Text buttonText;
    [SerializeField] private CueController cueController;
    [SerializeField] private SimuCaseChecklistManager checklistManager;

    [Header("Scene Config")]
    [SerializeField] private int startingScriptNumber = 11;
    [SerializeField] private string[] targetWords = { "green", "sweet", "blue", "dogs", "december" };

    private int currentTargetIndex = 0;
    private int ScriptNumOffset => startingScriptNumber;
    private int MaxTargetIndex => TargetCount - 1;
    private Button targetButton;

    public string CurrentTargetWord =>
        targetWords != null && currentTargetIndex < targetWords.Length ? targetWords[currentTargetIndex] : "";

    public int CurrentTargetIndex => currentTargetIndex;
    public int CurrentItemOrdinal => currentTargetIndex + 1;
    public int StartingScriptNumber => startingScriptNumber;
    public int CurrentScriptNumber => currentTargetIndex + ScriptNumOffset;
    public int TargetCount => targetWords != null ? targetWords.Length : 0;

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

        TryFinalizeCurrentStudyItem();

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

    public bool TryGetCurrentStudyItemMetadata(out StudyItemMetadata metadata)
    {
        return Phase1StudyItemMetadataResolver.TryResolveCurrentItem(this, cueController, out metadata);
    }

    public StudyItemMetadata GetCurrentStudyItemMetadata()
    {
        TryGetCurrentStudyItemMetadata(out StudyItemMetadata metadata);
        return metadata;
    }

    private bool TryFinalizeCurrentStudyItem()
    {
        if (!TryGetCurrentStudyItemMetadata(out StudyItemMetadata metadata))
            return false;

        int? selectedScore = null;
        SimuCaseChecklistManager checklist = TryResolveChecklistManager();
        if (checklist != null)
            selectedScore = checklist.GetSelectedScore(CurrentTargetIndex);

        return StudyTaskResultBuffer.TryFinalizeCurrentItem(metadata, selectedScore, out _);
    }

    private SimuCaseChecklistManager TryResolveChecklistManager()
    {
        if (checklistManager != null && checklistManager.isActiveAndEnabled)
            return checklistManager;

        checklistManager = FindObjectOfType<SimuCaseChecklistManager>();
        return checklistManager;
    }

    private void UpdateButtonLabel()
    {
        if (buttonText == null) return;
        buttonText.text = currentTargetIndex >= MaxTargetIndex
            ? $"Final Target ({currentTargetIndex + 1}/{TargetCount})"
            : $"Next Target ({currentTargetIndex + 1}/{TargetCount})";
    }

    private void UpdateButtonInteractivity()
    {
        if (targetButton != null)
            targetButton.interactable = currentTargetIndex < MaxTargetIndex;
    }
}
