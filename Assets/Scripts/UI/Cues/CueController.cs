using UnityEngine;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;

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
        [SerializeField] private Image imageBorder;
        [SerializeField] private float imageBorderPadding = 5f;
        [SerializeField] private bool includePromptInTargetText = false;
        [SerializeField] private string targetLabelPrefix = "Target:";

        [Header("Target Images Mapping")]
        [SerializeField] private List<TargetImageEntry> targetImages = new List<TargetImageEntry>();

        private Vector2? targetImageMaxSize;

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
                Debug.LogError("No targetText object found");

            if (hintButton != null)
                hintButton.onClick.AddListener(ToggleCuePanel);

            if (confirmTargetButton != null)
                confirmTargetButton.onClick.AddListener(OnConfirmTargetButtonPressed);

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
                targetText.text = BuildTargetDisplayText();
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
                    ResizeTargetImageToSprite();
                    UpdateImageBorderSize();

                    Debug.Log("Updated target image for: " + currentScript.target_word);
                    return;
                }
            }

            Debug.LogWarning("No image mapping found for target word: " + currentScript.target_word);
            targetImage.sprite = null;
            targetImage.enabled = false;
            UpdateImageBorderSize();
        }

        private Vector2 GetTargetImageMaxSize(RectTransform targetRect)
        {
            if (!targetImageMaxSize.HasValue)
                targetImageMaxSize = targetRect.sizeDelta;

            return targetImageMaxSize.Value;
        }

        private void ResizeTargetImageToSprite()
        {
            if (targetImage == null || targetImage.sprite == null)
                return;

            RectTransform targetRect = targetImage.rectTransform;
            Vector2 maxSize = GetTargetImageMaxSize(targetRect);
            Vector2 displaySize = GetSpriteDisplaySize(targetImage.sprite, maxSize);
            targetRect.sizeDelta = displaySize;
        }

        private void UpdateImageBorderSize()
        {
            if (imageBorder == null)
                return;

            if (targetImage == null || !targetImage.enabled || targetImage.sprite == null)
            {
                imageBorder.enabled = false;
                return;
            }

            Vector2 targetSize = targetImage.rectTransform.sizeDelta;

            float padding = Mathf.Max(0f, imageBorderPadding) * 2f;
            RectTransform borderRect = imageBorder.rectTransform;
            borderRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetSize.x + padding);
            borderRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetSize.y + padding);
            imageBorder.enabled = true;
        }

        private static Vector2 GetSpriteDisplaySize(Sprite sprite, Vector2 maxSize)
        {
            float spriteWidth = sprite.rect.width;
            float spriteHeight = sprite.rect.height;
            if (spriteWidth <= 0f || spriteHeight <= 0f || maxSize.x <= 0f || maxSize.y <= 0f)
                return maxSize;

            float scale = Mathf.Min(maxSize.x / spriteWidth, maxSize.y / spriteHeight);
            return new Vector2(spriteWidth * scale, spriteHeight * scale);
        }

        private static string NormalizeWord(string word)
        {
            if (string.IsNullOrWhiteSpace(word))
                return "";

            return word.Trim().ToLower();
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

        public void ResetCueing()
        {
            if (hintBox != null)
                hintBox.SetActive(false);

            if (hintText != null)
                hintText.text = "";
        }

        public void HandleResponse(string patientResponse)
        {
        }

        public void ToggleCuePanel()
        {
            if (cueButtonPanel != null)
                cueButtonPanel.SetActive(!cueButtonPanel.activeSelf);
        }

        public void OnCueButtonPressed(CueLevel pressedCue)
        {
            ShowHint(pressedCue);

            // Section B-only direct cue evidence bridge.
            //
            // Section B has no IStudyItemMetadataProvider (SimuCaseTargetButtonUI
            // is m_Enabled:0 in sectionB.unity) and no SLP↔patient dialogue, so
            // StudyActiveItemTracker.ActiveItemMetadata never becomes B-01 on
            // its own. The generic CueEvidenceRecorder skips sectionB (see
            // CueEvidenceRecorder.HandleCuePressed) to prevent duplicates with
            // this bridge. Recording here, at the exact same call site that
            // already runs ShowHint reliably (verified in Editor), guarantees
            // one cue_pressed StudyInteractionEvent per inner Semantic /
            // Phonemic / Model click, attributed to the synthetic B-01 item
            // Section B finalize uses.
            //
            // Sections A / C / D and Phase 2 are unaffected: scene name guard
            // skips this block, and they continue to use CueEvidenceRecorder
            // with its IStudyItemMetadataProvider-first resolution chain.
            //
            // Outer Q / hintButton presses do NOT call OnCueButtonPressed
            // (the hintButton listener is ToggleCuePanel), so opening the
            // panel never produces cue_pressed evidence. Only inner cue
            // option clicks reach this code.
            //
            // Safe-fail: any failure inside the bridge is caught and logged
            // as a warning; the cue UI and the existing CuePressedEvent
            // invocation continue unchanged so the student flow is never
            // blocked by a recorder bug.
            if (pressedCue != CueLevel.None)
            {
                try
                {
                    // Guard on scriptNum == 26 instead of
                    // SceneManager.GetActiveScene().name == "sectionB". scriptNum
                    // is set by sectionB.unity's PrefabInstance override at
                    // scene-deserialization time (before any code runs) and is
                    // provably unique to Section B across all Phase 1 sections,
                    // every scene file, every prefab default, every JSON content
                    // file, and every Phase 2 runtime script range (A=21..25,
                    // C=11..15, D=16..20, Phase 2 ObjectNaming=1..5, Phase 2
                    // SentenceCompletion=201..210). Using scriptNum removes the
                    // dependency on Unity's active-scene-name reporting, which
                    // was the only static link in the bridge that could explain
                    // the observed WebGL failure (B-01 reaches the payload via
                    // finalize but cue evidence does not).
                    if (scriptNum == 26)
                    {
                        if (Phase1StudyItemMetadataResolver.TryResolve(
                                26,
                                0,
                                null,
                                null,
                                SceneManager.GetActiveScene().name,
                                out StudyItemMetadata metadata) &&
                            metadata != null)
                        {
                            StudyActiveItemTracker.RecordCueEvent(metadata, pressedCue.ToString());
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[CueController] Section B cue record failed: {ex.Message}");
                }
            }

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
            ResetCueing();
        }

        private void ShowHint(CueLevel cue)
        {
            if (hintBox == null || hintText == null)
                return;

            string hint = GetStudentHint(cue);
            if (string.IsNullOrEmpty(hint))
            {
                hintBox.SetActive(false);
                return;
            }

            hintText.text = hint;
            hintBox.SetActive(true);
        }
    }
}
