using System.Collections;
using System.Collections.Generic;
using UI.Cues;
using UI.Cues.WarningSystem;
using UnityEngine;
using UnityEngine.UI;

public class Phase2SentenceCompletionMetadataProvider : MonoBehaviour, IStudyItemMetadataProvider
{
    [SerializeField] private string groupId = Phase2TrainingDefaults.GroupBen;
    [SerializeField] private string configRelativePath = Phase2TrainingDefaults.SentenceCompletionConfigPath;
    [SerializeField] private int currentItemIndex = 0;

    [Header("Phase 2 Scene Wiring")]
    [SerializeField] private CueController cueController;
    [SerializeField] private CueSkipGuard cueSkipGuard;
    [SerializeField] private Button nextTargetButton;
    [SerializeField] private StudyCompletionChecklistManager completionChecklistManager;
    [SerializeField] private bool injectRuntimeCueScripts = true;
    [SerializeField] private bool showTargetOnly = true;

    private Phase2SentenceCompletionConfig config;
    private Phase2SentenceCompletionGroupConfig activeGroup;
    private readonly List<Phase2SentenceCompletionItemConfig> activeItems = new List<Phase2SentenceCompletionItemConfig>();

    public string GroupId => groupId;
    public int CurrentItemIndex => currentItemIndex;
    public int CurrentItemCount => activeItems.Count;
    public string CurrentTargetWord => TryGetCurrentItem(out Phase2SentenceCompletionItemConfig item) ? item.targetWord : "";

    private IEnumerator Start()
    {
        ResolveSceneReferences();

        if (nextTargetButton != null)
            nextTargetButton.onClick.AddListener(OnNextTargetClicked);

        yield return Phase2SentenceCompletionConfigLoader.Load(
            loadedConfig =>
            {
                config = loadedConfig;
                if (SelectGroup(groupId))
                    ApplyLoadedGroupToScene();
            },
            error => Debug.LogError($"[Phase2SentenceCompletionMetadataProvider] {error}"),
            configRelativePath);
    }

    private void OnDestroy()
    {
        if (nextTargetButton != null)
            nextTargetButton.onClick.RemoveListener(OnNextTargetClicked);
    }

    public bool SelectGroup(string nextGroupId)
    {
        if (config == null)
            return false;

        if (!Phase2SentenceCompletionConfigLoader.TryGetGroup(config, nextGroupId, out Phase2SentenceCompletionGroupConfig group))
        {
            Debug.LogWarning($"[Phase2SentenceCompletionMetadataProvider] Group '{nextGroupId}' was not found.");
            return false;
        }

        groupId = group.groupId;
        activeGroup = group;
        activeItems.Clear();
        activeItems.AddRange(Phase2SentenceCompletionConfigLoader.GetItemsForGroup(config, groupId));
        currentItemIndex = Mathf.Clamp(currentItemIndex, 0, Mathf.Max(0, activeItems.Count - 1));
        return activeItems.Count > 0;
    }

    public bool SetCurrentItemIndex(int index)
    {
        if (activeItems.Count == 0 || index < 0 || index >= activeItems.Count)
            return false;

        currentItemIndex = index;
        return true;
    }

    public bool AdvanceToNextItem()
    {
        return SetCurrentItemIndex(currentItemIndex + 1);
    }

    public bool TryGetCurrentStudyItemMetadata(out StudyItemMetadata metadata)
    {
        metadata = null;
        if (!TryGetCurrentItem(out Phase2SentenceCompletionItemConfig item))
            return false;

        metadata = Phase2SentenceCompletionConfigLoader.ToStudyItemMetadata(
            config,
            activeGroup,
            item,
            currentItemIndex);
        return metadata != null;
    }

    public bool TryGetCurrentScriptEntry(out ScriptEntry scriptEntry)
    {
        scriptEntry = null;
        if (!TryGetCurrentItem(out Phase2SentenceCompletionItemConfig item))
            return false;

        int scriptNumber = activeGroup != null ? activeGroup.scriptNumberStart + currentItemIndex : item.ordinal;
        scriptEntry = Phase2SentenceCompletionConfigLoader.ToScriptEntry(item, scriptNumber);
        return scriptEntry != null;
    }

    public List<StudyItemMetadata> GetActiveStudyItemMetadataList()
    {
        List<StudyItemMetadata> metadataItems = new List<StudyItemMetadata>(activeItems.Count);
        for (int i = 0; i < activeItems.Count; i++)
        {
            StudyItemMetadata metadata = Phase2SentenceCompletionConfigLoader.ToStudyItemMetadata(
                config,
                activeGroup,
                activeItems[i],
                i);

            if (metadata != null)
                metadataItems.Add(metadata);
        }

        return metadataItems;
    }

    public List<string> GetTargetOnlyChecklistLabels()
    {
        List<string> labels = new List<string>(activeItems.Count);
        foreach (Phase2SentenceCompletionItemConfig item in activeItems)
        {
            if (item == null)
                continue;

            labels.Add("Practice Target: " + item.targetWord);
        }

        return labels;
    }

    private void ApplyLoadedGroupToScene()
    {
        ResolveSceneReferences();

        if (injectRuntimeCueScripts)
            ApplyRuntimeCueScripts();

        if (completionChecklistManager != null)
            completionChecklistManager.ConfigureCompletionItems(GetActiveStudyItemMetadataList(), GetTargetOnlyChecklistLabels());

        UpdateNextTargetButtonInteractivity();
    }

    private void ApplyRuntimeCueScripts()
    {
        if (cueController == null || config == null || activeGroup == null)
            return;

        if (showTargetOnly)
            cueController.SetIncludePromptInTargetText(false);

        cueController.SetTargetLabelPrefix("Current Target:");

        ScriptEntry[] runtimeScripts = Phase2SentenceCompletionConfigLoader.ToScriptEntriesForGroup(config, groupId);
        if (runtimeScripts == null || runtimeScripts.Length == 0)
            return;

        cueController.UseRuntimeScripts(runtimeScripts, activeGroup.scriptNumberStart + currentItemIndex);
        cueController.ResetCueing();
    }

    private void OnNextTargetClicked()
    {
        if (activeItems.Count == 0)
            return;

        if (currentItemIndex >= activeItems.Count - 1)
        {
            Debug.Log($"[Phase2SentenceCompletionMetadataProvider] Already at final target: {CurrentTargetWord}");
            UpdateNextTargetButtonInteractivity();
            return;
        }

        if (TryGetCurrentStudyItemMetadata(out StudyItemMetadata metadata))
            StudyTaskResultBuffer.TryFinalizeCurrentItem(metadata, null, null, out _);

        currentItemIndex++;

        if (cueController != null && activeGroup != null)
        {
            cueController.SetCurrentScript(activeGroup.scriptNumberStart + currentItemIndex);
            cueController.ResetCueing();
        }

        if (cueSkipGuard != null)
        {
            cueSkipGuard.ResetCueing();
            cueSkipGuard.PauseWarnings(0f);
        }

        UpdateNextTargetButtonInteractivity();
    }

    private void UpdateNextTargetButtonInteractivity()
    {
        if (nextTargetButton != null)
            nextTargetButton.interactable = activeItems.Count > 0 && currentItemIndex < activeItems.Count - 1;
    }

    private void ResolveSceneReferences()
    {
        if (cueController == null)
            cueController = FindObjectOfType<CueController>();

        if (cueSkipGuard == null)
            cueSkipGuard = FindObjectOfType<CueSkipGuard>();

        if (completionChecklistManager == null)
            completionChecklistManager = FindObjectOfType<StudyCompletionChecklistManager>();

        if (nextTargetButton == null)
            nextTargetButton = FindButtonByName("MarkCorrectButton");
    }

    private static Button FindButtonByName(string buttonName)
    {
        Button[] buttons = Resources.FindObjectsOfTypeAll<Button>();
        foreach (Button button in buttons)
        {
            if (button != null && button.gameObject.scene.IsValid() && button.gameObject.name == buttonName)
                return button;
        }

        return null;
    }

    private bool TryGetCurrentItem(out Phase2SentenceCompletionItemConfig item)
    {
        item = null;
        if (activeItems.Count == 0 || currentItemIndex < 0 || currentItemIndex >= activeItems.Count)
            return false;

        item = activeItems[currentItemIndex];
        return item != null;
    }
}
