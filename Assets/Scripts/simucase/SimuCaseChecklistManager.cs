using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    [SerializeField] private bool startAsIcon = true;

    [Header("On Finish")]
    public UnityEvent onFinish;
    [SerializeField] private Button backButton;
    [SerializeField] private string scenarioSelectSceneName = "ScenarioSelect";

    private CameraClipboardController cameraClipboardController;

    private int questionCount;
    private bool[] answered;

    private void Start()
    {
        cameraClipboardController = FindObjectOfType<CameraClipboardController>();
        SetupQuestions();

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
        questionCount = contentParent.childCount;
        answered = new bool[questionCount];

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
        HidePanel();
        onFinish?.Invoke();
        cameraClipboardController?.TriggerClipboardView();
        ResetAll();
        if (backButton != null) backButton.gameObject.SetActive(true);
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
            ToggleGroup group = contentParent.GetChild(i).GetComponentInChildren<ToggleGroup>();
            if (group != null) group.SetAllTogglesOff();
        }
        RefreshFinishButton();
    }
}
