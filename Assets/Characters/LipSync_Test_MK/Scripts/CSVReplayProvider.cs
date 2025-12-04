using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class CSVReplayProvider : MonoBehaviour, IExpressionProvider
{
    [Header("CSV File Settings")]
    [Tooltip("CSV filename in StreamingAssets/Arkit_Capture folder")]
    public string csvFileName = "smile_iPhone.csv";
    
    [Header("Playback Settings")]
    [Tooltip("Speed multiplier for playback")]
    [Range(0.1f, 3f)]
    public float playbackSpeed = 1f;
    
    [Tooltip("Loop the animation when it reaches the end")]
    public bool loopPlayback = true;
    
    [Tooltip("Frames per second of the recorded data")]
    public float recordedFPS = 60f;

    [Header("Transition Settings")]
    [Tooltip("Duration for smooth transition between expressions")]
    public float transitionDuration = 0.3f;

    [Header("Stroke Simulation")]
    public StrokeSimulator strokeSimulator;

    private bool isTransitioning = false;
    private float transitionTimer = 0f;
    private Dictionary<string, float> previousWeights = new Dictionary<string, float>();

    [Header("Runtime Info (Read-Only)")]
    public int currentFrame = 0;
    public int totalFrames = 0;
    public bool isPlaying = false;

    private float timer = 0f;
    private List<Dictionary<string, float>> frames;
    private float frameInterval;

    void Start()
    {
        frameInterval = 1f / recordedFPS;
        LoadCSVFile();
    }

    public void LoadCSVFile()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "Arkit_Capture", csvFileName);
        
        Debug.Log("Loading CSV from: " + path);
        
        frames = CSVParser.ParseARKitCSV(path);
        totalFrames = frames != null ? frames.Count : 0;
        currentFrame = 0;
        timer = 0f;
        isPlaying = totalFrames > 0;

        if (isPlaying)
        {
            float duration = totalFrames / recordedFPS;
            Debug.Log("CSV Replay Ready: " + csvFileName);
            Debug.Log("Frames: " + totalFrames + " | FPS: " + recordedFPS + " | Duration: " + duration.ToString("F2") + "s");
        }
        else
        {
            Debug.LogError("Failed to load CSV: " + csvFileName);
        }
    }

    void Update()
    {
        if (!isPlaying || frames == null || frames.Count == 0) return;

        timer += Time.deltaTime * playbackSpeed;

        while (timer >= frameInterval)
        {
            timer -= frameInterval;
            currentFrame++;

            // Handle end of animation
            if (currentFrame >= frames.Count)
            {
                if (loopPlayback)
                {
                    currentFrame = 0;
                    Debug.Log("Looping: " + csvFileName);
                }
                else
                {
                    currentFrame = frames.Count - 1;
                    isPlaying = false;
                    Debug.Log("Finished: " + csvFileName);
                }
                break;
            }
        }
    }

    // Implement IExpressionProvider interface
    public Dictionary<string, float> GetWeights()
    {
        if (frames == null || frames.Count == 0)
            return new Dictionary<string, float>();

        if (currentFrame < 0 || currentFrame >= frames.Count)
            return new Dictionary<string, float>();

        // Get current frame weights
        Dictionary<string, float> currentWeights = new Dictionary<string, float>();
        foreach (var kvp in frames[currentFrame])
        {
            currentWeights[kvp.Key] = kvp.Value / 100f;
        }

        // Apply transition blending if transitioning
        Dictionary<string, float> finalWeights;
        
        if (isTransitioning)
        {
            transitionTimer += Time.deltaTime;
            float t = Mathf.Clamp01(transitionTimer / transitionDuration);
            
            Dictionary<string, float> blendedWeights = new Dictionary<string, float>();
            
            foreach (var kvp in currentWeights)
            {
                float previousValue = 0f;
                if (previousWeights.ContainsKey(kvp.Key))
                {
                    previousValue = previousWeights[kvp.Key];
                }
                
                blendedWeights[kvp.Key] = Mathf.Lerp(previousValue, kvp.Value, t);
            }
            
            if (transitionTimer >= transitionDuration)
            {
                isTransitioning = false;
            }
            
            finalWeights = blendedWeights;
        }
        else
        {
            finalWeights = currentWeights;
        }
        
        // Apply stroke simulation if enabled
        if (strokeSimulator != null)
        {
            finalWeights = strokeSimulator.ApplyStrokeEffect(finalWeights);
        }

        return finalWeights;
    }

    // Public method to switch emotion clips
    public void PlayClip(string newCSVFileName)
    {
        // Store current weights for smooth transition
        if (frames != null && frames.Count > 0 && currentFrame >= 0 && currentFrame < frames.Count)
        {
            previousWeights.Clear();
            foreach (var kvp in frames[currentFrame])
            {
                previousWeights[kvp.Key] = kvp.Value / 100f;
            }
            isTransitioning = true;
            transitionTimer = 0f;
        }

        if (csvFileName == newCSVFileName && isPlaying)
        {
            currentFrame = 0;
            timer = 0f;
            Debug.Log("Restarting clip: " + newCSVFileName);
        }
        else
        {
            csvFileName = newCSVFileName;
            LoadCSVFile();
            Debug.Log("Switched to clip: " + newCSVFileName);
        }
    }

    // Debug helper
    public float GetProgress()
    {
        if (totalFrames == 0) return 0f;
        return (float)currentFrame / totalFrames;
    }
}