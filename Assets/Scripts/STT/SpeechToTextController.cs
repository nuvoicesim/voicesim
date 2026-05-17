using UnityEngine;
using TMPro;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using UnityEngine.Networking;
using Newtonsoft.Json;

public class SpeechToTextController : MonoBehaviour
{
    public TextMeshProUGUI transcriptText;
    private bool isRecording = false;
    private AudioClip recordedClip;

    private string openAiApiKey;

    private const int RecordingLengthSeconds = 10;
    private const int RecordingFrequency = 44100;

    private void Start()
    {
#if UNITY_WEBGL
        // WebGL note:
        // 1) Do not store/use OpenAI keys in WebGL client builds.
        // 2) Microphone capture on WebGL should use a WebGL-compatible plugin and HTTPS hosting.
        if (transcriptText != null)
        {
            transcriptText.text = "Web demo: voice input is not enabled yet. Please use text input.";
        }
#else
        // Desktop/Mobile: keep existing behavior for now.
        // Security note: client-side API keys are risky. Prefer server proxy for production.
        openAiApiKey = EnvironmentLoader.GetEnvVariable("OPENAI_API_KEY");
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
            _ = TranscribeAudio(); // Fire and forget
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
            string filePath = Path.Combine(Application.persistentDataPath, "recordedAudio.wav");
            SavWav.Save("recordedAudio.wav", recordedClip);

            string transcription = await SendToWhisperAPI(filePath, "whisper-1", "en", "json", 0.2f);

            if (transcriptText != null)
            {
                transcriptText.text = transcription;
            }

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            // Integrate with patient NPC (keep your original behavior)
            if (sitPatientSpeech.Instance != null)
            {
                sitPatientSpeech.Instance.ReceiveNurseTranscription(transcription);
            }
            else
            {
                Debug.LogError("sitPatientSpeech instance not found.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error in transcription: " + e.Message);
            if (transcriptText != null)
            {
                transcriptText.text = "Error in transcription";
            }
        }
#endif
    }

    private async Task<string> SendToWhisperAPI(string filePath, string model, string language, string responseFormat, float temperature)
    {
#if UNITY_WEBGL
        // WebGL: disabled in Phase 1
        await Task.CompletedTask;
        return "Web demo: voice input is not enabled yet.";
#else
        if (string.IsNullOrEmpty(openAiApiKey))
        {
            Debug.LogError("OPENAI_API_KEY is missing.");
            return "Error in transcription";
        }

        if (!File.Exists(filePath))
        {
            Debug.LogError("Audio file not found: " + filePath);
            return "Error in transcription";
        }

        byte[] audioBytes = File.ReadAllBytes(filePath);

        List<IMultipartFormSection> formData = new List<IMultipartFormSection>
        {
            new MultipartFormDataSection("model", model),
            new MultipartFormDataSection("response_format", responseFormat),
            new MultipartFormDataSection("temperature", temperature.ToString())
        };

        if (!string.IsNullOrEmpty(language))
        {
            formData.Add(new MultipartFormDataSection("language", language));
        }

        formData.Add(new MultipartFormFileSection("file", audioBytes, Path.GetFileName(filePath), "audio/wav"));

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
                var responseContent = request.downloadHandler.text;
                var transcriptionResponse = JsonConvert.DeserializeObject<TranscriptionResponse>(responseContent);
                return transcriptionResponse != null ? transcriptionResponse.text : "";
            }

            string errorContent = request.downloadHandler != null ? request.downloadHandler.text : "";
            Debug.LogError("Transcription failed: " + request.error + " - " + errorContent);
            return "Error in transcription";
        }
#endif
    }

    private class TranscriptionResponse
    {
        public string text { get; set; }
    }
}
