using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Attach to any GameObject in the sectionC scene.
// contentParent = the Content object containing all QuestionItem children.
// Each QuestionItem must have a ScoreRow child with 3 Toggle children.
public class SimuCaseChecklistManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform contentParent;
    [SerializeField] private Button finishButton;
    [SerializeField] private GameObject checkListPanel;
    [SerializeField] private Button checklistIconButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private SimuCaseTargetButtonUI targetButtonUI;
    [SerializeField] private bool startAsIcon = true;

    [Header("On Finish")]
    public UnityEvent onFinish;
    [SerializeField] private string scenarioSelectSceneName = "ScenarioSelect";

    [Header("Score Capture")]
    [SerializeField] private string scoreObjectNamePrefix = "Score";

    private CameraClipboardController cameraClipboardController;

    private int questionCount;
    private bool[] answered;
    private int?[] selectedScores;

    private void Start()
    {
        cameraClipboardController = FindObjectOfType<CameraClipboardController>();
        SetupQuestions();
        ResetStudyResultBufferForSection();

        if (checklistIconButton != null) checklistIconButton.onClick.AddListener(ShowPanel);
        if (closeButton != null)         closeButton.onClick.AddListener(HidePanel);
        if (finishButton != null)        finishButton.onClick.AddListener(OnFinishClicked);

        if (startAsIcon) HidePanel(); else ShowPanel();
        RefreshFinishButton();
    }

    private void SetupQuestions()
    {
        questionCount = contentParent.childCount;
        answered = new bool[questionCount];
        selectedScores = new int?[questionCount];

        for (int i = 0; i < questionCount; i++)
        {
            Transform item = contentParent.GetChild(i);
            int capturedIndex = i;

            // Add ToggleGroup if not already present (makes toggles single-select)
            ToggleGroup group = item.GetComponentInChildren<ToggleGroup>();
            if (group == null)
            {
                Transform scoreRow = item.Find("ScoreRow");
                group = (scoreRow != null ? scoreRow : item).gameObject.AddComponent<ToggleGroup>();
            }
            group.allowSwitchOff = true;

            foreach (Toggle toggle in item.GetComponentsInChildren<Toggle>())
            {
                toggle.group = group;
                toggle.isOn = false;
                toggle.onValueChanged.RemoveAllListeners();
                toggle.onValueChanged.AddListener(_ =>
                {
                    answered[capturedIndex] = group.AnyTogglesOn();
                    selectedScores[capturedIndex] = ResolveSelectedScore(group);
                    RefreshFinishButton();
                });
            }
        }
    }

    // ── Panel ────────────────────────────────────────────────────────────────

    public void ShowPanel()
    {
        if (checkListPanel != null)      checkListPanel.SetActive(true);
        if (checklistIconButton != null) checklistIconButton.gameObject.SetActive(false);
    }

    public void HidePanel()
    {
        if (checkListPanel != null)      checkListPanel.SetActive(false);
        if (checklistIconButton != null) checklistIconButton.gameObject.SetActive(true);
    }

    // ── Finish ───────────────────────────────────────────────────────────────

    private void OnFinishClicked()
    {
        TryFinalizeCurrentStudyItem();
        StudyTaskResultBuffer.RefreshSelectedScores(this);
        StudyTaskResultPayload payload = StudyTaskResultBuffer.BuildPayload(StudyDataDefaults.StatusCompleted);
        StudyTaskResultSubmissionHook.SubmitStudyTaskResults(payload);

        HidePanel();
        onFinish?.Invoke();
        cameraClipboardController?.TriggerClipboardView();
        ResetAll();
        SceneManager.LoadScene(scenarioSelectSceneName);
    }

    private void RefreshFinishButton()
    {
        if (finishButton == null) return;

        bool allAnswered = questionCount > 0;
        foreach (bool a in answered)
            if (!a) { allAnswered = false; break; }

        finishButton.interactable = allAnswered;
        ColorBlock cb = finishButton.colors;
        cb.normalColor      = allAnswered ? Color.green              : Color.gray;
        cb.highlightedColor = allAnswered ? new Color(0f, 0.8f, 0f) : Color.gray;
        finishButton.colors = cb;
    }

    private void ResetAll()
    {
        for (int i = 0; i < questionCount; i++)
        {
            answered[i] = false;
            selectedScores[i] = null;
            ToggleGroup group = contentParent.GetChild(i).GetComponentInChildren<ToggleGroup>();
            if (group != null) group.SetAllTogglesOff();
        }
        RefreshFinishButton();
    }

    public bool TryGetSelectedScore(int rowIndex, out int score)
    {
        score = 0;
        if (selectedScores == null || rowIndex < 0 || rowIndex >= selectedScores.Length)
            return false;

        if (!selectedScores[rowIndex].HasValue)
            return false;

        score = selectedScores[rowIndex].Value;
        return true;
    }

    public int? GetSelectedScore(int rowIndex)
    {
        return TryGetSelectedScore(rowIndex, out int score) ? score : (int?)null;
    }

    public bool TryGetSelectedScoreForCurrentItem(SimuCaseTargetButtonUI targetButtonUI, out int score)
    {
        score = 0;
        if (targetButtonUI == null)
            return false;

        return TryGetSelectedScore(targetButtonUI.CurrentTargetIndex, out score);
    }

    public int?[] GetSelectedScoresSnapshot()
    {
        if (selectedScores == null)
            return new int?[0];

        int?[] snapshot = new int?[selectedScores.Length];
        selectedScores.CopyTo(snapshot, 0);
        return snapshot;
    }

    private void ResetStudyResultBufferForSection()
    {
        SimuCaseTargetButtonUI targetButton = TryResolveTargetButtonUI();
        if (targetButton == null || !targetButton.TryGetCurrentStudyItemMetadata(out StudyItemMetadata metadata))
            return;

        StudyTaskResultBuffer.ResetForTask(metadata);
    }

    private bool TryFinalizeCurrentStudyItem()
    {
        SimuCaseTargetButtonUI targetButton = TryResolveTargetButtonUI();
        if (targetButton == null || !targetButton.TryGetCurrentStudyItemMetadata(out StudyItemMetadata metadata))
            return false;

        int? selectedScore = GetSelectedScore(targetButton.CurrentTargetIndex);
        return StudyTaskResultBuffer.TryFinalizeCurrentItem(metadata, selectedScore, out _);
    }

    private SimuCaseTargetButtonUI TryResolveTargetButtonUI()
    {
        if (targetButtonUI != null && targetButtonUI.isActiveAndEnabled)
            return targetButtonUI;

        targetButtonUI = FindObjectOfType<SimuCaseTargetButtonUI>();
        return targetButtonUI;
    }

    private int? ResolveSelectedScore(ToggleGroup group)
    {
        if (group == null)
            return null;

        foreach (Toggle activeToggle in group.ActiveToggles())
        {
            if (TryResolveScoreValue(activeToggle, out int score))
                return score;
        }

        return null;
    }

    private bool TryResolveScoreValue(Toggle toggle, out int score)
    {
        score = 0;
        if (toggle == null)
            return false;

        string objectName = toggle.gameObject != null ? toggle.gameObject.name : "";
        if (TryParseScoreText(objectName, out score))
            return true;

        TMP_Text label = toggle.GetComponentInChildren<TMP_Text>(true);
        if (label != null && TryParseScoreText(label.text, out score))
            return true;

        Debug.LogWarning($"SimuCaseChecklistManager: Could not resolve numeric score value for toggle '{objectName}'.");
        return false;
    }

    private bool TryParseScoreText(string text, out int score)
    {
        score = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string trimmed = text.Trim();
        if (!string.IsNullOrWhiteSpace(scoreObjectNamePrefix) &&
            trimmed.StartsWith(scoreObjectNamePrefix, System.StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(scoreObjectNamePrefix.Length).Trim();
        }

        if (int.TryParse(trimmed, out score))
            return true;

        int end = text.Length - 1;
        while (end >= 0 && char.IsWhiteSpace(text[end]))
            end--;

        int start = end;
        while (start >= 0 && char.IsDigit(text[start]))
            start--;

        if (start == end)
            return false;

        if (start >= 0 && text[start] == '-')
            start--;

        string trailingNumber = text.Substring(start + 1, end - start);
        return int.TryParse(trailingNumber, out score);
    }
}
