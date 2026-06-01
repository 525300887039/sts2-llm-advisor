# Spec: STS2 Mod(杀戮尖塔2 mod 开发约定)

本项目是一个《Slay the Spire 2》的 C#/.NET mod。以下是从首个骨架任务(`06-01-mvp-skeleton`)中固化的、对后续任务有复用价值的约定与坑。

## Pre-Development Checklist(动手前必看)

- [ ] **许可证铁律**:复用 `research/STS2-Agent`(**AGPL-3.0**)的任何代码都必须**重写为独立实现**(同 API、不同代码),严禁逐行照抄——否则整个 mod 被传染成 AGPL。`research/sts2-advisor`(**MIT**)可复用,加一行 `// adapted from sts2-advisor (MIT)`。曾发生:`build_pck.gd` 被逐行照抄,check 阶段才拦下。
- [ ] **游戏对象只在主线程访问**:`RunManager`/`Player`/`Deck`/`CardModel`/任何 Godot 节点都只能在游戏主线程读写。耗时操作(LLM HTTP)用 `Task.Run` 离开主线程,结果必须经 `GameThread.InvokeAsync` 编组回主线程后才能碰节点。
- [ ] **csproj 引用游戏 DLL**:`net9.0`;引用 `sts2.dll`/`GodotSharp.dll`/`0Harmony.dll` 且 `<Private>false</Private>`;`<EnableDynamicLoading>true</EnableDynamicLoading>`。`Sts2DataDir` 解析:`local.props` → `STS2_DATA_DIR` 环境变量 → 默认 `C:/Program Files (x86)/Steam/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64`(本机已确认存在)。
- [ ] **入口**:`[ModInitializer("方法名")]` 静态类(命名空间 `MegaCrit.Sts2.Core.Modding`);用游戏自带 Harmony 2.4.2 patch。
- [ ] **mod 产物**:`DLL` + `PCK`(PCK 内含 `res://mod_manifest.json`,`pck_name` 必须等于 .pck 文件名)+ `mod_id.json`,装到 `<游戏>/mods/`。

## 已验证的游戏 API(编译链接 sts2.dll 通过,真实存在)

- 读状态:`RunManager.Instance.DebugOnlyGetState()` → `LocalContext.GetMe(runState)` → `player.Deck.Cards` / `player.Relics` / `player.Creature.CurrentHp/MaxHp` / `player.Gold` / `player.Character.Id.Entry`;`runState.AscensionLevel` / `TotalFloor` / `CurrentActIndex`。`CombatManager.Instance.DebugOnlyGetState()`(非战斗为 null)。
- 卡牌:`CardModel.Id.Entry`(UPPER_SNAKE)/ `.Title` / `.Type` / `.Rarity` / `.EnergyCost.Canonical`;升级判断 `Id.Entry.EndsWith("+")`。
- 选牌页 hook:Harmony postfix 钩 `NCardRewardSelectionScreen.ShowScreen`,候选卡参数名 `options`(`IReadOnlyList<CardCreationResult>`,`CardCreationResult.Card`);该屏幕被删牌/看牌堆复用,保留 `options.Count > 5 → skip` 守卫。
- 日志:`MegaCrit.Sts2.Core.Logging.Log`(Info/Warn/Error)。

## 项目结构(骨架确立)

```
src/Sts2AiAdvisor/        ModEntry.cs, GamePatches.cs, ModLog.cs, *.csproj, manifest, config.example.json
  Game/                   GameThread.cs(线程编组), GameStateReader.cs, GameState/CardInfo/RelicInfo POCO
  Llm/                    ILlmAdvisor, OpenAiCompatibleAdvisor, LlmConfig, AdviceModels(provider 无关,零三方 SDK)
tools/pck_builder/        build_pck.gd, project.godot(Godot headless PCKPacker)
build/build-mod.ps1       dotnet build → 打 PCK → 拷进 <游戏>/mods
```

## LLM 层约定

- provider 无关:OpenAI 兼容 `{baseUrl}/chat/completions`,`config.json` 配 `baseUrl/apiKey/model`(放 DLL 同目录),换 provider 只改配置。
- 零第三方 NuGet:只用 `HttpClient` + `System.Text.Json`(避免 mod 加载额外依赖的麻烦)。
- 健壮性:缺 apiKey → 面板友好提示不抛异常;`response_format:{type:"json_object"}`;JSON 解析失败 → 原文落 `Summary`;`HttpClient` 单例复用;尊重 `CancellationToken`。
- 注意:个别 OpenAI 兼容后端(某些 Ollama 模型)会拒绝 `json_object`(400),目前表现为面板报错而非崩溃。

## Quality Check(完成前自检)

- [ ] `dotnet build -c Debug` 0 警告 0 错误。
- [ ] 复用自 AGPL 仓库的逻辑确为独立重写(非照抄)。
- [ ] 按钮点击全链路:主线程抓状态 → 离线程调 LLM → 编组回主线程改节点;无线程池线程上访问游戏对象/节点。
- [ ] 无范围蔓延(无 tier 打分/流派/SQLite/云/遗物药水商店事件逻辑)。
- [ ] 运行时风险已知:游戏内才能验证的点(类型/字段反射、PCK 打包)均有 try/catch + 守卫,降级而非崩溃。
