using UnityEngine;
using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using UI.Cues.WarningSystem;

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

        public int scriptNum = 1;
        public ScriptEntry currentScript;
        private ScriptData allScripts;

        private CueLevel lastCueLevel = CueLevel.None;
        private CueLevel currentCueLevel = CueLevel.None;

        private int maxCueCount = 2;
        private int semanticCueCount = 0;
        private int phonemicCueCount = 0;

        private CueLevel pressedCueButton = CueLevel.None;
        private string lastComputedHint = "";

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

            if (confirmTargetButton != null)
                confirmTargetButton.onClick.AddListener(() => OnConfirmTargetButtonPressed());

            yield return LoadScriptsFromStreamingAssets();

            if (!SetCurrentScript(scriptNum))
                yield break;

            UpdateTargetText();
        }

        private void LoadAllScripts(string jsonText)
        {
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
                    Debug.LogError("printing path" + path);
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

        private void UpdateTargetText()
        {
            if (targetText != null && currentScript != null)
            {
                targetText.text = "Target: " + currentScript.target_word;
            }
        }

        private void UpdateTargetUI()
        {
            if (currentScript == null)
                return;

            if (targetText != null)
                targetText.text = "Target: " + currentScript.target_word;

            UpdateTargetImage();
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
            CueLevel nextCueLevel = currentCueLevel;

            if (string.IsNullOrEmpty(patientResponse) || currentScript == null)
                return lastCueLevel;

            string responseLower = patientResponse.ToLower();

            if (currentCueLevel == CueLevel.Model)
            {
                Debug.Log("Model cue given again. Do not check for other levels");
                return currentCueLevel;
            }

            foreach (var desc in currentScript.semanticKeywords)
            {
                if (responseLower.Contains(desc.ToLower()))
                {
                    Debug.Log("Semantic keyword detected: " + desc + " Setting cue level to Semantic.");
                    nextCueLevel = CueLevel.Semantic;
                    break;
                }
            }

            foreach (var word in currentScript.phonemicKeywords)
            {
                if (responseLower.Contains(word.ToLower()))
                {
                    Debug.Log("Phonemic keyword detected: " + word + " Setting cue level to Phonemic.");
                    nextCueLevel = CueLevel.Phonemic;
                    break;
                }
            }

            if (currentCueLevel == CueLevel.Semantic && nextCueLevel == CueLevel.Semantic)
            {
                Debug.Log("Semantic cue given again. Current semantic cue count " + semanticCueCount);
                if (semanticCueCount >= maxCueCount)
                {
                    Debug.Log("Max semantic cue count reached. Escalating to phonemic cue.");
                    nextCueLevel = CueLevel.Phonemic;
                }
            }
            else if (currentCueLevel == CueLevel.Phonemic && nextCueLevel == CueLevel.Phonemic)
            {
                phonemicCueCount++;
                Debug.Log("Phonemic cue given again. Current phonemic cue count " + phonemicCueCount);
                if (phonemicCueCount >= maxCueCount)
                {
                    Debug.Log("Max phonemic cue count reached. Escalating to model cue.");
                    nextCueLevel = CueLevel.Model;
                }
            }

            if (nextCueLevel == CueLevel.Semantic)
                semanticCueCount++;
            else if (nextCueLevel == CueLevel.Phonemic)
                phonemicCueCount++;

            return nextCueLevel;
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

            Debug.Log("Patient response: " + patientResponse);
            Debug.Log("Target said? " + saidTarget);
            Debug.Log("Cue level: " + currentCueLevel);
            Debug.Log("Cached hint: " + lastComputedHint);
            Debug.Log("Semantic cue count: " + semanticCueCount);
            Debug.Log("Phonemic cue count: " + phonemicCueCount);
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

                warningBanner.Show("Warning: You have pressed a hint for a higher cue level than expected. Expected: " + currentCueLevel + ", Pressed: " + pressedCueButton);
                Debug.Log("Showing warning banner and hint for " + pressedCueButton);
                return;
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
