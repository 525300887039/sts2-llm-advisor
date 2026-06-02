# Spec: STS2 Mod(杀戮尖塔2 mod 开发约定)

本项目是一个《Slay the Spire 2》的 C#/.NET mod。以下是从首个骨架任务(`06-01-mvp-skeleton`)中固化的、对后续任务有复用价值的约定与坑。

## Pre-Development Checklist(动手前必看)

- [ ] **许可证铁律**:复用 `research/STS2-Agent`(**AGPL-3.0**)的任何代码都必须**重写为独立实现**(同 API、不同代码),严禁逐行照抄——否则整个 mod 被传染成 AGPL。`research/sts2-advisor`(**MIT**)可复用,加一行 `// adapted from sts2-advisor (MIT)`。曾发生:`build_pck.gd` 被逐行照抄,check 阶段才拦下。
  - **已落地(2026-06 公开发布)**:仓库公开到 `github.com/525300887039/sts2-llm-advisor`,**整库 AGPL-3.0-only**。根目录有 `LICENSE`(gnu.org 官方 AGPL 全文)与 `THIRD_PARTY_LICENSES.md`(致谢 ebadon16/sts2-advisor MIT + 说明 CharTyr/STS2-Agent AGPL 仅作 API 文档)。`research/` 已 gitignore、不随仓库分发;真实密钥只在游戏目录 `mods/config.json`(`.gitignore` 含 `config.json`),全历史审计无泄漏。新增代码沿用 AGPL,无需再纠结"是否衍生作品"。
- [ ] **游戏对象只在主线程访问**:`RunManager`/`Player`/`Deck`/`CardModel`/任何 Godot 节点都只能在游戏主线程读写。耗时操作(LLM HTTP)用 `Task.Run` 离开主线程,结果必须经 `GameThread.InvokeAsync` 编组回主线程后才能碰节点。
- [ ] **csproj 引用游戏 DLL**:`net9.0`;引用 `sts2.dll`/`GodotSharp.dll`/`0Harmony.dll` 且 `<Private>false</Private>`;`<EnableDynamicLoading>true</EnableDynamicLoading>`。`Sts2DataDir` 解析:`local.props` → `STS2_DATA_DIR` 环境变量 → 默认 `C:/Program Files (x86)/Steam/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64`(本机已确认存在)。
- [ ] **入口**:`[ModInitializer("方法名")]` 静态类(命名空间 `MegaCrit.Sts2.Core.Modding`);用游戏自带 Harmony 2.4.2 patch。
- [ ] **mod 产物**:`DLL` + `PCK`(PCK 内含 `res://mod_manifest.json`,`pck_name` 必须等于 .pck 文件名)+ `mod_id.json`,装到 `<游戏>/mods/`。

## 已验证的游戏 API(编译链接 sts2.dll 通过,真实存在)

- 读状态:`RunManager.Instance.DebugOnlyGetState()` → `LocalContext.GetMe(runState)` → `player.Deck.Cards` / `player.Relics` / `player.Creature.CurrentHp/MaxHp` / `player.Gold` / `player.Character.Id.Entry`;`runState.AscensionLevel` / `TotalFloor` / `CurrentActIndex`。`CombatManager.Instance.DebugOnlyGetState()`(非战斗为 null)。
- 卡牌:`CardModel.Id.Entry`(UPPER_SNAKE)/ `.Title` / `.Type` / `.Rarity` / `.EnergyCost.Canonical`;升级判断 `Id.Entry.EndsWith("+")`。
- 选牌页 hook:Harmony postfix 钩 `NCardRewardSelectionScreen.ShowScreen`,候选卡参数名 `options`(`IReadOnlyList<CardCreationResult>`,`CardCreationResult.Card`);该屏幕被删牌/看牌堆复用,保留 `options.Count > 5 → skip` 守卫。
- 日志:`MegaCrit.Sts2.Core.Logging.Log`(Info/Warn/Error)。

## 通过反射读取的成员(06-01,未编译验证 → 一律降级处理)

这些是为"建议质量"新增、**没有编译期验证**的游戏成员,统一封装在 `Game/CardReflection.cs`,每次访问都 try/property-chain/fallback,成员被改名/不存在时降级为空而非崩溃(理由:成员名来自 STS2-Agent 的 API 文档,未必匹配当前补丁)。

- 卡面效果文本:属性链 `Description/RulesText/Body/Text/RawText/DescriptionText` → 再试无参方法;读出后用 `Clean()` 去掉 BBCode `[...]` 标记。
- `Keywords` / `Tags`:属性或 `_`+小写首字母字段,`IEnumerable`→字符串列表。
- 数值预览(伤害/格挡):`DynamicVars` 枚举,按 `Name/Id` 匹配,读 `BaseValue/Value/PreviewValue/Amount`。
- 全卡库:`MegaCrit.Sts2.Core.Models.ModelDb.AllCards`(反射解析类型);`GameStateReader.DumpAllCards()` 一次性导出 `cards_dump.txt`(由 `config.json` 的 `dumpCards:true` 触发,仅 grounding 用)。

> **Gotcha:角色 id 带前缀**。`player.Character.Id.Entry` 返回的是 `CHARACTER.REGENT` 这种带前缀的串。必须取最后一个 `.` 之后并小写 → `regent`,否则对不上 `archetypes.json` 的 key(`regent.*`)。`GameStateReader.ReadCharacter` 已做此归一化。

## 项目结构(骨架确立)

```
src/Sts2AiAdvisor/        ModEntry.cs, GamePatches.cs, ModLog.cs, *.csproj, manifest, config.example.json
                          Sts2AiAdvisor.archetypes.json(流派速查数据文件,随构建安装)
  Game/                   GameThread.cs(线程编组), GameStateReader.cs, CardReflection.cs(反射降级), GameState/CardInfo/RelicInfo POCO
  Game/Archetypes/        Archetype, ArchetypeDefinitions(PROVISIONAL/弱回退), DeckAnalysis, DeckAnalyzer(纯 POCO,离线程安全)
  Llm/                    ILlmAdvisor, OpenAiCompatibleAdvisor, LlmConfig, AdviceModels, ArchetypeGuide(读 archetypes.json)
  Ui/                     AdvisorOverlay.cs(浮层:收起为可拖动小按钮)
tools/pck_builder/        build_pck.gd, project.godot(Godot headless PCKPacker)
build/build-mod.ps1       dotnet build → 打 PCK → 拷 DLL/PCK/manifest/config.example/archetypes.json 进 <游戏>/mods
```

## LLM 层约定

- provider 无关:OpenAI 兼容 `{baseUrl}/chat/completions`,`config.json` 配 `baseUrl/apiKey/model`(放 DLL 同目录),换 provider 只改配置。
- 零第三方 NuGet:只用 `HttpClient` + `System.Text.Json`(避免 mod 加载额外依赖的麻烦)。
- 健壮性:缺 apiKey → 面板友好提示不抛异常;`response_format:{type:"json_object"}`;JSON 解析失败 → 原文落 `Summary`;`HttpClient` 单例复用;尊重 `CancellationToken`。
- 注意:个别 OpenAI 兼容后端(某些 Ollama 模型)会拒绝 `json_object`(400),目前表现为面板报错而非崩溃。

## 建议质量:机制注入 + 流派识别 + SKIP(06-01-advice-quality)

把候选卡机制、牌组流派、人工速查一起塞进 prompt,让评级更准。要点:

- **机制注入**:每张候选卡带 Effect(去标记的效果文本)/Keywords/Tags/伤害/格挡(全经 `CardReflection` 反射,降级安全),写进 user prompt。
- **关键现实:tags 极稀疏**。`card.Tags` 实测只有 `Strike/Defend/Shiv/Minion/OstyAttack`(全库 577 卡仅 44 张有 tag);**Keywords 才丰富**(`Exhaust/Sly/Ethereal/Retain/Innate/Eternal/Unplayable`)。
  - **设计后果**:流派识别**不能靠 tag 硬匹配**。改为把「关键词直方图 + 牌组卡名 + 该角色全部速查菜单」喂给 LLM,**由 LLM 判断流派**并在返回 JSON 的 `archetype` 字段给出;面板顶部 `BuildArchetypeLabel` 显示("流派:…")。`DeckAnalyzer` 基于 tag 的检测(`ArchetypeDefinitions`,标 **PROVISIONAL**)仅作弱回退,当前补丁基本检不出。
- **流派速查数据文件(唯一需周期刷新的件)**:`Sts2AiAdvisor.archetypes.json`,date-stamped,key=`"character.archetypeId"`(如 `silent.poison`),值含 `name/win/priorities/confidence`;覆盖 5 角色。放 DLL 同目录,`ArchetypeGuide.Load()` 读取,`build-mod.ps1` 负责安装。换补丁/流派过时只改这一个文件。
- **SKIP 选项**:把"不拿任何牌"做成**可评级项**,cardId 固定 `"SKIP"`,system prompt 要求 LLM 始终把它作为合法选项评级;面板 `FormatAdvice` 本地化为「跳过(不拿任何牌)」/ "Skip (take nothing)"。
- **线程**:`DeckAnalyzer.Analyze(character, deck)` 与 prompt 构建只吃 `CardInfo` POCO、不碰任何游戏对象 → 可安全在 LLM 调用的离线程里跑。

## 浮层 UI 约定(AdvisorOverlay)

- **结构**:`CanvasLayer`(Layer=100)→ `PanelContainer`(锚右上)→ 标题行(`Label` 撑开 + "—"收起按钮)+ 可折叠 body(content `Label` + "获取建议" `Button`)。浮层挂游戏 SceneTree root 下,继承主题 CJK 字体。
- **收起 = 缩成一个小按钮**(不是只藏正文):折叠时整个 `_panel` 隐藏,另显一个独立小按钮(同样锚右上)。`ApplyCollapsedState()` 切换两者可见性;`Show()` 尊重当前折叠态(再次进选牌页不会强行弹大面板)。
- **小按钮可自由拖动**:用 `GuiInput`(不接 `Pressed`)自己分辨点击/拖动 —— 左键按下记起点;`InputEventMouseMotion` 时按 `mm.Relative` 同时平移四个 `Offset*`(锚右上,offset 即相对右上角,直接加减即可);**累计位移 >5px 判为拖动**,否则松手视为点击 → 展开;每个事件 `AcceptEvent()` 吞掉,避免 Button 自身触发。左键按住期间 viewport 会把 motion 持续路由给该控件,快速拖动也跟手。
- **展开跟随小按钮位置**:`_panel` 与小按钮**共享右上角锚的 offset 参照系**,展开时把 panel 右上角 offset 对齐到小按钮当前 offset(保宽高,向左下展开)→ 面板出现在被拖到的位置,不弹回原位。面板只会被小按钮带着走、不独立移动,故折叠/拖动/展开位置一路一致。

## 部署与运行时(进游戏实测踩坑,务必遵守)

- **PCK 引擎版本门禁**:游戏跑 Godot 4.5.1,会**拒绝**用更新引擎打的 PCK(报 `Pack created with a newer version of the engine`)。`pack_format_version` 相同还不够。本机 Godot 是 4.6.3,故 `build-mod.ps1` 在打包后**改写 PCK 头部引擎版本戳为 4.5.1**(头部布局:magic 0-3 / pack_format 4-7 / major 8-11 / minor 12-15 / patch 16-19,小端;把 minor 设 5、patch 设 1)。游戏升级 Godot 时需同步调整。
- **manifest 双 schema(关键,抄错就不加载)**:
  - 松散文件名必须是 `<ModName>.json`(与 dll/pck 同名,**不是** `mod_id.json`),字段 **snake_case**:`id`(=文件名)/`name`/`author`/`description`/`version`/`has_pck`/`has_dll`/`dependencies`/`affects_gameplay`。
  - PCK 内 `res://mod_manifest.json` 字段不同:`pck_name`(=pck 文件名)/`name`/`author`/`description`/`version`。
  - (从能用的 RouteSuggest / Booba mod 反推确认)
- **`mods/*.json` 全被当 manifest 扫描(无害误报)**:游戏启动会把 `mods/` 下**每个 `*.json`** 当 mod manifest 解析,所以 `config.json`/`config.example.json`/`Sts2AiAdvisor.archetypes.json` 各会刷一条 `[ERROR] ... is missing the 'id' field`。**纯无害**——mod 照常加载。要彻底消除可改成子目录安装(把附属 json 放进 `mods/Sts2AiAdvisor/`),目前未做。
- **C# DLL 改动必须重启游戏**:游戏运行时锁住 `mods/*.dll`,`build-mod.ps1` 拷贝会失败。改逻辑→让用户完全退游戏→重新部署→重启。
- **LLM 端点常在 Cloudflare 后**:默认 .NET/`HttpClient` UA 会被 **403(error 1010)**。必须带浏览器 UA(已加)。验证新 provider 时先用浏览器 UA 直连测试。
- **reasoning 模型要给足 `max_tokens`**:如 `deepseek-v4-*` 会先花几百~上千 token 推理(`completion_tokens_details.reasoning_tokens`),`max_tokens` 太小会把 JSON 截断。已设 4096。实时建议优先选快模型(opencode-go 上 `deepseek-v4-flash` ~9s vs `deepseek-v4-pro` ~40s)。
- **语言自适应**:`Godot.TranslationServer.GetLocale()`(游戏线程内调)返回如 `zh_Hans`;据此让 LLM 用对应语言回复。
- **本地化卡名**:`card.Title` 直接就是当前语言的本地化名(实测 `zh_Hans` 下 = 战利品/流光溢彩/光子切割)。面板按 cardId 映射回 `Name` 显示,而非显示英文 id。
- **浮层字体**:浮层 Control 挂到游戏 SceneTree root 下,**继承游戏主题的 CJK 字体**,中文 Label 直接能渲染,无需自带字体。
- **provider 实例**:用户用 opencode-go(`https://opencode.ai/zen/go/v1`,OpenAI 兼容);真实 `config.json`(含 key)放 `mods/config.json`,已 gitignore。

## Quality Check(完成前自检)

- [ ] `dotnet build -c Debug` 0 警告 0 错误。
- [ ] 复用自 AGPL 仓库的逻辑确为独立重写(非照抄)。
- [ ] 按钮点击全链路:主线程抓状态 → 离线程调 LLM → 编组回主线程改节点;无线程池线程上访问游戏对象/节点。
- [ ] 范围:卡牌机制注入 + 牌组流派识别(LLM 命名)+ SKIP 已**有意纳入**(06-01-advice-quality);仍**不做** SQLite/云/tier 持久打分/遗物·药水·商店·事件建议。
- [ ] 运行时风险已知:游戏内才能验证的点(类型/字段反射、PCK 打包)均有 try/catch + 守卫,降级而非崩溃。
