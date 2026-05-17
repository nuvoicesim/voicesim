using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Phase 1 Section B (Word Fluency) checklist manager.
//
// Unlike Sections C/D which iterate per-item ScoreRow toggles, Section B is
// task-level: ONE student-entered numeric Word Fluency score (0-20). At Finish
// time the manager finalizes a single synthetic task item into
// StudyTaskResultBuffer carrying that score, then triggers the existing Phase 1
// clipboard-report flow (CameraClipboardController.TriggerClipboardView →
// ScoreManager.SubmitEvaluation → unified rubricAssessment with
// assessmentGranularity = "task_level").
//
// No request-side or response-side schema changes are introduced; the synthetic
// task item travels through studyTaskContext.items[] exactly like Sections C/D
// items. Backend distinguishes Section B by taskContext.taskType = "word_fluency".
public class Phase1SectionBChecklistManager : MonoBehaviour
{
    private const int MinValidScore = 0;
    private const int MaxValidScore = 20;
    private const int SyntheticScriptNumber = 26;

    [Header("References")]
    [SerializeField] private TMP_InputField wordFluencyScoreInput;
    [SerializeField] private Button finishButton;
    [SerializeField] private GameObject checkListPanel;
    [SerializeField] private Button checklistIconButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private bool startAsIcon = true;

    [Header("On Finish")]
    public UnityEvent onFinish;
    [SerializeField] private Button backButton;
    [SerializeField] private string scenarioSelectSceneName = "ScenarioSelect";

    private CameraClipboardController cameraClipboardController;
    private int? validatedScore;

    public void ShowPanel()
    {
        if (checkListPanel != null) checkListPanel.SetActive(true);
        if (checklistIconButton != null) checklistIconButton.gameObject.SetActive(false);
    }

    public void HidePanel()
    {
        if (checkListPanel != null) checkListPanel.SetActive(false);
        if (checklistIconButton != null) checklistIconButton.gameObject.SetActive(true);
    }

    private void Start()
    {
        cameraClipboardController = FindObjectOfType<CameraClipboardController>();

        if (wordFluencyScoreInput != null)
        {
            wordFluencyScoreInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            wordFluencyScoreInput.characterLimit = 2;
            wordFluencyScoreInput.onValueChanged.AddListener(OnScoreInputValueChanged);
            wordFluencyScoreInput.onEndEdit.AddListener(OnScoreInputEndEdit);
        }

        if (checklistIconButton != null) checklistIconButton.onClick.AddListener(ShowPanel);
        if (closeButton != null) closeButton.onClick.AddListener(HidePanel);
        if (finishButton != null) finishButton.onClick.AddListener(OnFinishClicked);
        if (backButton != null)
        {
            backButton.gameObject.SetActive(false);
            backButton.onClick.AddListener(() => SceneManager.LoadScene(scenarioSelectSceneName));
        }

        if (startAsIcon) HidePanel(); else ShowPanel();

        ResetStudyResultBufferForSection();
        RefreshFinishButton();
    }

    private void OnScoreInputValueChanged(string raw)
    {
        validatedScore = TryParseAndClamp(raw, allowEmpty: true);
        RefreshFinishButton();
    }

    private void OnScoreInputEndEdit(string raw)
    {
        int? parsed = TryParseAndClamp(raw, allowEmpty: true);
        validatedScore = parsed;
        if (wordFluencyScoreInput != null && parsed.HasValue)
            wordFluencyScoreInput.text = parsed.Value.ToString();
        RefreshFinishButton();
    }

    private static int? TryParseAndClamp(string raw, bool allowEmpty)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return allowEmpty ? (int?)null : MinValidScore;
        if (!int.TryParse(raw.Trim(), out int parsed))
            return null;
        return Mathf.Clamp(parsed, MinValidScore, MaxValidScore);
    }

    private void RefreshFinishButton()
    {
        if (finishButton == null) return;
        bool valid = validatedScore.HasValue;
        finishButton.interactable = valid;
        ColorBlock colors = finishButton.colors;
        colors.normalColor = valid ? Color.green : Color.gray;
        colors.highlightedColor = valid ? new Color(0f, 0.8f, 0f) : Color.gray;
        finishButton.colors = colors;
    }

    private void ResetStudyResultBufferForSection()
    {
        if (TryBuildSyntheticMetadata(out StudyItemMetadata metadata))
            StudyTaskResultBuffer.ResetForTask(metadata);
    }

    private static bool TryBuildSyntheticMetadata(out StudyItemMetadata metadata)
    {
        return Phase1StudyItemMetadataResolver.TryResolve(
            SyntheticScriptNumber,
            zeroBasedItemIndex: 0,
            fallbackTargetAnswer: null,
            currentScript: null,
            sceneName: SceneManager.GetActiveScene().name,
            out metadata);
    }

    private void OnFinishClicked()
    {
        if (!validatedScore.HasValue)
            return;

        TryFinalizeSyntheticItem(validatedScore.Value);
        StudyTaskResultPayload payload = StudyTaskResultBuffer.BuildPayload(StudyDataDefaults.StatusCompleted);
        StudyTaskResultSubmissionHook.SubmitStudyTaskResults(payload);

        HidePanel();
        onFinish?.Invoke();
        cameraClipboardController?.TriggerClipboardView();
        if (backButton != null) backButton.gameObject.SetActive(true);
    }

    private static bool TryFinalizeSyntheticItem(int score)
    {
        if (!TryBuildSyntheticMetadata(out StudyItemMetadata metadata))
            return false;
        return StudyTaskResultBuffer.TryFinalizeCurrentItem(metadata, score, out _);
    }
}
