using UnityEngine;
using TMPro;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using UnityEngine.Networking;
using Newtonsoft.Json;

public class STTController : MonoBehaviour
{
    public TextMeshProUGUI transcriptText;

    private bool isRecording = false;
    private AudioClip recordedClip;
    private BodyMove bodyMove;
    private string openAiApiKey;
    private string lastTranscription;

    private const int RecordingLengthSeconds = 10;
    private const int RecordingFrequency = 44100;

    private void Start()
    {
        Debug.Log("[STTController] Start() ran. Platform=" + Application.platform + " GO=" + gameObject.name);
        bodyMove = FindObjectOfType<BodyMove>();

#if UNITY_WEBGL
        // WebGL note:
        // 1) Do not store or use OpenAI API keys in WebGL client builds.
        // 2) Microphone capture may require a WebGL-specific plugin and HTTPS hosting.
        Debug.Log("[STTController] UNITY_WEBGL branch entered.");
        if (transcriptText != null)
        {
            transcriptText.text = "Hold R to speak, release R to send.";
        }
#else
        // Desktop/Mobile: keep your existing approach for now.
        // Security note: API keys in client apps are still risky. Prefer server proxy whenever possible.
        openAiApiKey = EnvironmentLoader.GetEnvVariable("OPENAI_API_KEY");
#endif
    }

    private void Update()
    {
#if UNITY_WEBGL
        // WebGL: do nothing for microphone hotkey in this phase.
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

            if (transcriptText != null)
            {
                transcriptText.text = "Recording...";
            }
        }
#endif
    }

    private void StopRecordingAndTranscribe()
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

            if (transcriptText != null)
            {
                transcriptText.text = "Processing...";
            }

            _ = TranscribeAudio();
        }
#endif
    }

    private async Task TranscribeAudio()
    {
#if UNITY_WEBGL
        // WebGL: disabled in Phase 1
        await Task.CompletedTask;
        return;
#else
        try
        {
            // Save WAV locally (desktop/mobile path)
            string filePath = Path.Combine(Application.persistentDataPath, "recordedAudio.wav");
            SavWav.Save("recordedAudio.wav", recordedClip);

            lastTranscription = await SendToWhisperAPI(filePath, "whisper-1");

            if (transcriptText != null)
            {
                transcriptText.text = "You: " + lastTranscription;
            }

            if (bodyMove != null)
            {
                bodyMove.PlayerResponds(lastTranscription);
            }

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error in transcription: " + e.Message);
            if (transcriptText != null)
            {
                transcriptText.text = "Error in transcription. Please try again.";
            }
        }
#endif
    }

    private async Task<string> SendToWhisperAPI(string filePath, string model)
    {
#if UNITY_WEBGL
        // WebGL: disabled in Phase 1
        await Task.CompletedTask;
        throw new System.Exception("WebGL build: Whisper API is disabled in this phase.");
#else
        if (string.IsNullOrEmpty(openAiApiKey))
        {
            throw new System.Exception("OPENAI_API_KEY is missing.");
        }

        if (!File.Exists(filePath))
        {
            throw new System.Exception("Audio file not found: " + filePath);
        }

        byte[] audioBytes = File.ReadAllBytes(filePath);

        // UnityWebRequest multipart form
        List<IMultipartFormSection> formData = new List<IMultipartFormSection>
        {
            new MultipartFormDataSection("model", model),
            new MultipartFormDataSection("language", "en"),
            new MultipartFormDataSection("response_format", "json"),
            new MultipartFormDataSection("temperature", "0.2"),
            new MultipartFormFileSection("file", audioBytes, Path.GetFileName(filePath), "audio/wav")
        };

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
                return transcriptionResponse != null ? transcriptionResponse.text : "";
            }

            string errorContent = request.downloadHandler != null ? request.downloadHandler.text : "";
            Debug.LogError("Transcription failed: " + request.error + " - " + errorContent);
            throw new System.Exception("Failed to transcribe audio");
        }
#endif
    }

    private class TranscriptionResponse
    {
        public string text { get; set; }
    }
}
