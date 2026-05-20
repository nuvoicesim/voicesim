using UnityEngine;

public static class SimuCaseSectionCompletionStore
{
    private const string KeyPrefix = "simucase.section.completed.";

    public static void ClearAll()
    {
        PlayerPrefs.DeleteKey(KeyPrefix + "A");
        PlayerPrefs.DeleteKey(KeyPrefix + "B");
        PlayerPrefs.DeleteKey(KeyPrefix + "C");
        PlayerPrefs.DeleteKey(KeyPrefix + "D");
        PlayerPrefs.Save();
    }

    public static void MarkCompleted(string sectionId)
    {
        string normalized = Normalize(sectionId);
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        PlayerPrefs.SetInt(KeyPrefix + normalized, 1);
        PlayerPrefs.Save();
    }

    public static bool IsCompleted(string sectionId)
    {
        string normalized = Normalize(sectionId);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        return PlayerPrefs.GetInt(KeyPrefix + normalized, 0) == 1;
    }

    private static string Normalize(string sectionId)
    {
        if (string.IsNullOrWhiteSpace(sectionId))
            return "";

        string trimmed = sectionId.Trim();
        string upper = trimmed.ToUpperInvariant();

        // Accept "A" / "SectionA" / "section_a" etc and reduce to A/B/C/D when possible.
        if (upper.StartsWith("SECTION"))
            upper = upper.Substring("SECTION".Length).Trim();

        if (upper.Length > 0)
            upper = upper.Substring(0, 1);

        switch (upper)
        {
            case "A":
            case "B":
            case "C":
            case "D":
                return upper;
            default:
                return "";
        }
    }
}

