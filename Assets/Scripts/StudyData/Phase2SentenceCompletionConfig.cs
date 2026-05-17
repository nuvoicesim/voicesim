using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UI.Cues;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class Phase2SentenceCompletionConfig
{
    public string configId;
    public string phaseId;
    public string activityType;
    public string scoringMode;
    public string taskType;
    public string displayMode;
    public Phase2SentenceCompletionGroupConfig[] groups;
    public Phase2SentenceCompletionItemConfig[] items;
}

[Serializable]
public class Phase2SentenceCompletionGroupConfig
{
    public string groupId;
    public string patientProfile;
    public string taskId;
    public string sectionId;
    public int scriptNumberStart;
    public string[] itemIds;
}

[Serializable]
public class Phase2SentenceCompletionItemConfig
{
    public string itemId;
    public int ordinal;
    public string targetWord;
    public string alternateTarget;
    public string semanticCue;
    public string phonemicCue;
    public string modelCue;
    public string[] semanticKeywords;
    public string[] phonemicKeywords;
}

public static class Phase2TrainingDefaults
{
    public const string GroupBen = "ben";
    public const string GroupMaria = "maria";
    public const string SentenceCompletionConfigPath = "Cues/phase2_sentence_completion_items.json";
    public const string SentenceCompletionDisplayTargetOnly = "target_only";
}

public static class Phase2SentenceCompletionConfigLoader
{
    public static IEnumerator Load(
        Action<Phase2SentenceCompletionConfig> onLoaded,
        Action<string> onError = null,
        string relativePath = Phase2TrainingDefaults.SentenceCompletionConfigPath)
    {
        string path = BuildStreamingAssetsPath(relativePath);

        if (path.Contains("://"))
        {
            using (UnityWebRequest request = UnityWebRequest.Get(path))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    string message = $"Failed to load Phase 2 sentence completion config: {request.error} Path: {path}";
                    Debug.LogError(message);
                    onError?.Invoke(message);
                    yield break;
                }

                CompleteLoad(request.downloadHandler.text, onLoaded, onError);
            }

            yield break;
        }

        if (!File.Exists(path))
        {
            string message = $"Could not find Phase 2 sentence completion config at path: {path}";
            Debug.LogError(message);
            onError?.Invoke(message);
            yield break;
        }

        CompleteLoad(File.ReadAllText(path), onLoaded, onError);
    }

    public static bool TryParse(string json, out Phase2SentenceCompletionConfig config, out string error)
    {
        config = null;
        error = "";

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Phase 2 sentence completion config JSON is empty.";
            return false;
        }

        try
        {
            config = JsonUtility.FromJson<Phase2SentenceCompletionConfig>(json);
        }
        catch (Exception ex)
        {
            error = $"Phase 2 sentence completion config JSON parse failed: {ex.Message}";
            return false;
        }

        if (config == null || config.items == null || config.items.Length == 0)
        {
            error = "Phase 2 sentence completion config contains no items.";
            return false;
        }

        if (config.groups == null || config.groups.Length == 0)
        {
            error = "Phase 2 sentence completion config contains no groups.";
            return false;
        }

        NormalizeConfig(config);
        return true;
    }

    public static bool TryGetGroup(
        Phase2SentenceCompletionConfig config,
        string groupId,
        out Phase2SentenceCompletionGroupConfig group)
    {
        group = null;
        if (config == null || config.groups == null || string.IsNullOrWhiteSpace(groupId))
            return false;

        string normalizedGroupId = groupId.Trim();
        foreach (Phase2SentenceCompletionGroupConfig candidate in config.groups)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.groupId))
                continue;

            if (string.Equals(candidate.groupId.Trim(), normalizedGroupId, StringComparison.OrdinalIgnoreCase))
            {
                group = candidate;
                return true;
            }
        }

        return false;
    }

    public static List<Phase2SentenceCompletionItemConfig> GetItemsForGroup(
        Phase2SentenceCompletionConfig config,
        string groupId)
    {
        List<Phase2SentenceCompletionItemConfig> selectedItems = new List<Phase2SentenceCompletionItemConfig>();
        if (!TryGetGroup(config, groupId, out Phase2SentenceCompletionGroupConfig group) ||
            group.itemIds == null ||
            config.items == null)
        {
            return selectedItems;
        }

        foreach (string itemId in group.itemIds)
        {
            if (TryGetItem(config, itemId, out Phase2SentenceCompletionItemConfig item))
                selectedItems.Add(item);
        }

        return selectedItems;
    }

    public static bool TryGetItem(
        Phase2SentenceCompletionConfig config,
        string itemId,
        out Phase2SentenceCompletionItemConfig item)
    {
        item = null;
        if (config == null || config.items == null || string.IsNullOrWhiteSpace(itemId))
            return false;

        string normalizedItemId = itemId.Trim();
        foreach (Phase2SentenceCompletionItemConfig candidate in config.items)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.itemId))
                continue;

            if (string.Equals(candidate.itemId.Trim(), normalizedItemId, StringComparison.OrdinalIgnoreCase))
            {
                item = candidate;
                return true;
            }
        }

        return false;
    }

    public static ScriptEntry ToScriptEntry(Phase2SentenceCompletionItemConfig item, int scriptNumber)
    {
        if (item == null)
            return null;

        return new ScriptEntry
        {
            script_number = scriptNumber,
            title = item.semanticCue,
            target_word = item.targetWord,
            alternate_target = item.alternateTarget,
            semanticKeywords = item.semanticKeywords,
            phonemicKeywords = item.phonemicKeywords,
            studentHints = new StudentHints
            {
                Semantic = item.semanticCue,
                Phonemic = item.phonemicCue,
                Model = item.modelCue
            }
        };
    }

    public static ScriptEntry[] ToScriptEntriesForGroup(
        Phase2SentenceCompletionConfig config,
        string groupId)
    {
        if (!TryGetGroup(config, groupId, out Phase2SentenceCompletionGroupConfig group))
            return new ScriptEntry[0];

        List<Phase2SentenceCompletionItemConfig> items = GetItemsForGroup(config, groupId);
        List<ScriptEntry> scripts = new List<ScriptEntry>(items.Count);
        for (int i = 0; i < items.Count; i++)
        {
            ScriptEntry script = ToScriptEntry(items[i], group.scriptNumberStart + i);
            if (script != null)
                scripts.Add(script);
        }

        return scripts.ToArray();
    }

    public static StudyItemMetadata ToStudyItemMetadata(
        Phase2SentenceCompletionConfig config,
        Phase2SentenceCompletionGroupConfig group,
        Phase2SentenceCompletionItemConfig item,
        int zeroBasedIndex)
    {
        if (item == null)
            return null;

        return new StudyItemMetadata
        {
            phaseId = StudyDataDefaults.FirstNonEmpty(config?.phaseId, StudyDataDefaults.Phase2),
            itemId = item.itemId,
            taskId = StudyDataDefaults.FirstNonEmpty(group?.taskId, "phase2-sentence-completion"),
            taskType = StudyDataDefaults.FirstNonEmpty(config?.taskType, StudyDataDefaults.TaskTypeSentenceCompletionPractice),
            sectionId = group?.sectionId,
            sectionType = StudyDataDefaults.TaskTypeSentenceCompletionPractice,
            scriptNumber = group != null ? group.scriptNumberStart + Math.Max(0, zeroBasedIndex) : item.ordinal,
            promptText = item.semanticCue,
            targetAnswer = item.targetWord,
            alternateTarget = item.alternateTarget,
            activityType = StudyDataDefaults.FirstNonEmpty(config?.activityType, StudyDataDefaults.ActivityTraining),
            scoringMode = StudyDataDefaults.FirstNonEmpty(config?.scoringMode, StudyDataDefaults.ScoringNone)
        };
    }

    private static void CompleteLoad(
        string json,
        Action<Phase2SentenceCompletionConfig> onLoaded,
        Action<string> onError)
    {
        if (TryParse(json, out Phase2SentenceCompletionConfig config, out string error))
        {
            onLoaded?.Invoke(config);
            return;
        }

        Debug.LogError(error);
        onError?.Invoke(error);
    }

    private static string BuildStreamingAssetsPath(string relativePath)
    {
        string normalizedRelativePath = string.IsNullOrWhiteSpace(relativePath)
            ? Phase2TrainingDefaults.SentenceCompletionConfigPath
            : relativePath.Replace("\\", "/").TrimStart('/');

        return Application.streamingAssetsPath.TrimEnd('/') + "/" + normalizedRelativePath;
    }

    private static void NormalizeConfig(Phase2SentenceCompletionConfig config)
    {
        config.phaseId = StudyDataDefaults.FirstNonEmpty(config.phaseId, StudyDataDefaults.Phase2);
        config.activityType = StudyDataDefaults.FirstNonEmpty(config.activityType, StudyDataDefaults.ActivityTraining);
        config.scoringMode = StudyDataDefaults.FirstNonEmpty(config.scoringMode, StudyDataDefaults.ScoringNone);
        config.taskType = StudyDataDefaults.FirstNonEmpty(config.taskType, StudyDataDefaults.TaskTypeSentenceCompletionPractice);
        config.displayMode = StudyDataDefaults.FirstNonEmpty(config.displayMode, Phase2TrainingDefaults.SentenceCompletionDisplayTargetOnly);
    }
}
