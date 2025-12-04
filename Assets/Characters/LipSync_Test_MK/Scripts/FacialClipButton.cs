using UnityEngine;
using UnityEngine.UI;

public class FacialClipButton : MonoBehaviour
{
    [Header("References")]
    public CSVReplayProvider replayProvider;
    
    [Header("Clip Settings")]
    public string csvClipName = "smile_iPhone.csv";
    public string emotionName = "Smile";

    private Button button;

    void Start()
    {
        button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(OnClick);
        }
        else
        {
            Debug.LogError("FacialClipButton: No Button component found!");
        }
        
        // Update button text
        Text buttonText = GetComponentInChildren<Text>();
        if (buttonText != null)
        {
            buttonText.text = emotionName;
        }
    }

    void OnClick()
    {
        if (replayProvider != null)
        {
            replayProvider.PlayClip(csvClipName);
            Debug.Log("Playing emotion: " + emotionName + " (" + csvClipName + ")");
        }
        else
        {
            Debug.LogError("CSVReplayProvider is not assigned!");
        }
    }
}