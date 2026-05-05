using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SimuCaseSectionSelect : MonoBehaviour
{
    [Header("Title")]
    [SerializeField] private GameObject titleObject;

    [Header("Panels")]
    [SerializeField] private GameObject sectionSelectPanel;
    [SerializeField] private GameObject sectionAPanel;
    [SerializeField] private GameObject sectionBPanel;
    [SerializeField] private GameObject sectionCPanel;
    [SerializeField] private GameObject sectionDPanel;

    [Header("Section Select Buttons")]
    [SerializeField] private Button sectionAButton;
    [SerializeField] private Button sectionBButton;
    [SerializeField] private Button sectionCButton;
    [SerializeField] private Button sectionDButton;

    [Header("Description Back Buttons")]
    [SerializeField] private Button sectionCBackButton;
    [SerializeField] private Button sectionDBackButton;

    [Header("Description Start Buttons")]
    [SerializeField] private Button sectionCStartButton;
    [SerializeField] private Button sectionDStartButton;

    [Header("Scene Names")]
    [SerializeField] private string sectionCSceneName = "sectionC";
    [SerializeField] private string sectionDSceneName = "sectionD";

    private void Start()
    {
        ShowPanel(sectionSelectPanel);
        HidePanel(sectionAPanel);
        HidePanel(sectionBPanel);
        HidePanel(sectionCPanel);
        HidePanel(sectionDPanel);

        if (sectionAButton != null) sectionAButton.interactable = false;
        if (sectionBButton != null) sectionBButton.interactable = false;

        if (sectionCButton != null) sectionCButton.onClick.AddListener(() => ShowDescription(sectionCPanel));
        if (sectionDButton != null) sectionDButton.onClick.AddListener(() => ShowDescription(sectionDPanel));

        if (sectionCBackButton != null) sectionCBackButton.onClick.AddListener(() => BackToSelect(sectionCPanel));
        if (sectionDBackButton != null) sectionDBackButton.onClick.AddListener(() => BackToSelect(sectionDPanel));

        if (sectionCStartButton != null) sectionCStartButton.onClick.AddListener(() => LoadSection(sectionCSceneName));
        if (sectionDStartButton != null) sectionDStartButton.onClick.AddListener(() => LoadSection(sectionDSceneName));
    }

    private void ShowDescription(GameObject panel)
    {
        HidePanel(sectionSelectPanel);
        HidePanel(titleObject);
        ShowPanel(panel);
    }

    private void BackToSelect(GameObject panel)
    {
        HidePanel(panel);
        ShowPanel(sectionSelectPanel);
        ShowPanel(titleObject);
    }

    private void LoadSection(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    private static void ShowPanel(GameObject panel)
    {
        if (panel != null) panel.SetActive(true);
    }

    private static void HidePanel(GameObject panel)
    {
        if (panel != null) panel.SetActive(false);
    }
}
