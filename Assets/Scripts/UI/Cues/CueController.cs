using UnityEngine;
using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using UI.Cues.WarningSystem;
using System.Text;

namespace UI.Cues
{
    [System.Serializable]
    public class ScriptEntry
    {
        public int script_number;
        public string title;
        public string target_word;
        public string alternate_target;

        public string[] semanticKeywords;
        public string[] phonemicKeywords;

        public StudentHints studentHints;
    }

    [System.Serializable]
    public class StudentHints
    {
        public string Semantic;
        public string Phonemic;
        public string Model;
    }

    [System.Serializable]
    public class ScriptData
    {
        public ScriptEntry[] scripts;
    }

    public enum CueLevel
    {
        None = 0,
        Semantic = 1,
        Phonemic = 2,
        Model = 3
    }

    // 这个类用来在 Inspector 里配置：
    // targetWord -> sprite
    [System.Serializable]
    public class TargetImageEntry
    {
        public string targetWord;
        public Sprite image;
    }

    public class CueController : MonoBehaviour
    {
        public bool cueingActive = false;

        public event System.Action<CueLevel> CuePressedEvent;

        public int scriptNum = 1;
        public ScriptEntry currentScript;
        private ScriptData allScripts;

        private CueLevel lastCueLevel = CueLevel.None;
        private CueLevel currentCueLevel = CueLevel.None;

        private int maxCueCount = 2;
        private int semanticCueCount = 0;
        private int phonemicCueCount = 0;
        private int noSignalCount = 0;

        private CueLevel pressedCueButton = CueLevel.None;
        private string lastComputedHint = "";
        private bool usingRuntimeScripts = false;

        [Header("Cue UI")]
        [SerializeField] private GameObject cueButtonPanel;
        [SerializeField] private Button hintButton;
        [SerializeField] private Button semanticCueButton;
        [SerializeField] private Button phonemicCueButton;
        [SerializeField] private Button modelCueButton;
        [SerializeField] private Button confirmTargetButton;
        [SerializeField] private GameObject hintBox;
        [SerializeField] private TextMeshProUGUI hintText;

        [Header("Target UI")]
        [SerializeField] private TextMeshProUGUI targetText;
        [SerializeField] private Image targetImage;
        [SerializeField] private bool includePromptInTargetText = false;
        [SerializeField] private string targetLabelPrefix = "Target:";

        [Header("Target Images Mapping")]
        [SerializeField] private List<TargetImageEntry> targetImages = new List<TargetImageEntry>();

        [Header("Warning Banner")]
        [SerializeField] private WarningBanner warningBanner;

        private IEnumerator Start()
        {
            if (semanticCueButton != null)
                semanticCueButton.onClick.AddListener(() => OnCueButtonPressed(CueLevel.Semantic));

            if (phonemicCueButton != null)
                phonemicCueButton.onClick.AddListener(() => OnCueButtonPressed(CueLevel.Phonemic));

            if (modelCueButton != null)
                modelCueButton.onClick.AddListener(() => OnCueButtonPressed(CueLevel.Model));

            if (hintBox != null)
                hintBox.SetActive(false);

            if (targetText == null)
            {
                Debug.LogError("No targetText object found");
            }

            if (hintButton != null)
                hintButton.onClick.AddListener(ToggleCuePanel);

            if (confirmTargetButton != null)
                confirmTargetButton.onClick.AddListener(() => OnConfirmTargetButtonPressed());

            if (!usingRuntimeScripts)
                yield return LoadScriptsFromStreamingAssets();

            if (usingRuntimeScripts)
            {
                UpdateTargetText();
                yield break;
            }

            if (!SetCurrentScript(scriptNum))
                yield break;

            UpdateTargetText();
        }

        private void LoadAllScripts(string jsonText)
        {
            if (usingRuntimeScripts)
                return;

            if (string.IsNullOrEmpty(jsonText))
            {
                Debug.LogError("JSON text is empty");
                return;
            }

            allScripts = JsonUtility.FromJson<ScriptData>(jsonText);
            if (allScripts != null && allScripts.scripts != null && allScripts.scripts.Length > 0)
                Debug.Log("Loaded " + allScripts.scripts.Length + " scripts");
            else
                Debug.LogError("Failed to parse JSON target scripts");
        }

        private IEnumerator LoadScriptsFromStreamingAssets()
        {
            string relativePath = "Cues/target_words.json";
            string path = BuildStreamingAssetsPath(relativePath);

            if (path.Contains("://"))
            {
                using (UnityWebRequest request = UnityWebRequest.Get(path))
                {
                    yield return request.SendWebRequest();

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError("Failed to load target_words.json from StreamingAssets: " + request.error + " Path: " + path);
                        yield break;
                    }
                    if (usingRuntimeScripts)
                        yield break;

                    LoadAllScripts(request.downloadHandler.text);
                    Debug.Log("Loaded scripts from StreamingAssets via UnityWebRequest");
                }

                yield break;
            }

            if (!File.Exists(path))
            {
                Debug.LogError("Could not find target_words.json in StreamingAssets at path: " + path);
                yield break;
            }

            if (usingRuntimeScripts)
                yield break;

            LoadAllScripts(File.ReadAllText(path));
            Debug.Log("Loaded scripts from StreamingAssets");
        }

        private static string BuildStreamingAssetsPath(string relativePath)
        {
            string normalizedRelativePath = relativePath.Replace("\\", "/").TrimStart('/');
            return Application.streamingAssetsPath.TrimEnd('/') + "/" + normalizedRelativePath;
        }

        /// <summary>
        /// Sets the current script based on the given script number.
        /// </summary>
        /// <param name="scriptNumber">The script number to select</param>
        public bool SetCurrentScript(int scriptNumber)
        {
            if (allScripts == null || allScripts.scripts == null)
            {
                Debug.LogError("Target scripts not loaded. Call LoadAllScripts first");
                return false;
            }

            foreach (var entry in allScripts.scripts)
            {
                if (entry.script_number == scriptNumber)
                {
                    currentScript = entry;
                    scriptNum = scriptNumber;

                    Debug.Log("Current script set: " + entry.title + " | target_word = " + entry.target_word);

                    UpdateTargetUI();
                    return true;
                }
            }

            Debug.LogError("Target script number not found: " + scriptNumber);
            return false;
        }

        public bool TryGetScriptEntry(int scriptNumber, out ScriptEntry scriptEntry)
        {
            scriptEntry = null;
            if (allScripts == null || allScripts.scripts == null)
                return false;

            foreach (ScriptEntry entry in allScripts.scripts)
            {
                if (entry != null && entry.script_number == scriptNumber)
                {
                    scriptEntry = entry;
                    return true;
                }
            }

            return false;
        }

        public bool UseRuntimeScripts(ScriptEntry[] scripts, int initialScriptNumber)
        {
            if (scripts == null || scripts.Length == 0)
            {
                Debug.LogError("CueController: runtime script data is empty.");
                return false;
            }

            allScripts = new ScriptData { scripts = scripts };
            usingRuntimeScripts = true;
            return SetCurrentScript(initialScriptNumber);
        }

        public void SetIncludePromptInTargetText(bool includePrompt)
        {
            includePromptInTargetText = includePrompt;
            UpdateTargetText();
        }

        public void SetTargetLabelPrefix(string labelPrefix)
        {
            targetLabelPrefix = string.IsNullOrWhiteSpace(labelPrefix) ? "Target:" : labelPrefix.Trim();
            UpdateTargetText();
        }

        private void UpdateTargetText()
        {
            if (targetText != null && currentScript != null)
            {
                targetText.text = BuildTargetDisplayText();
            }
        }

        private void UpdateTargetUI()
        {
            if (currentScript == null)
                return;

            if (targetText != null)
                targetText.text = BuildTargetDisplayText();

            UpdateTargetImage();
        }

        private string BuildTargetDisplayText()
        {
            if (currentScript == null)
                return string.Empty;

            string prefix = string.IsNullOrWhiteSpace(targetLabelPrefix) ? "Target:" : targetLabelPrefix.Trim();
            string targetLine = prefix + " " + currentScript.target_word;
            if (!string.IsNullOrWhiteSpace(currentScript.alternate_target))
                targetLine += "/" + currentScript.alternate_target;

            if (!includePromptInTargetText)
                return targetLine;

            string prompt = currentScript.title ?? string.Empty;
            return string.IsNullOrWhiteSpace(prompt) ? targetLine : prompt.Trim() + "\n" + targetLine;
        }

        private void UpdateTargetImage()
        {
            if (targetImage == null)
            {
                Debug.LogWarning("CueController: targetImage is not assigned.");
                return;
            }

            if (currentScript == null)
                return;

            string targetWordNormalized = NormalizeWord(currentScript.target_word);

            foreach (var entry in targetImages)
            {
                if (entry == null || entry.image == null || string.IsNullOrWhiteSpace(entry.targetWord))
                    continue;

                if (NormalizeWord(entry.targetWord) == targetWordNormalized)
                {
                    targetImage.sprite = entry.image;
                    targetImage.enabled = true;
                    targetImage.preserveAspect = true;

                    Debug.Log("Updated target image for: " + currentScript.target_word);
                    return;
                }
            }

            Debug.LogWarning("No image mapping found for target word: " + currentScript.target_word);
            targetImage.sprite = null;
            targetImage.enabled = false;
        }

        private string NormalizeWord(string word)
        {
            if (string.IsNullOrWhiteSpace(word))
                return "";

            return word.Trim().ToLower();
        }

        private bool CheckTargetWord(string patientResponse)
        {
            if (string.IsNullOrEmpty(patientResponse) || currentScript == null)
                return false;

            string responseLower = patientResponse.ToLower();
            string targetLower = currentScript.target_word.ToLower();

            if (responseLower.Contains(targetLower))
                return true;

            if (!string.IsNullOrEmpty(currentScript.alternate_target))
            {
                if (responseLower.Contains(currentScript.alternate_target.ToLower()))
                    return true;
            }

            return false;
        }

        private CueLevel DetermineCueLevel(string patientResponse, bool saidTarget)
        {
            if (string.IsNullOrEmpty(patientResponse) || currentScript == null)
            {
                Debug.LogWarning("Cue progression: skipped DetermineCueLevel due to empty response or missing script.");
                return currentCueLevel;
            }

            Debug.LogWarning("Cue progression: starting evaluation for response: \"" + patientResponse + "\" at level " + currentCueLevel);

            if (saidTarget)
            {
                noSignalCount = 0;
                semanticCueCount = 0;
                phonemicCueCount = 0;
                Debug.LogWarning("Cue progression: target detected in response. Resetting counters and returning None.");
                return CueLevel.None;
            }

            if (currentCueLevel == CueLevel.Model)
            {
                Debug.LogWarning("Cue progression: already at Model level. Remaining at Model.");
                return currentCueLevel;
            }

            string normalizedResponse = NormalizeInput(patientResponse);
            int semanticScore = ComputeSemanticScore(normalizedResponse);
            int phonemicScore = ComputePhonemicScore(normalizedResponse);
            Debug.LogWarning("Cue progression: normalized response = \"" + normalizedResponse + "\"");
            Debug.LogWarning("Cue progression: semanticScore=" + semanticScore + ", phonemicScore=" + phonemicScore);

            bool hasSignal = semanticScore > 0 || phonemicScore > 0;
            noSignalCount = hasSignal ? 0 : noSignalCount + 1;
            Debug.LogWarning("Cue progression: hasSignal=" + hasSignal + ", noSignalCount=" + noSignalCount);

            CueLevel nextCueLevel = currentCueLevel;

            switch (currentCueLevel)
            {
                case CueLevel.None:
                    if (semanticScore > 0 || phonemicScore > 0 || noSignalCount >= 2)
                    {
                        nextCueLevel = CueLevel.Semantic;
                        Debug.LogWarning("Cue progression: None -> Semantic due to initial evidence or repeated no-signal.");
                    }
                    break;
                case CueLevel.Semantic:
                    // Escalate when we repeatedly fail at semantic level or detect stronger phonemic evidence.
                    if (phonemicScore >= 2 || semanticCueCount >= maxCueCount || noSignalCount >= 2)
                    {
                        nextCueLevel = CueLevel.Phonemic;
                        Debug.LogWarning("Cue progression: Semantic -> Phonemic (phonemicScore=" + phonemicScore
                            + ", semanticCueCount=" + semanticCueCount + ", noSignalCount=" + noSignalCount + ").");
                    }
                    else
                    {
                        nextCueLevel = CueLevel.Semantic;
                        Debug.LogWarning("Cue progression: staying at Semantic.");
                    }
                    break;
                case CueLevel.Phonemic:
                    // Escalate to model after repeated phonemic attempts with no successful naming.
                    if (phonemicCueCount >= maxCueCount || noSignalCount >= 2)
                    {
                        nextCueLevel = CueLevel.Model;
                        Debug.LogWarning("Cue progression: Phonemic -> Model (phonemicCueCount=" + phonemicCueCount
                            + ", noSignalCount=" + noSignalCount + ").");
                    }
                    else
                    {
                        nextCueLevel = CueLevel.Phonemic;
                        Debug.LogWarning("Cue progression: staying at Phonemic.");
                    }
                    break;
            }

            if (nextCueLevel == CueLevel.Semantic)
            {
                semanticCueCount++;
                phonemicCueCount = 0;
                Debug.LogWarning("Cue progression counters: semanticCueCount=" + semanticCueCount + ", phonemicCueCount reset to 0.");
            }
            else if (nextCueLevel == CueLevel.Phonemic)
            {
                phonemicCueCount++;
                semanticCueCount = 0;
                Debug.LogWarning("Cue progression counters: phonemicCueCount=" + phonemicCueCount + ", semanticCueCount reset to 0.");
            }
            else
            {
                semanticCueCount = 0;
                phonemicCueCount = 0;
                Debug.LogWarning("Cue progression counters: both cue counters reset to 0.");
            }

            Debug.LogWarning("Cue progression complete: " + currentCueLevel + " -> " + nextCueLevel
                + " | SemanticScore=" + semanticScore
                + " | PhonemicScore=" + phonemicScore
                + " | NoSignalCount=" + noSignalCount);

            return nextCueLevel;
        }

        private int ComputeSemanticScore(string normalizedResponse)
        {
            int score = 0;
            if (string.IsNullOrWhiteSpace(normalizedResponse) || currentScript == null)
                return score;

            if (ContainsNormalizedPhrase(normalizedResponse, currentScript.title))
            {
                score++;
                Debug.LogWarning("Semantic scoring: +1 from title match \"" + currentScript.title + "\".");
            }

            if (currentScript.semanticKeywords != null)
            {
                foreach (var keyword in currentScript.semanticKeywords)
                {
                    if (ContainsNormalizedPhrase(normalizedResponse, keyword))
                    {
                        score += 2;
                        Debug.LogWarning("Semantic scoring: +2 from phrase match \"" + keyword + "\".");
                    }
                    else if (HasTokenOverlap(normalizedResponse, keyword))
                    {
                        score++;
                        Debug.LogWarning("Semantic scoring: +1 from token overlap with \"" + keyword + "\".");
                    }
                }
            }

            Debug.LogWarning("Semantic scoring total: " + score);
            return score;
        }

        private int ComputePhonemicScore(string normalizedResponse)
        {
            int score = 0;
            if (string.IsNullOrWhiteSpace(normalizedResponse) || currentScript == null)
                return score;

            if (currentScript.phonemicKeywords != null)
            {
                foreach (var keyword in currentScript.phonemicKeywords)
                {
                    if (ContainsNormalizedPhrase(normalizedResponse, keyword))
                    {
                        score += 2;
                        Debug.LogWarning("Phonemic score increased by 2 for keyword: " + keyword);
                    }
                }
            }

            var responseTokens = Tokenize(normalizedResponse);
            string target = NormalizeInput(currentScript.target_word);
            string alternate = NormalizeInput(currentScript.alternate_target);

            foreach (var token in responseTokens)
            {
                if (token.Length < 2)
                    continue;

                if (!string.IsNullOrEmpty(target) && StartsWithEither(token, target))
                {
                    score++;
                    Debug.LogWarning("Phonemic scoring: +1 from target prefix relation token=\"" + token + "\" target=\"" + target + "\".");
                }
                if (!string.IsNullOrEmpty(alternate) && StartsWithEither(token, alternate))
                {
                    score++;
                    Debug.LogWarning("Phonemic scoring: +1 from alternate prefix relation token=\"" + token + "\" alternate=\"" + alternate + "\".");
                }

                if (!string.IsNullOrEmpty(target) && IsClosePhonemicMatch(token, target))
                {
                    score++;
                    Debug.LogWarning("Phonemic scoring: +1 from close match token=\"" + token + "\" target=\"" + target + "\".");
                }
                if (!string.IsNullOrEmpty(alternate) && IsClosePhonemicMatch(token, alternate))
                {
                    score++;
                    Debug.LogWarning("Phonemic scoring: +1 from close match token=\"" + token + "\" alternate=\"" + alternate + "\".");
                }
            }

            Debug.LogWarning("Phonemic scoring total: " + score);
            return score;
        }

        private static bool StartsWithEither(string token, string target)
        {
            if (token.Length < 2 || target.Length < 2)
                return false;

            return target.StartsWith(token) || token.StartsWith(target.Substring(0, Mathf.Min(3, target.Length)));
        }

        private static bool IsClosePhonemicMatch(string token, string target)
        {
            int minLen = Mathf.Min(token.Length, target.Length);
            if (minLen < 3)
                return false;

            string tokenPrefix = token.Substring(0, Mathf.Min(4, token.Length));
            string targetPrefix = target.Substring(0, Mathf.Min(4, target.Length));
            int distance = LevenshteinDistance(tokenPrefix, targetPrefix);

            return distance <= 1;
        }

        private static bool HasTokenOverlap(string normalizedResponse, string phrase)
        {
            string normalizedPhrase = NormalizeInput(phrase);
            if (string.IsNullOrEmpty(normalizedPhrase))
                return false;

            var responseTokens = Tokenize(normalizedResponse);
            var phraseTokens = Tokenize(normalizedPhrase);

            foreach (var responseToken in responseTokens)
            {
                foreach (var phraseToken in phraseTokens)
                {
                    if (responseToken == phraseToken)
                        return true;
                }
            }

            return false;
        }

        private static bool ContainsNormalizedPhrase(string normalizedResponse, string phrase)
        {
            string normalizedPhrase = NormalizeInput(phrase);
            if (string.IsNullOrEmpty(normalizedPhrase))
                return false;

            return normalizedResponse.Contains(normalizedPhrase);
        }

        private static List<string> Tokenize(string normalizedText)
        {
            var tokens = new List<string>();
            if (string.IsNullOrWhiteSpace(normalizedText))
                return tokens;

            string[] split = normalizedText.Split(' ');
            foreach (var token in split)
            {
                if (!string.IsNullOrWhiteSpace(token))
                    tokens.Add(token);
            }
            return tokens;
        }

        private static string NormalizeInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var sb = new StringBuilder(input.Length);
            for (int i = 0; i < input.Length; i++)
            {
                char c = char.ToLowerInvariant(input[i]);
                if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
                    sb.Append(c);
                else
                    sb.Append(' ');
            }

            return string.Join(" ", sb.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private static int LevenshteinDistance(string a, string b)
        {
            if (string.IsNullOrEmpty(a))
                return string.IsNullOrEmpty(b) ? 0 : b.Length;
            if (string.IsNullOrEmpty(b))
                return a.Length;

            int[,] d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++)
                d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++)
                d[0, j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = (a[i - 1] == b[j - 1]) ? 0 : 1;
                    d[i, j] = Mathf.Min(
                        Mathf.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost
                    );
                }
            }

            return d[a.Length, b.Length];
        }

        private string GetStudentHint(CueLevel cue)
        {
            if (currentScript == null || currentScript.studentHints == null)
                return "";

            switch (cue)
            {
                case CueLevel.Semantic:
                    return currentScript.studentHints.Semantic;
                case CueLevel.Phonemic:
                    return currentScript.studentHints.Phonemic;
                case CueLevel.Model:
                    return currentScript.studentHints.Model;
                default:
                    return "";
            }
        }

        public CueLevel GetCurrentCueLevel()
        {
            return currentCueLevel;
        }

        public void ResetCueing()
        {
            lastCueLevel = CueLevel.None;
            currentCueLevel = CueLevel.None;
            semanticCueCount = 0;
            phonemicCueCount = 0;
            lastComputedHint = "";
            pressedCueButton = CueLevel.None;

            if (hintBox != null)
                hintBox.SetActive(false);

            if (hintText != null)
                hintText.text = "";
        }

        public void HandleResponse(string patientResponse)
        {
            if (!cueingActive)
                return;

            if (currentScript == null)
            {
                Debug.LogError("No target script loaded");
                return;
            }

            bool saidTarget = CheckTargetWord(patientResponse);
            lastCueLevel = currentCueLevel;
            currentCueLevel = DetermineCueLevel(patientResponse, saidTarget);

            string hint = GetStudentHint(currentCueLevel);
            lastComputedHint = hint ?? "";

            Debug.LogWarning("HandleResponse summary | Response=\"" + patientResponse
                + "\" | SaidTarget=" + saidTarget
                + " | LastLevel=" + lastCueLevel
                + " | CurrentLevel=" + currentCueLevel
                + " | SemanticCueCount=" + semanticCueCount
                + " | PhonemicCueCount=" + phonemicCueCount);
        }

        public void ToggleCuePanel()
        {
            Debug.Log("Toggling cue button panel");
            if (cueButtonPanel != null)
                cueButtonPanel.SetActive(!cueButtonPanel.activeSelf);
        }

        public void ToggleHintBox()
        {
            Debug.Log("Toggling hint box");
            if (hintBox != null)
                hintBox.SetActive(!hintBox.activeSelf);
        }

        public void OnCueButtonPressed(CueLevel pressedCue)
        {
            pressedCueButton = pressedCue;
            Debug.Log("Cue button pressed: " + pressedCue);
            ShowHintIfAllowed();
            CuePressedEvent?.Invoke(pressedCue);
        }

        public void SetTargetImages(List<TargetImageEntry> entries)
        {
            if (entries == null)
                targetImages = new List<TargetImageEntry>();
            else
                targetImages = new List<TargetImageEntry>(entries);
            UpdateTargetImage();
        }

        public void OnConfirmTargetButtonPressed()
        {
            Debug.Log("Confirm target button pressed, resetting cueing");
            ResetCueing();
        }

        private void ShowHintIfAllowed()
        {
            if (hintBox == null || hintText == null)
                return;

            if (pressedCueButton != currentCueLevel)
            {
                Debug.Log("Pressed cue button does not match current cue level. Pressed: "
                    + pressedCueButton + ", Current: " + currentCueLevel + ". Showing warning banner and hint.");

                string hint = GetStudentHint(pressedCueButton);
                lastComputedHint = hint ?? "";

                hintText.text = lastComputedHint;
                hintBox.SetActive(true);

                // do not show warning banner if the pressed cue button is semantic
                if (pressedCueButton != CueLevel.Semantic)
                {
                    warningBanner.Show("Warning: You have pressed a hint for a higher cue level than expected. Expected: " + currentCueLevel + ", Pressed: " + pressedCueButton);
                    Debug.Log("Showing warning banner and hint for " + pressedCueButton);
                    return;
                }
            }

            else if (currentCueLevel != CueLevel.None &&
                pressedCueButton == currentCueLevel &&
                !string.IsNullOrEmpty(lastComputedHint))
            {
                hintText.text = lastComputedHint;
                hintBox.SetActive(true);
                Debug.Log("Showing student hint for " + currentCueLevel);
            }

            else
            {
                hintBox.SetActive(false);
            }
        }
    }
}
