using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class WebGLTTSPlayer : MonoBehaviour
{
    [Header("Assign an AudioSource in the scene")]
    public AudioSource audioSource;

    // Call this from other scripts (e.g., WebGLTextBridge / MockResponder)
    public void PlayFromUrl(string audioUrl)
    {
        if (audioSource == null)
        {
            Debug.LogError("[WebGLTTSPlayer] AudioSource not assigned.");
            return;
        }

        if (string.IsNullOrEmpty(audioUrl))
        {
            Debug.LogError("[WebGLTTSPlayer] audioUrl is empty.");
            return;
        }

        Debug.Log("[WebGLTTSPlayer] Fetching audio: " + audioUrl);
        StartCoroutine(FetchAndPlay(audioUrl));
    }

    private IEnumerator FetchAndPlay(string audioUrl)
    {
        using (var req = UnityWebRequestMultimedia.GetAudioClip(audioUrl, AudioType.MPEG))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[WebGLTTSPlayer] Audio download failed: " + req.error);
                yield break;
            }

            var clip = DownloadHandlerAudioClip.GetContent(req);
            if (clip == null)
            {
                Debug.LogError("[WebGLTTSPlayer] Downloaded clip is null.");
                yield break;
            }

            audioSource.clip = clip;
            audioSource.Play();

            // Wait one frame for WebGL decode
            yield return null;
            yield return new WaitForSeconds(0.1f);   

            Debug.Log("[WebGLTTSPlayer] Playing audio. samples=" + clip.samples +
                      " freq=" + clip.frequency +
                      " length=" + clip.length);
        }
    }

    [ContextMenu("Test Local MP3")]
    public void TestLocalMP3()
    {
        PlayFromUrl("http://localhost:8000/ElevenLabs_test.mp3");
    }
}