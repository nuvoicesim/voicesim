using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Phase2ScenarioSelect : MonoBehaviour
{
    private const string DefaultObjectNamingButtonName = "Scene1Button";
    private const string DefaultSentenceCompletionButtonName = "Scene2Button";
    private const string DefaultSelectorPanelName = "SectionSelectPanel";
    private const string DefaultTitleObjectName = "Title";
    private const string DefaultObjectNamingInstructionPanelName = "SectionCPanel";
    private const string DefaultSentenceCompletionInstructionPanelName = "SectionDPanel";
    private const string DefaultObjectNamingBackButtonName = "BackC Button";
    private const string DefaultObjectNamingStartButtonName = "StartC Button";
    private const string DefaultSentenceCompletionBackButtonName = "BackD Button";
    private const string DefaultSentenceCompletionStartButtonName = "StartD Button";

    [Header("Scene Names")]
    [SerializeField] private string objectNamingSceneName = "";
    [SerializeField] private string sentenceCompletionSceneName = "";

    [Header("Selector")]
    [SerializeField] private GameObject selectorPanel;
    [SerializeField] private GameObject titleObject;
    [SerializeField] private Button objectNamingButton;
    [SerializeField] private Button sentenceCompletionButton;

    [Header("Object Naming Instruction")]
    [SerializeField] private GameObject objectNamingInstructionPanel;
    [SerializeField] private Button objectNamingBackButton;
    [SerializeField] private Button objectNamingStartButton;

    [Header("Sentence Completion Instruction")]
    [SerializeField] private GameObject sentenceCompletionInstructionPanel;
    [SerializeField] private Button sentenceCompletionBackButton;
    [SerializeField] private Button sentenceCompletionStartButton;

    private void Awake()
    {
        if (selectorPanel == null)
            selectorPanel = FindGameObjectInCurrentScene(DefaultSelectorPanelName);
        if (titleObject == null)
            titleObject = FindGameObjectInCurrentScene(DefaultTitleObjectName);

        if (objectNamingButton == null)
            objectNamingButton = FindInCurrentScene<Button>(DefaultObjectNamingButtonName);
        if (sentenceCompletionButton == null)
            sentenceCompletionButton = FindInCurrentScene<Button>(DefaultSentenceCompletionButtonName);

        if (objectNamingInstructionPanel == null)
            objectNamingInstructionPanel = FindGameObjectInCurrentScene(DefaultObjectNamingInstructionPanelName);
        if (objectNamingBackButton == null)
            objectNamingBackButton = FindInCurrentScene<Button>(DefaultObjectNamingBackButtonName);
        if (objectNamingStartButton == null)
            objectNamingStartButton = FindInCurrentScene<Button>(DefaultObjectNamingStartButtonName);

        if (sentenceCompletionInstructionPanel == null)
            sentenceCompletionInstructionPanel = FindGameObjectInCurrentScene(DefaultSentenceCompletionInstructionPanelName);
        if (sentenceCompletionBackButton == null)
            sentenceCompletionBackButton = FindInCurrentScene<Button>(DefaultSentenceCompletionBackButtonName);
        if (sentenceCompletionStartButton == null)
            sentenceCompletionStartButton = FindInCurrentScene<Button>(DefaultSentenceCompletionStartButtonName);

        ShowPanel(selectorPanel);
        ShowPanel(titleObject);
        HidePanel(objectNamingInstructionPanel);
        HidePanel(sentenceCompletionInstructionPanel);

        if (objectNamingButton != null)
            objectNamingButton.onClick.AddListener(OpenObjectNaming);
        else
            Debug.LogWarning("Phase2ScenarioSelect: Object Naming button is not assigned or found.");

        if (sentenceCompletionButton != null)
            sentenceCompletionButton.onClick.AddListener(OpenSentenceCompletion);
        else
            Debug.LogWarning("Phase2ScenarioSelect: Sentence Completion button is not assigned or found.");

        if (objectNamingBackButton != null)
            objectNamingBackButton.onClick.AddListener(BackFromObjectNaming);
        if (objectNamingStartButton != null)
            objectNamingStartButton.onClick.AddListener(StartObjectNaming);
        if (sentenceCompletionBackButton != null)
            sentenceCompletionBackButton.onClick.AddListener(BackFromSentenceCompletion);
        if (sentenceCompletionStartButton != null)
            sentenceCompletionStartButton.onClick.AddListener(StartSentenceCompletion);
    }

    private void OnDestroy()
    {
        if (objectNamingButton != null)
            objectNamingButton.onClick.RemoveListener(OpenObjectNaming);
        if (sentenceCompletionButton != null)
            sentenceCompletionButton.onClick.RemoveListener(OpenSentenceCompletion);
        if (objectNamingBackButton != null)
            objectNamingBackButton.onClick.RemoveListener(BackFromObjectNaming);
        if (objectNamingStartButton != null)
            objectNamingStartButton.onClick.RemoveListener(StartObjectNaming);
        if (sentenceCompletionBackButton != null)
            sentenceCompletionBackButton.onClick.RemoveListener(BackFromSentenceCompletion);
        if (sentenceCompletionStartButton != null)
            sentenceCompletionStartButton.onClick.RemoveListener(StartSentenceCompletion);
    }

    public void OpenObjectNaming()
    {
        ShowInstruction(objectNamingInstructionPanel);
    }

    public void OpenSentenceCompletion()
    {
        ShowInstruction(sentenceCompletionInstructionPanel);
    }

    private void BackFromObjectNaming()
    {
        BackToSelector(objectNamingInstructionPanel);
    }

    private void BackFromSentenceCompletion()
    {
        BackToSelector(sentenceCompletionInstructionPanel);
    }

    private void StartObjectNaming()
    {
        LoadScene(objectNamingSceneName);
    }

    private void StartSentenceCompletion()
    {
        LoadScene(sentenceCompletionSceneName);
    }

    private void ShowInstruction(GameObject instructionPanel)
    {
        HidePanel(selectorPanel);
        HidePanel(titleObject);
        ShowPanel(instructionPanel);
    }

    private void BackToSelector(GameObject instructionPanel)
    {
        HidePanel(instructionPanel);
        ShowPanel(selectorPanel);
        ShowPanel(titleObject);
    }

    private void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("Phase2ScenarioSelect: Cannot load a Phase 2 task scene because the scene name is empty.");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private T FindInCurrentScene<T>(string objectName) where T : Component
    {
        T[] components = Resources.FindObjectsOfTypeAll<T>();
        foreach (T component in components)
        {
            if (component == null || component.gameObject.scene != gameObject.scene)
                continue;

            if (component.gameObject.name == objectName)
                return component;
        }

        return null;
    }

    private GameObject FindGameObjectInCurrentScene(string objectName)
    {
        Transform transform = FindInCurrentScene<Transform>(objectName);
        return transform != null ? transform.gameObject : null;
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
