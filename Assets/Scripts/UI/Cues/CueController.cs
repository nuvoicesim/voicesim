using UnityEngine;
using System;
using System.IO;
using UnityEngine.UI;
using TMPro;

namespace UI.Cues
{
    // ----------------------------
    // Data Classes
    // ----------------------------
    [System.Serializable]
    public class ScriptEntry
    {
        public int script_number;
        public string title;
        public string target_word;
        public string alternate_target;

        public string[] semanticKeywords;
        public string[] phonemicKeywords;
        public string[] modelKeywords;

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

    // ----------------------------
    // Cue Enum
    // ----------------------------
    public enum CueLevel
    {
        None,      
        Semantic,   
        Phonemic,   
        Model       
    }

    // ----------------------------
    // Main Class
    // ----------------------------
    public class CueController : MonoBehaviour
    {
        public bool cueingActive = false;

        public int scriptNum = 1;             // The active script number
        public ScriptEntry currentScript;     // The active script
        private ScriptData allScripts;        // All scripts loaded from JSON

        private CueLevel lastCueLevel = CueLevel.None;
        private CueLevel currentCueLevel = CueLevel.None;

        [SerializeField] private GameObject cueButtonPanel;             // Panel containing the cue buttons
        [SerializeField] private Button hintButton;                     // Brings up the cueButtonPanel when pressed
        [SerializeField] private Button semanticCueButton;              // Button that triggers the Semantic Cue hint
        [SerializeField] private Button phonemicCueButton;              // Button that triggers the Phonemic Cue hint
        [SerializeField] private Button modelCueButton;                 // Button that triggers the Model Cue hint
        [SerializeField] private GameObject hintBox;                    // Box with the student hint Text as a child element. This is what pops up when the student clicks on a cue button.
        [SerializeField] private TextMeshProUGUI hintText;              // Text element to display the student hint

        void Start()
        {
            // Construct full path to JSON file in StreamingAssets
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
        }

        /// <summary>
        /// Loads and parses all script entries from a JSON string.
        /// </summary>
        /// <param name="jsonText">JSON text containing all script data</param>
        private void LoadAllScripts(string jsonText)
        {
            if (string.IsNullOrEmpty(jsonText))
            {
                Debug.LogError("JSON text is empty in " + jsonText);
                return;
            }

            allScripts = JsonUtility.FromJson<ScriptData>(jsonText);
            if (allScripts != null)
                Debug.Log("Loaded " + allScripts.scripts.Length + " scripts");
            else
                Debug.LogError("Failed to parse JSON target scripts");
        }

        /// <summary>
        /// Sets the current script based on the given script number.
        /// </summary>
        /// <param name="scriptNumber">The script number to select</param>
        private void SetCurrentScript(int scriptNumber)
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
                    Debug.Log("Current script set: " + entry.title);
                    return;
                }
            }

            Debug.LogError("Target script number not found: " + scriptNumber);
        }

        /// <summary>
        /// Checks whether the patient response contains the target or alternate target word.
        /// </summary>
        /// <param name="patientResponse">The patient response text</param>
        /// <returns>True if the target word or alternate target was detected; otherwise false</returns>
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

        /// <summary>
        /// Determines the appropriate cue level for a patient response.
        /// Follows the hierarchy: Semantic -> Phonemic -> Model.
        /// </summary>
        /// <param name="patientResponse">The patient response text</param>
        /// <returns>The CueLevel that should be applied for this response</returns>
        private CueLevel DetermineCueLevel(string patientResponse, bool saidTarget)
        {
            if (string.IsNullOrEmpty(patientResponse) || currentScript == null)
                return lastCueLevel;

            string responseLower = patientResponse.ToLower();

            if (saidTarget)
                return CueLevel.None;

            foreach (var desc in currentScript.semanticKeywords)
            {
                if (responseLower.Contains(desc.ToLower()))
                    return CueLevel.Semantic;
            }

            foreach (var word in currentScript.phonemicKeywords)
            {
                if (responseLower.Contains(word.ToLower()))
                    return CueLevel.Phonemic;
            }

            foreach (var fragment in currentScript.modelKeywords)
            {
                if (responseLower.Contains(fragment.ToLower()))
                    return CueLevel.Model;
            }

            return CueLevel.Semantic;
        }

        /// <summary>
        /// Returns the student-facing hint corresponding to the provided cue level.
        /// </summary>
        /// <param name="cue">The CueLevel determined for the response</param>
        /// <returns>String containing the hint for the student; empty if not available</returns>
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

        /// <summary>
        /// Processes the patient response, determines the cue level, and provides
        /// the corresponding student hint if applicable.
        /// </summary>
        /// <param name="patientResponse">The patient response text</param>
        public void HandleResponse(string patientResponse)
        {
            // TODO:
            // - Update to take studentResponse as a parameter in order to toggle cueing on.
            // - Only check patientResponse if student has asked a question related to the target word.
            //      - How do we determine this?
            // - Make buttons greyed out if the corresponding CueLevel has not yet been reached.
            // - Display button lables on hover.
            // - Add button OnClick event to pop up student hint.
            // - Enable CueButtonPanel when CueHintButton is pressed.
            //      - This is where the Semantic, Phonemic, Model Cue buttons are.

            if (!cueingActive) return;

            SetCurrentScript(scriptNum);

            if (currentScript == null)
            {
                Debug.LogError("No target script loaded");
                return;
            }

            bool saidTarget = CheckTargetWord(patientResponse);
            lastCueLevel = currentCueLevel;
            currentCueLevel = DetermineCueLevel(patientResponse, saidTarget);
            string hint = GetStudentHint(currentCueLevel);
            if (hint != null) SetHintText(hint);

            Debug.Log("Patient response: " + patientResponse);
            Debug.Log("Target said? " + saidTarget);
            Debug.Log("Cue level: " + currentCueLevel);

            if (currentCueLevel != CueLevel.None)
                Debug.Log("Student hint: " + hint);
            else
                Debug.Log("No hint needed. Target word produced");
        }

        // ----------------------------
        // UI HANDLERS
        // ----------------------------
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

        private void SetHintText(string hint)
        {
            //TODO: Only show the hint if the cue button has been pressed and the cue level matches the button pressed.
            //      This way the student can choose when to see the hint, but they only get the hint that corresponds to the patient's current cue level.
            if (hintText != null)
                hintText.text = hint;

        }

    }
}