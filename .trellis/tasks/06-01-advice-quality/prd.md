# 建议质量增强:真实卡牌机制 + 牌组流派 + 打法速查

## Goal

让 AI 选牌建议从"只看卡名"升级到"看真实机制 + 当前牌组流派",使评级更准、更像懂行玩家。做法:在 LLM prompt 里 join ①每张候选卡的真实效果(伤害/格挡/关键词/效果文本)、②当前牌组的流派检测与构成、③可选的策展打法速查。所有数据**优先取自运行中的游戏本体**以天然抗版本过期。

## What I already know

- 现有 MVP 已跑通:选牌页浮层 +「获取建议」按钮 → 读 run 状态 → OpenAI 兼容 LLM → 带评级建议,正文与卡名按 locale(zh_Hans)本地化。provider=opencode-go / deepseek-v4-flash(~9s)。
- 候选卡/牌组机制可直接读游戏:`card.GetDescriptionForPile()`(本地化效果文本)、`card.Keywords`、`card.Tags`、`card.DynamicVars`(伤害/格挡)、`ModelDb.AllCards`(全卡枚举)。详见 [research/card-mechanics-data-sources.md](research/card-mechanics-data-sources.md)。
- sts2-advisor(MIT)已有现成流派引擎(`ArchetypeDefinitions.cs` + `DeckAnalyzer.cs`)可作结构模板。详见 [research/archetype-and-freshness.md](research/archetype-and-freshness.md)。
- 实时抓 slaythespire2.gg 不可行(Cloudflare 403)→ 一律先收集 / 运行时自算。

## Requirements

### A — 真实卡牌机制进 prompt(最大单点收益)
- 扩展 `CardInfo`:`Description`(本地化)、`Keywords`、`Tags`、`Damage`、`Block`、`TargetType`。
- `GameStateReader.CardModelToInfo` 在游戏主线程读取上述字段(带反射回退,降级不崩)。
- prompt:**候选卡给全量效果文本**;**牌组给紧凑摘要**(避免 token/延迟膨胀)。

### B — 运行时牌组流派检测进 prompt(用户点名)
- 移植精简版流派表 + DeckAnalyzer(独立重写 / MIT 改用注明出处):牌组 tag 直方图 → 命中流派(带强度)。
- prompt 加:检测到的主流派 + tag 直方图 + 能量曲线 + 牌型(攻击/技能/能力)计数 + **原始 deck tags**(即便静态表不全,LLM 仍见真实 tag)。
- 浮层在建议上方显示检测到的流派(如「当前流派:毒流」),让用户看到 AI 的判断依据。

### C — 策展打法速查
- bundle 一份 `Sts2AiAdvisor.archetypes.json`(松散、带 mod 前缀、放 DLL 同目录、热更新无需重打 PCK):每流派一行"获胜条件 + 优先级",从当前(2026-06)guides 手抄,带更新日期戳。
- **首发覆盖全部 5 角色**(ironclad/silent/defect/regent/necrobinder);Regent/Necrobinder 资料较新,条目标注「低置信,待校准」。
- prompt 仅注入"当前检测到的流派"对应条目。

### 抗过期(贯穿)
- A 靠运行时 → 天生最新;B 把流派表接地到游戏实时吐出的真实 `card.Tags`(先离线 dump 一次 tag 词表再建表);C 刻意做小、手工维护。

## Acceptance Criteria

- [ ] `CardInfo` 含 Description/Keywords/Tags/Damage/Block/TargetType,且 `GameStateReader` 能在游戏内填充(反射失败降级为空,不崩)。
- [ ] 候选卡 prompt 含本地化效果文本;牌组以紧凑摘要呈现。
- [ ] 牌组流派被检测并写入 prompt;浮层展示检测到的流派。
- [ ] 流派表建立在游戏实时真实 tag 上(有一次性 dump/校准证据)。
- [ ] (若做 C)`archetypes.json` 存在、按检测流派注入、带更新日期戳。
- [ ] 进游戏实测:同一局面下,建议质量较 MVP 明显更具体(引用具体机制/流派),且仍语言自适应、不崩。
- [ ] `dotnet build -c Debug` 0 警告 0 错误。

## Definition of Done

- 进游戏实测通过(主线程读状态 / 离线程调 LLM / 编组回主线程,无线程违规)。
- 复用自 AGPL 仓库的逻辑确为独立重写;MIT 改用处注明出处。
- spec(`.trellis/spec/sts2-mod/index.md`)补充新约定(机制读取 API、流派检测、数据文件落地、抗过期)。
- 无范围蔓延(见 Out of Scope)。

## Technical Approach

**节奏:A+B+C 一次性实现完,最后进游戏一次性验证**(减少进游戏次数)。分层只是内部结构:
1. **A**:扩展 CardInfo + GameStateReader 反射读取 → 改 prompt 拼装(候选卡全文 / 牌组摘要)。
2. **B**:新增流派表(静态)+ DeckAnalyzer(tag 直方图→流派)→ 注入 prompt + 浮层展示。流派表建立在 `ModelDb.AllCards` dump 出的真实 tag 词表上。
3. **C**:策展 `archetypes.json`(全 5 角色)→ 按检测流派注入 prompt。

> 为支撑 B 的"接地真实 tag",实现里加一个**受配置开关控制的一次性 dump**(枚举 `ModelDb.AllCards` 打出 id/tags/keywords/description 到日志或文件),用于建表/校表;默认关闭。

数据文件:松散 JSON,`Sts2AiAdvisor.archetypes.json`,DLL 同目录,加载方式同 config.json。

## Decision (ADR-lite)

**Context**:流派/攻略知识既要准又怕过期;guides 站点 Cloudflare 403 无法实时抓。
**Decision**:① 卡牌机制全部走游戏运行时(A);② 流派检测运行时自算并接地到真实 tag(B);③ 攻略知识离线策展成小文件、手工维护(C)。一律不在 mod 运行时联网抓站。
**Consequences**:A/B 几乎零维护、永远匹配版本;C 是唯一需定期回炉的部分,故刻意做小并打日期戳。最新数据永远是玩家本机那份游戏本体。

## Resolved Decisions

- 节奏:**A+B+C 一次性实现完,最后进游戏一次性验证**。
- C 覆盖:**全部 5 角色**(Regent/Necrobinder 标低置信)。
- 浮层**展示检测到的流派**(如「当前流派:毒流」);逐卡协同说明暂不强制(LLM 的 reason 已含),作为可选。

## Out of Scope

- 不做 tier 数值打分系统 / 自学习胜率库 / SQLite / 云端。
- 不做遗物/事件/商店建议(后续任务)。
- mod 运行时不联网抓任何攻略站。
- 不引入第三方 NuGet(LLM 层仍只用 HttpClient + System.Text.Json)。

## Research References

- [research/card-mechanics-data-sources.md](research/card-mechanics-data-sources.md) — 卡牌机制三来源对比;结论用游戏运行时。
- [research/archetype-and-freshness.md](research/archetype-and-freshness.md) — 流派引擎结构 + fetch-vs-bundle + 抗过期 + 参考仓库更新时间。

## Technical Notes

- 主要风险:`GetDescriptionForPile` 带参反射签名 → 进游戏验证 + 属性回退链(Description/RulesText/Body/...)。
- 许可证铁律:STS2-Agent(AGPL)只参考 API 独立重写;sts2-advisor(MIT)改用注明出处。
- 所有 CardModel/游戏对象读取只在主线程;LLM 调用仍离线程 + 编组回主线程。
- 控 prompt 体积:候选卡全文(3-5 张),牌组只给摘要 + tag 直方图。
