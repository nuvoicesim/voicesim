using UnityEngine;
using TMPro;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using System;

public class SpeechToTextController2 : MonoBehaviour
{
    public TextMeshProUGUI transcriptText; // Reference to the Text or TextMeshPro field in the UI
    private bool isRecording = false;
    private AudioClip recordedClip;

    // Set your OpenAI API Key here
    private string openAiApiKey;

    void Start()
    {
        // 使用与其他组件一致的API密钥加载方式
        LoadOpenAIApiKey();
    }

    // 等待OpenAIRequest实例并发送转录结果
    private async Task WaitForOpenAIRequestAndSend(string transcriptionText, float wpm)
    {
        // 等待最多5秒来获取OpenAIRequest实例
        float waitTime = 0f;
        float maxWaitTime = 5f;

        while (OpenAIRequest.Instance == null && waitTime < maxWaitTime)
        {
            await Task.Delay(100); // 等待100ms
            waitTime += 0.1f;
        }

        if (OpenAIRequest.Instance != null)
        {
            Debug.Log("STT: Found OpenAIRequest instance, sending transcription...");
            OpenAIRequest.Instance.ReceiveNurseTranscription(transcriptionText, wpm);
        }
        else
        {
            Debug.LogError("STT: OpenAIRequest instance not found after waiting 5 seconds!");

            // 尝试通过FindObjectOfType查找
            var openAIRequest = FindObjectOfType<OpenAIRequest>();
            if (openAIRequest != null)
            {
                Debug.Log("STT: Found OpenAIRequest via FindObjectOfType, sending transcription...");
                openAIRequest.ReceiveNurseTranscription(transcriptionText, wpm);
            }
            else
            {
                Debug.LogError("STT: No OpenAIRequest component found in the scene!");
            }
        }
    }

    // 新的API密钥加载方法
    private void LoadOpenAIApiKey()
    {
        Debug.Log("=== STT OPENAI API KEY LOADING ===");

        // 方法1: 从环境变量加载
        openAiApiKey = EnvironmentLoader.GetEnvVariable("OPENAI_API_KEY");

        if (!string.IsNullOrEmpty(openAiApiKey))
        {
            Debug.Log("✓ STT OpenAI API key loaded from environment variables");
            return;
        }

        // 方法2: 从StreamingAssets配置文件加载
        string configPath = Path.Combine(Application.streamingAssetsPath, "config.json");
        Debug.Log($"STT Looking for config file at: {configPath}");

        if (File.Exists(configPath))
        {
            try
            {
                string configContent = File.ReadAllText(configPath);
                var config = JsonConvert.DeserializeObject<Dictionary<string, string>>(configContent);

                if (config != null && config.ContainsKey("openai_api_key"))
                {
                    openAiApiKey = config["openai_api_key"];
                    Debug.Log("✓ STT OpenAI API key loaded from config file");
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"STT Error reading config file: {e.Message}");
            }
        }

        // 方法3: 检查是否直接在Inspector中设置了
        if (!string.IsNullOrEmpty(openAiApiKey))
        {
            Debug.Log("✓ STT OpenAI API key found in Inspector");
            return;
        }

        Debug.LogError("✗ STT No OpenAI API key found! Please set it via environment variable, config file, or Inspector");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            StartRecording();
        }
        if (Input.GetKeyUp(KeyCode.R))
        {
            StopRecordingAndTranscribe();
        }
    }

    private void StartRecording()
    {
        if (!isRecording)
        {
            recordedClip = Microphone.Start(null, false, 30, 44100);
            isRecording = true;
            Debug.Log("STT: Started recording...");
        }
    }

    public void StopRecordingAndTranscribe()
    {
        if (isRecording)
        {
            Microphone.End(null);
            isRecording = false;
            Debug.Log("STT: Stopped recording, starting transcription...");
            _ = TranscribeAudio(); // Fire and forget the async task
        }
    }

    private async Task TranscribeAudio()
    {
        // 检查API密钥
        if (string.IsNullOrEmpty(openAiApiKey))
        {
            Debug.LogError("STT: OpenAI API key not available. Cannot process transcription request.");
            transcriptText.text = "Error: No API key";

            // 仍然发送错误消息给OpenAI系统
            if (OpenAIRequest.Instance != null)
            {
                OpenAIRequest.Instance.ReceiveNurseTranscription("Error in transcription", 0f);
            }
            return;
        }

        // Save the AudioClip as a WAV file using SavWav
        string filePath = Path.Combine(Application.persistentDataPath, "recordedAudio.wav");
        SavWav.Save("recordedAudio.wav", recordedClip);
        Debug.Log($"STT: Saved audio to: {filePath}");

        Debug.Log("STT: Sending audio to Whisper API...");

        // Send the WAV file to OpenAI Whisper API
        WhisperResult speech = await SendToWhisperAPI(filePath, "whisper-1", "en", 0.2f);

        // Display only the transcription text
        transcriptText.text = speech.text;
        Debug.Log($"STT: Transcription result: '{speech.text}' (WPM: {speech.wpm})");

        // Optionally delete the temporary file
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            Debug.Log("STT: Deleted temporary audio file");
        }

        // Fiona update 11/13: integrate with patient NPC
        await WaitForOpenAIRequestAndSend(speech.text, speech.wpm);
    }

    private async Task<WhisperResult> SendToWhisperAPI(string filePath, string model, string language, float temperature)
    {
        using (HttpClient client = new HttpClient())
        {
            try
            {
                // 验证API密钥
                if (string.IsNullOrEmpty(openAiApiKey))
                {
                    Debug.LogError("STT: API key is null or empty");
                    return new WhisperResult { text = "Error: No API key", wpm = 0f };
                }

                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + openAiApiKey);

                Debug.Log("=== STT WHISPER API REQUEST ===");
                Debug.Log($"API Key preview: {openAiApiKey.Substring(0, Math.Min(10, openAiApiKey.Length))}...");
                Debug.Log($"File path: {filePath}");
                Debug.Log($"File exists: {File.Exists(filePath)}");

                using (var form = new MultipartFormDataContent())
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    var audioContent = new StreamContent(fileStream);
                    audioContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
                    form.Add(audioContent, "file", Path.GetFileName(filePath));
                    form.Add(new StringContent(model), "model");

                    if (!string.IsNullOrEmpty(language))
                        form.Add(new StringContent(language), "language");

                    form.Add(new StringContent("verbose_json"), "response_format");
                    form.Add(new StringContent(temperature.ToString()), "temperature");

                    HttpResponseMessage response = await client.PostAsync("https://api.openai.com/v1/audio/transcriptions", form);

                    Debug.Log($"STT Response Status: {response.StatusCode}");

                    if (response.IsSuccessStatusCode)
                    {
                        Debug.Log("✓ STT Whisper API success");

                        // Parse the JSON response and extract only the "text" field
                        var responseContent = await response.Content.ReadAsStringAsync();
                        Debug.Log($"STT Response length: {responseContent.Length}");

                        var transcriptionResponse = JsonConvert.DeserializeObject<TranscriptionResponse>(responseContent);

                        // check for missing or empty segments
                        if (transcriptionResponse.segments == null || transcriptionResponse.segments.Count == 0)
                        {
                            Debug.LogWarning("STT: No segments received from Whisper.");
                            return new WhisperResult
                            {
                                text = transcriptionResponse.text ?? "No transcription",
                                wpm = 0f
                            };
                        }

                        // Calculate the speech rate
                        float start = transcriptionResponse.segments[0].start;
                        float end = transcriptionResponse.segments[transcriptionResponse.segments.Count - 1].end;

                        float durationInMinutes = Mathf.Max((end - start) / 60f, 0.001f); // avoid division by zero

                        int wordCount = System.Text.RegularExpressions.Regex.Matches(transcriptionResponse.text ?? "", @"\b\w+\b").Count;
                        float wpm = wordCount / durationInMinutes;

                        Debug.Log($"STT: Calculated WPM: {wpm} (words: {wordCount}, duration: {durationInMinutes} min)");

                        return new WhisperResult
                        {
                            text = transcriptionResponse.text ?? "No transcription",
                            wpm = wpm
                        };
                    }
                    else
                    {
                        string errorContent = await response.Content.ReadAsStringAsync();
                        Debug.LogError($"STT: Transcription failed: {response.ReasonPhrase} - {errorContent}");
                        return new WhisperResult { text = "Error in transcription", wpm = 0f };
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"STT: Exception in SendToWhisperAPI: {e.Message}");
                Debug.LogError($"STT: Stack trace: {e.StackTrace}");
                return new WhisperResult { text = "Error in transcription", wpm = 0f };
            }
        }
    }

    // Define a class to represent the JSON response structure
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