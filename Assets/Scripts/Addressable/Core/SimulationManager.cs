using System.Collections;
using System.Linq; // Add this
using UnityEngine;
using UnityEngine.AddressableAssets; // Add this
using UnityEngine.ResourceManagement.AsyncOperations; // Add this

public class SimulationManager : MonoBehaviour
{
    public static SimulationManager Instance { get; private set; }
    
    private bool addressablesInitialized = false;
    
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        Debug.Log("[SimManager] SimulationManager Awake");
    }
    
    void Start()
    {
        Debug.Log("[SimManager] SimulationManager Started");
        
        // Debug paths
        Debug.Log($"[SimManager] Application.streamingAssetsPath: {Application.streamingAssetsPath}");
        Debug.Log($"[SimManager] Application.dataPath: {Application.dataPath}");
        
        // Initialize Addressables
        InitializeAddressables();
    }
    
    private void InitializeAddressables()
    {
        Debug.Log("[SimManager] Initializing Addressables...");
        
        var initOp = Addressables.InitializeAsync();
        initOp.Completed += (op) =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                Debug.Log("[SimManager] ✅ Addressables initialized successfully!");
                addressablesInitialized = true;
                
                // List all resource locators
                Debug.Log($"[SimManager] Resource Locators count: {Addressables.ResourceLocators.Count()}");
                foreach (var locator in Addressables.ResourceLocators)
                {
                    Debug.Log($"[SimManager] Resource Locator ID: {locator.LocatorId}");
                    
                    // Try to list keys if possible
                    var keys = locator.Keys;
                    if (keys != null)
                    {
                        Debug.Log($"[SimManager] Locator has {keys.Count()} keys");
                        // Log first few keys for debugging
                        int count = 0;
                        foreach (var key in keys)
                        {
                            Debug.Log($"[SimManager] Available key: {key}");
                            if (++count >= 10) break; // Only log first 10 keys
                        }
                    }
                }
            }
            else
            {
                Debug.LogError($"[SimManager] ❌ Addressables initialization failed!");
                Debug.LogError($"[SimManager] Error: {op.OperationException}");
                addressablesInitialized = false;
            }
        };
    }
    
    // Called from React via JavaScript bridge
    public void InitializeFromBrowser(string jsonConfig)
    {
        Debug.Log($"[SimManager] Received config from browser: {jsonConfig}");
        
        try
        {
            SimConfig config = SimConfig.FromJson(jsonConfig);
            Debug.Log($"[SimManager] Parsed successfully - Disease: {config.disease}, Avatar: {config.avatarId}, Environment: {config.environmentId}");
            
            // Check if Addressables is ready
            if (!addressablesInitialized)
            {
                Debug.LogWarning("[SimManager] ⚠️ Addressables not initialized yet, waiting...");
                StartCoroutine(WaitForAddressablesAndLoad(config));
            }
            else
            {
                StartCoroutine(LoadSimulation(config));
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SimManager] Failed to parse config: {e.Message}");
        }
    }
    
    private IEnumerator WaitForAddressablesAndLoad(SimConfig config)
    {
        float waitTime = 0f;
        while (!addressablesInitialized && waitTime < 10f)
        {
            yield return new WaitForSeconds(0.1f);
            waitTime += 0.1f;
        }

        if (addressablesInitialized)
        {
            Debug.Log("[SimManager] Addressables ready, loading simulation...");
            yield return LoadSimulation(config);
        }
        else
        {
            Debug.LogError("[SimManager] ❌ Timeout waiting for Addressables initialization");
        }
    }
    
    private IEnumerator LoadSimulation(SimConfig config)
    {
        Debug.Log("[SimManager] Starting asset load sequence...");
        
        // Load avatar
        Debug.Log($"[SimManager] Requesting avatar: {config.avatarId}");
        yield return AssetManager.Instance.LoadAvatar(config.avatarId);
        
        // Load environment
        Debug.Log($"[SimManager] Requesting environment: {config.environmentId}");
        yield return AssetManager.Instance.LoadEnvironment(config.environmentId);
        
        Debug.Log("[SimManager] All assets loaded. Simulation ready!");
        
        // Configure mode
        ConfigureMode(config.mode);
    }
    
    private void ConfigureMode(string mode)
    {
        if (mode == "practice")
        {
            Debug.Log("[SimManager] Practice mode enabled - hints ON");
            // TODO: Enable hints
        }
        else if (mode == "assessment")
        {
            Debug.Log("[SimManager] Assessment mode enabled - hints OFF");
            // TODO: Disable hints, enable scoring
        }
    }
}