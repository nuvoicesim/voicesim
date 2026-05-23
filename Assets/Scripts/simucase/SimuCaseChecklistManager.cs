using System.Collections.Generic;
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
    // Post-Finish manual return to the selector. The clipboard/report view
    // animation, /llm-scoring submission, rubric rendering, and task-progress
    // PUT all happen inside the TriggerClipboardView() coroutine; the section
    // scene must therefore remain loaded until the student has read the
    // report. The Back button is hidden during the task and revealed at the
    // end of OnFinishClicked(); clicking it loads scenarioSelectSceneName.
    // Falls back to FindObjectsOfType<Button>() by name if the Inspector
    // slot is unwired, mirroring the auto-resolve pattern used for the
    // Section B optional fields.
    [SerializeField] private Button backButton;
    [SerializeField] private string backButtonObjectName = "BackButton";

    [Header("Score Capture")]
    [SerializeField] private string scoreObjectNamePrefix = "Score";

    [Header("Section B (optional)")]
    [SerializeField] private TMP_InputField sectionBNotesInput;
    [SerializeField] private Toggle sectionBCompletionToggle;

    private CameraClipboardController cameraClipboardController;

    private int questionCount;
    private bool[] answered;
    private int?[] selectedScores;
    private readonly List<Transform> questionItemRows = new List<Transform>();

    private void Start()
    {
        cameraClipboardController = FindObjectOfType<CameraClipboardController>();
        SetupQuestions();
        ResetStudyResultBufferForSection();

        TryAutoResolveSectionBOptionalFields();
        TryAutoResolveBackButton();

        if (checklistIconButton != null) checklistIconButton.onClick.AddListener(ShowPanel);
        if (closeButton != null)         closeButton.onClick.AddListener(HidePanel);
        if (finishButton != null)        finishButton.onClick.AddListener(OnFinishClicked);

        if (backButton != null)
        {
            backButton.gameObject.SetActive(false);
            backButton.onClick.AddListener(() => SceneManager.LoadScene(scenarioSelectSceneName));
        }

        if (startAsIcon) HidePanel(); else ShowPanel();
        RefreshFinishButton();
    }

    private void SetupQuestions()
    {
        questionItemRows.Clear();
        if (contentParent != null)
        {
            for (int i = 0; i < contentParent.childCount; i++)
            {
                Transform item = contentParent.GetChild(i);
                if (item.GetComponentsInChildren<Toggle>(true).Length > 0)
                    questionItemRows.Add(item);
            }
        }

        questionCount = questionItemRows.Count;
        answered = new bool[questionCount];
        selectedScores = new int?[questionCount];

        for (int i = 0; i < questionCount; i++)
        {
            Transform item = questionItemRows[i];
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

        // Phase 1 (May 19): rewrite the visible Label text on each rubric
        // score toggle so students see a short meaning hint ("3 Correct" /
        // "1 Cued" / etc.) instead of a bare digit. The numeric score value
        // is resolved from the Toggle GameObject name ("Score0".."Score3")
        // by TryResolveScoreValue, so the label rewrite is purely cosmetic
        // and never participates in score capture, /llm-scoring payload
        // construction, or task-progress PUT. Section B and unknown scenes
        // are no-ops.
        ApplyScoreLabels();
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

        MarkCurrentSectionCompleted();

        HidePanel();
        onFinish?.Invoke();
        // Kicks off the camera transition, POST /llm-scoring, rubric
        // rendering, and the task-progress PUT. Returning to the selector
        // before this coroutine completes destroys the section scene and
        // cancels the entire chain. The student returns manually via the
        // Back button revealed below.
        cameraClipboardController?.TriggerClipboardView();
        ResetAll();
        if (backButton != null) backButton.gameObject.SetActive(true);
    }

    private void MarkCurrentSectionCompleted()
    {
        // Prefer inferring from scene name (it is authoritative for the section scenes).
        // Some metadata sources in this project can report a fixed/incorrect sectionId.
        string sceneName = SceneManager.GetActiveScene().name ?? "";
        string lower = sceneName.ToLowerInvariant();
        if (lower.Contains("sectiona")) { SimuCaseSectionCompletionStore.MarkCompleted("A"); return; }
        if (lower.Contains("sectionb")) { SimuCaseSectionCompletionStore.MarkCompleted("B"); return; }
        if (lower.Contains("sectionc")) { SimuCaseSectionCompletionStore.MarkCompleted("C"); return; }
        if (lower.Contains("sectiond")) { SimuCaseSectionCompletionStore.MarkCompleted("D"); return; }

        // Fallback: use metadata if scene name is not informative.
        SimuCaseTargetButtonUI targetButton = TryResolveTargetButtonUI();
        if (targetButton != null && targetButton.TryGetCurrentStudyItemMetadata(out StudyItemMetadata metadata))
            SimuCaseSectionCompletionStore.MarkCompleted(metadata.sectionId);
    }

    private void TryAutoResolveBackButton()
    {
        // Allow per-scene Inspector wiring to win; fall back to a Button
        // named backButtonObjectName so the pilot scene works even without
        // an explicit prefab-instance override.
        if (backButton != null)
            return;

        if (string.IsNullOrWhiteSpace(backButtonObjectName))
            return;

        Button[] buttons = FindObjectsOfType<Button>(true);
        foreach (Button button in buttons)
        {
            if (button == null || button.gameObject == null)
                continue;

            if (button.gameObject.name == backButtonObjectName)
            {
                backButton = button;
                return;
            }
        }
    }

    private void TryAutoResolveSectionBOptionalFields()
    {
        // Section B swaps QuestionItems for a single notes input + checkbox.
        // These refs can be wired in the Inspector, but we also try to resolve by name
        // so the scene works even if the fields weren't manually assigned.
        if (sectionBNotesInput == null)
        {
            TMP_InputField[] inputs = FindObjectsOfType<TMP_InputField>(true);
            foreach (TMP_InputField input in inputs)
            {
                if (input != null && input.gameObject != null && input.gameObject.name == "SectionBNotesInput")
                {
                    sectionBNotesInput = input;
                    break;
                }
            }
        }

        if (sectionBCompletionToggle == null)
        {
            // Common checkbox object names in this project UI.
            Toggle[] toggles = FindObjectsOfType<Toggle>(true);
            foreach (Toggle toggle in toggles)
            {
                if (toggle == null || toggle.gameObject == null) continue;
                string n = toggle.gameObject.name;
                if (n == "Score2" || n == "Score3" || n == "CompletionToggle" || n == "Completion")
                {
                    sectionBCompletionToggle = toggle;
                    break;
                }
            }
        }
    }

    private void RefreshFinishButton()
    {
        if (finishButton == null) return;

        bool allAnswered = questionCount == 0;
        if (questionCount > 0)
        {
            allAnswered = true;
            foreach (bool a in answered)
                if (!a) { allAnswered = false; break; }
        }

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
            Transform item = questionItemRows[i];
            ToggleGroup group = item.GetComponentInChildren<ToggleGroup>();
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
        bool? completionChecked = sectionBCompletionToggle != null ? sectionBCompletionToggle.isOn : (bool?)null;
        bool finalized = StudyTaskResultBuffer.TryFinalizeCurrentItem(metadata, selectedScore, completionChecked, out StudyItemResult result);
        if (!finalized || result == null)
            return finalized;

        string notes = sectionBNotesInput != null ? sectionBNotesInput.text : null;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            result.interactionEvents.Add(new StudyInteractionEvent
            {
                eventId = null,
                sessionId = StudyRuntimeContext.SessionContext?.sessionId,
                taskId = metadata.taskId,
                sectionId = metadata.sectionId,
                itemId = metadata.itemId,
                eventType = "section_b_notes",
                timestamp = StudyDataDefaults.NowIso(),
                cueLevel = null,
                scoreValue = null,
                message = notes,
                metadataJson = null
            });
        }

        return true;
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

    // ── Score-label display (Phase 1 May 19) ─────────────────────────────────
    //
    // Visible score labels on each Score toggle are a UnityEngine.UI.Text
    // (legacy uGUI Text) component on a child GameObject named "Label", e.g.:
    //   QuestionItem → ScoreRow → Score3 → Label → UnityEngine.UI.Text
    //
    // These are NOT TextMeshPro. A prior attempt that used
    // GetComponentInChildren<TMP_Text> silently no-op'd. The implementation
    // below targets UnityEngine.UI.Text first and falls back to TMP_Text
    // only if a future scene/prefab migrates to TMP.
    //
    // The numeric score value is still resolved from the Toggle GameObject
    // name ("Score0" / "Score1" / "Score2" / "Score3") via the existing
    // TryResolveScoreValue path. Nothing in this label-rewrite block
    // touches Toggle.isOn, Toggle.group, listeners, selectedScores, or
    // GameObject names.

    // Maps the active scene name to a section identifier ("A"/"B"/"C"/"D"),
    // mirroring the same scene-name heuristic MarkCurrentSectionCompleted
    // already trusts. Returns the empty string for non-section scenes.
    private string ResolveSectionIdFromSceneName()
    {
        string sceneName = SceneManager.GetActiveScene().name ?? "";
        string lower = sceneName.ToLowerInvariant();
        if (lower.Contains("sectiona")) return "A";
        if (lower.Contains("sectionb")) return "B";
        if (lower.Contains("sectionc")) return "C";
        if (lower.Contains("sectiond")) return "D";
        return "";
    }

    // Returns the labeled string for a given (sectionId, score) pair, or
    // null when the section has no labeled rubric (Section B) or the score
    // is outside the section's option set. Returning null at the call site
    // means "leave the existing Label text alone".
    private static string BuildScoreLabel(string sectionId, int score)
    {
        // Two-line format: number on top, short meaning word below. Words
        // were finalized after a manual visual pass — keep them exact.
        if (string.Equals(sectionId, "A", System.StringComparison.Ordinal))
        {
            switch (score)
            {
                case 3: return "3\nRight";
                case 2: return "2\nClose";
                case 1: return "1\nCue";
                case 0: return "0\nWrong";
                default: return null;
            }
        }

        if (string.Equals(sectionId, "C", System.StringComparison.Ordinal)
            || string.Equals(sectionId, "D", System.StringComparison.Ordinal))
        {
            switch (score)
            {
                case 2: return "2\nRight";
                case 1: return "1\nPartial";
                case 0: return "0\nWrong";
                default: return null;
            }
        }

        return null;
    }

    // Fixed font size shared by every rewritten Score label across all
    // rows and sections. Hardcoding a single value here is intentional:
    // legacy uGUI Best Fit was causing per-label drift (each toggle's
    // text shrank independently, producing visually mismatched sizes
    // across a single row). 15 fits the longest word ("Partial",
    // Section C/D) inside the prefab/scene-authored Label rect on two
    // lines without overflow or clipping at the sandbox resolutions
    // tested, and is consistent for Section A's 4-toggle row + the
    // 3-toggle rows in Sections C and D. Bumped from 14 → 15 after a
    // manual visual pass; nudge in 1-step increments if needed.
    private const int Phase1ScoreLabelFontSize = 15;

    private void ApplyScoreLabels()
    {
        string sectionId = ResolveSectionIdFromSceneName();
        if (string.IsNullOrEmpty(sectionId) || string.Equals(sectionId, "B", System.StringComparison.Ordinal))
            return;

        foreach (Transform item in questionItemRows)
        {
            if (item == null)
                continue;

            foreach (Toggle toggle in item.GetComponentsInChildren<Toggle>(true))
            {
                if (toggle == null)
                    continue;

                // TryResolveScoreValue resolves via the Toggle GameObject
                // name first ("Score3" / "Score2" / etc.), so the captured
                // value is independent of the Label text we're about to
                // overwrite. If we can't resolve a numeric score, skip the
                // toggle (it isn't a rubric option).
                if (!TryResolveScoreValue(toggle, out int score))
                    continue;

                string newLabel = BuildScoreLabel(sectionId, score);
                if (string.IsNullOrEmpty(newLabel))
                    continue;

                // Per the Codex finding, the visible label lives on a
                // direct child GameObject named "Label" carrying a legacy
                // UnityEngine.UI.Text. Use Transform.Find for the direct
                // child lookup; fall back to a recursive search if a future
                // prefab restructure breaks that assumption.
                Transform labelTransform = toggle.transform.Find("Label");

                UnityEngine.UI.Text legacy = null;
                if (labelTransform != null)
                    legacy = labelTransform.GetComponent<UnityEngine.UI.Text>();
                if (legacy == null)
                    legacy = toggle.GetComponentInChildren<UnityEngine.UI.Text>(true);

                if (legacy != null)
                {
                    legacy.text = newLabel;

                    // Pin a consistent font size for every rewritten
                    // Score label. Best Fit is explicitly disabled
                    // because it was shrinking each label independently
                    // — labels in the same row ended up at visually
                    // different sizes depending on word length. Center
                    // alignment keeps the two-line "digit + word"
                    // visually balanced inside the existing Label rect.
                    // Font, color, RectTransform, and the GameObject
                    // hierarchy are preserved.
                    legacy.resizeTextForBestFit = false;
                    legacy.fontSize = Phase1ScoreLabelFontSize;
                    legacy.alignment = TextAnchor.MiddleCenter;
                    continue;
                }

                // Optional TMP fallback. Not expected to fire in the
                // current sandbox (all known score Labels are legacy
                // UnityEngine.UI.Text), but keeps the helper resilient
                // if a future scene migrates a row to TextMeshPro.
                TMP_Text tmp = null;
                if (labelTransform != null)
                    tmp = labelTransform.GetComponent<TMP_Text>();
                if (tmp == null)
                    tmp = toggle.GetComponentInChildren<TMP_Text>(true);

                if (tmp != null)
                {
                    tmp.text = newLabel;
                    // Mirror the legacy path: fixed size, autosize off,
                    // centered. Keeps the two paths visually consistent
                    // if a future migration mixes Text + TMP in the
                    // same checklist.
                    tmp.enableAutoSizing = false;
                    tmp.fontSize = Phase1ScoreLabelFontSize;
                    tmp.alignment = TextAlignmentOptions.Center;
                }
            }
        }
    }
}
