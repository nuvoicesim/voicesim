using System.Collections.Generic;
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
    [SerializeField] private string sectionASceneName = "sectionA";
    [SerializeField] private string sectionBSceneName = "sectionB";
    [SerializeField] private string sectionCSceneName = "sectionC";
    [SerializeField] private string sectionDSceneName = "sectionD";

    private void Start()
    {
        ShowPanel(sectionSelectPanel);
        HidePanel(sectionAPanel);
        HidePanel(sectionBPanel);
        HidePanel(sectionCPanel);
        HidePanel(sectionDPanel);

        if (sectionAButton != null) sectionAButton.onClick.AddListener(() => LoadSection(sectionASceneName));
        if (sectionBButton != null) sectionBButton.onClick.AddListener(() => LoadSection(sectionBSceneName));
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

    public void ApplySectionStatusesJson(string json)
    {
        if (!StudySectionStatusAdapter.TryParseSectionStatuses(json, out List<StudySectionStatus> statuses))
            return;

        ApplySectionStatuses(statuses);
    }

    public void ApplySectionStatuses(List<StudySectionStatus> statuses)
    {
        if (statuses == null)
            return;

        SetSectionInteractable("A", true);
        SetSectionInteractable("B", true);
        SetSectionInteractable("C", true);
        SetSectionInteractable("D", true);

        Dictionary<string, StudySectionStatus> statusMap = StudySectionStatusAdapter.BuildStatusMap(statuses);
        foreach (KeyValuePair<string, StudySectionStatus> entry in statusMap)
        {
            bool isInteractable = !StudySectionStatusAdapter.IsDisabledStatus(entry.Value.status);
            SetSectionInteractable(entry.Key, isInteractable);
        }
    }

    public void ApplySectionStatus(string sectionId, string status)
    {
        string normalizedSectionId = StudySectionStatusAdapter.NormalizeSectionId(sectionId);
        bool isInteractable = !StudySectionStatusAdapter.IsDisabledStatus(status);
        SetSectionInteractable(normalizedSectionId, isInteractable);
    }

    private void SetSectionInteractable(string sectionId, bool isInteractable)
    {
        switch (StudySectionStatusAdapter.NormalizeSectionId(sectionId))
        {
            case "A":
                SetButtonInteractable(sectionAButton, isInteractable);
                break;
            case "B":
                SetButtonInteractable(sectionBButton, isInteractable);
                break;
            case "C":
                SetButtonInteractable(sectionCButton, isInteractable);
                SetButtonInteractable(sectionCStartButton, isInteractable);
                break;
            case "D":
                SetButtonInteractable(sectionDButton, isInteractable);
                SetButtonInteractable(sectionDStartButton, isInteractable);
                break;
        }
    }

    private static void SetButtonInteractable(Button button, bool isInteractable)
    {
        if (button != null)
            button.interactable = isInteractable;
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
