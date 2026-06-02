using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Sts2AiAdvisor.Llm;

/// <summary>One archetype's hand-curated strategy note (the "cheat-sheet" / C layer).</summary>
public sealed class ArchetypeGuideEntry
{
    public string Name { get; set; } = "";
    public string Win { get; set; } = "";        // win condition in one line
    public string Priorities { get; set; } = ""; // what to prioritize when picking cards
    public string Confidence { get; set; } = ""; // e.g. "low — needs validation"
}

/// <summary>
/// Loads <c>Sts2AiAdvisor.archetypes.json</c> from beside the DLL. Entries are keyed
/// "character.archetypeId" (e.g. "silent.poison"). Hand-curated from current (2026-06) guides;
/// kept small and date-stamped so it is the only piece that needs periodic refresh.
/// </summary>
public sealed class ArchetypeGuide
{
    public string Updated { get; set; } = "";
    public Dictionary<string, ArchetypeGuideEntry> Archetypes { get; set; } = new();

    public ArchetypeGuideEntry? Lookup(string? character, string? archetypeId)
    {
        if (string.IsNullOrEmpty(character) || string.IsNullOrEmpty(archetypeId)) return null;
        return Archetypes.TryGetValue($"{character.ToLowerInvariant()}.{archetypeId.ToLowerInvariant()}", out var e)
            ? e : null;
    }

    /// <summary>All curated archetype entries for a character (the full "menu" to hand the LLM as reference).</summary>
    public IEnumerable<ArchetypeGuideEntry> ForCharacter(string? character)
    {
        if (string.IsNullOrEmpty(character)) yield break;
        string prefix = character.ToLowerInvariant() + ".";
        foreach (KeyValuePair<string, ArchetypeGuideEntry> kv in Archetypes)
            if (kv.Key.ToLowerInvariant().StartsWith(prefix)) yield return kv.Value;
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static ArchetypeGuide Load()
    {
        try
        {
            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
            string path = Path.Combine(dir, "Sts2AiAdvisor.archetypes.json");
            if (!File.Exists(path))
            {
                ModLog.Warn("Sts2AiAdvisor.archetypes.json not found — archetype cheat-sheet disabled.");
                return new ArchetypeGuide();
            }
            var guide = JsonSerializer.Deserialize<ArchetypeGuide>(File.ReadAllText(path), JsonOptions)
                        ?? new ArchetypeGuide();
            ModLog.Info($"Loaded archetype guide (updated {guide.Updated}) with {guide.Archetypes.Count} entries.");
            return guide;
        }
        catch (Exception ex)
        {
            ModLog.Error("Failed to load archetype guide", ex);
            return new ArchetypeGuide();
        }
    }
}
