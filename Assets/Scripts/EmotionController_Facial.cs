using System;
using System.Collections;
using System.Collections.Generic;
// Legacy timeline helper import kept visible for side-by-side review.
//using System.Linq;
//using UnityEditor.Animations;
using UnityEngine;
// Legacy timeline namespaces are intentionally commented out in this candidate.
//using UnityEngine.Playables;
using UnityEngine.Serialization;
//using UnityEngine.Timeline;

// Review-only candidate for side-by-side comparison with EmotionController.cs.
// Active facial runtime path is kept live; older timeline and trigger-driven sections are commented below.
public class EmotionController_Facial : MonoBehaviour
{
    // Legacy overlap: the original script exposes an Animator for trigger-based motion.
    // Kept here as a review marker because the original integration still references this field.
    public Animator animator;
    
    [Header("Debug Settings")]
    // These overrides still affect the active facial runtime path because the bridge consumes these values.
    public bool setEmotionCode = false;
    public bool setMotionCode = false;
    // Legacy motion toggle kept for comparison; trigger-based motion blocks are commented out below.
    public bool disableMotion = false;
    public int currentEmotionCode;
    public int currentMotionCode;
    // Legacy/debug labels from the older animation path. Kept for inspector parity during review.
    public string[] emotionNames = {"Neutral", "Discomfort", "Happy", "Pain", "Sad", "Anger", "Frustrated", "Thinking", "Apologetic", "Cry"};
    public string[] motionNames = { "Neutral", "Confused", "Nod 1", "Nod 2", "Nod 3", "Nod 4", "Head Shake 1", "Head Shake 2", "Tap Table", "Struggling"};

    [Header("Facial Expression Integration")]
    // Active path: OpenAIRequest -> EmotionController -> FacialExpressionRuntimeBridge.
    public FacialExpressionRuntimeBridge facialExpressionBridge;
    
    /*
    // Legacy timeline track state. The current facial runtime path no longer drives Timeline tracks.
    private List<TrackAsset> allTracks = new();
    */
    // Still used by the active TTS entry point, even though the old per-word timeline playback is disabled.
    private List<TTSManager.WordTiming> charTimings;
    private List<TTSManager.WordTiming> wordTimings;
    // Kept for structure parity with the original. These were previously used by the trigger-based motion path.
    private int previousEmotionCode = 0;
    private int previousMotionCode = 0;
    /*
    // Legacy timeline cache state. Not part of the current facial bridge path.
    private TimelineAsset cachedTimeline;
    private bool hasLoggedMissingTimeline;
    */
    private bool hasLoggedMissingFacialBridge;

    /*
    // Legacy timeline helper. Not part of the current facial-expression core path.
    private static bool DirectorHasUsableTracks(PlayableDirector candidate)
    {
        if (candidate == null) return false;
        TimelineAsset timeline = candidate.playableAsset as TimelineAsset;
        if (timeline == null) return false;
        return timeline.GetOutputTracks().Any();
    }

    // Legacy timeline fallback lookup. Not part of the current facial-expression core path.
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
    */
    /*
    // Legacy timeline initialization kept commented for side-by-side review.
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

    // Legacy timeline clamp helper kept commented for side-by-side review.
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
        // Legacy timeline startup intentionally not used in this candidate.
        //bool timelineReady = TryEnsureTimelineTracks("Start");

        // Active facial path: resolve the bridge that applies expressions at runtime.
        TryResolveFacialBridge();

        //Debug.Log($"[EmotionController_Facial] Startup: initialized on '{name}'. timelineReady={timelineReady}, trackCount={(allTracks != null ? allTracks.Count : 0)}");
        Debug.Log($"[EmotionController_Facial] Startup: facial bridge {(facialExpressionBridge != null ? "found" : "not found")}");
    }

    private bool TryResolveFacialBridge()
    {
        // Active facial path: resolve the bridge on self/children/parent just like the original script.
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
        // This entry point is still called by TTSManager in the current runtime.
        // Keep it active for compatibility, but route it away from the legacy timeline path.
        charTimings = timings;
        wordTimings = BuildWordsFromCharTimings(charTimings);
        StartCoroutine(TriggerAnimationsWithTiming());
    }

    private List<TTSManager.WordTiming> BuildWordsFromCharTimings(List<TTSManager.WordTiming> timings)
    {
        // Still active because SyncAnimationsWithWordTimings uses it to normalize timing input.
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
            
            // Legacy breadcrumb: this mapping used to feed the older per-word timeline path.
            int emotionCode = MapWordToEmotion(wordTiming.Word);
            if (emotionCode != 0)
            {
                // The current facial bridge path does not switch timeline tracks here.
            }

            /*
            // Legacy timeline playback block kept commented for side-by-side review.
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
        // Legacy helper retained for review. The current facial bridge path does not use this result directly.
        if (word.ToLower().Contains("thanks")) return 2;
        if (word.ToLower().Contains("thank")) return 2;
        return 0; 
    }
    
    
    public void HandleEmotionCode(int emotionCode, int motionCode)
    {
        /*
        // Legacy trigger-reset path from the older Animator-driven motion system.
        if (!disableMotion)
        {
            animator.ResetTrigger(motionNames[previousMotionCode]);
            Debug.Log("Reset trigger: " + motionNames[previousMotionCode]);
        }
        */

        // Kept active to preserve the original runtime state updates for comparison.
        previousEmotionCode = emotionCode;
        previousMotionCode = motionCode;
        
        if (!setEmotionCode) { currentEmotionCode = emotionCode;}
        if (!setMotionCode) { currentMotionCode = motionCode; }

        // Active facial path: forward the resolved codes into FacialExpressionRuntimeBridge.
        if (TryResolveFacialBridge())
        {
            hasLoggedMissingFacialBridge = false;
            Debug.Log($"[EmotionController_Facial] Forwarding to facial bridge: emotionCode={currentEmotionCode}, motionCode={currentMotionCode}");
            facialExpressionBridge.HandleEmotionAndMotion(currentEmotionCode, currentMotionCode);
        }
        else if (!hasLoggedMissingFacialBridge)
        {
            Debug.LogWarning($"[EmotionController_Facial] Facial bridge not found on '{name}'.");
            hasLoggedMissingFacialBridge = true;
        }
        /*
        // Legacy timeline clamp path kept commented for review.
        int validEmotionIndex = ClampTrackIndex(currentEmotionCode, "HandleEmotionCode");
        if (validEmotionIndex < 0)
        {
            return;
        }
        currentEmotionCode = validEmotionIndex;
        */
        
        /*
        // Legacy timeline track playback path. This is not part of the current facial bridge path.
        TrackAsset selectedTrack = allTracks[currentEmotionCode];
        
        Debug.Log("Emotion Code: " + emotionCode);
        Debug.Log("Motion Code: " + motionCode);
        Debug.Log($"Selected track: {selectedTrack.name}");

        foreach (var track in allTracks.Where(track => track.name != "Blink Track"))
        {
            track.muted = (track != selectedTrack);
        }
        */
        
        /*
        // Legacy trigger-based motion path. Body motion is now handled outside this facial bridge flow.
        if (!disableMotion)
        {
            animator.SetTrigger(motionNames[currentMotionCode]);
            Debug.Log("Set trigger: " + motionNames[currentMotionCode]);
        }
        */
        
    }
    /*
    // Legacy timeline playback entry point kept commented for side-by-side review.
    public void PlayEmotion()
    {
        if (!TryEnsureTimelineTracks("PlayEmotion")) return;

        director.RebuildGraph();
        director.Play();
    }
    */
}
