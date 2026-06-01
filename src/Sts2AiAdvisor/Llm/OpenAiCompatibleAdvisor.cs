using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Sts2AiAdvisor.Game;

namespace Sts2AiAdvisor.Llm;

/// <summary>
/// Talks to any OpenAI-compatible /chat/completions endpoint (DeepSeek, Kimi, GLM, OpenRouter,
/// Ollama, ...). Zero third-party SDK: System.Net.Http + System.Text.Json only.
/// </summary>
public sealed class OpenAiCompatibleAdvisor : ILlmAdvisor
{
    // One shared client for the process; the request carries its own auth header per call.
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    private static readonly JsonSerializerOptions PromptJson = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions ParseJson = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly LlmConfig _config;

    public OpenAiCompatibleAdvisor(LlmConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public async Task<AdviceResult> GetAdviceAsync(AdviceRequest req, CancellationToken ct)
    {
        if (!_config.IsValid)
            throw new InvalidOperationException("LLM config is invalid (missing apiKey or baseUrl).");

        string systemPrompt = BuildSystemPrompt();
        string userPrompt = BuildUserPrompt(req.State);

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
        };

        string url = _config.BaseUrl.TrimEnd('/') + "/chat/completions";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using HttpResponseMessage resp = await Http.SendAsync(request, ct).ConfigureAwait(false);
        string respText = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"LLM HTTP {(int)resp.StatusCode}: {Truncate(respText, 400)}");

        string content = ExtractContent(respText);
        return ParseAdvice(content);
    }

    private static string BuildSystemPrompt()
    {
        return "You are an expert Slay the Spire 2 coach. Given the player's run state and the cards "
            + "offered as a reward, advise which to pick. Reply with a SINGLE JSON object of the form: "
            + "{\"cards\":[{\"cardId\":\"<id>\",\"grade\":\"S|A|B|C|D|F\",\"reason\":\"<short>\","
            + "\"recommended\":true|false}],\"summary\":\"<one-line overall recommendation>\"}. "
            + "Use the exact cardId values from the offered cards. Keep reasons concise.";
    }

    private static string BuildUserPrompt(GameState state)
    {
        string stateJson = JsonSerializer.Serialize(state, PromptJson);
        var sb = new StringBuilder();
        sb.AppendLine("Current run state (JSON):");
        sb.AppendLine(stateJson);
        sb.AppendLine();
        sb.AppendLine("Offered cards to choose from (cardId list):");
        if (state.OfferedCards.Count == 0)
        {
            sb.AppendLine("(none detected)");
        }
        else
        {
            foreach (CardInfo c in state.OfferedCards)
                sb.AppendLine($"- {c.Id} ({c.Name}, {c.Rarity} {c.Type}, cost {c.Cost})");
        }
        sb.AppendLine();
        sb.AppendLine("Grade each offered card and recommend the best pick.");
        return sb.ToString();
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
        try
        {
            using JsonDocument doc = JsonDocument.Parse(content);
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("summary", out JsonElement summaryEl)
                && summaryEl.ValueKind == JsonValueKind.String)
            {
                summary = summaryEl.GetString() ?? "";
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

        return new AdviceResult(cards, summary);
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
