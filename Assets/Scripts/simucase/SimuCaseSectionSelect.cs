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

    [Header("Description Start Buttons")]
    [SerializeField] private Button sectionAStartButton;
    [SerializeField] private Button sectionBStartButton;
    [SerializeField] private Button sectionCStartButton;
    [SerializeField] private Button sectionDStartButton;

    [Header("Description Back Buttons")]
    [SerializeField] private Button sectionABackButton;
    [SerializeField] private Button sectionBBackButton;
    [SerializeField] private Button sectionCBackButton;
    [SerializeField] private Button sectionDBackButton;

    [Header("Scene Names")]
    [SerializeField] private string sectionASceneName = "sectionA";
    [SerializeField] private string sectionBSceneName = "sectionB";
    [SerializeField] private string sectionCSceneName = "sectionC";
    [SerializeField] private string sectionDSceneName = "sectionD";

    private List<Button> startButtonList;
    private List<Button> backButtonList;
    private List<Button> sectionButtonList;
    private List<GameObject> sectionPanelList;
    private List<string> sceneNameList;

    private void Awake()
    {
        EnsureListsInitialized();
    }

    private void OnEnable()
    {
        EnsureListsInitialized();
        RefreshCompletionTags();
    }

    private void Start()
    {
        EnsureListsInitialized();

        ShowPanel(sectionSelectPanel);
        HidePanel(sectionAPanel);
        HidePanel(sectionBPanel);
        HidePanel(sectionCPanel);
        HidePanel(sectionDPanel);

        foreach (Button button in sectionButtonList) {
            if (button != null)
                button.onClick.AddListener(() => ShowDescription(sectionPanelList[sectionButtonList.IndexOf(button)]));
        }

        foreach (Button button in startButtonList) {
            if (button != null)
                button.onClick.AddListener(() => LoadSection(sceneNameList[startButtonList.IndexOf(button)]));
        }
        foreach (Button button in backButtonList) {
            if (button != null)
                button.onClick.AddListener(() => BackToSelect(sectionPanelList[backButtonList.IndexOf(button)]));
        }

        RefreshCompletionTags();
    }

    private void EnsureListsInitialized()
    {
        if (startButtonList == null)
            startButtonList = new List<Button> { sectionAStartButton, sectionBStartButton, sectionCStartButton, sectionDStartButton };
        if (backButtonList == null)
            backButtonList = new List<Button> { sectionABackButton, sectionBBackButton, sectionCBackButton, sectionDBackButton };
        if (sectionButtonList == null)
            sectionButtonList = new List<Button> { sectionAButton, sectionBButton, sectionCButton, sectionDButton };
        if (sectionPanelList == null)
            sectionPanelList = new List<GameObject> { sectionAPanel, sectionBPanel, sectionCPanel, sectionDPanel };
        if (sceneNameList == null)
            sceneNameList = new List<string> { sectionASceneName, sectionBSceneName, sectionCSceneName, sectionDSceneName };
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

    private void RefreshCompletionTags()
    {
        // Only affects ScenarioSelectV2 UI; section scenes won't have these tags.
        string[] sectionIds = { "A", "B", "C", "D" };
        for (int i = 0; i < sectionIds.Length && i < sectionButtonList.Count; i++)
        {
            Button sectionButton = sectionButtonList[i];
            if (sectionButton == null) continue;

            bool isCompleted = SimuCaseSectionCompletionStore.IsCompleted(sectionIds[i]);

            foreach (Transform complete in FindChildrenByName(sectionButton.transform, "Complete Tag"))
            {
                if (complete != null) 
                {
                    Debug.Log("Setting complete tag active: " + isCompleted);
                    complete.gameObject.SetActive(isCompleted);
                }
            }

            foreach (Transform incomplete in FindChildrenByName(sectionButton.transform, "Incomplete Tag"))
            {
                if (incomplete != null)
                {
                    Debug.Log("Setting incomplete tag active: " + !isCompleted);
                    incomplete.gameObject.SetActive(!isCompleted);
                }
            }
        }
    }

    private static List<Transform> FindChildrenByName(Transform root, string name)
    {
        List<Transform> results = new List<Transform>();
        if (root == null || string.IsNullOrWhiteSpace(name))
            return results;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in children)
        {
            if (t != null && t.name == name)
                results.Add(t);
        }
        return results;
    }
}
