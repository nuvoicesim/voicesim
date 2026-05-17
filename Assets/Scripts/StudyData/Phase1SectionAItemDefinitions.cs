using System.Collections.Generic;
using UI.Cues;

// Phase 1 Section A (Object Naming) item definitions.
//
// Section A uses runtime-injected CueController scripts so its target words remain
// independent of target_words.json (which is owned by Phase 2 + Phase 1 C/D). The
// five target items below are the SimuCase-aligned Object Naming foundation set.
//
// Script numbers 21-25 are reserved for Section A and matched in
// Phase1StudyItemMetadataResolver. They do not overlap with Phase 2 (1-10),
// Phase 2 Sentence Completion (11+ runtime injected), Section C (11-15),
// Section D (16-20), or Section B (26).
public static class Phase1SectionAItemDefinitions
{
    public const int ScriptStart = 21;
    public const int ScriptEnd = 25;

    public static readonly string[] TargetWordsLowercase =
    {
        "book",
        "ball",
        "knife",
        "cup",
        "safety pin"
    };

    public static readonly string[] TargetWordsDisplay =
    {
        "Book",
        "Ball",
        "Knife",
        "Cup",
        "Safety Pin"
    };

    // Empty image entries by default; Phase1SectionATargetController exposes a
    // SerializeField list in the scene Inspector so final sprites can be dropped
    // in without code changes.
    public static List<TargetImageEntry> BuildDefaultImageSlots()
    {
        List<TargetImageEntry> slots = new List<TargetImageEntry>(TargetWordsLowercase.Length);
        for (int i = 0; i < TargetWordsLowercase.Length; i++)
        {
            slots.Add(new TargetImageEntry
            {
                targetWord = TargetWordsLowercase[i],
                image = null
            });
        }
        return slots;
    }

    public static ScriptEntry[] BuildScriptEntries()
    {
        ScriptEntry[] scripts = new ScriptEntry[TargetWordsLowercase.Length];
        for (int i = 0; i < TargetWordsLowercase.Length; i++)
        {
            scripts[i] = new ScriptEntry
            {
                script_number = ScriptStart + i,
                title = TargetWordsDisplay[i],
                target_word = TargetWordsLowercase[i],
                alternate_target = "",
                semanticKeywords = new string[0],
                phonemicKeywords = new string[0],
                studentHints = new StudentHints
                {
                    Semantic = "",
                    Phonemic = "",
                    Model = ""
                }
            };
        }
        return scripts;
    }
}
