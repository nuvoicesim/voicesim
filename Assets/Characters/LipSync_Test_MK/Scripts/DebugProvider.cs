// DebugProvider.cs
using System.Collections.Generic;
using UnityEngine;

public class DebugProvider : MonoBehaviour, IExpressionProvider
{
    float browInnerUp = 0f;
    float eyeBlinkLeft = 0f;
    float jawOpen = 0f;
    float eyeBlinkRight = 0f;
    float mouthSmileLeft = 0f;
    float mouthSmileRight = 0f;

    public float rise = 2.0f;
    public float fall = 1.2f;

    void Update()
    {
        browInnerUp     = Step(browInnerUp,     Input.GetKey(KeyCode.Alpha1));
        eyeBlinkLeft    = Step(eyeBlinkLeft,    Input.GetKey(KeyCode.Alpha2));
        jawOpen         = Step(jawOpen,         Input.GetKey(KeyCode.Alpha3));
        eyeBlinkRight   = Step(eyeBlinkRight,   Input.GetKey(KeyCode.Alpha4));
        mouthSmileLeft  = Step(mouthSmileLeft,  Input.GetKey(KeyCode.Alpha5));
        mouthSmileRight = Step(mouthSmileRight, Input.GetKey(KeyCode.Alpha6));
    }

    float Step(float current, bool held)
    {
        float s = held ? rise : -fall;
        current += s * Time.deltaTime;
        return Mathf.Clamp01(current);
    }

    public Dictionary<string, float> GetWeights()
    {
        return new Dictionary<string, float>
        {
            { "browInnerUp",     browInnerUp },
            { "eyeBlinkLeft",    eyeBlinkLeft },
            { "jawOpen",         jawOpen },
            { "eyeBlinkRight",   eyeBlinkRight },
            { "mouthSmileLeft",  mouthSmileLeft },
            { "mouthSmileRight", mouthSmileRight }
        };
    }
}
