using UnityEngine;

public class KeyboardEmotionTester : MonoBehaviour
{
    public CSVReplayProvider replayProvider;

    void Update()
    {
        // Press 1 for Smile
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            replayProvider.PlayClip("smile_iPhone.csv");
            Debug.Log("Playing: Smile");
        }
        
        // Press 2 for Confuse
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            replayProvider.PlayClip("confuse_iPhone.csv");
            Debug.Log("Playing: Confuse");
        }
        
        // Press 3 for Shock
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            replayProvider.PlayClip("shock_iPhone.csv");
            Debug.Log("Playing: Shock");
        }
        
        // Press 4 for Sad
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
        {
            replayProvider.PlayClip("sad_iPhone.csv");
            Debug.Log("Playing: Sad");
        }
        
        // Press 5 for Pain
        if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5))
        {
            replayProvider.PlayClip("pain_iPhone.csv");
            Debug.Log("Playing: Pain");
        }
        
        // Press 6 for Neutral
        if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6))
        {
            replayProvider.PlayClip("neutral_iPhone.csv");
            Debug.Log("Playing: Neutral");
        }
        
        // Press S to toggle stroke simulation
        if (Input.GetKeyDown(KeyCode.S))
        {
            StrokeSimulator simulator = replayProvider.GetComponent<StrokeSimulator>();
            if (simulator != null)
            {
                simulator.ToggleStrokeSimulation();
            }
        }
    }
}