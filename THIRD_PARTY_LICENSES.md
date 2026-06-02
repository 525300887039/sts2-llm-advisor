# Third-party notices

This project (**STS2 AI Advisor**) is licensed under **AGPL-3.0-only** (see `LICENSE`).
It was developed with reference to two other Slay the Spire 2 advisor projects. This file
records the attribution and license obligations for that reuse.

The reference repositories themselves are **not redistributed** here (their working copies live
under `research/`, which is git-ignored). The notices below cover the *ideas, API knowledge, and
small pieces of scaffolding* that influenced this code, file by file.

---

## 1. sts2-advisor — MIT

- Source: https://github.com/ebadon16/sts2-advisor
- Author: ebadon16
- License: MIT (declared in the project's README; the upstream repository does **not** ship a
  `LICENSE` file or an explicit copyright line, so the notice below is reproduced on a best-effort
  basis, crediting the author).

Portions of the following files are **adapted** from sts2-advisor (the structure / scaffolding was
reused and reimplemented; see the per-file header comments in the source):

- `src/Sts2AiAdvisor/ModEntry.cs` — entry bootstrap (Plugin.cs), with SQLite/cloud/tier services dropped
- `src/Sts2AiAdvisor/GamePatches.cs` — card-reward Harmony hook structure
- `src/Sts2AiAdvisor/Ui/AdvisorOverlay.cs` — CanvasLayer/panel/button creation idiom (OverlayManager)
- `src/Sts2AiAdvisor/Game/GameState.cs`, `CardInfo.cs`, `RelicInfo.cs` — state POCO shapes
- `src/Sts2AiAdvisor/Game/GameStateReader.cs` — cross-checked reflection reader
- `src/Sts2AiAdvisor/Game/CardReflection.cs` — reflection fallback approach
- `src/Sts2AiAdvisor/Game/Archetypes/*` — archetype/deck-analysis scoring shape (independent reimplementation)

MIT License terms (reproduced for compliance):

```
MIT License

Copyright (c) ebadon16 (https://github.com/ebadon16/sts2-advisor)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## 2. STS2-Agent — AGPL-3.0-only

- Source: https://github.com/CharTyr/STS2-Agent
- Author: CharTyr
- License: GNU Affero General Public License v3.0 only (AGPL-3.0-only)

**No source code from STS2-Agent was copied into this project.** Its public API surface — the game
member names and call shapes used to read run/deck/card state via reflection (e.g. the
`GameStateService` route, `ModelDb.AllCards`, card description members, `DynamicVars`) — was used
purely as *documentation* to write an independent implementation. The relevant files
(`src/Sts2AiAdvisor/Game/GameStateReader.cs`, `CardReflection.cs`) carry header comments stating
this explicitly.

Because STS2-Agent is AGPL-3.0, this project is also released under **AGPL-3.0-only** so that, even
if any portion were ever deemed a derivative work, the copyleft obligations are already satisfied.

---

## 3. The game

This is a third-party mod for **Slay the Spire 2** (© MegaCrit). It is **not affiliated with or
endorsed by MegaCrit**. It does not redistribute any game code or assets; it loads at runtime and
reads game state via reflection. Distribution and use of this mod are subject to the game's own
modding terms / EULA.
