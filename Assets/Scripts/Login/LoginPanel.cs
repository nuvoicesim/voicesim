using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using TMPro;                       // TextMeshPro UI
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.UI;

public class LoginPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField usernameField;
    [SerializeField] private TMP_InputField passwordField;
    [SerializeField] private Button loginButton;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject spinner;          // Optional loader
    [SerializeField] private GameObject panelRoot;        // Optional: leave empty to use this GameObject

    [Header("Network Settings")]
    [SerializeField]
    private string loginUrl =
        "https://gu6dg3g185.execute-api.us-west-2.amazonaws.com/dev/auth/login";
    [Tooltip("Total seconds before the request times out.")]
    [SerializeField, Range(5, 120)] private int requestTimeoutSeconds = 20;

    [Header("Events")]
    public UnityEvent OnLoginStarted;
    public IntEvent OnLoginSucceededWithLevel;      // emits simulationLevel
    public StringEvent OnLoginSucceededWithUserId;  // emits userID
    public StringEvent OnLoginFailedWithMessage;    // emits human-readable error

    // --- Internal state ---
    private Coroutine _inflight;
    private const string LastUsernamePrefsKey = "LoginPanel_LastUsername";
    private GameObject Root => panelRoot == null ? gameObject : panelRoot;

    // --- DTOs matching your API schema ---
    [Serializable]
    private sealed class LoginPayload
    {
        public string username;
        public string password;
    }

    // Success Response (200):
    // { "message": "Login successful", "userID": "...", "simulationLevel": 1 }
    [Serializable]
    private sealed class LoginSuccess
    {
        public string message;
        public string userID;
        public int simulationLevel;
    }

    // Failure Response (400):
    // { "error": "Invalid username or password" }
    // Failure Response (403):
    // { "error": "You have completed all simulations.", "currentStep": "level-3-simulation", "simulationLevel": null }
    [Serializable]
    private sealed class LoginError
    {
        public string error;
        public string currentStep;       // only on 403
        public string simulationLevel;   // may be null or "null"
    }

    private void OnValidate()
    {
        // Helpful default in editor
        if (panelRoot == null) panelRoot = gameObject;
    }

    private void Awake()
    {
        if (spinner != null) spinner.SetActive(false);
        SetStatus(string.Empty);

        if (loginButton != null)
            loginButton.onClick.AddListener(HandleLoginClicked);

        if (usernameField != null)
            usernameField.onSubmit.AddListener(_ => HandleLoginClicked());

        if (passwordField != null)
            passwordField.onSubmit.AddListener(_ => HandleLoginClicked());

        // Prefill last username for convenience (never store passwords)
        if (usernameField != null && PlayerPrefs.HasKey(LastUsernamePrefsKey))
        {
            usernameField.text = PlayerPrefs.GetString(LastUsernamePrefsKey, string.Empty);
        }

        Root.SetActive(true);
    }

    private void OnDestroy()
    {
        if (loginButton != null)
            loginButton.onClick.RemoveListener(HandleLoginClicked);

        if (usernameField != null)
            usernameField.onSubmit.RemoveAllListeners();

        if (passwordField != null)
            passwordField.onSubmit.RemoveAllListeners();

        if (_inflight != null)
        {
            StopCoroutine(_inflight);
            _inflight = null;
        }
    }

    private void HandleLoginClicked()
    {
        if (!ValidateBindings()) return;

        string user = usernameField.text?.Trim();
        string pass = passwordField.text;

        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
        {
            Fail("Please enter your username and password.");
            return;
        }

        if (_inflight != null) return; // prevent duplicate submits

        _inflight = StartCoroutine(LoginRoutine(user, pass));
    }

    private bool ValidateBindings()
    {
        if (usernameField == null || passwordField == null || loginButton == null)
        {
            Fail("Login UI is not wired. Check the Inspector references.");
            return false;
        }
        return true;
    }

    private IEnumerator LoginRoutine(string username, string password)
    {
        ToggleInteractable(false);
        ShowSpinner(true);
        SetStatus("Signing in…");
        OnLoginStarted?.Invoke();

        // Remember last username (never store password)
        PlayerPrefs.SetString(LastUsernamePrefsKey, username);
        PlayerPrefs.Save();

        var payload = new LoginPayload { username = username, password = password };
        string json = JsonConvert.SerializeObject(payload);

        using (var req = new UnityWebRequest(loginUrl, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Accept", "application/json");
            req.timeout = requestTimeoutSeconds;

            yield return req.SendWebRequest();

            // Clear inflight marker asap
            _inflight = null;

            ShowSpinner(false);
            ToggleInteractable(true);

            if (req.result == UnityWebRequest.Result.Success && req.responseCode == 200)
            {
                HandleSuccess(req.downloadHandler?.text);
            }
            else
            {
                HandleFailure((int)req.responseCode, req.error, req.downloadHandler?.text);
            }
        }

        // Hygiene: clear the password field after any attempt
        if (passwordField != null) passwordField.text = string.Empty;
    }

    private void HandleSuccess(string body)
    {
        try
        {
            var ok = JsonConvert.DeserializeObject<LoginSuccess>(body ?? string.Empty);
            if (ok == null)
            {
                Fail("Unexpected server response.");
                return;
            }

            // Persist userID if you truly need it (avoid sensitive tokens)
            if (!string.IsNullOrEmpty(ok.userID))
            {
                PlayerPrefs.SetString("userID", ok.userID);
                PlayerPrefs.Save();
            }

            // Fire events for external listeners
            OnLoginSucceededWithLevel?.Invoke(ok.simulationLevel);
            OnLoginSucceededWithUserId?.Invoke(ok.userID ?? string.Empty);

            // Pass both userID and simulationLevel to TTSManager, AWSAPI, OpenAI
            var tts = TTSManager.Instance ?? FindObjectOfType<TTSManager>();
            if (tts != null)
            {
                tts.ApplyLoginContext(ok.userID, ok.simulationLevel);
            }
            else
            {
                Debug.LogWarning("[LoginPanel] TTSManager not found in scene; skipping ApplyLoginContext.");
            }

            var aws = AWSAPIConnector.Instance ?? FindObjectOfType<AWSAPIConnector>();
            if (aws != null)
            {
                aws.ApplyLoginContext(ok.userID, ok.simulationLevel);
            }
            else
            {
                Debug.LogWarning("[LoginPanel] AWSAPIConnector not found in scene; skipping ApplyLoginContext.");
            }

            // Pass both userID and simulationLevel to OpenAIRequest
            var openAI = OpenAIRequest.Instance ?? FindObjectOfType<OpenAIRequest>();
            if (openAI != null)
            {
                openAI.ApplyLoginContext(ok.userID, ok.simulationLevel);
            }
            else
            {
                Debug.LogWarning("[LoginPanel] OpenAIRequest not found in scene; skipping ApplyLoginContext.");
            }

            SetStatus("Login successful.");
            Root.SetActive(false);
        }
        catch (Exception e)
        {
            Fail($"Parse error: {e.Message}");
        }
    }

    private void HandleFailure(int statusCode, string transportError, string body)
    {
        // Try to parse structured error from server
        LoginError err = null;
        if (!string.IsNullOrEmpty(body))
        {
            try { err = JsonConvert.DeserializeObject<LoginError>(body); }
            catch { /* fall back to generic */ }
        }

        string message;
        switch (statusCode)
        {
            case 400:
                message = "Invalid username or password.";
                break;
            case 403:
                message = "You have completed all simulations. Please head to post survey.";
                break;
            case 0:
                message = $"Network error. {transportError ?? "Check your connection."}";
                break;
            default:
                // Fallback: favor server error text if provided
                message = !string.IsNullOrWhiteSpace(err?.error)
                    ? $"Login failed ({statusCode}). {err.error}"
                    : $"Login failed ({statusCode}). {transportError ?? "Unexpected error."}";
                break;
        }

        Fail(message);
        OnLoginFailedWithMessage?.Invoke(message);
    }

    private static string FormatCurrentStep(string step)
    {
        if (string.IsNullOrWhiteSpace(step)) return string.Empty;
        return $" ({step})";
    }

    private void ToggleInteractable(bool enabled)
    {
        if (loginButton != null) loginButton.interactable = enabled;
        if (usernameField != null) usernameField.interactable = enabled;
        if (passwordField != null) passwordField.interactable = enabled;
    }

    private void ShowSpinner(bool show)
    {
        if (spinner != null) spinner.SetActive(show);
    }

    private void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg ?? string.Empty;
        if (!string.IsNullOrEmpty(msg))
            Debug.Log($"[LoginPanel] {msg}");
    }

    private void Fail(string msg)
    {
        SetStatus(msg);
        Debug.LogWarning($"[LoginPanel] {msg}");
    }
}

// UnityEvent specializations for Inspector friendliness
[Serializable] public sealed class StringEvent : UnityEvent<string> { }
[Serializable] public sealed class IntEvent : UnityEvent<int> { }
