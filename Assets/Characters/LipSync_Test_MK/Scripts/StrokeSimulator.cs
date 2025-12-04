using UnityEngine;
using System.Collections.Generic;

public class StrokeSimulator : MonoBehaviour
{
    [Header("Stroke Simulation Settings")]
    [Tooltip("Enable stroke simulation")]
    public bool enableStrokeSimulation = false;
    
    [Header("Affected Side")]
    [Tooltip("Which side is affected by stroke")]
    public StrokeSide affectedSide = StrokeSide.Right;
    
    [Header("Severity")]
    [Tooltip("How severe is the paralysis (0 = no effect, 1 = complete paralysis)")]
    [Range(0f, 1f)]
    public float severity = 0.6f;
    
    public enum StrokeSide
    {
        Left,
        Right
    }
    
    // Left side blendshapes
    private string[] leftBlendshapes = new string[]
    {
        "browDownLeft", "browOuterUpLeft",
        "eyeBlinkLeft", "eyeSquintLeft", "eyeWideLeft",
        "cheekSquintLeft", "mouthSmileLeft", "mouthFrownLeft",
        "mouthDimpleLeft", "mouthStretchLeft", "mouthPressLeft",
        "mouthLowerDownLeft", "mouthUpperUpLeft", "noseSneerLeft"
    };
    
    // Right side blendshapes
    private string[] rightBlendshapes = new string[]
    {
        "browDownRight", "browOuterUpRight",
        "eyeBlinkRight", "eyeSquintRight", "eyeWideRight",
        "cheekSquintRight", "mouthSmileRight", "mouthFrownRight",
        "mouthDimpleRight", "mouthStretchRight", "mouthPressRight",
        "mouthLowerDownRight", "mouthUpperUpRight", "noseSneerRight"
    };
    
    // Apply stroke effect to weights
    public Dictionary<string, float> ApplyStrokeEffect(Dictionary<string, float> weights)
    {
        if (!enableStrokeSimulation) return weights;
        
        Dictionary<string, float> modifiedWeights = new Dictionary<string, float>(weights);
        
        string[] affectedBlendshapes = (affectedSide == StrokeSide.Left) ? leftBlendshapes : rightBlendshapes;
        
        foreach (string blendshape in affectedBlendshapes)
        {
            if (modifiedWeights.ContainsKey(blendshape))
            {
                // Reduce the strength of affected side
                modifiedWeights[blendshape] *= (1f - severity);
            }
        }
        
        return modifiedWeights;
    }
    
    // Toggle stroke simulation on/off
    public void ToggleStrokeSimulation()
    {
        enableStrokeSimulation = !enableStrokeSimulation;
        Debug.Log("Stroke simulation: " + (enableStrokeSimulation ? "ON" : "OFF") + 
                  " | Side: " + affectedSide + " | Severity: " + severity);
    }
}