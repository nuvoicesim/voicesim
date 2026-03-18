using UnityEngine;
using System;
using System.IO;
using UnityEngine.UI;
using TMPro;
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

        [SerializeField] private GameObject cueButtonPanel;
        [SerializeField] private Button hintButton;
        [SerializeField] private Button semanticCueButton;
        [SerializeField] private Button phonemicCueButton;
        [SerializeField] private Button modelCueButton;
        [SerializeField] private GameObject hintBox;
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private TextMeshProUGUI targetText;
        [SerializeField] private TargetButtonUI targetButtonUI;

        void Start()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Cues/target_words.json");

            if (File.Exists(path))
            {
                string jsonText = File.ReadAllText(path);
                LoadAllScripts(jsonText);
                Debug.Log("Loaded scripts from StreamingAssets");
            }
            else
            {
                Debug.LogError("Could not find target_words.json in StreamingAssets at path: " + path);
            }

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
                return;
            }

            SetCurrentScriptByNumber(scriptNum);
        }

        private void LoadAllScripts(string jsonText)
        {
            if (string.IsNullOrEmpty(jsonText))
            {
                Debug.LogError("JSON text is empty in " + jsonText);
                return;
            }

            allScripts = JsonUtility.FromJson<ScriptData>(jsonText);
            if (allScripts != null && allScripts.scripts != null)
                Debug.Log("Loaded " + allScripts.scripts.Length + " scripts");
            else
                Debug.LogError("Failed to parse JSON target scripts");
        }

        public void SetCurrentScriptByNumber(int scriptNumber)
        {
            if (allScripts == null || allScripts.scripts == null)
            {
                Debug.LogError("Target scripts not loaded. Call LoadAllScripts first");
                return;
            }

            foreach (var entry in allScripts.scripts)
            {
                if (entry.script_number == scriptNumber)
                {
                    currentScript = entry;
                    scriptNum = scriptNumber;

                    Debug.Log("Current script set: " + entry.title);

                    if (targetText != null)
                        targetText.text = "Target: " + currentScript.target_word;

                    return;
                }
            }

            Debug.LogError("Target script number not found: " + scriptNumber);
        }

        private bool CheckTargetWord(string patientResponse)
        {
            if (string.IsNullOrEmpty(patientResponse) || currentScript == null) return false;

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
            if (!cueingActive) return;

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

            if (currentCueLevel != CueLevel.None)
                Debug.Log("Student hint (cached): " + lastComputedHint);
            else
                Debug.Log("No hint needed");
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

        private void ShowHintIfAllowed()
        {
            if (hintBox == null || hintText == null)
                return;

            if (pressedCueButton == CueLevel.Model && currentCueLevel != CueLevel.Model)
            {
                Debug.Log("Model cue button pressed but current cue level is not Model.");

                string hint = GetStudentHint(CueLevel.Model);
                lastComputedHint = hint ?? "";

                hintText.text = lastComputedHint;
                hintBox.SetActive(true);
                Debug.Log("Showing student hint for " + CueLevel.Model);
                return;
            }

            if (currentCueLevel != CueLevel.None
                && pressedCueButton == currentCueLevel
                && !string.IsNullOrEmpty(lastComputedHint))
            {
                hintText.text = lastComputedHint;
                hintBox.SetActive(true);
                Debug.Log("Showing student hint for " + currentCueLevel);
            }
            else
            {
                hintBox.SetActive(false);
                if (pressedCueButton != currentCueLevel)
                    Debug.Log("Hint hidden because pressed cue does not match current cue level. Pressed: "
                        + pressedCueButton + " Current: " + currentCueLevel);
            }
        }
    }
}