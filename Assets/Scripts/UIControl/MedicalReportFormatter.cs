using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;
using System.Collections.Generic;

public class MedicalReportFormatter : MonoBehaviour
{
    [Header("Text Components")]
    public TextMeshProUGUI reportText;

    [Header("Medical Report Styling")]
    [SerializeField] private TMP_FontAsset headerFont;
    [SerializeField] private TMP_FontAsset bodyFont;
    [SerializeField] private float headerSize = 24f;
    [SerializeField] private float subHeaderSize = 18f;
    [SerializeField] private float bodySize = 14f;
    [SerializeField] private float lineSpacing = 1.2f;

    [Header("Colors")]
    public Color headerColor = new Color(0.2f, 0.4f, 0.6f, 1f);     // 医疗蓝
    public Color subHeaderColor = new Color(0.3f, 0.3f, 0.3f, 1f);  // 深灰
    public Color bodyColor = new Color(0.2f, 0.2f, 0.2f, 1f);       // 文本黑
    public Color scoreColor = new Color(0.8f, 0.4f, 0.2f, 1f);      // 分数橙
    public Color passColor = new Color(0.2f, 0.6f, 0.2f, 1f);       // 通过绿
    public Color failColor = new Color(0.8f, 0.2f, 0.2f, 1f);       // 未通过红

    void Start()
    {
        if (reportText == null)
            reportText = GetComponent<TextMeshProUGUI>();

        SetupTextProperties();
    }

    private void SetupTextProperties()
    {
        if (reportText != null)
        {
            reportText.fontSize = bodySize;
            reportText.lineSpacing = lineSpacing;
            reportText.color = bodyColor;
            reportText.alignment = TextAlignmentOptions.TopLeft;
            reportText.enableWordWrapping = true;
            reportText.overflowMode = TextOverflowModes.Overflow;
        }
    }

    public string FormatMedicalReport(DynamicEvaluationResult evaluation, int conversationCount)
    {
        StringBuilder formattedReport = new StringBuilder();

        // 报告头部
        formattedReport.Append(CreateHeader());
        formattedReport.AppendLine();

        // 基本信息
        formattedReport.Append(CreateBasicInfo(conversationCount));
        formattedReport.AppendLine();

        // 评估标准详情
        formattedReport.Append(CreateAssessmentDetails(evaluation.criteria));
        formattedReport.AppendLine();

        // 总结评估
        formattedReport.Append(CreateOverallAssessment(evaluation));
        formattedReport.AppendLine();

        // 总结和建议
        formattedReport.Append(CreateSummaryAndRecommendations(evaluation));

        return formattedReport.ToString();
    }

    private string CreateHeader()
    {
        StringBuilder header = new StringBuilder();

        header.AppendLine($"<size={headerSize}><color=#{ColorUtility.ToHtmlStringRGB(headerColor)}><b>NURSING SKILLS EVALUATION REPORT</b></color></size>");
        header.AppendLine($"<color=#{ColorUtility.ToHtmlStringRGB(subHeaderColor)}>Clinical Communication Assessment</color>");
        header.AppendLine("=================================================");
        header.AppendLine();

        // 报告信息
        header.AppendLine($"<b>Date:</b> {System.DateTime.Now:MMMM dd, yyyy}");
        header.AppendLine($"<b>Time:</b> {System.DateTime.Now:HH:mm}");
        header.AppendLine($"<b>Assessment Type:</b> Patient Communication Simulation");
        header.AppendLine($"<b>Evaluator:</b> Automated Assessment System");

        return header.ToString();
    }

    private string CreateBasicInfo(int conversationCount)
    {
        StringBuilder info = new StringBuilder();

        info.AppendLine($"<size={subHeaderSize}><color=#{ColorUtility.ToHtmlStringRGB(subHeaderColor)}><b>SIMULATION SUMMARY</b></color></size>");
        info.AppendLine("=========================================");
        info.AppendLine();
        info.AppendLine($"• <b>Total Dialogue Exchanges:</b> {conversationCount}");
        info.AppendLine($"• <b>Session Duration:</b> Complete interaction recorded");
        info.AppendLine($"• <b>Assessment Method:</b> Comprehensive conversation analysis");
        info.AppendLine($"• <b>Standards Applied:</b> Clinical communication best practices");

        return info.ToString();
    }

    private string CreateAssessmentDetails(List<CriterionScore> criteria)
    {
        StringBuilder details = new StringBuilder();

        details.AppendLine($"<size={subHeaderSize}><color=#{ColorUtility.ToHtmlStringRGB(subHeaderColor)}><b>DETAILED ASSESSMENT CRITERIA</b></color></size>");
        details.AppendLine("=========================================");
        details.AppendLine();

        for (int i = 0; i < criteria.Count; i++)
        {
            var criterion = criteria[i];

            // 计算通过状态
            float percentage = (float)criterion.score / criterion.maxScore;
            Color statusColor = percentage >= 0.7f ? passColor : (percentage >= 0.5f ? scoreColor : failColor);
            string status = percentage >= 0.7f ? "PROFICIENT" : (percentage >= 0.5f ? "DEVELOPING" : "NEEDS IMPROVEMENT");

            details.AppendLine($"<b>{i + 1}. {criterion.name.ToUpper()}</b>");
            details.AppendLine($"   <color=#{ColorUtility.ToHtmlStringRGB(statusColor)}>Score: {criterion.score}/{criterion.maxScore} ({percentage:P0}) - {status}</color>");
            details.AppendLine($"   <i>Assessment:</i> {criterion.explanation}");
            details.AppendLine();
        }

        return details.ToString();
    }

    private string CreateOverallAssessment(DynamicEvaluationResult evaluation)
    {
        StringBuilder assessment = new StringBuilder();

        // 计算总体通过率
        int totalPossible = 0;
        foreach (var criterion in evaluation.criteria)
        {
            totalPossible += criterion.maxScore;
        }

        float overallPercentage = (float)evaluation.totalScore / totalPossible;
        Color overallColor = overallPercentage >= 0.7f ? passColor : (overallPercentage >= 0.5f ? scoreColor : failColor);

        assessment.AppendLine($"<size={subHeaderSize}><color=#{ColorUtility.ToHtmlStringRGB(subHeaderColor)}><b>OVERALL PERFORMANCE ASSESSMENT</b></color></size>");
        assessment.AppendLine("=========================================");
        assessment.AppendLine();

        assessment.AppendLine($"<size=16><b>Final Score:</b> <color=#{ColorUtility.ToHtmlStringRGB(overallColor)}>{evaluation.totalScore}/{totalPossible} ({overallPercentage:P0})</color></size>");
        assessment.AppendLine($"<b>Performance Level:</b> <color=#{ColorUtility.ToHtmlStringRGB(overallColor)}>{evaluation.performanceLevel}</color>");
        assessment.AppendLine();
        assessment.AppendLine($"<b>Clinical Summary:</b>");
        assessment.AppendLine($"{evaluation.overallExplanation}");

        return assessment.ToString();
    }

    private string CreateSummaryAndRecommendations(DynamicEvaluationResult evaluation)
    {
        StringBuilder summary = new StringBuilder();

        summary.AppendLine($"<size={subHeaderSize}><color=#{ColorUtility.ToHtmlStringRGB(subHeaderColor)}><b>LEARNING RECOMMENDATIONS</b></color></size>");
        summary.AppendLine("=========================================");
        summary.AppendLine();

        // 基于分数提供建议
        List<string> recommendations = GenerateRecommendations(evaluation.criteria);

        foreach (var recommendation in recommendations)
        {
            summary.AppendLine($"• {recommendation}");
        }

        summary.AppendLine();
        summary.AppendLine("=========================================");
        summary.AppendLine($"<i>Report generated on {System.DateTime.Now:MMMM dd, yyyy} at {System.DateTime.Now:HH:mm}</i>");
        summary.AppendLine($"<i>For questions about this assessment, consult with your clinical instructor.</i>");

        return summary.ToString();
    }

    private List<string> GenerateRecommendations(List<CriterionScore> criteria)
    {
        List<string> recommendations = new List<string>();

        foreach (var criterion in criteria)
        {
            float percentage = (float)criterion.score / criterion.maxScore;

            if (percentage < 0.5f)
            {
                switch (criterion.name.ToLower())
                {
                    case var name when name.Contains("rapport"):
                        recommendations.Add("Focus on building stronger therapeutic relationships through active listening and empathy");
                        break;
                    case var name when name.Contains("communication"):
                        recommendations.Add("Practice clear, professional communication techniques and active listening skills");
                        break;
                    case var name when name.Contains("information"):
                        recommendations.Add("Develop systematic approaches to gathering comprehensive patient information");
                        break;
                    case var name when name.Contains("clinical"):
                        recommendations.Add("Strengthen clinical reasoning skills through case study practice");
                        break;
                    case var name when name.Contains("professional"):
                        recommendations.Add("Review professional nursing standards and practice professional communication");
                        break;
                    default:
                        recommendations.Add($"Continue developing skills in {criterion.name.ToLower()}");
                        break;
                }
            }
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("Continue practicing excellent communication skills in various clinical scenarios");
            recommendations.Add("Consider mentoring junior nursing students to reinforce your strong skills");
        }

        return recommendations;
    }

    // 公共方法：应用格式化后的报告
    public void ApplyFormattedReport(DynamicEvaluationResult evaluation, int conversationCount)
    {
        if (reportText != null)
        {
            string formattedContent = FormatMedicalReport(evaluation, conversationCount);
            reportText.text = formattedContent;
        }
    }

    // 可选：保存报告为文本文件
    public void SaveReportToFile(DynamicEvaluationResult evaluation, int conversationCount)
    {
        string content = FormatMedicalReport(evaluation, conversationCount);
        string fileName = $"NursingAssessment_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt";
        string path = System.IO.Path.Combine(Application.persistentDataPath, fileName);

        System.IO.File.WriteAllText(path, content);
        Debug.Log($"Report saved to: {path}");
    }
}