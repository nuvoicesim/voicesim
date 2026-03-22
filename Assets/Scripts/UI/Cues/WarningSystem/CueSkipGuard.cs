using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UI.Cues;

namespace UI.Cues.WarningSystem
{
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

        [Header("Cue source")]
        [SerializeField] private CueController _cueController;

        [Header("Classifier thresholds")]
        [Tooltip("Minimum score needed to classify as Semantic")]
        [SerializeField] private int semanticThreshold = 2;

        [Tooltip("Minimum score needed to classify as Phonemic")]
        [SerializeField] private int phonemicThreshold = 2;

        [Tooltip("Minimum score needed to classify as Model")]
        [SerializeField] private int modelThreshold = 3;

        [Tooltip("Whether to print detailed classifier reasoning in Console")]
        [SerializeField] private bool enableVerboseClassifierLog = true;

        private float _lastWarningTime = -999f;
        private string _lastWarningKey = "";
        private float _pauseUntilTime = 0f;
        private bool _targetSucceeded = false;

        public bool WarningsPaused => Time.time < _pauseUntilTime;

        // -----------------------------
        // Regex patterns
        // -----------------------------

        private static readonly Regex[] ModelStrongPatterns =
        {
            new Regex(@"\brepeat after me\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bplease repeat\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bjust say\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bsay it\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bsay the word\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bcan you say\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\btry saying\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        };

        private static readonly Regex[] ModelWeakPatterns =
        {
            new Regex(@"\bsay\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\brepeat\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        };

        private static readonly Regex[] PhonemicPatterns =
        {
            new Regex(@"\bstarts?\s+with\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bbegins?\s+with\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bfirst\s+(sound|letter|syllable)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\binitial\s+(sound|letter|syllable)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bwhat\s+sound\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\brhymes?\s+with\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bsounds?\s+like\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"/[a-z]+/", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\b[a-z]+-[a-z]+\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bthe\s+(first|second|next)\s+syllable\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bsound\s+it\s+out\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        };

        private static readonly Regex[] SemanticStrongPatterns =
        {
            new Regex(@"\byou use it\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bused for\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\ba kind of\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\ba type of\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bsomething you\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bit helps\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\byou can use it\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\byou drink it\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\byou eat it\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\byou wear it\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bit'?s\s+(hot|warm|cold|soft|hard|sweet|sour|round|flat|small|large|heavy|light)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bhas\s+(caffeine|buttons|wheels|screens?|legs?|wings?|handle)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\byou\s+(swallow|stand\s+under|take\s+for|wear\s+on|watch\s+on|sit\s+in|drive)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bcovers?\s+your\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bin\s+the\s+(middle|bathroom|kitchen|bedroom|living room)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bwith\s+(meat|cheese|bread|water|milk|butter)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bfor\s+(watching|eating|drinking|sleeping|washing|driving)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bno\s+(clouds?|energy|strength)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bsun\s+is\s+shining\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\blow\s+energy\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bneed\s+rest\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bfour\s+wheels?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bstand\s+under\s+water\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\b(she'?s?|he'?s?)\s+(female|male|young|old|your child)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\byour\s+(child|son|daughter|family|relative)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        };

        private static readonly Regex[] SemanticWeakPatterns =
        {
            new Regex(@"\bbathroom\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bhot\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bcold\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bsweet\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bcaffeine\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bexhausted\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bvehicle\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bscreen\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\btablets?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bblood\s+pressure\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        };

        public void ResetCueing()
        {
            Debug.Log("CueSkipGuard: Resetting cueing level.");

            _targetSucceeded = false;

            if (_cueController != null)
            {
                _cueController.ResetCueing();
                Debug.Log("CueSkipGuard: CueController reset called.");
            }
            else
            {
                Debug.LogWarning("CueSkipGuard: _cueController is null during ResetCueing().");
            }

            expectedLevel = CueLevel.Semantic;
            Debug.Log("CueSkipGuard: expectedLevel reset to Semantic.");

            if (_cueController != null)
            {
                Debug.Log("CueSkipGuard: CueController current level after reset: " + _cueController.GetCurrentCueLevel());
            }
        }

        public void PauseWarnings(float seconds = -1f)
        {
            if (seconds == 0f)
            {
                _pauseUntilTime = 0f;
                return;
            }

            if (seconds < 0f)
                seconds = defaultPauseSeconds;

            _pauseUntilTime = Time.time + seconds;
        }

        public void OnTargetSuccess(float pauseSeconds = -1f)
        {
            _targetSucceeded = true;
            PauseWarnings(pauseSeconds);
            Debug.Log("CueSkipGuard: Target succeeded. Warnings permanently suppressed until ResetCueing() is called.");
        }

        public void CheckStudentUtterance(string studentText, string targetWord = "")
        {
            if (string.IsNullOrWhiteSpace(studentText))
                return;

            if (studentText.Trim().Equals("Error in transcription", StringComparison.OrdinalIgnoreCase))
                return;

            if (_targetSucceeded)
                return;

            if (WarningsPaused)
                return;

            if (_cueController == null)
            {
                Debug.LogError("CueSkipGuard: CueController reference not set. Cannot sync expectedLevel with current cue.");
                return;
            }

            expectedLevel = _cueController.GetCurrentCueLevel();

            // If CueController hasn't initialized yet, treat as Semantic (first level)
            if (expectedLevel == CueLevel.None)
                expectedLevel = CueLevel.Semantic;

            ClassificationResult result = ClassifyDetailed(studentText, targetWord);
            CueLevel used = result.Level;

            if (enableVerboseClassifierLog)
            {
                Debug.Log(
                    $"CueSkipGuard Classify: raw=\"{studentText}\" | normalized=\"{result.NormalizedText}\" | " +
                    $"target=\"{result.NormalizedTarget}\" | semantic={result.SemanticScore} phonemic={result.PhonemicScore} model={result.ModelScore} | " +
                    $"used={used} expected={expectedLevel} | reasons=[{string.Join("; ", result.Reasons)}]");
            }
            else
            {
                Debug.Log($"CueSkipGuard: text=\"{studentText}\" | used={used} expected={expectedLevel} bannerNull={(banner == null)} paused={WarningsPaused}");
            }

            if (used == CueLevel.None)
                return;

            if (used > expectedLevel)
            {
                string msg = BuildWarning(expectedLevel, used);
                string key = $"{expectedLevel}->{used}";

                if (Time.time - _lastWarningTime > warningCooldownSeconds || key != _lastWarningKey)
                {
                    banner?.Show(msg);
                    _lastWarningTime = Time.time;
                    _lastWarningKey = key;
                }

                expectedLevel = NextLevel(used);
                return;
            }

            if (used == expectedLevel)
            {
                expectedLevel = NextLevel(expectedLevel);
            }
        }

        private CueLevel NextLevel(CueLevel level)
        {
            if (level == CueLevel.Semantic) return CueLevel.Phonemic;
            if (level == CueLevel.Phonemic) return CueLevel.Model;
            return CueLevel.Model;
        }

        private string BuildWarning(CueLevel expected, CueLevel used)
        {
            if (expected == CueLevel.Semantic && used == CueLevel.Phonemic)
                return "Please try a Semantic cue first before moving to a Phonemic cue.";

            if (expected == CueLevel.Semantic && used == CueLevel.Model)
                return "Please try Semantic → Phonemic cues before providing the Model cue.";

            if (expected == CueLevel.Phonemic && used == CueLevel.Model)
                return "Please try a Phonemic cue before providing the Model cue.";

            return "Please use cues in order: Semantic → Phonemic → Model.";
        }

        private ClassificationResult ClassifyDetailed(string text, string targetWord)
        {
            ClassificationResult result = new ClassificationResult
            {
                RawText = text ?? "",
                RawTarget = targetWord ?? "",
                NormalizedText = NormalizeText(text),
                NormalizedTarget = NormalizeText(targetWord)
            };

            string t = result.NormalizedText;
            string target = result.NormalizedTarget;

            if (string.IsNullOrWhiteSpace(t))
            {
                result.Level = CueLevel.None;
                result.Reasons.Add("Empty normalized text.");
                return result;
            }

            foreach (var regex in ModelStrongPatterns)
            {
                if (regex.IsMatch(t))
                {
                    result.ModelScore += 3;
                    result.Reasons.Add($"Model +3: matched strong pattern [{regex}]");
                }
            }

            foreach (var regex in ModelWeakPatterns)
            {
                if (regex.IsMatch(t))
                {
                    result.ModelScore += 1;
                    result.Reasons.Add($"Model +1: matched weak pattern [{regex}]");
                }
            }

            if (!string.IsNullOrEmpty(target))
            {
                if (ContainsWholePhrase(t, target))
                {
                    result.ModelScore += 3;
                    result.Reasons.Add($"Model +3: utterance directly contains target word [{target}]");
                }

                if (Regex.IsMatch(t, $@"[""']\s*{Regex.Escape(target)}\s*[""']", RegexOptions.IgnoreCase))
                {
                    result.ModelScore += 3;
                    result.Reasons.Add($"Model +3: target word appears quoted [{target}]");
                }

                if (Regex.IsMatch(t, $@"\b(say|repeat|try saying|can you say)\b.*\b{Regex.Escape(target)}\b", RegexOptions.IgnoreCase))
                {
                    result.ModelScore += 4;
                    result.Reasons.Add($"Model +4: directive + target pattern matched [{target}]");
                }
            }

            foreach (var regex in PhonemicPatterns)
            {
                if (regex.IsMatch(t))
                {
                    int add = regex.ToString() == @"/[a-z]/" ? 3 : 2;
                    result.PhonemicScore += add;
                    result.Reasons.Add($"Phonemic +{add}: matched pattern [{regex}]");
                }
            }

            if (!string.IsNullOrEmpty(target) && target.Length > 0)
            {
                char firstChar = target[0];

                if (Regex.IsMatch(t, $@"\b(first|initial)\s+letter\s+is\s+{Regex.Escape(firstChar.ToString())}\b", RegexOptions.IgnoreCase))
                {
                    result.PhonemicScore += 3;
                    result.Reasons.Add($"Phonemic +3: initial letter matches target [{firstChar}]");
                }

                if (Regex.IsMatch(t, $@"\bstarts?\s+with\s+{Regex.Escape(firstChar.ToString())}\b", RegexOptions.IgnoreCase))
                {
                    result.PhonemicScore += 3;
                    result.Reasons.Add($"Phonemic +3: starts-with-letter matches target [{firstChar}]");
                }
            }

            foreach (var regex in SemanticStrongPatterns)
            {
                if (regex.IsMatch(t))
                {
                    result.SemanticScore += 2;
                    result.Reasons.Add($"Semantic +2: matched strong pattern [{regex}]");
                }
            }

            foreach (var regex in SemanticWeakPatterns)
            {
                if (regex.IsMatch(t))
                {
                    result.SemanticScore += 1;
                    result.Reasons.Add($"Semantic +1: matched weak pattern [{regex}]");
                }
            }

            if (!string.IsNullOrEmpty(target))
            {
                if (Regex.IsMatch(t, $@"\b(it'?s|it\s+is)\s+{Regex.Escape(target)}\b", RegexOptions.IgnoreCase))
                {
                    result.ModelScore += 4;
                    result.Reasons.Add($"Model +4: direct answer pattern matched [it's {target}]");
                }
            }

            if (result.ModelScore >= modelThreshold &&
                result.ModelScore >= result.PhonemicScore &&
                result.ModelScore >= result.SemanticScore)
            {
                result.Level = CueLevel.Model;
                result.Reasons.Add($"Final => Model (score {result.ModelScore} >= threshold {modelThreshold})");
                return result;
            }

            if (result.PhonemicScore >= phonemicThreshold &&
                result.PhonemicScore >= result.SemanticScore)
            {
                result.Level = CueLevel.Phonemic;
                result.Reasons.Add($"Final => Phonemic (score {result.PhonemicScore} >= threshold {phonemicThreshold})");
                return result;
            }

            if (result.SemanticScore >= semanticThreshold)
            {
                result.Level = CueLevel.Semantic;
                result.Reasons.Add($"Final => Semantic (score {result.SemanticScore} >= threshold {semanticThreshold})");
                return result;
            }

            result.Level = CueLevel.None;
            result.Reasons.Add("Final => None (all scores below threshold)");
            return result;
        }

        private string NormalizeText(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            string t = input.ToLowerInvariant().Trim();
            t = Regex.Replace(t, @"[^\w\s\/'""-]", " ");
            t = Regex.Replace(t, @"\s+", " ").Trim();
            t = t.Replace("wanna", "want to");
            t = t.Replace("gonna", "going to");

            return t;
        }

        private bool ContainsWholePhrase(string text, string phrase)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(phrase))
                return false;

            return Regex.IsMatch(
                text,
                $@"\b{Regex.Escape(phrase)}\b",
                RegexOptions.IgnoreCase);
        }

        [Serializable]
        private class ClassificationResult
        {
            public string RawText;
            public string RawTarget;
            public string NormalizedText;
            public string NormalizedTarget;

            public int SemanticScore;
            public int PhonemicScore;
            public int ModelScore;

            public CueLevel Level = CueLevel.None;
            public List<string> Reasons = new List<string>();
        }
    }
}