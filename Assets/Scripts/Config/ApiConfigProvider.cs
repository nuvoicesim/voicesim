using UnityEngine;

public static class ApiConfigProvider
{
    private const string ResourcePath = "Config/ApiEnvironmentConfig";
    private static ApiEnvironmentConfig cachedConfig;

    public static ApiEnvironmentConfig Config
    {
        get
        {
            if (cachedConfig == null)
                cachedConfig = Resources.Load<ApiEnvironmentConfig>(ResourcePath);

            return cachedConfig;
        }
    }

    public static bool TryBuildBackendUrl(string path, out string url)
    {
        var config = Config;
        if (config == null)
        {
            Debug.LogError("[ApiConfigProvider] Missing ApiEnvironmentConfig asset. Create it at Assets/Resources/Config/ApiEnvironmentConfig.asset.");
            url = null;
            return false;
        }

        if (string.IsNullOrWhiteSpace(config.backendBaseUrl))
        {
            Debug.LogError("[ApiConfigProvider] ApiEnvironmentConfig.backendBaseUrl is empty.");
            url = null;
            return false;
        }

        url = config.BuildBackendUrl(path);
        return true;
    }
}
