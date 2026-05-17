using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StudyCompletionChecklistManager : MonoBehaviour
{
    private const float CompletionToggleMinWidth = 180f;
    private const float CompletionLabelMinWidth = 140f;
    private const float CompletionLabelLeftOffset = 50f;

    [Header("References")]
    [SerializeField] private Transform contentParent;
    [SerializeField] private Button finishButton;
    [SerializeField] private GameObject checkListPanel;
    [SerializeField] private Button checklistIconButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private MonoBehaviour studyItemMetadataProviderBehaviour;
    [SerializeField] private bool startAsIcon = true;

    [Header("Finish")]
    [SerializeField] private bool submitPayloadOnFinish = true;
    [SerializeField] private CameraClipboardController cameraClipboardController;
    [SerializeField] private Button backButton;
    [SerializeField] private string scenarioSelectSceneName = "";
    public UnityEvent onFinish;

    private readonly List<Toggle> toggles = new List<Toggle>();
    private readonly List<StudyItemMetadata> configuredMetadataItems = new List<StudyItemMetadata>();
    private readonly Dictionary<string, int> itemIdToRowIndex = new Dictionary<string, int>();
    private bool[] completed;
    private IStudyItemMetadataProvider metadataProvider;

    public int ItemCount => completed != null ? completed.Length : 0;

    private void Start()
    {
        ResolveSceneReferences();
        SetupChecklistItems();

        if (checklistIconButton != null)
            checklistIconButton.onClick.AddListener(ShowPanel);

        if (closeButton != null)
            closeButton.onClick.AddListener(HidePanel);

        if (finishButton != null)
            finishButton.onClick.AddListener(OnFinishClicked);

        if (backButton != null)
        {
            backButton.gameObject.SetActive(false);
            backButton.onClick.AddListener(ReturnToScenarioSelect);
        }

        if (startAsIcon)
            HidePanel();
        else
            ShowPanel();

        ResetStudyResultBufferForCurrentTask();
        RefreshFinishButton();
    }

    private void OnDestroy()
    {
        if (backButton != null)
            backButton.onClick.RemoveListener(ReturnToScenarioSelect);
    }

    public void ConfigureCompletionItems(IReadOnlyList<StudyItemMetadata> metadataItems, IReadOnlyList<string> displayLabels)
    {
        ResolveSceneReferences();

        configuredMetadataItems.Clear();
        itemIdToRowIndex.Clear();

        if (metadataItems != null)
        {
            for (int i = 0; i < metadataItems.Count; i++)
            {
                StudyItemMetadata metadata = metadataItems[i];
                if (metadata == null)
                    continue;

                configuredMetadataItems.Add(metadata);
                if (!string.IsNullOrWhiteSpace(metadata.itemId))
                    itemIdToRowIndex[metadata.itemId.Trim()] = i;
            }
        }

        ApplyCompletionChecklistLabels(displayLabels);
        SetupChecklistItems();
        ResetStudyResultBufferForCurrentTask();
        RefreshFinishButton();
    }

    public void ShowPanel()
    {
        if (checkListPanel != null)
            checkListPanel.SetActive(true);

        if (checklistIconButton != null)
            checklistIconButton.gameObject.SetActive(false);
    }

    public void HidePanel()
    {
        if (checkListPanel != null)
            checkListPanel.SetActive(false);

        if (checklistIconButton != null)
            checklistIconButton.gameObject.SetActive(true);
    }

    public bool TryGetCompletionState(int rowIndex, out bool isCompleted)
    {
        isCompleted = false;
        if (completed == null || rowIndex < 0 || rowIndex >= completed.Length)
            return false;

        isCompleted = completed[rowIndex];
        return true;
    }

    public bool SetCompleted(int rowIndex, bool isCompleted)
    {
        if (completed == null || rowIndex < 0 || rowIndex >= completed.Length)
            return false;

        completed[rowIndex] = isCompleted;
        if (rowIndex < toggles.Count && toggles[rowIndex] != null && toggles[rowIndex].isOn != isCompleted)
            toggles[rowIndex].isOn = isCompleted;

        RefreshFinishButton();
        return true;
    }

    private void SetupChecklistItems()
    {
        toggles.Clear();
        if (contentParent == null)
        {
            completed = new bool[0];
            return;
        }

        List<Toggle> completionToggles = ResolveCompletionToggles();
        completed = new bool[completionToggles.Count];

        for (int i = 0; i < completionToggles.Count; i++)
        {
            Toggle toggle = completionToggles[i];
            int capturedIndex = i;
            toggles.Add(toggle);

            toggle.onValueChanged.RemoveAllListeners();
            toggle.isOn = false;
            toggle.onValueChanged.AddListener(value =>
            {
                completed[capturedIndex] = value;
                RefreshFinishButton();
            });
        }
    }

    private void OnFinishClicked()
    {
        FinalizeChecklistItems();

        if (submitPayloadOnFinish)
        {
            StudyTaskResultPayload payload = StudyTaskResultBuffer.BuildPayload(StudyDataDefaults.StatusCompleted);
            StudyTaskResultSubmissionHook.SubmitStudyTaskResults(payload);
        }

        HidePanel();
        onFinish?.Invoke();

        if (cameraClipboardController != null)
            cameraClipboardController.TriggerClipboardView();

        if (backButton != null)
            backButton.gameObject.SetActive(true);
    }

    private void ResetStudyResultBufferForCurrentTask()
    {
        if (TryResolveCurrentStudyItemMetadata(out StudyItemMetadata metadata))
            StudyTaskResultBuffer.ResetForTask(metadata);
    }

    private bool TryResolveCurrentStudyItemMetadata(out StudyItemMetadata metadata)
    {
        metadata = null;
        IStudyItemMetadataProvider provider = TryResolveMetadataProvider();
        return provider != null && provider.TryGetCurrentStudyItemMetadata(out metadata);
    }

    private IStudyItemMetadataProvider TryResolveMetadataProvider()
    {
        if (IsUsableProvider(metadataProvider))
            return metadataProvider;

        metadataProvider = null;

        if (studyItemMetadataProviderBehaviour is IStudyItemMetadataProvider configuredProvider &&
            studyItemMetadataProviderBehaviour.isActiveAndEnabled)
        {
            metadataProvider = configuredProvider;
            return metadataProvider;
        }

        MonoBehaviour[] candidates = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (MonoBehaviour candidate in candidates)
        {
            if (candidate == null || candidate == this)
                continue;

            if (candidate is IStudyItemMetadataProvider provider && candidate.isActiveAndEnabled)
            {
                studyItemMetadataProviderBehaviour = candidate;
                metadataProvider = provider;
                return metadataProvider;
            }
        }

        return null;
    }

    private static bool IsUsableProvider(IStudyItemMetadataProvider provider)
    {
        if (provider == null)
            return false;

        MonoBehaviour behaviour = provider as MonoBehaviour;
        return behaviour == null || behaviour.isActiveAndEnabled;
    }

    private List<Toggle> ResolveCompletionToggles()
    {
        List<Toggle> resolved = new List<Toggle>();

        for (int i = 0; i < contentParent.childCount; i++)
        {
            Transform itemRoot = contentParent.GetChild(i);
            if (itemRoot == null || !itemRoot.gameObject.activeSelf)
                continue;

            Toggle itemToggle = ResolveCompletionToggle(itemRoot);
            if (itemToggle != null)
                resolved.Add(itemToggle);
        }

        if (resolved.Count > 0)
            return resolved;

        Toggle[] childToggles = contentParent.GetComponentsInChildren<Toggle>(true);
        foreach (Toggle toggle in childToggles)
        {
            if (toggle != null && toggle.gameObject.activeSelf)
                resolved.Add(toggle);
        }

        return resolved;
    }

    private static Toggle ResolveCompletionToggle(Transform itemRoot)
    {
        if (itemRoot == null)
            return null;

        Toggle[] childToggles = itemRoot.GetComponentsInChildren<Toggle>(true);
        foreach (Toggle toggle in childToggles)
        {
            if (toggle != null && toggle.gameObject.activeSelf)
                return toggle;
        }

        return childToggles.Length > 0 ? childToggles[0] : null;
    }

    private void ApplyCompletionChecklistLabels(IReadOnlyList<string> displayLabels)
    {
        if (contentParent == null)
            return;

        bool hasConfiguredLabels = displayLabels != null && displayLabels.Count > 0;
        for (int i = 0; i < contentParent.childCount; i++)
        {
            Transform itemRoot = contentParent.GetChild(i);
            bool hasRowLabel = hasConfiguredLabels &&
                               i < displayLabels.Count &&
                               !string.IsNullOrWhiteSpace(displayLabels[i]);

            if (hasConfiguredLabels && itemRoot != null)
                itemRoot.gameObject.SetActive(hasRowLabel);

            if (hasConfiguredLabels && !hasRowLabel)
                continue;

            string label = hasRowLabel ? displayLabels[i] : "";
            ConfigureCompletionItemRow(itemRoot, label);
        }
    }

    private static void ConfigureCompletionItemRow(Transform itemRoot, string label)
    {
        if (itemRoot == null)
            return;

        Toggle[] childToggles = itemRoot.GetComponentsInChildren<Toggle>(true);
        Toggle keptToggle = ResolveCompletionToggle(itemRoot);

        bool wroteOuterLabel = false;

        TMP_Text[] textComponents = itemRoot.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in textComponents)
        {
            if (text == null)
                continue;

            string objectName = text.gameObject.name;
            string currentText = text.text ?? "";

            if (IsTaskLabelText(text.transform, objectName, keptToggle) && !string.IsNullOrWhiteSpace(label))
            {
                text.text = label;
                wroteOuterLabel = true;
                continue;
            }

            if (currentText.Contains("Score:"))
            {
                text.text = "";
                continue;
            }
        }

        Text[] legacyTextComponents = itemRoot.GetComponentsInChildren<Text>(true);
        foreach (Text text in legacyTextComponents)
        {
            if (text == null)
                continue;

            string objectName = text.gameObject.name;
            string currentText = text.text ?? "";

            if (IsTaskLabelText(text.transform, objectName, keptToggle) && !string.IsNullOrWhiteSpace(label))
            {
                text.text = label;
                wroteOuterLabel = true;
                continue;
            }

            if (currentText.Contains("Score:"))
                text.text = "";
        }

        string toggleLabel = (!wroteOuterLabel && !string.IsNullOrWhiteSpace(label)) ? label : "Completed";

        bool keptOneToggle = false;
        foreach (Toggle toggle in childToggles)
        {
            if (toggle == null)
                continue;

            if (!keptOneToggle)
            {
                keptOneToggle = true;
                keptToggle = toggle;
                toggle.gameObject.SetActive(true);
                SetToggleLabel(toggle, toggleLabel);
            }
            else
            {
                toggle.gameObject.SetActive(false);
            }
        }

        HideResidualScoreOptionLabels(itemRoot, keptToggle);
    }

    private static void SetToggleLabel(Toggle toggle, string label)
    {
        if (toggle == null)
            return;

        ExpandCompletionToggleRect(toggle);

        TMP_Text[] tmpLabels = toggle.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in tmpLabels)
        {
            if (text == null)
                continue;

            text.text = label;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            AlignCompletionLabelRect(text.rectTransform);
        }

        Text[] legacyLabels = toggle.GetComponentsInChildren<Text>(true);
        foreach (Text text in legacyLabels)
        {
            if (text == null)
                continue;

            text.text = label;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            AlignCompletionLabelRect(text.rectTransform);
        }
    }

    private static void ExpandCompletionToggleRect(Toggle toggle)
    {
        RectTransform rectTransform = toggle.GetComponent<RectTransform>();
        if (rectTransform == null)
            return;

        if (rectTransform.rect.width < CompletionToggleMinWidth)
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, CompletionToggleMinWidth);
    }

    private static void AlignCompletionLabelRect(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return;

        Vector2 anchorMin = rectTransform.anchorMin;
        Vector2 anchorMax = rectTransform.anchorMax;
        Vector2 pivot = rectTransform.pivot;
        Vector2 anchoredPosition = rectTransform.anchoredPosition;

        anchorMin.x = 0f;
        anchorMax.x = 0f;
        pivot.x = 0f;
        anchoredPosition.x = CompletionLabelLeftOffset;

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.anchoredPosition = anchoredPosition;

        if (rectTransform.rect.width < CompletionLabelMinWidth)
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, CompletionLabelMinWidth);
    }

    private static void HideResidualScoreOptionLabels(Transform itemRoot, Toggle keptToggle)
    {
        if (itemRoot == null)
            return;

        TMP_Text[] tmpLabels = itemRoot.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in tmpLabels)
        {
            if (text != null && IsScoreOptionText(text.text) && !IsDescendantOf(text.transform, keptToggle != null ? keptToggle.transform : null))
                text.gameObject.SetActive(false);
        }

        Text[] legacyLabels = itemRoot.GetComponentsInChildren<Text>(true);
        foreach (Text text in legacyLabels)
        {
            if (text != null && IsScoreOptionText(text.text) && !IsDescendantOf(text.transform, keptToggle != null ? keptToggle.transform : null))
                text.gameObject.SetActive(false);
        }
    }

    private static bool IsScoreOptionText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string value = text.Trim();
        return value == "0" || value == "1" || value == "2";
    }

    private static bool IsTaskLabelText(Transform textTransform, string objectName, Toggle completionToggle)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return false;

        if (objectName == "QuestionText")
            return true;

        return objectName == "Label" &&
               !IsDescendantOf(textTransform, completionToggle != null ? completionToggle.transform : null);
    }

    private static bool IsDescendantOf(Transform child, Transform possibleParent)
    {
        if (child == null || possibleParent == null)
            return false;

        Transform current = child;
        while (current != null)
        {
            if (current == possibleParent)
                return true;

            current = current.parent;
        }

        return false;
    }

    private void FinalizeChecklistItems()
    {
        if (configuredMetadataItems.Count == 0 || completed == null)
            return;

        int count = Mathf.Min(configuredMetadataItems.Count, completed.Length);
        for (int i = 0; i < count; i++)
        {
            StudyItemMetadata metadata = configuredMetadataItems[i];
            if (metadata == null)
                continue;

            if (StudyTaskResultBuffer.HasFinalizedItem(metadata.itemId))
            {
                StudyTaskResultBuffer.TryUpdateCompletionChecked(metadata.itemId, completed[i]);
                continue;
            }

            StudyTaskResultBuffer.TryFinalizeCurrentItem(metadata, null, completed[i], out _);
        }
    }

    private void ResolveSceneReferences()
    {
        TryResolveFromLegacyChecklistManager();

        if (checkListPanel == null)
            checkListPanel = FindCheckListPanel();

        if (contentParent == null)
            contentParent = FindChecklistContent(checkListPanel);

        if (finishButton == null)
            finishButton = FindButtonInHierarchy(checkListPanel, "FinishButton")
                ?? FindButtonByName("FinishButton");

        if (checklistIconButton == null)
            checklistIconButton = FindButtonByName("CheckListIcon") ?? FindButtonByName("ChecklistIcon");

        if (closeButton == null)
            closeButton = FindButtonInHierarchy(checkListPanel, "CloseButton")
                ?? FindButtonByName("CloseButton");

        if (backButton == null)
            backButton = FindButtonByName("ClipboardBack")
                ?? FindButtonByName("Back Button")
                ?? FindButtonByName("Back");

        if (cameraClipboardController == null)
            cameraClipboardController = FindObjectOfType<CameraClipboardController>();
    }

    private void TryResolveFromLegacyChecklistManager()
    {
        ChecklistManager[] legacyManagers = FindObjectsByType<ChecklistManager>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        ChecklistManager legacy = null;
        for (int i = 0; i < legacyManagers.Length; i++)
        {
            ChecklistManager candidate = legacyManagers[i];
            if (candidate == null || !candidate.gameObject.scene.IsValid())
                continue;

            legacy = candidate;
            break;
        }

        if (legacy == null)
            return;

        if (checkListPanel == null && legacy.checkListPanel != null)
            checkListPanel = legacy.checkListPanel;

        if (contentParent == null && legacy.checklistParent != null)
            contentParent = legacy.checklistParent;

        if (finishButton == null && legacy.finishButton != null)
            finishButton = legacy.finishButton;

        if (checklistIconButton == null && legacy.checklistIconButton != null)
            checklistIconButton = legacy.checklistIconButton;

        if (closeButton == null && legacy.closeButton != null)
            closeButton = legacy.closeButton;

        if (cameraClipboardController == null && legacy.cameraController != null)
            cameraClipboardController = legacy.cameraController;
    }

    private void ReturnToScenarioSelect()
    {
        if (!string.IsNullOrWhiteSpace(scenarioSelectSceneName))
            SceneManager.LoadScene(scenarioSelectSceneName);
    }

    private void RefreshFinishButton()
    {
        if (finishButton == null)
            return;

        bool allCompleted = completed != null && completed.Length > 0;
        if (completed != null)
        {
            foreach (bool isCompleted in completed)
            {
                if (!isCompleted)
                {
                    allCompleted = false;
                    break;
                }
            }
        }

        finishButton.interactable = allCompleted;
        ColorBlock colors = finishButton.colors;
        colors.normalColor = allCompleted ? Color.green : Color.gray;
        colors.highlightedColor = allCompleted ? new Color(0f, 0.8f, 0f) : Color.gray;
        finishButton.colors = colors;
    }

    private static int ResolveRowIndex(StudyItemMetadata metadata)
    {
        if (metadata == null || string.IsNullOrWhiteSpace(metadata.itemId))
            return -1;

        int dashIndex = metadata.itemId.LastIndexOf('-');
        if (dashIndex < 0 || dashIndex >= metadata.itemId.Length - 1)
            return -1;

        string ordinalText = metadata.itemId.Substring(dashIndex + 1);
        return int.TryParse(ordinalText, out int ordinal) ? Math.Max(0, ordinal - 1) : -1;
    }

    private int ResolveRowIndexForMetadata(StudyItemMetadata metadata)
    {
        if (metadata != null && !string.IsNullOrWhiteSpace(metadata.itemId) &&
            itemIdToRowIndex.TryGetValue(metadata.itemId.Trim(), out int mappedIndex))
        {
            return mappedIndex;
        }

        return ResolveRowIndex(metadata);
    }

    private static Transform FindTransformByName(string objectName)
    {
        GameObject gameObject = FindGameObjectByName(objectName);
        return gameObject != null ? gameObject.transform : null;
    }

    private static GameObject FindCheckListPanel()
    {
        GameObject panel = FindGameObjectByName("CheckListPanel");
        if (panel != null)
            return panel;

        Button finish = FindButtonByName("FinishButton");
        if (finish != null)
        {
            Transform current = finish.transform;
            while (current != null)
            {
                if (current.name.IndexOf("panel", StringComparison.OrdinalIgnoreCase) >= 0)
                    return current.gameObject;
                current = current.parent;
            }
        }

        return null;
    }

    private static Transform FindChecklistContent(GameObject panelRoot)
    {
        if (panelRoot != null)
        {
            Transform namedContent = FindChildTransformByName(panelRoot, "Content");
            if (IsChecklistContent(namedContent))
                return namedContent;

            Transform bestInPanel = FindBestContentTransform(panelRoot.transform);
            if (bestInPanel != null)
                return bestInPanel;
        }

        Transform globalContent = FindTransformByName("Content");
        if (IsChecklistContent(globalContent))
            return globalContent;

        Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        Transform bestGlobal = null;
        int bestScore = -1;
        foreach (Transform transform in allTransforms)
        {
            if (transform == null || !transform.gameObject.scene.IsValid())
                continue;

            int score = ScoreChecklistContent(transform);
            if (score > bestScore)
            {
                bestScore = score;
                bestGlobal = transform;
            }
        }

        return bestScore > 0 ? bestGlobal : null;
    }

    private static Transform FindBestContentTransform(Transform root)
    {
        if (root == null)
            return null;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        Transform best = null;
        int bestScore = -1;
        foreach (Transform transform in transforms)
        {
            int score = ScoreChecklistContent(transform);
            if (score > bestScore)
            {
                bestScore = score;
                best = transform;
            }
        }

        return bestScore > 0 ? best : null;
    }

    private static bool IsChecklistContent(Transform transform)
    {
        return ScoreChecklistContent(transform) > 0;
    }

    private static int ScoreChecklistContent(Transform transform)
    {
        if (transform == null || !transform.gameObject.scene.IsValid())
            return -1;

        int rowCount = 0;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.GetComponentsInChildren<Toggle>(true).Length > 0)
                rowCount++;
        }

        if (rowCount == 0)
            return -1;

        int nameBonus = string.Equals(transform.name, "Content", StringComparison.OrdinalIgnoreCase) ? 10 : 0;
        return rowCount + nameBonus;
    }

    private static Transform FindChildTransformByName(GameObject root, string objectName)
    {
        if (root == null)
            return null;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform transform in transforms)
        {
            if (transform != null && transform.gameObject.name == objectName)
                return transform;
        }

        return null;
    }

    private static Button FindButtonInHierarchy(GameObject root, string objectName)
    {
        Transform transform = FindChildTransformByName(root, objectName);
        return transform != null ? transform.GetComponent<Button>() : null;
    }

    private static Button FindButtonByName(string objectName)
    {
        GameObject gameObject = FindGameObjectByName(objectName);
        return gameObject != null ? gameObject.GetComponent<Button>() : null;
    }

    private static GameObject FindGameObjectByName(string objectName)
    {
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (Transform transform in transforms)
        {
            if (transform == null || !transform.gameObject.scene.IsValid())
                continue;

            if (transform.gameObject.name == objectName)
                return transform.gameObject;
        }

        return null;
    }
}
