using UnityEngine;
using UnityEditor;

public class FindWebGLTextBridge : EditorWindow
{
    [MenuItem("Tools/Find WebGLTextBridge GameObject")]
    public static void ShowWindow()
    {
        Debug.Log("=== SEARCHING FOR WebGLTextBridge COMPONENT ===");
        
        WebGLTextBridge[] bridges = FindObjectsOfType<WebGLTextBridge>();
        
        if (bridges.Length == 0)
        {
            Debug.LogError("NO WebGLTextBridge component found in the scene.");
            Debug.LogError("Please add WebGLTextBridge component to a GameObject");
        }
        else
        {
            foreach (var bridge in bridges)
            {
                Debug.Log($"Found WebGLTextBridge on GameObject: '{bridge.gameObject.name}'");
                Debug.Log($"   Full path: {GetGameObjectPath(bridge.gameObject)}");
                
                if (bridge.gameObject.name != "WebGLTextBridge")
                {
                    Debug.LogWarning($"GameObject name is '{bridge.gameObject.name}' but JavaScript expects 'WebGLTextBridge'");
                    Debug.LogWarning($"   SOLUTION: Rename GameObject to 'WebGLTextBridge' (case-sensitive!)");
                    
                    // Highlight the object in hierarchy
                    Selection.activeGameObject = bridge.gameObject;
                    EditorGUIUtility.PingObject(bridge.gameObject);
                }
                else
                {
                    Debug.Log("GameObject name matches. Should work correctly.");
                }
            }
        }
        
        Debug.Log("=== SEARCH COMPLETE ===");
    }
    
    private static string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;
        
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        
        return path;
    }
}
