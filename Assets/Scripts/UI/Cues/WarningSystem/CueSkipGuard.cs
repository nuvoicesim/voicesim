using UnityEngine;
using UI.Cues;

namespace UI.Cues.WarningSystem
{
    //public enum CueLevel { None = 0, Semantic = 1, Phonemic = 2, Model = 3 }

    public class CueSkipGuard : MonoBehaviour
    {
        [Header("Assign in Inspector")]
        [SerializeField] private WarningBanner banner;

        [Header("Practice mode (warn only)")]
        public CueLevel expectedLevel = CueLevel.Semantic;

        [Header("Warning spam control")]
        [Tooltip("Seconds to suppress repeated warnings (prevents spam)")]
        [SerializeField] private float warningCooldownSeconds = 1.0f;

        [Header("Pause after success")]
        [Tooltip("Default seconds to pause warnings after a target is marked correct / patient succeeds")]
        [SerializeField] private float defaultPauseSeconds = 10f;

        private float _lastWarningTime = -999f;
        private string _lastWarningKey = "";

        private float _pauseUntilTime = 0f;
        public bool WarningsPaused => Time.time < _pauseUntilTime;

        [SerializeField] private CueController _cueController;

        public void ResetCueing()
        {
            Debug.Log("CueSkipGuard: Resetting cueing level.");
            _cueController.ResetCueing();
            expectedLevel = UI.Cues.CueLevel.Semantic;
            Debug.Log("CueSkipGuard: expectedLevel reset to Semantic.");
            Debug.Log("CueSkipGuard: CueController current level after reset: " + _cueController.GetCurrentCueLevel());
        }

        /// <summary>
        /// Pause all warning checks temporarily (e.g., after patient successfully produces target word).
        /// </summary>
        public void PauseWarnings(float seconds = -1f)
        {
            if (seconds == 0f)
            {
                _pauseUntilTime = 0f; // 立即恢复
                return;
            }

            if (seconds < 0f) seconds = defaultPauseSeconds;
            _pauseUntilTime = Time.time + seconds;
        }

        /// <summary>
        /// Convenience method: call this when target is successful.
        /// Resets expected level and pauses warnings for a short time.
        /// </summary>
        public void OnTargetSuccess(float pauseSeconds = -1f)
        {
            ResetCueing();
            PauseWarnings(pauseSeconds);
        }

        public void CheckStudentUtterance(string studentText, string targetWord = "")
        {
            if (string.IsNullOrWhiteSpace(studentText))
                return;

            // Filter out STT error placeholder
            if (studentText.Trim().Equals("Error in transcription", System.StringComparison.OrdinalIgnoreCase))
                return;

            // If we are paused (e.g., patient already succeeded), skip checks
            if (WarningsPaused)
                return;

            if (_cueController == null)
            {
                Debug.LogError("CueSkipGuard: CueController reference not set. Cannot sync expectedLevel with current cue.");
                return;
            }


            Debug.Log("expectedLevel before CueController check: " + expectedLevel);
            Debug.Log("Setting expectedLevel from CueController: " + _cueController.GetCurrentCueLevel());

            expectedLevel = _cueController.GetCurrentCueLevel();

            Debug.Log("expectedLevel after CueController check: " + expectedLevel);

            UI.Cues.CueLevel used = Classify(studentText, targetWord);

            // Helpful debug (you can remove later)
            Debug.Log($"CueSkipGuard: text=\"{studentText}\" | used={used} expected={expectedLevel} bannerNull={(banner == null)} paused={WarningsPaused}");

            if (used == UI.Cues.CueLevel.None)
                return;

            // If student skips ahead, warn
            if (used > expectedLevel)
            {
                string msg = BuildWarning(expectedLevel, used);

                // simple anti-spam cooldown (same warning within cooldown window won't repeat)
                string key = $"{expectedLevel}->{used}";
                if (Time.time - _lastWarningTime > warningCooldownSeconds || key != _lastWarningKey)
                {
                    banner?.Show(msg);
                    _lastWarningTime = Time.time;
                    _lastWarningKey = key;
                }

                // Practice mode: advance expected to reduce repeated warnings
                expectedLevel = NextLevel(used);
                return;
            }

            // If student used the expected cue level, advance to next level
            if (used == expectedLevel)
            {
                expectedLevel = NextLevel(expectedLevel);
            }
        }

        private UI.Cues.CueLevel NextLevel(UI.Cues.CueLevel level)
        {
            if (level == UI.Cues.CueLevel.Semantic) return UI.Cues.CueLevel.Phonemic;
            if (level == UI.Cues.CueLevel.Phonemic) return UI.Cues.CueLevel.Model;
            return UI.Cues.CueLevel.Model;
        }

        private string BuildWarning(UI.Cues.CueLevel expected, UI.Cues.CueLevel used)
        {
            if (expected == UI.Cues.CueLevel.Semantic && used == UI.Cues.CueLevel.Phonemic)
                return "Please try a Semantic cue first before moving to a Phonemic cue.";
            if (expected == UI.Cues.CueLevel.Semantic && used == UI.Cues.CueLevel.Model)
                return "Please try Semantic → Phonemic cues before providing the Model cue.";
            if (expected == UI.Cues.CueLevel.Phonemic && used == UI.Cues.CueLevel.Model)
                return "Please try a Phonemic cue before providing the Model cue.";
            return "Please use cues in order: Semantic → Phonemic → Model.";
        }

        // Rule-based classifier (MVP)
        private UI.Cues.CueLevel Classify(string text, string targetWord)
        {
            if (string.IsNullOrWhiteSpace(text)) return UI.Cues.CueLevel.None;

            string t = text.ToLowerInvariant().Trim();
            string target = (targetWord ?? "").ToLowerInvariant().Trim();

            // --- Model cue: do NOT require targetWord (works even when targetWord="")
            // Examples: "repeat after me", "say 'coffee'", "say: coffee"
            if (ContainsAny(t,
                    "repeat after me",
                    "repeat after",
                    "say:",
                    "say '",
                    "say \"",
                    "please repeat",
                    "just say"))
            {
                return UI.Cues.CueLevel.Model;
            }

            // If targetWord is known, explicit quoting of target is also model-like
            if (!string.IsNullOrEmpty(target) && (t.Contains($"\"{target}\"") || t.Contains($"'{target}'")))
            {
                return UI.Cues.CueLevel.Model;
            }

            // --- Phonemic cue
            if (ContainsAny(t,
                    "starts with",
                    "begins with",
                    "first sound",
                    "first letter",
                    "/k/",
                    "/t/",
                    "/s/",
                    "sounds like",
                    "rhymes with"))
            {
                return UI.Cues.CueLevel.Phonemic;
            }

            // --- Semantic cue (light heuristic)
            if (ContainsAny(t,
                    "it's",
                    "it is",
                    "you use it",
                    "used for",
                    "a kind of",
                    "a type of",
                    "something you",
                    "it helps",
                    "you can"))
            {
                return UI.Cues.CueLevel.Semantic;
            }

            return UI.Cues.CueLevel.None;
        }

        private bool ContainsAny(string t, params string[] keys)
        {
            foreach (var k in keys)
                if (t.Contains(k)) return true;
            return false;
        }
    }
}