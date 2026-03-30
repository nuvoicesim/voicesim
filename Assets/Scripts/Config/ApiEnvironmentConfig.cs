using UnityEngine;

[CreateAssetMenu(fileName = "ApiEnvironmentConfig", menuName = "Config/API Environment")]
public class ApiEnvironmentConfig : ScriptableObject
{
    [Header("Backend")]
    public string backendBaseUrl;

    public string BuildBackendUrl(string path) =>
        $"{backendBaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
}
