using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class CSVParser
{
    public static List<Dictionary<string, float>> ParseARKitCSV(string filePath)
    {
        List<Dictionary<string, float>> frames = new List<Dictionary<string, float>>();
        
        if (!File.Exists(filePath))
        {
            Debug.LogError("CSV file not found: " + filePath);
            return frames;
        }
        
        string[] lines = File.ReadAllLines(filePath);
        
        if (lines.Length < 2)
        {
            Debug.LogError("CSV file is empty or has no data rows");
            return frames;
        }
        
        // Parse header (column names)
        string[] headers = lines[0].Split(',');
        
        // Parse each frame (skip header row)
        for (int i = 1; i < lines.Length; i++)
        {
            string[] values = lines[i].Split(',');
            Dictionary<string, float> frame = new Dictionary<string, float>();
            
            for (int j = 0; j < headers.Length && j < values.Length; j++)
            {
                string key = headers[j].Trim();
                
                // Skip non-blendshape columns
                if (key == "Timecode" || key == "BlendshapeCount" || 
                    key.Contains("Head") || (key.Contains("Eye") && (key.Contains("Yaw") || key.Contains("Pitch") || key.Contains("Roll"))))
                {
                    continue;
                }
                
                // Convert PascalCase to camelCase (EyeBlinkLeft -> eyeBlinkLeft)
                string camelKey = char.ToLower(key[0]) + key.Substring(1);
                
                if (float.TryParse(values[j], out float val))
                {
                    // Store in 0-100 range (will be converted back to 0-1 in GetWeights)
                    frame[camelKey] = val * 100f;
                }
            }
            
            frames.Add(frame);
        }
        
        Debug.Log("Loaded " + frames.Count + " frames from CSV");
        if (frames.Count > 0)
        {
            Debug.Log("First frame has " + frames[0].Count + " blendshapes");
        }
        
        return frames;
    }
}