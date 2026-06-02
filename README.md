# STS2 AI Advisor

An LLM-powered card-reward advisor mod for **Slay the Spire 2** (Godot 4.5 + C#/.NET 9).
On the card-reward screen it shows a floating panel with a **获取建议** ("Get advice") button;
clicking it reads your current run state and asks an OpenAI-compatible LLM which card to pick.

This is the MVP skeleton: only the card-reward screen is supported. No tier scoring, archetype
analysis, tracking, or relic/potion/shop/event advice.

## Configure

The mod talks to any OpenAI-compatible `/chat/completions` endpoint (DeepSeek, Kimi, GLM,
OpenRouter, Ollama, ...). After installing (see below), rename `config.example.json` to
`config.json` in the game `mods/` folder and fill in your key:

```json
{
  "baseUrl": "https://api.deepseek.com/v1",
  "apiKey": "YOUR_KEY_HERE",
  "model": "deepseek-chat"
}
```

If `apiKey` is empty the panel still appears, but clicking the button shows
`请在 config.json 配置 apiKey`.

## Build & install

Prerequisites:
- .NET 9 SDK.
- A Godot 4.5 binary (used headless to pack the `.pck`). Point the build at it via the
  `GODOT_BIN` environment variable, or pass `-GodotExe`.
- Slay the Spire 2 installed. The build auto-detects the default Steam path; override the
  *reference* path for compiling with `STS2_DATA_DIR` or a `local.props` (see
  `src/Sts2AiAdvisor/local.props.example`).

```powershell
$env:GODOT_BIN = "C:\path\to\Godot_v4.5-stable_win64_console.exe"
powershell -File build\build-mod.ps1
```

This builds the DLL, packs `Sts2AiAdvisor.pck`, and copies `Sts2AiAdvisor.dll`,
`Sts2AiAdvisor.pck`, `mod_id.json`, and `config.example.json` into
`<game>/mods/`. Close the game first — the DLL gets locked while it runs.

By default it installs to
`C:/Program Files (x86)/Steam/steamapps/common/Slay the Spire 2/mods`; override with
`-GameRoot`.

## Where the button appears

There is no hotkey. Start a run and finish a combat — when the **card-reward screen** opens,
a panel appears in the **top-right** corner with a `STS2 AI Advisor` title and a **获取建议**
button. Click it to fetch advice. While the request is in flight the panel shows `思考中…`.

## License

Copyright (C) 2026 ducat and contributors.

This project is licensed under the **GNU Affero General Public License v3.0 only (AGPL-3.0-only)** —
see [`LICENSE`](LICENSE). It is released under AGPL because it was developed with reference to the
AGPL-licensed [STS2-Agent](https://github.com/CharTyr/STS2-Agent); choosing AGPL keeps the project
compliant regardless of how that influence is characterized.

Third-party attribution and the obligations for reused scaffolding (notably
[sts2-advisor](https://github.com/ebadon16/sts2-advisor), MIT) are recorded in
[`THIRD_PARTY_LICENSES.md`](THIRD_PARTY_LICENSES.md). No source code from either reference
repository is redistributed here.

This is an unofficial mod for **Slay the Spire 2** (© MegaCrit) and is **not affiliated with or
endorsed by MegaCrit**. It redistributes no game code or assets and is subject to the game's own
modding terms / EULA.
