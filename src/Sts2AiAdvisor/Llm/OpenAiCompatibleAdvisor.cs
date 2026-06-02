using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sts2AiAdvisor.Game;
using Sts2AiAdvisor.Game.Archetypes;

namespace Sts2AiAdvisor.Llm;

/// <summary>
/// Talks to any OpenAI-compatible /chat/completions endpoint (DeepSeek, Kimi, GLM, OpenRouter,
/// Ollama, ...). Zero third-party SDK: System.Net.Http + System.Text.Json only.
/// </summary>
public sealed class OpenAiCompatibleAdvisor : ILlmAdvisor
{
    // One shared client for the process; the request carries its own auth header per call.
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(90) };

    private static readonly JsonSerializerOptions ParseJson = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly LlmConfig _config;
    private readonly ArchetypeGuide _guide;

    public OpenAiCompatibleAdvisor(LlmConfig config, ArchetypeGuide? guide = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _guide = guide ?? new ArchetypeGuide();
    }

    public async Task<AdviceResult> GetAdviceAsync(AdviceRequest req, CancellationToken ct)
    {
        if (!_config.IsValid)
            throw new InvalidOperationException("LLM config is invalid (missing apiKey or baseUrl).");

        DeckAnalysis analysis = DeckAnalyzer.Analyze(req.State.Character, req.State.DeckCards);
        string systemPrompt = BuildSystemPrompt(req.State.Locale);
        string userPrompt = BuildUserPrompt(req.State, analysis);

        var body = new
        {
            model = _config.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt },
            },
            response_format = new { type = "json_object" },
            temperature = 0.3,
            // Reasoning models (e.g. deepseek-v4-*) spend a large token budget on hidden
            // reasoning before the JSON answer; too small a cap truncates the JSON. Keep generous.
            max_tokens = 4096,
        };

        string url = _config.BaseUrl.TrimEnd('/') + "/chat/completions";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);
        // Some OpenAI-compatible gateways (e.g. opencode-go) sit behind Cloudflare, which 403s the
        // default .NET User-Agent (error 1010). Present a browser-like UA so the call isn't bot-blocked.
        request.Headers.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
        request.Headers.Accept.ParseAdd("application/json");
        request.Content = new StringContent(
            JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using HttpResponseMessage resp = await Http.SendAsync(request, ct).ConfigureAwait(false);
        string respText = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"LLM HTTP {(int)resp.StatusCode}: {Truncate(respText, 400)}");

        string content = ExtractContent(respText);
        AdviceResult parsed = ParseAdvice(content);
        return parsed with { DeckSummary = BuildArchetypeLabel(parsed.Archetype, analysis, req.State.Locale) };
    }

    private static string BuildSystemPrompt(string locale)
    {
        return "You are an expert Slay the Spire 2 coach. You are given the player's run context, a "
            + "summary of their current deck (card list + type/energy/keyword/tag histograms), the "
            + "character's known archetypes for reference, and the reward cards offered — each with its "
            + "real in-game effect text, keywords and tags. "
            + "FIRST infer the deck's current direction/archetype from its cards and keywords. "
            + "THEN grade each offered card RELATIVE to that direction and the run context (act, HP, "
            + "ascension), favoring cards that advance a viable archetype or fix a critical weakness, and "
            + "calling out anti-synergy or trap cards. "
            + "SKIP (taking no card) is ALWAYS a valid option: include it as a graded entry with cardId "
            + "\"SKIP\", and recommend it when every offered card would dilute the deck or none is worth it. "
            + "Reply with a SINGLE JSON object of the form: {\"archetype\":\"<short deck direction + a few "
            + "words why>\",\"cards\":[{\"cardId\":\"<id or SKIP>\",\"grade\":\"S|A|B|C|D|F\",\"reason\":"
            + "\"<short, cite the concrete mechanic/synergy>\",\"recommended\":true|false}],\"summary\":"
            + "\"<one-line overall recommendation>\"}. Use the exact cardId values from the offered cards "
            + "(or \"SKIP\"). Keep reasons concise and concrete."
            + LanguageDirective(locale);
    }

    /// <summary>Instruct the model to write human-readable text in the game's UI language.</summary>
    private static string LanguageDirective(string locale)
    {
        string lang = LocaleToLanguageName(locale);
        if (string.IsNullOrEmpty(lang))
        {
            // Unknown code: still tell the model to match the game's locale so it can adapt.
            return string.IsNullOrWhiteSpace(locale)
                ? ""
                : $" IMPORTANT: Write the \"archetype\", every \"reason\" and the \"summary\" in the game's UI language (locale code \"{locale}\"). Keep cardId values and grade letters unchanged.";
        }
        return $" IMPORTANT: Write the \"archetype\", every \"reason\" and the \"summary\" in {lang}. Keep cardId values and grade letters unchanged.";
    }

    /// <summary>Map a Godot/STS2 locale code to an explicit language name for the prompt.</summary>
    private static string LocaleToLanguageName(string locale)
    {
        if (string.IsNullOrWhiteSpace(locale)) return "";
        string l = locale.Trim().ToLowerInvariant().Replace('-', '_');
        if (l.StartsWith("zh"))
        {
            if (l.Contains("tw") || l.Contains("hk") || l.Contains("hant") || l == "zht")
                return "Traditional Chinese (繁體中文)";
            return "Simplified Chinese (简体中文)";
        }
        if (l.StartsWith("ja") || l.StartsWith("jp")) return "Japanese (日本語)";
        if (l.StartsWith("ko")) return "Korean (한국어)";
        if (l.StartsWith("fr")) return "French";
        if (l.StartsWith("de")) return "German";
        if (l.StartsWith("es") || l.StartsWith("sp")) return "Spanish";
        if (l.StartsWith("ru")) return "Russian";
        if (l.StartsWith("pt")) return "Portuguese";
        if (l.StartsWith("it")) return "Italian";
        if (l.StartsWith("en")) return "English";
        return "";
    }

    private string BuildUserPrompt(GameState state, DeckAnalysis analysis)
    {
        var sb = new StringBuilder();

        sb.AppendLine("## Run context");
        sb.Append("Character: ").Append(state.Character)
          .Append(" | Act ").Append(state.ActNumber)
          .Append(" | Floor ").Append(state.Floor)
          .Append(" | HP ").Append(state.CurrentHP).Append('/').Append(state.MaxHP)
          .Append(" | Gold ").Append(state.Gold)
          .Append(" | Ascension ").Append(state.AscensionLevel)
          .AppendLine();
        if (state.Relics.Count > 0)
            sb.Append("Relics: ").AppendLine(string.Join(", ", state.Relics.ConvertAll(r => r.Name)));
        sb.AppendLine();

        sb.AppendLine("## Current deck");
        sb.AppendLine(DescribeDeck(analysis, state.DeckCards));
        sb.AppendLine();

        string menu = BuildCharacterArchetypes(state.Character);
        if (!string.IsNullOrWhiteSpace(menu))
        {
            sb.Append("## Known archetypes for ").Append(state.Character)
              .AppendLine(" (reference — pick the one that fits, if any)");
            sb.AppendLine(menu);
            sb.AppendLine();
        }

        sb.AppendLine("## Offered cards (choose ONE, or SKIP)");
        if (state.OfferedCards.Count == 0)
        {
            sb.AppendLine("(none detected)");
        }
        else
        {
            foreach (CardInfo c in state.OfferedCards)
            {
                sb.Append("- ").Append(c.Id)
                  .Append(" (").Append(c.Name).Append(", ")
                  .Append(c.Rarity).Append(' ').Append(c.Type)
                  .Append(", cost ").Append(c.Cost);
                if (c.Upgraded) sb.Append(", upgraded");
                if (!string.IsNullOrEmpty(c.TargetType)) sb.Append(", target ").Append(c.TargetType);
                sb.AppendLine(")");
                if (!string.IsNullOrWhiteSpace(c.Description))
                    sb.Append("    Effect: ").AppendLine(c.Description);
                if (c.Keywords.Count > 0)
                    sb.Append("    Keywords: ").AppendLine(string.Join(", ", c.Keywords));
                if (c.Tags.Count > 0)
                    sb.Append("    Tags: ").AppendLine(string.Join(", ", c.Tags));
            }
            sb.AppendLine("- SKIP (take no card — keep the deck lean; a valid pick when nothing improves the deck)");
        }
        sb.AppendLine();
        sb.AppendLine("First infer this deck's direction/archetype, then grade each offered card AND the SKIP option relative to it and the run context; recommend the single best choice.");
        return sb.ToString();
    }

    /// <summary>Compact deck summary: counts, energy curve, real tags + keywords, and the card list (by name).</summary>
    private static string DescribeDeck(DeckAnalysis a, IReadOnlyList<CardInfo> deck)
    {
        var sb = new StringBuilder();
        sb.Append("Size ").Append(a.TotalCards)
          .Append(" (Attack ").Append(a.AttackCount)
          .Append(" / Skill ").Append(a.SkillCount)
          .Append(" / Power ").Append(a.PowerCount)
          .Append("), avg cost ").Append(a.AverageCost.ToString("0.0")).Append('.').AppendLine();

        var curve = new List<string>();
        for (int i = 0; i <= 5; i++)
            if (a.EnergyCurve.TryGetValue(i, out int cnt) && cnt > 0)
                curve.Add(i == 5 ? $"5+:{cnt}" : $"{i}:{cnt}");
        if (curve.Count > 0)
            sb.Append("Energy curve ").Append(string.Join(", ", curve)).Append('.').AppendLine();

        AppendTop(sb, "Keywords", a.KeywordCounts, 10);
        AppendTop(sb, "Tags", a.TagCounts, 8);

        // Card list by name (deduped with counts) — the strongest archetype signal the model can read.
        if (deck != null && deck.Count > 0)
        {
            var nameCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var order = new List<string>();
            foreach (CardInfo c in deck)
            {
                string name = string.IsNullOrWhiteSpace(c.Name) ? c.Id : c.Name;
                if (!nameCounts.ContainsKey(name)) { nameCounts[name] = 0; order.Add(name); }
                nameCounts[name]++;
            }
            var parts = new List<string>();
            foreach (string name in order)
                parts.Add(nameCounts[name] > 1 ? $"{name} x{nameCounts[name]}" : name);
            sb.Append("Cards: ").Append(string.Join(", ", parts)).Append('.');
        }
        return sb.ToString();
    }

    private static void AppendTop(StringBuilder sb, string label, Dictionary<string, int> counts, int max)
    {
        if (counts.Count == 0) return;
        var list = new List<KeyValuePair<string, int>>(counts);
        list.Sort((x, y) => y.Value.CompareTo(x.Value));
        var top = new List<string>();
        for (int i = 0; i < list.Count && i < max; i++) top.Add($"{list[i].Key} x{list[i].Value}");
        sb.Append(label).Append(": ").Append(string.Join(", ", top)).Append('.').AppendLine();
    }

    /// <summary>The character's full curated archetype menu (C layer), handed to the model as reference.</summary>
    private string BuildCharacterArchetypes(string character)
    {
        var sb = new StringBuilder();
        foreach (ArchetypeGuideEntry e in _guide.ForCharacter(character))
        {
            sb.Append("- ").Append(e.Name).Append(": win = ").Append(e.Win)
              .Append(" | prioritize = ").Append(e.Priorities).AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>Locale-prefixed archetype line for the panel, preferring the model's inference, then
    /// the local tag-based detection, then a neutral fallback.</summary>
    private static string BuildArchetypeLabel(string llmArchetype, DeckAnalysis a, string locale)
    {
        bool zh = !string.IsNullOrEmpty(locale) && locale.Trim().ToLowerInvariant().StartsWith("zh");
        string prefix = zh ? "流派" : "Archetype";
        string body = (llmArchetype ?? "").Trim();
        if (body.Length == 0 && a.DetectedArchetypes.Count > 0)
            body = a.DetectedArchetypes[0].Archetype.DisplayName;
        if (body.Length == 0)
            body = zh ? "暂不明显(早期/灵活牌组)" : "unclear (early/flexible deck)";
        return $"{prefix}: {body}";
    }

    private static string ExtractContent(string respText)
    {
        using JsonDocument doc = JsonDocument.Parse(respText);
        JsonElement root = doc.RootElement;
        if (root.TryGetProperty("choices", out JsonElement choices)
            && choices.ValueKind == JsonValueKind.Array
            && choices.GetArrayLength() > 0)
        {
            JsonElement first = choices[0];
            if (first.TryGetProperty("message", out JsonElement message)
                && message.TryGetProperty("content", out JsonElement contentEl)
                && contentEl.ValueKind == JsonValueKind.String)
            {
                return contentEl.GetString() ?? "";
            }
        }
        throw new InvalidOperationException($"Unexpected LLM response shape: {Truncate(respText, 400)}");
    }

    /// <summary>
    /// Parse the model's JSON content into <see cref="AdviceResult"/>. If parsing fails, fall back
    /// to placing the raw text into the summary so the player still sees something.
    /// </summary>
    private static AdviceResult ParseAdvice(string content)
    {
        var cards = new List<CardAdvice>();
        string summary = "";
        string archetype = "";
        try
        {
            using JsonDocument doc = JsonDocument.Parse(content);
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("summary", out JsonElement summaryEl)
                && summaryEl.ValueKind == JsonValueKind.String)
            {
                summary = summaryEl.GetString() ?? "";
            }

            if (root.TryGetProperty("archetype", out JsonElement archEl)
                && archEl.ValueKind == JsonValueKind.String)
            {
                archetype = archEl.GetString() ?? "";
            }

            if (root.TryGetProperty("cards", out JsonElement cardsEl)
                && cardsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in cardsEl.EnumerateArray())
                {
                    cards.Add(new CardAdvice(
                        ReadString(item, "cardId"),
                        ReadString(item, "grade"),
                        ReadString(item, "reason"),
                        ReadBool(item, "recommended")));
                }
            }
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Could not parse LLM advice as JSON ({ex.Message}); showing raw text.");
            return new AdviceResult(cards, content);
        }

        if (cards.Count == 0 && string.IsNullOrWhiteSpace(summary))
            summary = content;

        return new AdviceResult(cards, summary, "", archetype);
    }

    private static string ReadString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out JsonElement el) && el.ValueKind == JsonValueKind.String
            ? el.GetString() ?? ""
            : "";

    private static bool ReadBool(JsonElement obj, string name)
        => obj.TryGetProperty(name, out JsonElement el)
            && (el.ValueKind == JsonValueKind.True
                || (el.ValueKind == JsonValueKind.String && bool.TryParse(el.GetString(), out bool b) && b));

    private static string Truncate(string s, int max)
        => string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max) + "...";
}
