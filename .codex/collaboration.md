# Claude + Codex 协作约定

本文件定义在单个 CLI 会话中由 Codex 设计与实现、Claude 审查的双 AI 协作规则。它是稳定的协作流程约定，不是玩法或架构权威文档。架构与玩法权威仍遵循 `AGENTS.md` 与 `gameplay-alignment/authority-map.md`。

## 角色

```text
Codex   = 技术方案设计 + 方案实现（通过文档交付）
Claude  = 审查者（review 设计与实现）+ 验收门
```

| 职责 | 归属 | 理由 |
|---|---|---|
| 技术方案设计、架构权衡、实现 | Codex | 设计者兼实现者，对方案负责 |
| 设计文档 / 实现交付（以文档为接口） | Codex | 文档是 Codex → Claude 的解耦交付物 |
| 审查设计文档、审查实现、给出裁决 | Claude | 独立视角把关，不与设计耦合 |
| 验收闸（build + 回归测试） | Claude | 客观验证，设计者不自验 |

文档是 Codex 与 Claude 之间的解耦接口：Codex 交付设计/实现文档，Claude 读文档来 review，而不是直接耦合在同一段思路里。

## 解耦原则

**文档即接口**：Codex 的设计与实现结论必须落到文档（`--output-last-message` 或仓库内的设计文件），Claude 通过读该文档来 review。Claude 不替 Codex 设计，Codex 不自行验收。两者通过文档握手，降低耦合与上下文串味。

**子 Agent 隔离 review**：Claude 做 review 时，应派 Claude 子 Agent（`Agent` 工具，优先 `Explore` 类型做只读审查）去深读代码/文档并返回结论，避免长期深读污染主会话上下文。主会话只持有结论与裁决。

## 调用机制

Claude 通过 Bash 把 `codex exec` 当子进程调用。整个过程对用户表现为「只与 Claude 对话」。

```text
用户 → Claude(转达需求/定 review 标准)
     → Bash → codex exec(设计+实现，产出文档)
     → Claude 读文档 → 派子 Agent 深读审查 → 收结论
     → Claude 验收(build+测试) → 裁决 → 用户
```

### 标准调用形态

让 Codex 做技术方案设计（产出设计文档）：

```bash
codex exec -C /d/godot/rpg -s workspace-write --color never \
  --output-last-message /tmp/codex_design.md \
  "设计 <主题> 的技术方案，产出到 <设计文档路径>。说明现状、目标、方案、权衡、影响文件、测试策略。不要实现代码。"
```

让 Codex 实现已审查通过的方案：

```bash
codex exec -C /d/godot/rpg -s workspace-write --color never \
  --output-last-message /tmp/codex_impl.md \
  "按 <已通过的设计文档路径> 实现。完成后在结论文件说明改了哪些文件、为什么。"
```

只读任务（Codex 调研，不改文件）：

```bash
codex exec -C /d/godot/rpg -s read-only --ephemeral --color never \
  --output-last-message /tmp/codex_out.md \
  "<只读任务描述>"
```

关键参数：
- `-C <dir>`：工作根目录，固定为仓库根。
- `-s <mode>`：沙箱。设计/实现用 `workspace-write`，纯调研用 `read-only`。除非用户明确要求，不使用 `danger-full-access`。
- `--output-last-message <file>`：把 Codex 最终结论写入文件供 Claude 读取与留痕。
- `--color never`：避免 ANSI 噪声污染输出。
- `--ephemeral`：只读任务不落 session 文件。
- `--output-schema <file>`：需要结构化结论时强制 JSON Schema 输出。

注意：项目 `.codex/config.toml` 默认 `sandbox_mode = "danger-full-access"`。Claude 调用时必须显式用 `-s` 覆盖为最小必要权限，不依赖默认。

## 硬性约束

这些规则防止两个 AI 互相破坏，必须遵守：

1. **职责不串**：Codex 负责设计与实现，Claude 负责审查与验收。Claude 不替 Codex 重做设计；发现问题时给出 review 意见退回 Codex 修，而不是自己改实现（除非用户另行指示）。

2. **文档解耦**：Codex 的交付必须有文档载体。Claude 的 review 针对文档与实现，结论也落文档/裁决，不靠口头默契。

3. **子 Agent 做深读 review**：Claude review 时派子 Agent 深读，主会话只收结论。避免主会话上下文被大段代码长期占用。

4. **验收闸不下放**：Codex 的实现一律由 Claude 执行验证后才算完成。验证至少包括：
   ```bash
   dotnet build rpg.csproj -maxcpucount:2 -v:minimal
   dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj -v:minimal
   ```
   以及与改动相关的其它回归项目。构建或测试失败则任务未完成，退回 Codex，绝不接受自报完成。

5. **单写者原则**：同一时刻只有一方写文件。Codex 实现期间 Claude 不碰同批文件；Codex 返回后 Claude 只读取、审查、验收。禁止并发编辑同一文件。

6. **最小权限沙箱**：默认 `workspace-write`（限仓库内）。需要更高权限必须先向用户说明并获许可。

7. **可追溯**：每次 Codex 调用都用 `--output-last-message` 落结论文件，便于用户事后审查 Codex 实际做了什么。

8. **遵循项目治理**：Codex 的设计与实现必须符合 `AGENTS.md` 的设计先行、拒绝临时代码、单一权威、显式失败原则。涉及玩法规则/架构/持久化/运行时归属的改动，仍须先走提案流。Claude review 时把这些治理规则作为审查清单的一部分。

9. **一次一个 requirement**：与项目纪律一致，每个任务对应一个清晰的工作切片，不打包多个不相关改动。

## 典型流程（设计 → 审查 → 实现 → 验收）

```text
1. 用户给 Claude 一个目标
2. Claude 转达需求 + 约束给 Codex（codex exec, workspace-write）
3. Codex 做技术方案设计，产出设计文档
4. Claude 读设计文档 → 派子 Agent 深读相关代码核对 → 给 review 意见
   有问题 → 退回 Codex 修订设计，回到步骤 3
   通过   → 进入实现
5. Claude 让 Codex 按已通过的设计实现
6. Codex 实现，产出实现结论文档
7. Claude 读结论 + git diff → 派子 Agent 审查实现 → 收结论
8. Claude 跑 build + 相关回归测试验收
   失败 → 退回 Codex，回到步骤 6
   通过 → Claude 确认守护测试与文档同步 → 提交
```

## 审查清单（Claude review 时对照）

- 设计是否符合 `AGENTS.md` 治理（设计先行、单一权威、无临时代码、显式失败）？
- 是否触碰玩法/架构/持久化/运行时归属而未走提案流？
- 实现是否守住分层边界（Runtime 单一真相、Presentation 只读观测/提交意图）？
- 是否确定性（tie-break 完整、无随机/时间依赖泄漏进战斗解析）？
- 是否有越界 mutate、空引用、边界条件、命名问题？
- build 是否 0 警告 0 错误？相关回归测试是否全绿？
- 改动是否配了守护测试，防止债务/违规复发？
