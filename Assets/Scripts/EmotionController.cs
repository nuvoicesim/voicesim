using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
//using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Serialization;
using UnityEngine.Timeline;

public class EmotionController : MonoBehaviour
{
    //public PlayableDirector director;
    public Animator animator;
    
    [Header("Debug Settings")]
    public bool setEmotionCode = false;
    public bool setMotionCode = false;
    public bool disableMotion = false;
    public int currentEmotionCode;
    public int currentMotionCode;
    public string[] emotionNames = {"Neutral", "Discomfort", "Happy", "Pain", "Sad", "Anger", "Frustrated", "Thinking", "Apologetic", "Cry"};
    public string[] motionNames = { "Neutral", "Confused", "Nod 1", "Nod 2", "Nod 3", "Nod 4", "Head Shake 1", "Head Shake 2", "Tap Table", "Struggling"};

    [Header("Facial Expression Integration")]
    public FacialExpressionRuntimeBridge facialExpressionBridge;
    
    private List<TrackAsset> allTracks = new();
    private List<TTSManager.WordTiming> charTimings;
    private List<TTSManager.WordTiming> wordTimings;
    private int previousEmotionCode = 0;
    private int previousMotionCode = 0;
    private TimelineAsset cachedTimeline;
    private bool hasLoggedMissingTimeline;
    private bool hasLoggedMissingFacialBridge;

    private static bool DirectorHasUsableTracks(PlayableDirector candidate)
    {
        if (candidate == null) return false;
        TimelineAsset timeline = candidate.playableAsset as TimelineAsset;
        if (timeline == null) return false;
        return timeline.GetOutputTracks().Any();
    }

    private PlayableDirector ResolveFallbackDirector()
    {
        var candidates = FindObjectsOfType<PlayableDirector>();

        foreach (var candidate in candidates)
        {
            if (DirectorHasUsableTracks(candidate))
            {
                return candidate;
            }
        }

        // Fallback to any director if none expose tracks yet.
        return candidates.FirstOrDefault();
    }
    /*
    private bool TryEnsureTimelineTracks(string context)
    {
        
        if (director == null)
        {
            director = GetComponent<PlayableDirector>();
            if (director == null) director = GetComponentInChildren<PlayableDirector>();
            if (director == null) director = GetComponentInParent<PlayableDirector>();
            if (director == null) director = ResolveFallbackDirector();
        }

        if (director == null)
        {
            if (!hasLoggedMissingTimeline)
            {
                Debug.LogError($"PlayableDirector not assigned in {context}.");
                hasLoggedMissingTimeline = true;
            }
            return false;
        }
        

        TimelineAsset timeline = director.playableAsset as TimelineAsset;
        if (timeline == null)
        {
            if (!hasLoggedMissingTimeline)
            {
                Debug.LogError($"No TimelineAsset assigned to PlayableDirector in {context}.");
                hasLoggedMissingTimeline = true;
            }
            return false;
        }

        if (cachedTimeline != timeline || allTracks == null || allTracks.Count == 0)
        {
            cachedTimeline = timeline;
            allTracks = timeline.GetOutputTracks().ToList();
        }

        if (allTracks == null || allTracks.Count == 0)
        {
            if (!hasLoggedMissingTimeline)
            {
                Debug.LogError($"Timeline '{timeline.name}' has no output tracks in {context}.");
                hasLoggedMissingTimeline = true;
            }
            return false;
        }

        hasLoggedMissingTimeline = false;
        return true;
       
    }

    private int ClampTrackIndex(int requestedIndex, string context)
    {
        if (!TryEnsureTimelineTracks(context))
        {
            return -1;
        }

        int clampedIndex = Mathf.Clamp(requestedIndex, 0, allTracks.Count - 1);
        if (clampedIndex != requestedIndex)
        {
            Debug.LogWarning($"Track index {requestedIndex} out of bounds in {context}. Auto-corrected to {clampedIndex}.");
        }

        return clampedIndex;
    }
    */

    void Start()
    {
        //bool timelineReady = TryEnsureTimelineTracks("Start");

        TryResolveFacialBridge();

        //Debug.Log($"[EmotionController] Startup: initialized on '{name}'. timelineReady={timelineReady}, trackCount={(allTracks != null ? allTracks.Count : 0)}");
        Debug.Log($"[EmotionController] Startup: facial bridge {(facialExpressionBridge != null ? "found" : "not found")}");
    }

    private bool TryResolveFacialBridge()
    {
        if (facialExpressionBridge != null)
        {
            return true;
        }

        facialExpressionBridge = GetComponent<FacialExpressionRuntimeBridge>();
        if (facialExpressionBridge == null)
        {
            facialExpressionBridge = GetComponentInChildren<FacialExpressionRuntimeBridge>(true);
        }
        if (facialExpressionBridge == null)
        {
            facialExpressionBridge = GetComponentInParent<FacialExpressionRuntimeBridge>();
        }

        return facialExpressionBridge != null;
    }
    
    public void SyncAnimationsWithWordTimings(List<TTSManager.WordTiming> timings)
    {
        charTimings = timings;
        wordTimings = BuildWordsFromCharTimings(charTimings);
        StartCoroutine(TriggerAnimationsWithTiming());
    }

    private List<TTSManager.WordTiming> BuildWordsFromCharTimings(List<TTSManager.WordTiming> timings)
    {
        List<TTSManager.WordTiming> words = new List<TTSManager.WordTiming>();
        if (timings == null || timings.Count == 0)
        {
            Debug.LogWarning("No character timings provided.");
            return words;
        }

        TTSManager.WordTiming currentWord = null;
        foreach (var timing in timings)
        {
            if (timing.Word == "... ")
            {
                continue;
            }

            if (currentWord == null || timing.Word.EndsWith(" "))
            {
                currentWord = new TTSManager.WordTiming
                {
                    Word = timing.Word.Trim(),
                    StartTime = timing.StartTime,
                    EndTime = timing.EndTime
                };
                words.Add(currentWord);
            }
            else
            {
                currentWord.Word += timing.Word;
                currentWord.EndTime = timing.EndTime;
            }
        }

        Debug.Log($"Built {words.Count} words from character timings.");
        return words;
    }


        private IEnumerator TriggerAnimationsWithTiming()
    {
        if (wordTimings == null || wordTimings.Count == 0)
        {
            yield break;
        }

        foreach (var wordTiming in wordTimings)
        {
            float delay = wordTiming.EndTime - wordTiming.StartTime;
            //Debug.Log($"Triggering animation for word: {wordTiming.Word} after delay: {delay}");
            yield return new WaitForSeconds(delay);
            
            int emotionCode = MapWordToEmotion(wordTiming.Word);
            /*
            if (emotionCode != 0)
            {
                
                int mappedIndex = ClampTrackIndex(emotionCode, "TriggerAnimationsWithTiming(mapped emotion)");
                if (mappedIndex < 0)
                {
                    yield break;
                }
                

                TrackAsset selectedTrack = allTracks[mappedIndex];
                foreach (var track in allTracks.Where(track => track.name != "Blink Track"))
                {
                    track.muted = (track != selectedTrack);
                }

                PlayEmotion();

                var animationDelay = selectedTrack.duration;
                yield return new WaitForSeconds((float)animationDelay);
                
                int currentIndex = ClampTrackIndex(currentEmotionCode, "TriggerAnimationsWithTiming(current emotion)");
                if (currentIndex < 0)
                {
                    yield break;
                }

                selectedTrack = allTracks[currentIndex];
                foreach (var track in allTracks.Where(track => track.name != "Blink Track"))
                {
                    track.muted = (track != selectedTrack);
                }
                PlayEmotion();
            }
            
            //float delay = wordTiming.EndTime - wordTiming.StartTime;
            //Debug.Log($"Triggering animation for word: {wordTiming.Word} after delay: {delay}");
            //yield return new WaitForSeconds(delay);
            */

        }

    }


    private int MapWordToEmotion(string word)
    {
        if (word.ToLower().Contains("thanks")) return 2;
        if (word.ToLower().Contains("thank")) return 2;
        return 0; 
    }
    
    
    public void HandleEmotionCode(int emotionCode, int motionCode)
    {
        if (!disableMotion)
        {
            animator.ResetTrigger(motionNames[previousMotionCode]);
            Debug.Log("Reset trigger: " + motionNames[previousMotionCode]);
        }

        previousEmotionCode = emotionCode;
        previousMotionCode = motionCode;
        
        if (!setEmotionCode) { currentEmotionCode = emotionCode;}
        if (!setMotionCode) { currentMotionCode = motionCode; }

        if (TryResolveFacialBridge())
        {
            hasLoggedMissingFacialBridge = false;
            Debug.Log($"[EmotionController] Forwarding to facial bridge: emotionCode={currentEmotionCode}, motionCode={currentMotionCode}");
            facialExpressionBridge.HandleEmotionAndMotion(currentEmotionCode, currentMotionCode);
        }
        else if (!hasLoggedMissingFacialBridge)
        {
            Debug.LogWarning($"[EmotionController] Facial bridge not found on '{name}'.");
            hasLoggedMissingFacialBridge = true;
        }
        /*
        int validEmotionIndex = ClampTrackIndex(currentEmotionCode, "HandleEmotionCode");
        if (validEmotionIndex < 0)
        {
            return;
        }
        currentEmotionCode = validEmotionIndex;
        */
        
        TrackAsset selectedTrack = allTracks[currentEmotionCode];
        
        Debug.Log("Emotion Code: " + emotionCode);
        Debug.Log("Motion Code: " + motionCode);
        Debug.Log($"Selected track: {selectedTrack.name}");

        foreach (var track in allTracks.Where(track => track.name != "Blink Track"))
        {
            track.muted = (track != selectedTrack);
        }
        
        if (!disableMotion)
        {
            animator.SetTrigger(motionNames[currentMotionCode]);
            Debug.Log("Set trigger: " + motionNames[currentMotionCode]);
        }
        
    }
    /*
    public void PlayEmotion()
    {
        if (!TryEnsureTimelineTracks("PlayEmotion")) return;

        director.RebuildGraph();
        director.Play();
    }
    */
}
