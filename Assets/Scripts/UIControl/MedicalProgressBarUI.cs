using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MedicalProgressBarUI : MonoBehaviour
{
    [Header("UI Components")]
    public GameObject loadingPanel;
    public Slider progressBar;
    public TextMeshProUGUI loadingTextTMP;

    [Header("Progress Bar Colors")]
    public Color backgroundColor = new Color(0.91f, 0.96f, 0.97f, 1f); // #E8F4F8
    public Color fillColor = new Color(0.18f, 0.53f, 0.67f, 1f);       // #2E86AB

    [Header("Text Settings")]
    public Color textColor = Color.white;
    public string defaultLoadingText = "Report is generating...";

    [Header("Loading Panel")]
    public Color panelBackgroundColor = new Color(0f, 0f, 0f, 0.7f);

    void Start()
    {
        InitializeProgressBar();
        ApplyColorScheme();
        HideProgressBar();
    }

    private void InitializeProgressBar()
    {
        if (progressBar != null)
        {
            progressBar.minValue = 0f;
            progressBar.maxValue = 1f;
            progressBar.value = 0f;
            progressBar.interactable = false;
        }

        if (loadingTextTMP != null)
        {
            loadingTextTMP.text = defaultLoadingText;
        }
    }

    private void ApplyColorScheme()
    {
        // 应用进度条颜色
        if (progressBar != null)
        {
            Transform backgroundTransform = progressBar.transform.Find("Background");
            Transform fillTransform = progressBar.transform.Find("Fill Area/Fill");

            if (backgroundTransform != null)
            {
                Image backgroundImage = backgroundTransform.GetComponent<Image>();
                if (backgroundImage != null)
                    backgroundImage.color = backgroundColor;
            }

            if (fillTransform != null)
            {
                Image fillImage = fillTransform.GetComponent<Image>();
                if (fillImage != null)
                    fillImage.color = fillColor;
            }
        }

        // 应用文本颜色
        if (loadingTextTMP != null)
        {
            loadingTextTMP.color = textColor;
        }

        // 应用面板背景色
        if (loadingPanel != null)
        {
            Image panelImage = loadingPanel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.color = panelBackgroundColor;
            }
        }
    }

    public void ShowProgressBar()
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
            Debug.Log("Progress bar shown");
        }

        if (progressBar != null)
        {
            progressBar.value = 0f;
        }

        if (loadingTextTMP != null)
        {
            loadingTextTMP.text = defaultLoadingText;
        }
    }

    public void HideProgressBar()
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
            Debug.Log("Progress bar hidden");
        }
    }

    public void UpdateProgress(float progress, string message = "")
    {
        if (progressBar != null)
        {
            progressBar.value = Mathf.Clamp01(progress);
        }

        if (loadingTextTMP != null && !string.IsNullOrEmpty(message))
        {
            loadingTextTMP.text = message;
        }

        Debug.Log($"Progress updated: {progress:F2} - {message}");
    }

    public bool IsVisible()
    {
        return loadingPanel != null && loadingPanel.activeSelf;
    }

    // 在Inspector中可以点击应用颜色
    [ContextMenu("Apply Colors")]
    public void ApplyColorsManually()
    {
        ApplyColorScheme();
        Debug.Log("Colors applied manually");
    }

    // 测试方法
    [ContextMenu("Test Show")]
    public void TestShow()
    {
        ShowProgressBar();
    }

    [ContextMenu("Test Hide")]
    public void TestHide()
    {
        HideProgressBar();
    }
}