using UnityEngine;
using TMPro;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System;
using UI.Cues.WarningSystem;

public class SpeechToTextController2 : MonoBehaviour
{
    [SerializeField] private CueSkipGuard cueSkipGuard;

    public TextMeshProUGUI transcriptText;
    private bool isRecording = false;
    private AudioClip recordedClip;

    // OpenAI API key (desktop/mobile only in Phase 1)
    private string openAiApiKey;

    private const int RecordingLengthSeconds = 30;
    private const int RecordingFrequency = 44100;

    private void Start()
    {
#if UNITY_WEBGL
        // WebGL note:
        // 1) Do not store/use OpenAI API keys in WebGL client builds.
        // 2) Microphone capture on WebGL should use a WebGL-compatible plugin and HTTPS hosting.
        if (transcriptText != null)
        {
            //transcriptText.text = "Hold R to speak. Release R to send.";
            transcriptText.text = "";
        }
#else
        LoadOpenAIApiKey();
#endif
    }

    private void Update()
    {
#if UNITY_WEBGL
        // WebGL: disable microphone hotkey in Phase 1.
        return;
#else
        if (Input.GetKeyDown(KeyCode.R))
        {
            StartRecording();
        }
        if (Input.GetKeyUp(KeyCode.R))
        {
            StopRecordingAndTranscribe();
        }
#endif
    }

    private void StartRecording()
    {
        if (!Phase1SessionInputGate.IsEnabled)
            return;
#if UNITY_WEBGL
        if (transcriptText != null)
        {
            transcriptText.text = "Web demo: microphone is not enabled yet.";
        }
        return;
#else
        if (!isRecording)
        {
            recordedClip = UnityEngine.Microphone.Start(null, false, RecordingLengthSeconds, RecordingFrequency);
            isRecording = true;
            Debug.Log("STT: Started recording...");
        }
#endif
    }

    public void StopRecordingAndTranscribe()
    {
#if UNITY_WEBGL
        if (transcriptText != null)
        {
            transcriptText.text = "Web demo: microphone is not enabled yet.";
        }
        return;
#else
        if (isRecording)
        {
            UnityEngine.Microphone.End(null);
            isRecording = false;
            Debug.Log("STT: Stopped recording, starting transcription...");
            _ = TranscribeAudio(); // Fire and forget
        }
#endif
    }

#if !UNITY_WEBGL
    // Desktop/Mobile transcription pipeline (Phase 1)
    private async Task TranscribeAudio()
    {
        // Validate API key
        if (string.IsNullOrEmpty(openAiApiKey))
        {
            Debug.LogError("STT: OpenAI API key not available. Cannot process transcription request.");
            if (transcriptText != null)
            {
                transcriptText.text = "Error: No API key";
            }

            if (OpenAIRequest.Instance != null)
            {
                OpenAIRequest.Instance.ReceiveNurseTranscription("Error in transcription", 0f);
            }
            return;
        }

        // Save WAV locally
        string filePath = Path.Combine(Application.persistentDataPath, "recordedAudio.wav");
        SavWav.Save("recordedAudio.wav", recordedClip);
        Debug.Log("STT: Saved audio to: " + filePath);

        // Send to Whisper
        WhisperResult speech = await SendToWhisperAPI(filePath, "whisper-1", "en", 0.2f);

        // Update UI
        if (transcriptText != null)
        {
            transcriptText.text = speech.text;
        }
        Debug.Log("STT: Transcription result: '" + speech.text + "' (WPM: " + speech.wpm + ")");

        if (cueSkipGuard != null)
            cueSkipGuard.CheckStudentUtterance(speech.text, "");
        else
            Debug.LogWarning("STT: CueSkipGuard reference is missing!");

        // Cleanup file
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            Debug.Log("STT: Deleted temporary audio file");
        }

        // Send to OpenAIRequest consumer
        await WaitForOpenAIRequestAndSend(speech.text, speech.wpm, null, null);
    }

    // Wait for OpenAIRequest instance and send transcription
    private async Task WaitForOpenAIRequestAndSend(string transcriptionText, float wpm, string userSpeechStartAt, string userSpeechEndAt)
    {
        float waitTime = 0f;
        float maxWaitTime = 5f;

        while (OpenAIRequest.Instance == null && waitTime < maxWaitTime)
        {
            await Task.Delay(100);
            waitTime += 0.1f;
        }

        if (OpenAIRequest.Instance != null)
        {
            Debug.Log("STT: Found OpenAIRequest instance, sending transcription...");
            OpenAIRequest.Instance.ReceiveNurseTranscription(transcriptionText, wpm, userSpeechStartAt, userSpeechEndAt);
            return;
        }

        Debug.LogError("STT: OpenAIRequest instance not found after waiting 5 seconds!");

        // Fallback: FindObjectOfType
        var openAIRequest = FindObjectOfType<OpenAIRequest>();
        if (openAIRequest != null)
        {
            Debug.Log("STT: Found OpenAIRequest via FindObjectOfType, sending transcription...");
            openAIRequest.ReceiveNurseTranscription(transcriptionText, wpm, userSpeechStartAt, userSpeechEndAt);
        }
        else
        {
            Debug.LogError("STT: No OpenAIRequest component found in the scene!");
        }
    }

    // Load API key from environment/config/inspector
    private void LoadOpenAIApiKey()
    {
        Debug.Log("=== STT OPENAI API KEY LOADING ===");

        // Method 1: Environment variable
        openAiApiKey = EnvironmentLoader.GetEnvVariable("OPENAI_API_KEY");
        if (!string.IsNullOrEmpty(openAiApiKey))
        {
            Debug.Log("STT OpenAI API key loaded from environment variables");
            return;
        }

        // Method 2: StreamingAssets config file (desktop/mobile)
        string configPath = Path.Combine(Application.streamingAssetsPath, "config.json");
        Debug.Log("STT Looking for config file at: " + configPath);

        if (File.Exists(configPath))
        {
            try
            {
                string configContent = File.ReadAllText(configPath);
                var config = JsonConvert.DeserializeObject<Dictionary<string, string>>(configContent);

                if (config != null && config.ContainsKey("openai_api_key"))
                {
                    openAiApiKey = config["openai_api_key"];
                    Debug.Log("STT OpenAI API key loaded from config file");
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("STT Error reading config file: " + e.Message);
            }
        }

        // Method 3: Inspector (kept for completeness)
        if (!string.IsNullOrEmpty(openAiApiKey))
        {
            Debug.Log("STT OpenAI API key found in Inspector");
            return;
        }

        Debug.LogError("STT No OpenAI API key found! Please set it via environment variable, config file, or Inspector");
    }

    private async Task<WhisperResult> SendToWhisperAPI(string filePath, string model, string language, float temperature)
    {
        // Note: using UnityWebRequest instead of HttpClient for broader Unity compatibility.
        if (string.IsNullOrEmpty(openAiApiKey))
        {
            Debug.LogError("STT: API key is null or empty");
            return new WhisperResult { text = "Error: No API key", wpm = 0f };
        }

        if (!File.Exists(filePath))
        {
            Debug.LogError("STT: Audio file not found: " + filePath);
            return new WhisperResult { text = "Error: Audio not found", wpm = 0f };
        }

        byte[] audioBytes = File.ReadAllBytes(filePath);

        // Use verbose_json to get segments for WPM
        List<IMultipartFormSection> formData = new List<IMultipartFormSection>
        {
            new MultipartFormDataSection("model", model),
            new MultipartFormDataSection("response_format", "verbose_json"),
            new MultipartFormDataSection("temperature", temperature.ToString()),
            new MultipartFormFileSection("file", audioBytes, Path.GetFileName(filePath), "audio/wav")
        };

        if (!string.IsNullOrEmpty(language))
        {
            formData.Add(new MultipartFormDataSection("language", language));
        }

        using (UnityWebRequest request = UnityWebRequest.Post("https://api.openai.com/v1/audio/transcriptions", formData))
        {
            request.SetRequestHeader("Authorization", "Bearer " + openAiApiKey);

            var op = request.SendWebRequest();
            while (!op.isDone)
            {
                await Task.Yield();
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseContent = request.downloadHandler.text;
                var transcriptionResponse = JsonConvert.DeserializeObject<TranscriptionResponse>(responseContent);

                if (transcriptionResponse == null)
                {
                    return new WhisperResult { text = "Error in transcription", wpm = 0f };
                }

                float wpm = 0f;
                if (transcriptionResponse.segments != null && transcriptionResponse.segments.Count > 0)
                {
                    float start = transcriptionResponse.segments[0].start;
                    float end = transcriptionResponse.segments[transcriptionResponse.segments.Count - 1].end;

                    float durationInMinutes = Mathf.Max((end - start) / 60f, 0.001f);
                    int wordCount = System.Text.RegularExpressions.Regex.Matches(transcriptionResponse.text ?? "", @"\b\w+\b").Count;
                    wpm = wordCount / durationInMinutes;
                }

                return new WhisperResult
                {
                    text = transcriptionResponse.text ?? "No transcription",
                    wpm = wpm
                };
            }

            string errorContent = request.downloadHandler != null ? request.downloadHandler.text : "";
            Debug.LogError("STT: Transcription failed: " + request.error + " - " + errorContent);
            return new WhisperResult { text = "Error in transcription", wpm = 0f };
        }
    }
#endif

    // Response DTOs
    private class TranscriptionResponse
    {
        public string text { get; set; }
        public List<Segment> segments { get; set; }
    }

    private class Segment
    {
        public float start { get; set; }
        public float end { get; set; }
        public string text { get; set; }
    }

    public class WhisperResult
    {
        public string text;
        public float wpm;
    }
}
