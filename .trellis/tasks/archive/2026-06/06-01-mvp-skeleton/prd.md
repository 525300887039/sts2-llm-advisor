# STS2 AI 选牌建议 mod —— 最小可运行骨架

## Goal

为《杀戮尖塔2 / Slay the Spire 2》做一个 mod:在**选牌(card reward)页面**加一个按钮,点击后读取当前 run 状态,(后续接 LLM)给出选牌建议。本任务只做**第一版最小骨架**——把"mod 能加载 → 选牌页 hook 触发 → 浮层 UI + 按钮 → 读到游戏状态"这条最硬的集成链路打通,为后续接入 LLM 建议铺路。

## What I already know(来自前期调研,已验证)

- **游戏技术栈**:Godot 4.5.1 + C#/.NET 9。mod 用游戏自带 `[ModInitializer]` 属性加载(**不是 BepInEx**);Harmony 2.4.2 游戏自带;mod = `DLL` + `PCK`(PCK 内须含 `res://mod_manifest.json`,`pck_name` 必须等于 .pck 文件名)+ `mod_id.json`;装到 `游戏目录/mods`。
- **两个参考开源仓库**(已 clone 到 `research/`,已 gitignore):
  - `sts2-advisor`(ebadon16)= 成品 tier-advisor,**带浮层 UI**。许可证 **MIT**(README 声明,仓库缺 LICENSE 文件)。→ 可直接复用的主来源。
  - `STS2-Agent`(CharTyr)= 状态读取/HTTP 基础设施。许可证 **AGPL-3.0**(强传染)。→ **只当文档参考,按其揭示的 API 自己重写,不照抄代码**。
- **架构已定**(上一轮与用户确认):**进程内 C# + OpenAI 兼容端点**(可配置 `base_url/key/model`,覆盖 DeepSeek/Kimi/GLM/OpenRouter/Claude/Ollama),**不绑定 Claude**,**不引第三方 SDK**(用 `HttpClient` + `System.Text.Json`)。Pydantic AI / LangGraph 因是 Python、无法进 C# mod 进程,已排除(除非将来做 Python 边车)。
- **关键已验证的实现要点**(详见 research/repo-analysis.md):
  - 读 run 状态优先用公开方法 `RunManager.Instance.DebugOnlyGetState()` → `LocalContext.GetMe(runState)` → `player.Deck.Cards` / `player.Relics` / `player.Creature.CurrentHp/MaxHp` / `player.Gold`(比 advisor 反射非公开 `State` 更稳)。
  - 候选卡:Harmony postfix 钩 `NCardRewardSelectionScreen.ShowScreen` 拿参数 `IReadOnlyList<CardCreationResult> options`(advisor 的 MIT 实现可抄);或遍历节点树 `FindDescendants<NCardHolder>().Where(n => n.CardModel != null)`。
  - UI:`CanvasLayer{Layer=100}` + `PanelContainer` + `Button`,延迟挂到 `SceneTree.Root`(`CallDeferred("add_child", ...)`)。
  - 线程:游戏对象只能在主线程访问 → 用捕获的 `SynchronizationContext.Post` 做主线程编组(STS2-Agent 的 `GameThread` 模式,~30 行,自己重写)。
  - ID 体系 `model.Id.Entry`(UPPER_SNAKE);csproj 引用 `sts2.dll`/`GodotSharp.dll`/`0Harmony.dll` 且 `<Private>false</Private>`。

## Assumptions / 已确认

- ✅ STS2 已安装于默认 Steam 路径:`C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64`,3 个 DLL(`sts2.dll`/`GodotSharp.dll`/`0Harmony.dll`)齐全 → csproj 默认 `Sts2DataDir` 直接可用。
- ✅ .NET 9 SDK:本机原本没有,正在用 winget 安装(`Microsoft.DotNet.SDK.9`)。
- LLM 用玩家自己的 API key(填进 mod 目录的 config.json)。
- 目标平台 Windows。

## 已解决的决策(原 Open Questions)

1. **骨架深度 → 完整纵切(含 LLM)**:骨架包含 ILlmAdvisor + OpenAI 兼容调用 + config.json,点击按钮直接出 LLM 建议。
2. **构建环境 → 现在装 SDK + 用默认游戏路径**,搭到 `dotnet build` 通过 + 可在游戏里验证。
3. **mod 标识 → id=`sts2-ai-advisor`、name=`STS2 AI Advisor`**(可后续改)。

## Requirements (evolving)

- [ ] 一个可编译的 C# mod 工程(`.csproj` 引用游戏 3 个 DLL + `mod_manifest.json` + `mod_id.json`)。
- [ ] `[ModInitializer]` 入口,加载时初始化 Harmony 与主线程编组。
- [ ] Harmony postfix 钩选牌页 `NCardRewardSelectionScreen.ShowScreen`,拿到候选卡。
- [ ] 读取当前 run 状态:候选卡、牌组、遗物、HP、Act。
- [ ] 浮层 UI:`CanvasLayer` 面板 + 一个"获取建议"按钮 + loading/错误态。
- [ ] 主线程编组(`SynchronizationContext`):读游戏状态在主线程,LLM 网络调用离主线程,结果编组回主线程渲染。
- [ ] `ILlmAdvisor` 接口 + `OpenAiCompatibleAdvisor` 实现(`HttpClient` + `System.Text.Json`,零第三方 SDK)。
- [ ] `config.json`(mod 目录):`baseUrl` / `apiKey` / `model`,缺失时面板提示去配置。
- [ ] 点击按钮 → 抓状态 → 调 LLM → 面板显示建议(含每张候选卡的简要评级/理由)。
- [ ] PCK 打包流程(参考 STS2-Agent 的 `build_pck.gd`,自己写)。

## Acceptance Criteria (evolving)

- [ ] mod 放进 `游戏目录/mods` 后能被加载(日志可见初始化)。
- [ ] 进到选牌页时 Harmony hook 触发,日志打印出候选卡 id 列表。
- [ ] 选牌页出现浮层面板 + "获取建议"按钮。
- [ ] 点击按钮后,面板显示当前候选卡 + 牌组 + 遗物 + HP(证明状态读取贯通)。
- [ ] 不卡游戏主线程(读状态在主线程、耗时操作离开主线程)。

## Definition of Done

- 代码可编译(`dotnet build` 通过)。
- 能在真实游戏里加载并复现上面的验收点(若构建环境就绪)。
- 关键决策与坑记录到 `.trellis/spec/`。

## Out of Scope(本任务明确不做)

- LLM 的 prompt 工程与建议质量优化(骨架阶段最多打通一次调用)。
- tier 打分引擎、流派分析、自学习/SQLite、云端统计。
- 选遗物/选药水/商店/事件建议。
- OpenAI 兼容之外的 provider 适配器(如 Claude 原生协议)。
- Steam Workshop 发布、本地化、UI 美化。

## Technical Approach

进程内 C# mod。数据流:`选牌页打开 → Harmony postfix 拿候选卡 → (主线程)DebugOnlyGetState 读 run 状态 → 浮层面板渲染 → 按钮点击触发(骨架:显示状态;后续:离主线程调 LLM → 编组回主线程填面板)`。复用 sts2-advisor(MIT)的入口/hook/UI/POCO,按 STS2-Agent(AGPL)揭示的 API 自己重写状态读取与线程编组。

## Decision (ADR-lite)

- **Context**:要给玩家用的 mod,需 provider 灵活、分发简单、不卡游戏。
- **Decision**:进程内 C# + OpenAI 兼容端点(配置可换 provider),零第三方 SDK;复用 MIT 仓库代码、重写 AGPL 仓库逻辑。
- **Consequences**:分发为单一 DLL+PCK;复杂多步 agent 暂不可用(将来需要再上 Python 边车);需自行处理 Claude 原生协议(可走 OpenRouter 规避)。

## Out-of-process 备选

若将来要用 Pydantic AI/LangGraph → 改为 mod(C#)↔ 本地 HTTP ↔ Python 边车;代价是玩家需装 Python,故 MVP 不采用。

## Research References

- [`research/repo-analysis.md`](research/repo-analysis.md) — 两个参考仓库的"可复用 vs 要自己写"逐文件清单 + 已验证的游戏 API 标识符。

## Technical Notes

- **许可证坑**:STS2-Agent=AGPL(重写,别抄);sts2-advisor=MIT(可抄,建议让作者补 LICENSE 文件)。
- **构建阻塞**:本机无 .NET SDK(`dotnet` 不可用),需装 .NET 9 SDK;csproj 的 `HintPath` 指向游戏 `data_sts2_windows_x86_64/` 下的 `sts2.dll`/`GodotSharp.dll`/`0Harmony.dll`。
- **NCardRewardSelectionScreen 被复用**于删牌/看牌堆/事件等,需保留 advisor 的"真奖励"判定(`options.Count>5` 跳过等)。
- 存档隔离:`affectsGameplay:false`;必要时把 `IsRunningModded` patch 成 false 留在主存档。
