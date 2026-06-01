using System;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Sts2AiAdvisor.Llm;

/// <summary>OpenAI-compatible endpoint config, loaded from config.json beside the mod DLL.</summary>
public sealed class LlmConfig
{
    public string BaseUrl { get; set; } = "https://api.deepseek.com/v1";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "deepseek-chat";

    /// <summary>True when an API key is present and the endpoint looks usable.</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(BaseUrl);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Load config.json (falling back to config.example.json) from the directory of the executing
    /// assembly. Returns a config object even on failure — check <see cref="IsValid"/> before use.
    /// </summary>
    public static LlmConfig Load()
    {
        try
        {
            string dir = AssemblyDir();
            string configPath = Path.Combine(dir, "config.json");
            if (!File.Exists(configPath))
            {
                string examplePath = Path.Combine(dir, "config.example.json");
                if (File.Exists(examplePath))
                {
                    ModLog.Warn("config.json not found — falling back to config.example.json (no apiKey).");
                    configPath = examplePath;
                }
                else
                {
                    ModLog.Warn("No config.json or config.example.json found beside the mod DLL.");
                    return new LlmConfig();
                }
            }

            string json = File.ReadAllText(configPath);
            var cfg = JsonSerializer.Deserialize<LlmConfig>(json, JsonOptions) ?? new LlmConfig();
            ModLog.Info($"Loaded LLM config: baseUrl={cfg.BaseUrl}, model={cfg.Model}, apiKey={(string.IsNullOrWhiteSpace(cfg.ApiKey) ? "(empty)" : "(set)")}.");
            return cfg;
        }
        catch (Exception ex)
        {
            ModLog.Error("Failed to load LLM config", ex);
            return new LlmConfig();
        }
    }

    private static string AssemblyDir()
    {
        string? dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        return string.IsNullOrEmpty(dir) ? AppContext.BaseDirectory : dir;
    }
}
