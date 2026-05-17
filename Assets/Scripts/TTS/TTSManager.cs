using UnityEngine;
using UnityEngine.Networking;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Text;
// for animation
using System.Text.RegularExpressions;

public class TTSManager : MonoBehaviour
{
    public static TTSManager Instance { get; private set; }

    [Header("Audio Settings")]
    [Tooltip("Reference to the AudioSource where the speech will be played")]
    public AudioSource audioSource;

    [Header("TTS Configuration")]
    [SerializeField, Range(1, 3)] private int maxRetries = 2;
    [SerializeField] private int requestTimeoutSeconds = 45;

    [Tooltip("Model ID for ElevenLabs")]
    public string modelId = "eleven_multilingual_v2";

    [Header("Voice Settings")]
    [Range(0f, 1f)]
    [Tooltip("Stability value (0-1)")]
    public float stability = 0.4f;

    [Range(0f, 1f)]
    [Tooltip("Similarity boost value (0-1)")]
    public float similarityBoost = 0.75f;

    [Range(0f, 1f)]
    [Tooltip("Style exaggeration value (0-1)")]
    public float styleExaggeration = 0.3f;

    [Range(0.7f, 1.2f)]
    [Tooltip("Speed value (0.7 - 1.2)")]
    public float speed = 1.0f;


    [Header("Audio2Face Integration")]
    [Tooltip("Whether to use Audio2Face for facial animation")]
    public bool useAudio2Face = true;

    [Tooltip("Whether to delete cached audio files after use")]
    public bool deleteCachedFiles = true;

    [Header("Blood Effect Configuration")]
    [Tooltip("Reference to the BloodEffectController for blood effects")]
    public bool useBloodEffectController = true;

    // Runtime request context
    public string CurrentUserId { get; private set; }
    public string CurrentScenario { get; private set; } = "assignment-runtime";

    private string sessionId = "";
    private int turnIndex = 0;

    // Component references
    private CharacterAnimationController animationController;
    private Animator motionAnimator;
    private BloodEffectController bloodEffectController;
    private BloodTextController bloodTextController;
    private Audio2FaceManager audio2FaceManager;
    public EmotionController emotionController;
    private bool hasLoggedMissingMotionTarget;
    private bool hasLoggedMissingFacialBridge;
    private int previousDirectMotionCode = -1;
    private const string TtsPath = "/tts";
    private static readonly string[] OriginalMotionTriggers =
    {
        "Neutral", "Confused", "Nod 1", "Nod 2", "Nod 3",
        "Nod 4", "Head Shake 1", "Head Shake 2", "Tap Table", "Struggling"
    };

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log($"[TTSManager] Instance assigned to '{BuildHierarchyPath(transform)}' (scene='{gameObject.scene.name}')");
            // If you need to maintain this during scene transitions, please uncomment the following line.
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            Debug.LogWarning(
                $"[TTSManager] Duplicate detected on '{BuildHierarchyPath(transform)}' (scene='{gameObject.scene.name}'). " +
                $"Keeping existing instance '{BuildHierarchyPath(Instance.transform)}' (scene='{Instance.gameObject.scene.name}') " +
                "and removing only the duplicate TTSManager component.");
            Destroy(this);
        }
    }

    private static string BuildHierarchyPath(Transform node)
    {
        if (node == null) return "<null>";

        string path = node.name;
        Transform current = node.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    void Start()
    {
        RuntimeSessionContext.Changed += HandleRuntimeContextChanged;
        ApplyRuntimeContextFromHost();

        // Get references to required components
        TryResolveMotionTargets();

        // Find the Audio2FaceManager if we're using it
        if (useAudio2Face)
        {
            audio2FaceManager = FindFirstObjectByType<Audio2FaceManager>();
            if (audio2FaceManager == null)
            {
                Debug.LogWarning("Audio2FaceManager not found in the scene. Audio2Face integration disabled.");
                useAudio2Face = false;
            }
            else
            {
                Debug.Log("Audio2Face integration enabled");
            }

        }

        if (!useBloodEffectController) return;

        // Find the blood effect in the UI
        bloodEffectController = FindFirstObjectByType<BloodEffectController>();
        if (bloodEffectController == null)
        {
            Debug.LogError("BloodEffectController not found in the scene. Make sure it exists in the UI!");
        }

        bloodTextController = FindFirstObjectByType<BloodTextController>();
        if (bloodTextController == null)
        {
            Debug.LogError("BloodTextController not found in the scene. Make sure it exists in the UI!");
        }

        if (emotionController == null)
        {
            Debug.LogError("EmotionController not found in the scene. Make sure it exists!");
        }
    }

    private void OnDestroy()
    {
        RuntimeSessionContext.Changed -= HandleRuntimeContextChanged;
    }

    private bool TryResolveMotionTargets()
    {
        if (animationController != null || motionAnimator != null)
        {
            return true;
        }

        animationController = GetComponent<CharacterAnimationController>();
        if (animationController == null)
        {
            animationController = GetComponentInChildren<CharacterAnimationController>();
        }
        if (animationController == null)
        {
            animationController = GetComponentInParent<CharacterAnimationController>();
        }
        if (animationController == null && emotionController != null && emotionController.animator != null)
        {
            animationController = emotionController.animator.GetComponent<CharacterAnimationController>();
        }
        if (animationController == null)
        {
            animationController = FindFirstObjectByType<CharacterAnimationController>();
        }

        if (motionAnimator == null)
        {
            motionAnimator = GetComponent<Animator>();
        }
        if (motionAnimator == null)
        {
            motionAnimator = GetComponentInChildren<Animator>();
        }
        if (motionAnimator == null)
        {
            motionAnimator = GetComponentInParent<Animator>();
        }
        if (motionAnimator == null && emotionController != null)
        {
            motionAnimator = emotionController.animator;
        }
        if (motionAnimator == null)
        {
            motionAnimator = FindFirstObjectByType<Animator>();
        }

        if (animationController == null && motionAnimator == null)
        {
            if (!hasLoggedMissingMotionTarget)
            {
                Debug.LogWarning("Cannot update motion: no CharacterAnimationController or Animator was found");
                hasLoggedMissingMotionTarget = true;
            }
            return false;
        }

        hasLoggedMissingMotionTarget = false;
        return true;
    }

    private FacialExpressionRuntimeBridge TryResolveFacialBridge()
    {
        if (emotionController == null)
        {
            emotionController = GetComponent<EmotionController>();
            if (emotionController == null)
                emotionController = GetComponentInChildren<EmotionController>(true);
            if (emotionController == null)
                emotionController = GetComponentInParent<EmotionController>();
            if (emotionController == null)
                emotionController = FindFirstObjectByType<EmotionController>();
        }

        if (emotionController == null)
            return null;

        if (emotionController.facialExpressionBridge != null)
            return emotionController.facialExpressionBridge;

        FacialExpressionRuntimeBridge bridge = emotionController.GetComponent<FacialExpressionRuntimeBridge>();
        if (bridge == null)
            bridge = emotionController.GetComponentInChildren<FacialExpressionRuntimeBridge>(true);
        if (bridge == null)
            bridge = emotionController.GetComponentInParent<FacialExpressionRuntimeBridge>();

        if (bridge != null && emotionController.facialExpressionBridge == null)
            emotionController.facialExpressionBridge = bridge;

        return bridge;
    }

    private void BeginSpeakingPresentation()
    {
        FacialExpressionRuntimeBridge bridge = TryResolveFacialBridge();
        if (bridge != null)
        {
            hasLoggedMissingFacialBridge = false;
            bridge.BeginSpeakingPresentation();
            return;
        }

        if (!hasLoggedMissingFacialBridge)
        {
            Debug.LogWarning("[TTSManager] Facial bridge not found; skipping speaking presentation transition.");
            hasLoggedMissingFacialBridge = true;
        }
    }

    private void RestoreIdlePresentation()
    {
        FacialExpressionRuntimeBridge bridge = TryResolveFacialBridge();
        if (bridge != null)
        {
            hasLoggedMissingFacialBridge = false;
            bridge.RestoreIdlePresentation();
            return;
        }

        if (!hasLoggedMissingFacialBridge)
        {
            Debug.LogWarning("[TTSManager] Facial bridge not found; skipping idle restoration.");
            hasLoggedMissingFacialBridge = true;
        }
    }

    public void ApplyLoginContext(string userId, int simulationLevel)
    {
        CurrentUserId = userId;
        CurrentScenario = RuntimeSessionContext.HasContext ? RuntimeSessionContext.ContextLabel : CurrentScenario;
        if (string.IsNullOrWhiteSpace(CurrentScenario))
            CurrentScenario = "assignment-runtime";
        sessionId = $"{CurrentUserId}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        turnIndex = 0;
        Debug.Log($"[TTSManager] Legacy local context applied. userID={CurrentUserId}, contextLabel={CurrentScenario}");
    }

    // Public method to be called to convert text to speech
    public void ConvertTextToSpeech(string text)
    {
        ConvertTextToSpeech(text, null);
    }

    // Optional motionCode supports structured dialogue outputs where code is separate from text.
    public void ConvertTextToSpeech(string text, int? motionCode)
    {
        ConvertTextToSpeech(text, motionCode, null);
    }

    public void ConvertTextToSpeech(string text, int? motionCode, int? resolvedTurnIndex)
    {
        if (string.IsNullOrEmpty(text))
        {
            Debug.Log("TTS Manager: No text provided for TTS");
            return;
        }

        Debug.Log($"TTS Manager: Processing text: '{text}'");
        StartCoroutine(ConvertTextToSpeechRoutine(text, motionCode, resolvedTurnIndex));
    }

    private IEnumerator ConvertTextToSpeechRoutine(string inputText, int? motionCode, int? resolvedTurnIndex)
    {
        if (!RuntimeSessionContext.HasRuntimeToken)
        {
            Debug.LogError("TTS Manager: Missing runtime token. The host app must inject runtime context before calling /tts.");
            yield break;
        }

        if (!ApiConfigProvider.TryBuildBackendUrl(TtsPath, out string endpoint))
        {
            Debug.LogError("TTS Manager: API environment config is missing or incomplete.");
            yield break;
        }

        int attempts = Mathf.Max(1, maxRetries);
        int requestTurnIndex = ReserveTurnIndex(resolvedTurnIndex);

        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            string requestId = $"{BuildRequestIdPrefix()}-tts-{requestTurnIndex}-try-{attempt}";
            string jsonContent = JsonConvert.SerializeObject(BuildTTSRequestPayload(inputText, requestTurnIndex));

            using (var request = new UnityWebRequest(endpoint, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonContent);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = Mathf.Max(5, requestTimeoutSeconds);
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accept", "application/json");
                request.SetRequestHeader("X-Request-ID", requestId);

                if (!RuntimeSessionContext.ApplyAuthorization(request, "TTSManager"))
                    yield break;

                Debug.Log("=== AWS TTS REQUEST ===");
                Debug.Log("POST endpoint: " + endpoint);
                Debug.Log("X-Request-ID: " + requestId);
                Debug.Log("Request body:\n" + jsonContent);

                yield return request.SendWebRequest();

                string responseBody = request.downloadHandler?.text ?? string.Empty;
                bool success = request.result == UnityWebRequest.Result.Success && request.responseCode == 200;
                Debug.Log($"Response Status: {request.responseCode}");

                if (success)
                {
                    Debug.Log("JSON response preview: " + responseBody.Substring(0, Math.Min(responseBody.Length, 500)));
                    AwsTTSResponse parsed = null;
                    try
                    {
                        parsed = JsonConvert.DeserializeObject<AwsTTSResponse>(responseBody);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to parse AWS /tts response JSON: {ex.Message}");
                    }

                    if (parsed?.AudioBase64 == null || parsed.Alignment == null)
                    {
                        Debug.LogError("TTS response missing audio data or alignment.");
                        yield break;
                    }

                    byte[] audioBytes = null;
                    try
                    {
                        audioBytes = Convert.FromBase64String(parsed.AudioBase64);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to decode audio_base64 from AWS /tts: {ex.Message}");
                        yield break;
                    }

                    var wordTimings = BuildWordTimings(parsed.Alignment);
                    if (wordTimings == null || wordTimings.Count == 0)
                    {
                        Debug.LogError("TTS response had empty or invalid alignment arrays.");
                        yield break;
                    }

                    Debug.Log($"[TTSManager] AWS TTS success: {audioBytes.Length} bytes, requestId={parsed.RequestId}");
                    ProcessAudioBytes(audioBytes, wordTimings, inputText, motionCode, requestTurnIndex);
                    yield break;
                }

                bool retryableResponse = IsRetryableResponse((int)request.responseCode, responseBody);
                ErrorEnvelope errorEnvelope = null;
                try { errorEnvelope = JsonConvert.DeserializeObject<ErrorEnvelope>(responseBody); } catch { }

                Debug.LogError(
                    $"[TTSManager] AWS /tts failed (attempt {attempt}/{attempts}). " +
                    $"status={(int)request.responseCode}, transport={request.error}, retryable={retryableResponse}, " +
                    $"requestId={errorEnvelope?.requestId ?? requestId}, body={responseBody}");

                if (!retryableResponse || attempt >= attempts)
                    break;
            }

            yield return new WaitForSeconds(0.35f * attempt);
        }

        Debug.LogError("TTS Manager: Failed to get audio data from AWS /tts");
    }

    private object BuildTTSRequestPayload(string inputText, int requestTurnIndex)
    {
        return new TTSRequestPayload
        {
            text = inputText,
            options = new TTSOptions
            {
                format = "pcm_16000",
                includeAlignment = true
            },
            metadata = new TTSMetadata
            {
                turnIndex = requestTurnIndex,
                client = "unity-webgl"
            }
        };
    }

    private static List<WordTiming> BuildWordTimings(Alignment alignment)
    {
        if (alignment?.Characters == null ||
            alignment.CharacterStartTimesSeconds == null ||
            alignment.CharacterEndTimesSeconds == null)
            return null;

        int count = Math.Min(
            alignment.Characters.Count,
            Math.Min(alignment.CharacterStartTimesSeconds.Count, alignment.CharacterEndTimesSeconds.Count));

        if (count == 0)
            return null;

        var timings = new List<WordTiming>(count);
        for (int i = 0; i < count; i++)
        {
            timings.Add(new WordTiming
            {
                Word = alignment.Characters[i],
                StartTime = alignment.CharacterStartTimesSeconds[i],
                EndTime = alignment.CharacterEndTimesSeconds[i]
            });
        }

        return timings;
    }

    private static bool IsRetryableResponse(int statusCode, string responseBody)
    {
        if (statusCode == 408 || statusCode == 425 || statusCode == 429 || statusCode >= 500)
            return true;

        if (string.IsNullOrWhiteSpace(responseBody))
            return false;

        try
        {
            var error = JsonConvert.DeserializeObject<ErrorEnvelope>(responseBody);
            return error != null && error.retryable;
        }
        catch
        {
            return false;
        }
    }

    private void AddWavHeaderAndSave(byte[] pcmData, string filePath, int sampleRate = 16000, int channels = 1)
    {
        using (var fileStream = new FileStream(filePath, FileMode.Create))
        using (var writer = new BinaryWriter(fileStream))
        {
            // Calculate sizes
            int dataSize = pcmData.Length;
            int fileSize = dataSize + 36; // 36 = size of WAV header minus 8 bytes

            // RIFF header
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(fileSize);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));

            // Format chunk
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16); // Chunk size
            writer.Write((short)1); // Audio format (1 = PCM)
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * 2); // Byte rate (SampleRate * NumChannels * BitsPerSample/8)
            writer.Write((short)(channels * 2)); // Block align (NumChannels * BitsPerSample/8)
            writer.Write((short)16); // Bits per sample

            // Data chunk
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);

            // Write the PCM data
            writer.Write(pcmData);
        }

        Debug.Log($"Saved WAV file with PCM data to: {filePath}");
    }


    // Method to process and play the audio bytes received
    private void ProcessAudioBytes(byte[] audioData, List<WordTiming> wordTimings, string messageContent, int? motionCode, int requestTurnIndex)
    {
        AudioClip audioClip = CreateAudioClipFromPcm16(audioData, 16000, 1, "tts_audio");
        if (audioClip == null)
        {
            Debug.LogError("Failed to create AudioClip from PCM data.");
            return;
        }

        StartCoroutine(PlayAudioClip(audioClip, wordTimings, messageContent, motionCode, requestTurnIndex));
    }

    private AudioClip CreateAudioClipFromPcm16(byte[] pcmData, int sampleRate, int channels, string clipName)
    {
        if (pcmData == null || pcmData.Length < 2 || (pcmData.Length % 2) != 0)
            return null;

        int totalSamples = pcmData.Length / 2;
        int sampleFrames = totalSamples / channels;
        if (sampleFrames <= 0)
            return null;

        float[] samples = new float[totalSamples];
        for (int i = 0; i < totalSamples; i++)
        {
            short sample = BitConverter.ToInt16(pcmData, i * 2);
            samples[i] = sample / 32768f;
        }

        AudioClip clip = AudioClip.Create(clipName, sampleFrames, channels, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // Coroutine to play audio clip directly from memory (WebGL-safe).
    private IEnumerator PlayAudioClip(AudioClip audioClip, List<WordTiming> wordTimings, string messageContent, int? motionCode, int requestTurnIndex)
    {
        if (audioSource == null)
        {
            Debug.LogError("TTS Manager: AudioSource is not assigned.");
            yield break;
        }

        audioSource.clip = audioClip;
        audioSource.Play();
        bool playbackStarted = false;
        float playbackStartDeadline = Time.realtimeSinceStartup + 1.0f;

        while (!playbackStarted)
        {
            if (audioSource.isPlaying)
            {
                playbackStarted = true;

                if (OpenAIRequest.Instance != null)
                    OpenAIRequest.Instance.ReportPatientSpeechStart(requestTurnIndex);

                if (emotionController == null)
                {
                    Debug.LogError("EmotionController not found in the scene. Make sure it exists!");
                }
                else
                {
                    emotionController.SyncAnimationsWithWordTimings(wordTimings);
                }

                BeginSpeakingPresentation();

                if (motionCode.HasValue)
                {
                    UpdateMotion(motionCode.Value);
                }
                else
                {
                    UpdateMotionFromLegacySuffix(messageContent);
                }

                break;
            }

            if (Time.realtimeSinceStartup >= playbackStartDeadline)
            {
                Debug.LogWarning($"[TTSManager] Audio playback never started for turnIndex={requestTurnIndex}. Leaving patient speech timing unset.");
                yield break;
            }

            yield return null;
        }

        while (audioSource != null && audioSource.isPlaying)
            yield return null;

        RestoreIdlePresentation();

        if (OpenAIRequest.Instance != null)
            OpenAIRequest.Instance.ReportPatientSpeechEnd(requestTurnIndex);

        Debug.Log("Audio playback completed");
    }

    [Serializable]
    public class TTSRequestPayload
    {
        public string text { get; set; }
        public TTSOptions options { get; set; }
        public TTSMetadata metadata { get; set; }
    }

    [Serializable]
    public class TTSOptions
    {
        public string format { get; set; }
        public bool includeAlignment { get; set; }
    }

    [Serializable]
    public class TTSMetadata
    {
        public int turnIndex { get; set; }
        public string client { get; set; }
    }

    [Serializable]
    public class AwsTTSResponse
    {
        [JsonProperty("audio_base64")]
        public string AudioBase64 { get; set; }

        [JsonProperty("alignment")]
        public Alignment Alignment { get; set; }

        [JsonProperty("normalized_alignment")]
        public Alignment NormalizedAlignment { get; set; }

        [JsonProperty("provider")]
        public string Provider { get; set; }

        [JsonProperty("requestId")]
        public string RequestId { get; set; }
    }

    [Serializable]
    public class Alignment
    {
        [JsonProperty("characters")]
        public List<string> Characters { get; set; }

        [JsonProperty("character_start_times_seconds")]
        public List<float> CharacterStartTimesSeconds { get; set; }

        [JsonProperty("character_end_times_seconds")]
        public List<float> CharacterEndTimesSeconds { get; set; }
    }

    [Serializable]
    public class WordTiming
    {
        [JsonProperty("word")]
        public string Word { get; set; }

        [JsonProperty("start")]
        public float StartTime { get; set; }

        [JsonProperty("end")]
        public float EndTime { get; set; }
    }

    [Serializable]
    public class ErrorEnvelope
    {
        public string error { get; set; }
        public string requestId { get; set; }
        public bool retryable { get; set; }
    }

    public void UpdateMotionFromLegacySuffix(string message)
    {
        if (!TryResolveMotionTargets()) return;

        Match match = Regex.Match(message ?? string.Empty, @"\[([0-9]|10)\]$");
        if (!match.Success)
        {
            Debug.Log($"No legacy animation code suffix found. Skipping TTSManager.UpdateMotionFromLegacySuffix for message: {message}");
            return;
        }

        int motionCode = int.Parse(match.Groups[1].Value);
        UpdateMotion(motionCode);
    }

    public void UpdateMotion(int motionCode)
    {
        if (!TryResolveMotionTargets()) return;

        if (animationController != null)
        {
            ApplyMotionViaController(motionCode);
            return;
        }

        ApplyMotionDirectly(motionCode);
    }

    private void ApplyMotionViaController(int motionCode)
    {
        if (animationController == null)
        {
            return;
        }

        switch (motionCode)
        {
            case 0:
                animationController.PlayIdle();
                break;
            case 1:
                animationController.PlayHeadPain();
                break;
            case 2:
                animationController.PlayHappy();
                break;
            case 3:
                animationController.PlayShrug();
                break;
            case 4:
                animationController.PlayHeadNod();
                break;
            case 5:
                animationController.PlayHeadShake();
                break;
            case 6:
                animationController.PlayWrithingInPain();
                break;
            case 7:
                animationController.PlaySad();
                break;
            case 8:
                animationController.PlayArmStretch();
                break;
            case 9:
                animationController.PlayNeckStretch();
                break;
            case 10:
                animationController.PlayBloodPressure();
                if (bloodEffectController != null)
                    bloodEffectController.SetBloodVisibility(true);
                if (bloodTextController != null)
                    bloodTextController.SetBloodTextVisibility(true);
                break;
            default:
                Debug.LogWarning($"Unsupported motion code: {motionCode}");
                break;
        }
    }

    private void ApplyMotionDirectly(int motionCode)
    {
        if (motionAnimator == null)
        {
            Debug.LogWarning("Cannot update motion directly: Animator is null");
            return;
        }

        if (motionCode < 0 || motionCode >= OriginalMotionTriggers.Length)
        {
            Debug.LogWarning($"Unsupported motion code for original mapping: {motionCode}");
            return;
        }

        if (previousDirectMotionCode >= 0 && previousDirectMotionCode < OriginalMotionTriggers.Length)
        {
            motionAnimator.ResetTrigger(OriginalMotionTriggers[previousDirectMotionCode]);
        }

        string trigger = OriginalMotionTriggers[motionCode];
        motionAnimator.SetTrigger(trigger);
        previousDirectMotionCode = motionCode;
    }

    // Backward-compatible wrappers for existing callers.
    public void UpdateAnimation(string message)
    {
        UpdateMotionFromLegacySuffix(message);
    }

    public void UpdateAnimation(int code)
    {
        UpdateMotion(code);
    }

    private void HandleRuntimeContextChanged()
    {
        ApplyRuntimeContextFromHost();
    }

    private void ApplyRuntimeContextFromHost()
    {
        if (!RuntimeSessionContext.HasContext)
            return;

        string previousSessionId = sessionId;
        CurrentUserId = string.IsNullOrWhiteSpace(RuntimeSessionContext.UserId) ? CurrentUserId : RuntimeSessionContext.UserId;
        CurrentScenario = RuntimeSessionContext.ContextLabel;
        if (string.IsNullOrWhiteSpace(CurrentScenario))
            CurrentScenario = "assignment-runtime";

        if (!string.IsNullOrWhiteSpace(RuntimeSessionContext.SessionId))
            sessionId = RuntimeSessionContext.SessionId;

        bool isNewSession = !string.IsNullOrWhiteSpace(sessionId) && !string.Equals(previousSessionId, sessionId, StringComparison.Ordinal);
        if (isNewSession || string.IsNullOrWhiteSpace(previousSessionId))
            turnIndex = 0;

        Debug.Log($"[TTSManager] Runtime context applied. sessionId={sessionId}, assignmentId={RuntimeSessionContext.AssignmentId}, sceneId={RuntimeSessionContext.SceneId}, contextLabel={CurrentScenario}");
    }

    private string BuildRequestIdPrefix()
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
            return sessionId;

        return string.IsNullOrWhiteSpace(CurrentUserId) ? "unity-webgl" : CurrentUserId;
    }

    private int ReserveTurnIndex(int? resolvedTurnIndex)
    {
        if (resolvedTurnIndex.HasValue && resolvedTurnIndex.Value > 0)
        {
            turnIndex = resolvedTurnIndex.Value;
            return turnIndex;
        }

        turnIndex++;
        return turnIndex;
    }

}
