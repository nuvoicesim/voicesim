using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class StudyRubricFeedbackItem
{
    public string itemId;
    public int? studentSelectedScore;
    public int? expectedScore;
    public bool? scoreMatchesExpected;
    public string rubricReason;
}

[Serializable]
public class StudyRubricFeedbackBlock
{
    public string taskId;
    public string taskSummary;
    public string assessmentGranularity;
    public List<StudyRubricFeedbackItem> itemFeedback = new List<StudyRubricFeedbackItem>();
    public StudyRubricFeedbackTask taskFeedback;
}

[Serializable]
public class StudyRubricFeedbackTask
{
    public string taskId;
    public int? studentSelectedScore;
    public int? expectedScore;
    public bool? scoreMatchesExpected;
    public string rubricReason;
    public int? validUniqueResponseCount;
    public int? excludedExampleCount;
    public int? repeatedResponseCount;
    public int? offCategoryCount;
}

public class StudyFeedbackPresenter : MonoBehaviour
{
    [Header("Existing Report")]
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private TextMeshProUGUI aiInteractionReportText;

    [Header("Rubric Feedback")]
    [SerializeField] private bool showRubricBlockWhenEmpty = true;
    [SerializeField] private string rubricPlaceholderText =
        "Rubric scoring feedback will appear here once backend scoring is connected.";

    private GameObject rubricBlock;
    private TextMeshProUGUI rubricBodyText;
    private GameObject aiInteractionBlock;
    private TextMeshProUGUI aiInteractionHeaderText;

    public TextMeshProUGUI AiInteractionReportText => aiInteractionReportText;

    public void EnsureDefaultStructure(TextMeshProUGUI reportText = null)
    {
        if (reportText != null)
            aiInteractionReportText = reportText;

        contentRoot = ResolveContentRoot();
        if (contentRoot == null)
            return;

        EnsureContentLayout(contentRoot);
        EnsureRubricBlock();
        EnsureAiInteractionBlock();
        ShowRubricWaitingState();
        RebuildLayout();
    }

    public void ShowRubricWaitingState()
    {
        if (rubricBlock == null || rubricBodyText == null)
            return;

        rubricBlock.SetActive(showRubricBlockWhenEmpty);
        rubricBodyText.text = rubricPlaceholderText;
        RebuildLayout();
    }

    public void SetRubricFeedback(StudyRubricFeedbackBlock feedback)
    {
        if (rubricBlock == null || rubricBodyText == null)
            EnsureDefaultStructure();

        if (rubricBlock == null || rubricBodyText == null)
            return;

        if (feedback == null || !HasAnyRubricContent(feedback))
        {
            ShowRubricWaitingState();
            return;
        }

        rubricBlock.SetActive(true);
        rubricBodyText.text = FormatRubricFeedback(feedback);
        RebuildLayout();
    }

    private static bool HasAnyRubricContent(StudyRubricFeedbackBlock feedback)
    {
        if (!string.IsNullOrWhiteSpace(feedback.taskSummary))
            return true;
        if (feedback.itemFeedback != null && feedback.itemFeedback.Count > 0)
            return true;
        if (feedback.taskFeedback != null)
            return true;
        return false;
    }

    public void SetRubricBlockVisible(bool visible)
    {
        if (rubricBlock == null)
            EnsureDefaultStructure();

        if (rubricBlock != null)
            rubricBlock.SetActive(visible);

        RebuildLayout();
    }

    // Toggle the legacy AI Interaction Feedback block, which carries the
    // ScoreManager-owned narrative report text. The block is created
    // unconditionally by EnsureAiInteractionBlock() and is the right
    // container for legacy `report`-wrapper responses, the no-conversation
    // placeholder, and HTTP-error reports — they all populate
    // ScoreManager.reportText which this block hosts. For the flat Phase 1
    // rubricAssessment envelope nothing writes that text, so the block
    // appears empty next to the rubric content; ScoreManager hides it
    // immediately after applying that envelope. Other flows leave it
    // visible, matching the default created state.
    public void SetAiInteractionBlockVisible(bool visible)
    {
        if (aiInteractionBlock == null)
            EnsureDefaultStructure();

        if (aiInteractionBlock != null)
            aiInteractionBlock.SetActive(visible);

        RebuildLayout();
    }

    private RectTransform ResolveContentRoot()
    {
        if (contentRoot != null)
            return contentRoot;

        if (aiInteractionReportText == null)
            aiInteractionReportText = GetComponentInChildren<MedicalReportFormatter>(true)?.reportText;

        if (aiInteractionReportText == null)
            aiInteractionReportText = GetComponentInChildren<TextMeshProUGUI>(true);

        if (aiInteractionReportText == null)
            return null;

        Transform parent = aiInteractionReportText.transform.parent;
        if (parent != null && parent.name == "AIInteractionFeedbackBlock" && parent.parent != null)
            return parent.parent as RectTransform;

        return parent as RectTransform;
    }

    private void EnsureContentLayout(RectTransform root)
    {
        VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = root.gameObject.AddComponent<VerticalLayoutGroup>();

        layout.padding = new RectOffset(40, 40, 28, 28);
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = root.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = root.gameObject.AddComponent<ContentSizeFitter>();

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void EnsureRubricBlock()
    {
        Transform existing = contentRoot.Find("RubricBasedAssessmentFeedbackBlock");
        rubricBlock = existing != null ? existing.gameObject : CreateFeedbackBlock("RubricBasedAssessmentFeedbackBlock");
        rubricBlock.transform.SetSiblingIndex(0);

        TextMeshProUGUI header = EnsureTextChild(
            rubricBlock.transform,
            "RubricFeedbackHeader",
            "Rubric-Based Assessment Feedback",
            30f,
            FontStyles.Bold);
        header.color = new Color(0.12f, 0.22f, 0.32f, 1f);

        rubricBodyText = EnsureTextChild(
            rubricBlock.transform,
            "RubricFeedbackBody",
            rubricPlaceholderText,
            22f,
            FontStyles.Normal);
        rubricBodyText.color = new Color(0.24f, 0.28f, 0.34f, 1f);
    }

    private void EnsureAiInteractionBlock()
    {
        Transform existing = contentRoot.Find("AIInteractionFeedbackBlock");
        aiInteractionBlock = existing != null ? existing.gameObject : CreateFeedbackBlock("AIInteractionFeedbackBlock");
        aiInteractionBlock.SetActive(true);
        aiInteractionBlock.transform.SetAsLastSibling();

        aiInteractionHeaderText = EnsureTextChild(
            aiInteractionBlock.transform,
            "AIInteractionFeedbackHeader",
            "AI Interaction Feedback",
            30f,
            FontStyles.Bold);
        aiInteractionHeaderText.color = new Color(0.12f, 0.22f, 0.32f, 1f);
        aiInteractionHeaderText.transform.SetAsFirstSibling();

        if (aiInteractionReportText != null && aiInteractionReportText.transform.parent != aiInteractionBlock.transform)
            aiInteractionReportText.transform.SetParent(aiInteractionBlock.transform, false);

        if (aiInteractionReportText != null)
        {
            ConfigureText(aiInteractionReportText, 24f, FontStyles.Normal);
            aiInteractionReportText.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            aiInteractionReportText.transform.SetAsLastSibling();
        }
    }

    public void SetAiInteractionBlockVisible(bool visible)
    {
        if (aiInteractionBlock == null)
            EnsureDefaultStructure();

        if (aiInteractionBlock != null)
            aiInteractionBlock.SetActive(visible);

        RebuildLayout();
    }

    private GameObject CreateFeedbackBlock(string objectName)
    {
        GameObject block = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        block.transform.SetParent(contentRoot, false);

        Image image = block.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.72f);

        VerticalLayoutGroup layout = block.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(22, 22, 18, 18);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = block.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return block;
    }

    private TextMeshProUGUI EnsureTextChild(
        Transform parent,
        string objectName,
        string defaultText,
        float fontSize,
        FontStyles fontStyle)
    {
        Transform existing = parent.Find(objectName);
        TextMeshProUGUI text = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
        if (text == null)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            text = textObject.GetComponent<TextMeshProUGUI>();
        }

        if (string.IsNullOrEmpty(text.text))
            text.text = defaultText;

        ConfigureText(text, fontSize, fontStyle);
        return text;
    }

    private void ConfigureText(TextMeshProUGUI text, float fontSize, FontStyles fontStyle)
    {
        if (text == null)
            return;

        if (aiInteractionReportText != null && text != aiInteractionReportText)
        {
            text.font = aiInteractionReportText.font;
            text.fontSharedMaterial = aiInteractionReportText.fontSharedMaterial;
        }

        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;

        LayoutElement layoutElement = text.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = text.gameObject.AddComponent<LayoutElement>();

        layoutElement.minHeight = fontSize * 1.5f;
    }

    private string FormatRubricFeedback(StudyRubricFeedbackBlock feedback)
    {
        StringBuilder builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(feedback.taskSummary))
        {
            builder.AppendLine(feedback.taskSummary.Trim());
            builder.AppendLine();
        }

        if (IsTaskLevel(feedback) && feedback.taskFeedback != null)
        {
            builder.AppendLine(FormatRubricFeedbackTask(feedback.taskFeedback));
        }
        else if (feedback.itemFeedback != null)
        {
            foreach (StudyRubricFeedbackItem item in feedback.itemFeedback)
            {
                builder.AppendLine(FormatRubricFeedbackItem(item));
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static bool IsTaskLevel(StudyRubricFeedbackBlock feedback)
    {
        if (feedback == null)
            return false;
        string granularity = feedback.assessmentGranularity;
        if (!string.IsNullOrWhiteSpace(granularity))
            return string.Equals(granularity.Trim(), "task_level", StringComparison.OrdinalIgnoreCase);

        bool hasTask = feedback.taskFeedback != null;
        bool hasItems = feedback.itemFeedback != null && feedback.itemFeedback.Count > 0;
        return hasTask && !hasItems;
    }

    private static string FormatRubricFeedbackTask(StudyRubricFeedbackTask task)
    {
        if (task == null)
            return string.Empty;

        string label = string.IsNullOrWhiteSpace(task.taskId) ? "Task" : task.taskId.Trim();
        string selectedScore = task.studentSelectedScore.HasValue ? task.studentSelectedScore.Value.ToString() : "pending";
        string expectedScore = task.expectedScore.HasValue ? task.expectedScore.Value.ToString() : "pending";
        string matchText = task.scoreMatchesExpected.HasValue
            ? (task.scoreMatchesExpected.Value ? "Correct" : "Review needed")
            : "pending";
        string reason = string.IsNullOrWhiteSpace(task.rubricReason) ? "Rubric reason pending." : task.rubricReason.Trim();

        StringBuilder builder = new StringBuilder();
        builder.AppendLine(label);
        builder.AppendLine($"Student Selected Score: {selectedScore}");
        builder.AppendLine($"Expected Score: {expectedScore}");
        builder.AppendLine($"Result: {matchText}");
        builder.AppendLine(reason);

        if (HasScoringDetail(task))
        {
            builder.AppendLine();
            builder.AppendLine("Scoring Detail:");
            if (task.validUniqueResponseCount.HasValue)
                builder.AppendLine($"• Valid unique responses: {task.validUniqueResponseCount.Value}");
            if (task.excludedExampleCount.HasValue)
                builder.AppendLine($"• Excluded examples: {task.excludedExampleCount.Value}");
            if (task.repeatedResponseCount.HasValue)
                builder.AppendLine($"• Repeated responses: {task.repeatedResponseCount.Value}");
            if (task.offCategoryCount.HasValue)
                builder.AppendLine($"• Off-category responses: {task.offCategoryCount.Value}");
        }

        return builder.ToString().TrimEnd();
    }

    private static bool HasScoringDetail(StudyRubricFeedbackTask task)
    {
        if (task == null)
            return false;
        return task.validUniqueResponseCount.HasValue
            || task.excludedExampleCount.HasValue
            || task.repeatedResponseCount.HasValue
            || task.offCategoryCount.HasValue;
    }

    private string FormatRubricFeedbackItem(StudyRubricFeedbackItem item)
    {
        if (item == null)
            return string.Empty;

        string selectedScore = item.studentSelectedScore.HasValue ? item.studentSelectedScore.Value.ToString() : "pending";
        string expectedScore = item.expectedScore.HasValue ? item.expectedScore.Value.ToString() : "pending";
        string matchText = item.scoreMatchesExpected.HasValue
            ? (item.scoreMatchesExpected.Value ? "Correct" : "Review needed")
            : "pending";
        string reason = string.IsNullOrWhiteSpace(item.rubricReason) ? "Rubric reason pending." : item.rubricReason.Trim();

        return $"{item.itemId}\nStudent Selected Score: {selectedScore}\nExpected Score: {expectedScore}\nResult: {matchText}\n{reason}\n";
    }

    private void RebuildLayout()
    {
        if (contentRoot == null)
            return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
    }
}
