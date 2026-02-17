using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class AssetManager : MonoBehaviour
{
    public static AssetManager Instance { get; private set; }
    
    private Dictionary<string, GameObject> loadedAvatars = new Dictionary<string, GameObject>();
    private Dictionary<string, GameObject> loadedEnvironments = new Dictionary<string, GameObject>();
    
    public Transform avatarSpawnPoint;
    public Transform environmentSpawnPoint;
    
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    public IEnumerator LoadAvatar(string avatarId)
    {
        Debug.Log($"[AssetManager] Loading avatar: {avatarId}");
        
        // Check if already loaded
        if (loadedAvatars.ContainsKey(avatarId))
        {
            Debug.Log($"[AssetManager] Avatar already cached, instantiating from cache");
            Instantiate(loadedAvatars[avatarId], avatarSpawnPoint.position, Quaternion.identity);
            yield break;
        }
        
        // Load from Addressables
        AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(avatarId);
        yield return handle;
        
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            GameObject avatarPrefab = handle.Result;
            loadedAvatars[avatarId] = avatarPrefab;
            
            GameObject instance = Instantiate(avatarPrefab, avatarSpawnPoint.position, Quaternion.identity);
            Debug.Log($"[AssetManager] ✅ Avatar loaded and spawned: {avatarId}");
        }
        else
        {
            Debug.LogError($"[AssetManager] ❌ Failed to load avatar: {avatarId}");
        }
    }
    
    public IEnumerator LoadEnvironment(string environmentId)
    {
        Debug.Log($"[AssetManager] Loading environment: {environmentId}");
        
        // Check if already loaded
        if (loadedEnvironments.ContainsKey(environmentId))
        {
            Debug.Log($"[AssetManager] Environment already cached, instantiating from cache");
            Instantiate(loadedEnvironments[environmentId], environmentSpawnPoint.position, Quaternion.identity);
            yield break;
        }
        
        // Load from Addressables
        AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(environmentId);
        yield return handle;
        
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            GameObject envPrefab = handle.Result;
            loadedEnvironments[environmentId] = envPrefab;
            
            GameObject instance = Instantiate(envPrefab, environmentSpawnPoint.position, Quaternion.identity);
            Debug.Log($"[AssetManager] ✅ Environment loaded and spawned: {environmentId}");
        }
        else
        {
            Debug.LogError($"[AssetManager] ❌ Failed to load environment: {environmentId}");
        }
    }
}