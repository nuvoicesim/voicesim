using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

[Serializable]
public class StudySectionStatus
{
    public string sectionId;
    public string status;
    public string label;
}

[Serializable]
public class StudySectionStatusPayload
{
    public List<StudySectionStatus> sections = new List<StudySectionStatus>();
}

public static class StudySectionStatusAdapter
{
    public const string StatusAvailable = "available";
    public const string StatusCompleted = "completed";
    public const string StatusLocked = "locked";

    public static bool TryParseSectionStatuses(string json, out List<StudySectionStatus> statuses)
    {
        statuses = null;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            string trimmed = json.Trim();
            if (trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                statuses = JsonConvert.DeserializeObject<List<StudySectionStatus>>(trimmed);
            }
            else
            {
                StudySectionStatusPayload payload = JsonConvert.DeserializeObject<StudySectionStatusPayload>(trimmed);
                statuses = payload != null ? payload.sections : null;
            }

            if (statuses == null)
                statuses = new List<StudySectionStatus>();

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[StudySectionStatusAdapter] Failed to parse section status JSON: {ex.Message}");
            return false;
        }
    }

    public static string NormalizeStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return StatusAvailable;

        string normalized = status.Trim().ToLowerInvariant();
        if (normalized == StatusCompleted || normalized == StatusLocked)
            return normalized;

        return StatusAvailable;
    }

    public static bool IsDisabledStatus(string status)
    {
        string normalized = NormalizeStatus(status);
        return normalized == StatusCompleted || normalized == StatusLocked;
    }

    public static string NormalizeSectionId(string sectionId)
    {
        if (string.IsNullOrWhiteSpace(sectionId))
            return string.Empty;

        string normalized = sectionId.Trim();
        string lower = normalized.ToLowerInvariant();

        if (lower == "a" || lower == "sectiona" || lower == "phase1-section-a")
            return "A";
        if (lower == "b" || lower == "sectionb" || lower == "phase1-section-b")
            return "B";
        if (lower == "c" || lower == "sectionc" || lower == "phase1-section-c")
            return "C";
        if (lower == "d" || lower == "sectiond" || lower == "phase1-section-d")
            return "D";

        return normalized;
    }

    public static Dictionary<string, StudySectionStatus> BuildStatusMap(List<StudySectionStatus> statuses)
    {
        Dictionary<string, StudySectionStatus> map = new Dictionary<string, StudySectionStatus>();
        if (statuses == null)
            return map;

        foreach (StudySectionStatus status in statuses)
        {
            string sectionId = NormalizeSectionId(status?.sectionId);
            if (string.IsNullOrWhiteSpace(sectionId))
                continue;

            map[sectionId] = new StudySectionStatus
            {
                sectionId = sectionId,
                status = NormalizeStatus(status.status),
                label = status.label
            };
        }

        return map;
    }
}
