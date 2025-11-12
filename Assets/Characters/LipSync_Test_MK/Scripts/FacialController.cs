// FacialController.cs
// Reads provider weights and applies them to the model's blendshapes.

using System.Collections.Generic;
using UnityEngine;

public class FacialController : MonoBehaviour
{
    [Header("Targets")]
    public SkinnedMeshRenderer face;   // Drag your character's SkinnedMeshRenderer here.
    public MonoBehaviour providerBehaviour; // Assign a component that implements IExpressionProvider.

    IExpressionProvider provider;

    [Header("Channels")]
    // Populate with ARKit-like names that exist on your model.
    public string[] channelNames = new string[] { "browInnerUp", "eyeBlinkLeft", "jawOpen" };

    int[] indices;

    void Awake()
    {
        // Resolve provider interface.
        provider = providerBehaviour as IExpressionProvider;
        if (provider == null)
        {
            Debug.LogError("FacialController: providerBehaviour does not implement IExpressionProvider.");
            enabled = false;
            return;
        }

        // Auto find face if not assigned.
        if (face == null)
            face = GetComponentInChildren<SkinnedMeshRenderer>();

        if (face == null)
        {
            Debug.LogError("FacialController: SkinnedMeshRenderer not assigned or not found.");
            enabled = false;
            return;
        }

        // Cache indices for faster runtime.
        var mesh = face.sharedMesh;
        indices = new int[channelNames.Length];
        for (int i = 0; i < channelNames.Length; i++)
        {
            indices[i] = mesh.GetBlendShapeIndex(channelNames[i]);
            if (indices[i] < 0)
                Debug.LogWarning($"Blendshape '{channelNames[i]}' not found on mesh.");
        }
    }

    void Update()
    {
        if (provider == null || face == null) return;

        Dictionary<string, float> w = provider.GetWeights();
        if (w == null) return;

        // Apply weights in 0..100 range expected by Unity.
        for (int i = 0; i < channelNames.Length; i++)
        {
            int idx = indices[i];
            if (idx < 0) continue;

            if (w.TryGetValue(channelNames[i], out float v))
            {
                float clamped = Mathf.Clamp01(v) * 100f;
                face.SetBlendShapeWeight(idx, clamped);
            }
        }
    }
}
