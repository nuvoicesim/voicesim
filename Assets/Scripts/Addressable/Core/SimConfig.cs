using System;
using UnityEngine;

[Serializable]
public class SimConfig
{
    public string disease;
    public string mode;
    public string difficulty;
    public string avatarId;
    public string environmentId;
    public string sessionId;
    
    public static SimConfig FromJson(string json)
    {
        return JsonUtility.FromJson<SimConfig>(json);
    }
}