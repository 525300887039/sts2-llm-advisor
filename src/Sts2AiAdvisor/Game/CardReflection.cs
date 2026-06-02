// New independent implementation. The member names probed here (Keywords, Tags, TargetType,
// DynamicVars, the description method/properties, ModelDb.AllCards) were learned from STS2-Agent's
// PUBLIC API surface (AGPL — used as documentation only, no code copied) and sts2-advisor's
// reflection fallback (MIT). Every access is reflection-based on purpose: an unverified or renamed
// member degrades to empty/null at runtime instead of breaking the build or crashing the game.
// Once cards_dump.txt (dumpCards=true) confirms the real member names for the installed version,
// the hot paths can be switched to direct calls.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Sts2AiAdvisor.Game;

internal static class CardReflection
{
    private const BindingFlags InstAll = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags InstPub = BindingFlags.Instance | BindingFlags.Public;

    // Property names tried, in order, for the card's effect/rules text.
    private static readonly string[] DescriptionProps =
        { "Description", "RulesText", "Body", "Text", "RawText", "DescriptionText" };
    // Parameterless methods tried for the same purpose.
    private static readonly string[] DescriptionMethods =
        { "GetDescription", "GetRulesText", "GetBodyText" };

    /// <summary>Best-effort localized effect text; "" when nothing usable is found.</summary>
    public static string ReadDescription(object? card)
    {
        if (card == null) return "";
        foreach (string p in DescriptionProps)
        {
            string s = ReadString(card, p);
            if (!string.IsNullOrWhiteSpace(s)) return Clean(s);
        }
        foreach (string m in DescriptionMethods)
        {
            try
            {
                MethodInfo? mi = card.GetType().GetMethod(m, InstPub, null, Type.EmptyTypes, null);
                if (mi != null && mi.Invoke(card, null) is string s && !string.IsNullOrWhiteSpace(s))
                    return Clean(s);
            }
            catch { /* try next */ }
        }
        return "";
    }

    /// <summary>Read an IEnumerable property (e.g. Keywords, Tags) as a list of strings.</summary>
    public static List<string> ReadStringList(object? card, string propName)
    {
        var list = new List<string>();
        if (card == null) return list;
        try
        {
            object? val = card.GetType().GetProperty(propName, InstAll)?.GetValue(card)
                          ?? card.GetType().GetField("_" + Lower1(propName), InstAll)?.GetValue(card);
            if (val is IEnumerable en && val is not string)
            {
                foreach (object? item in en)
                {
                    string s = item?.ToString() ?? "";
                    if (!string.IsNullOrWhiteSpace(s)) list.Add(s);
                }
            }
        }
        catch { /* degrade to empty */ }
        return list;
    }

    public static string ReadTargetType(object? card) => ReadString(card, "TargetType");

    /// <summary>Best-effort numeric dynamic value (e.g. "damage", "block") from a DynamicVars-like collection.</summary>
    public static int? ReadDynamicValue(object? card, string name)
    {
        if (card == null) return null;
        try
        {
            object? vars = card.GetType().GetProperty("DynamicVars", InstAll)?.GetValue(card);
            if (vars is not IEnumerable en) return null;
            foreach (object? v in en)
            {
                if (v == null) continue;
                string vn = ReadString(v, "Name");
                if (string.IsNullOrEmpty(vn)) vn = ReadString(v, "Id");
                if (!string.Equals(vn, name, StringComparison.OrdinalIgnoreCase)) continue;
                foreach (string numProp in new[] { "BaseValue", "Value", "PreviewValue", "Amount" })
                {
                    object? nv = v.GetType().GetProperty(numProp, InstAll)?.GetValue(v);
                    if (nv is int i) return i;
                    if (nv != null && int.TryParse(nv.ToString(), out int parsed)) return parsed;
                }
            }
        }
        catch { /* degrade */ }
        return null;
    }

    /// <summary>The card's UPPER_SNAKE id (Id.Entry), or "" on failure.</summary>
    public static string ReadId(object? card)
    {
        try
        {
            object? idObj = card?.GetType().GetProperty("Id", InstAll)?.GetValue(card);
            if (idObj == null) return "";
            string entry = ReadString(idObj, "Entry");
            return string.IsNullOrEmpty(entry) ? (idObj.ToString() ?? "") : entry;
        }
        catch { return ""; }
    }

    /// <summary>Enumerate the global card database (ModelDb.AllCards) reflectively. Empty on failure.</summary>
    public static IEnumerable<object> EnumerateAllCards()
    {
        Type? modelDb = ResolveType("MegaCrit.Sts2.Core.Models.ModelDb") ?? ResolveType("ModelDb");
        if (modelDb == null) yield break;
        object? all = modelDb.GetProperty("AllCards", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null)
                      ?? modelDb.GetField("AllCards", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
        if (all is not IEnumerable en) yield break;
        foreach (object? c in en)
            if (c != null) yield return c;
    }

    public static string ReadString(object? obj, string prop)
    {
        try
        {
            object? v = obj?.GetType().GetProperty(prop, InstAll)?.GetValue(obj);
            return v?.ToString() ?? "";
        }
        catch { return ""; }
    }

    private static Type? ResolveType(string fullName)
    {
        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
        {
            try { if (a.GetType(fullName) is { } t) return t; }
            catch { /* skip */ }
        }
        return null;
    }

    private static string Lower1(string s) => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

    // Strip the game's BBCode-ish markup ([gold]..[/gold], [blue].., [energy:1]) so the prompt stays clean.
    private static string Clean(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        bool inTag = false;
        foreach (char c in s)
        {
            if (c == '[') { inTag = true; continue; }
            if (c == ']') { inTag = false; continue; }
            if (!inTag) sb.Append(c);
        }
        return sb.ToString().Replace('\n', ' ').Replace("  ", " ").Trim();
    }
}
