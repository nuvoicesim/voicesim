using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Phase2ScenarioSelect : MonoBehaviour
{
    private const string DefaultObjectNamingButtonName = "Scene1Button";
    private const string DefaultSentenceCompletionButtonName = "Scene2Button";

    [Header("Scene Names")]
    [SerializeField] private string objectNamingSceneName = "";
    [SerializeField] private string sentenceCompletionSceneName = "";

    [Header("Buttons")]
    [SerializeField] private Button objectNamingButton;
    [SerializeField] private Button sentenceCompletionButton;

    private void Awake()
    {
        if (objectNamingButton == null)
            objectNamingButton = FindButtonInCurrentScene(DefaultObjectNamingButtonName);

        if (sentenceCompletionButton == null)
            sentenceCompletionButton = FindButtonInCurrentScene(DefaultSentenceCompletionButtonName);

        if (objectNamingButton != null)
            objectNamingButton.onClick.AddListener(OpenObjectNaming);
        else
            Debug.LogWarning("Phase2ScenarioSelect: Object Naming button is not assigned or found.");

        if (sentenceCompletionButton != null)
            sentenceCompletionButton.onClick.AddListener(OpenSentenceCompletion);
        else
            Debug.LogWarning("Phase2ScenarioSelect: Sentence Completion button is not assigned or found.");
    }

    private void OnDestroy()
    {
        if (objectNamingButton != null)
            objectNamingButton.onClick.RemoveListener(OpenObjectNaming);

        if (sentenceCompletionButton != null)
            sentenceCompletionButton.onClick.RemoveListener(OpenSentenceCompletion);
    }

    public void OpenObjectNaming()
    {
        LoadScene(objectNamingSceneName);
    }

    public void OpenSentenceCompletion()
    {
        LoadScene(sentenceCompletionSceneName);
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

    private Button FindButtonInCurrentScene(string buttonObjectName)
    {
        Button[] buttons = Resources.FindObjectsOfTypeAll<Button>();
        foreach (Button button in buttons)
        {
            if (button == null || button.gameObject.scene != gameObject.scene)
                continue;

            if (button.gameObject.name == buttonObjectName)
                return button;
        }

        return null;
    }
}
