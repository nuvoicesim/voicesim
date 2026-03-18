using UnityEngine;
using System;
using System.IO;
using UnityEngine.UI;
using TMPro;
using UI.Cues.WarningSystem;

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
        None = 0,      
        Semantic = 1,   
        Phonemic = 2,   
        Model = 3      
    }

    // ----------------------------
    // Main Class
    // ----------------------------
    public class CueController : MonoBehaviour
    {
        public bool cueingActive = false;

        public int scriptNum = 1;                               // The active script number
        public ScriptEntry currentScript;                       // The active script
        private ScriptData allScripts;                          // All scripts loaded from JSON

        private CueLevel lastCueLevel = CueLevel.None;
        private CueLevel currentCueLevel = CueLevel.None;
        
        private int maxCueCount = 2;                            // Maximum number of cues before escalating to the next level
        private int semanticCueCount = 0;                       // Tracks how many times the student has received a semantic cue for the current target word.
        private int phonemicCueCount = 0;                       // Tracks how many times the student has received a phonemic cue for the current target word.

        private CueLevel pressedCueButton = CueLevel.None;      // Tracks which cue button the student most recently pressed.
        private string lastComputedHint = "";                   // Stores the most recently computed hint for the currentCueLevel.

        [SerializeField] private GameObject cueButtonPanel;     // Panel containing the cue buttons
        [SerializeField] private Button hintButton;             // Brings up the cueButtonPanel when pressed
        [SerializeField] private Button semanticCueButton;      // Button that triggers the Semantic Cue hint
        [SerializeField] private Button phonemicCueButton;      // Button that triggers the Phonemic Cue hint
        [SerializeField] private Button modelCueButton;         // Button that triggers the Model Cue hint
        [SerializeField] private GameObject hintBox;            // Box with the student hint Text as a child element. This is what pops up when the student clicks on a cue button.
        [SerializeField] private TextMeshProUGUI hintText;      // Text element to display the student hint
        [SerializeField] private TextMeshProUGUI targetText;    // Text element to display the current target word for the student
        [SerializeField] private TargetButtonUI targetButtonUI; // Reference to the TargetButtonUI script to check if the target has been confirmed



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

            // Wire up UI button callbacks
            if (semanticCueButton != null)
                semanticCueButton.onClick.AddListener(() => OnCueButtonPressed(CueLevel.Semantic));
            if (phonemicCueButton != null)
                phonemicCueButton.onClick.AddListener(() => OnCueButtonPressed(CueLevel.Phonemic));
            if (modelCueButton != null)
                modelCueButton.onClick.AddListener(() => OnCueButtonPressed(CueLevel.Model));

            // Ensure hint box starts hidden
            if (hintBox != null)
                hintBox.SetActive(false);

            if (targetText == null)
            {
                Debug.LogError("No targetText object found");
                return;
            }

            SetCurrentScript(scriptNum);
            targetText.text = "Target: " + currentScript.target_word;
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
            CueLevel nextCueLevel = currentCueLevel;

            if (string.IsNullOrEmpty(patientResponse) || currentScript == null)
                return lastCueLevel;

            string responseLower = patientResponse.ToLower();

            //if (saidTarget)
                //return CueLevel.None;
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
                    break; // If we detect any semantic keyword, we can break out of the loop and
                }
            }

            foreach (var word in currentScript.phonemicKeywords)
            {
                if (responseLower.Contains(word.ToLower()))
                {
                    Debug.Log("Phonemic keyword detected: " + word + " Setting cue level to Phonemic.");
                    nextCueLevel = CueLevel.Phonemic;
                    break; // If we detect any phonemic keyword, we can break out of the loop and set the cue level to Phonemic.
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

        private void UpdateTarget()
        {
            if (targetButtonUI.IsConfirmed())
            {
                scriptNum = UnityEngine.Random.Range(2, 11);
                SetCurrentScript(scriptNum);

                if (targetText != null && currentScript != null)
                {
                    targetText.text = "Target: " + currentScript.target_word;
                }
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
            // Optionally hide the hint box when resetting
            if (hintBox != null)
                hintBox.SetActive(false);
            UpdateTarget();
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
            // - Make cue text look nicer.
            // - Make recommended cue button highlighted to guide the student towards the next appropriate cue level.
            // - Add a target word to the UI so the student knows what the target is and can better understand the hints.

            if (!cueingActive) return;

            if (currentScript == null)
            {
                Debug.LogError("No target script loaded");
                return;
            }

            bool saidTarget = CheckTargetWord(patientResponse);
            lastCueLevel = currentCueLevel;
            currentCueLevel = DetermineCueLevel(patientResponse, saidTarget);

            // compute and cache the hint but do NOT show it unless the student pressed the matching cue button
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

            // After updating currentCueLevel and caching the hint, update UI visibility
            
        }

        // ----------------------------
        // UI HANDLERS
        // ----------------------------

        /// <summary>
        /// Toggles the visibility of the cue button panel.
        /// </summary>
        public void ToggleCuePanel()
        {
            Debug.Log("Toggling cue button panel");
            if (cueButtonPanel != null)
                cueButtonPanel.SetActive(!cueButtonPanel.activeSelf);
        }

        /// <summary>
        /// Toggles the visibility of the hint box.
        /// </summary>
        public void ToggleHintBox()
        {
            Debug.Log("Toggling hint box");
            if (hintBox != null)
                hintBox.SetActive(!hintBox.activeSelf);
        }

        /// <summary>
        /// Function to be called when a Cue button is pressed. Caches which 
        /// button was pressed and then calls ShowHintIfAllowed to determine 
        /// whether to show the hint based on currentCueLevel and the pressed button.
        /// </summary>
        /// <param name="pressedCue"></param>
        public void OnCueButtonPressed(CueLevel pressedCue)
        {
            pressedCueButton = pressedCue;
            Debug.Log("Cue button pressed: " + pressedCue);
            ShowHintIfAllowed();
        }

        /// <summary>
        /// Shows the cached hint only when the student has pressed the cue button that matches currentCueLevel.
        /// Hides the hint box otherwise.
        /// </summary>
        private void ShowHintIfAllowed()
        {
            // Preconditions
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

            // Only show the hint when:
            // - there is a cached hint for the current cue level,
            // - the current cue level is not None,
            // - and the student pressed the matching cue button.
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
                // If not allowed to show, ensure hint box is hidden
                hintBox.SetActive(false);
                if (pressedCueButton != currentCueLevel)
                    Debug.Log("Hint hidden because pressed cue does not match current cue level. Pressed: " + pressedCueButton + " Current: " + currentCueLevel);
            }
        }
    }
}