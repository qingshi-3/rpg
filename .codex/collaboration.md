# Claude + Codex 协作约定

本文件定义在单个 CLI 会话中由 Codex 参与讨论调查与执行、Claude 负责用户沟通和审查的双 AI 协作规则。它是稳定的协作流程约定，不是玩法或架构权威文档。架构与玩法权威仍遵循 `AGENTS.md` 与 `gameplay-alignment/authority-map.md`。

## 角色

```text
Codex   = 讨论阶段的只读技术调查与方案支持 + 已确认活动任务的实现
Claude  = 用户讨论与确认结论成文 + 任务边界与实现审查 + 独立验收
```

| 职责 | 归属 | 理由 |
|---|---|---|
| 技术调查与方案支持 | Codex | 提供只读事实、工程取舍与验证建议，支持 Claude 与用户确认方向 |
| 已确认活动任务的实现 | Codex | 按同一活动任务边界执行并维护进度与证据 |
| 确认结论成文、任务边界与实现审查 | Claude | 将确认结论落入同一活动任务，并独立检查范围与实现 |
| 独立验收（按活动任务风险与验收标准） | Claude | 按范围验证，执行者不自设完成 |

同一活动任务文档是 Codex 与 Claude 之间的解耦接口：Claude 落确认结论与任务边界，Codex 维护执行进度和证据，Claude 再据此独立审查与验收。

## 解耦原则

**文档即接口**：用户确认后，Claude 在 `work-items/active/` 创建或更新一份自包含任务文档；Codex 通过同一文档接收结论并维护进度、证据和恢复入口，Claude 再据此 review。`--output-last-message` 只承载一次调用的简短返回，不替代仓库内任务。Claude 不替 Codex 设计，Codex 不自行做最终验收。

**子 Agent 隔离 review**：Claude 做 review 时，应派 Claude 子 Agent（`Agent` 工具，优先 `Explore` 类型做只读审查）去深读代码/文档并返回结论，避免长期深读污染主会话上下文。主会话只持有结论与裁决。

## 调用机制

Claude 通过 Bash 把 `codex exec` 当子进程调用。整个过程对用户表现为「只与 Claude 对话」。

```text
用户 ↔ Claude(循环讨论、确认方向)
     → Bash → codex exec(按需做只读技术调查)
     → 用户确认 → Claude 落活动任务文档
     → Bash → codex exec(按任务实现，更新进度与证据)
     → Claude 读文档 → 派子 Agent 深读审查 → 收结论
     → Claude 按任务风险与验收标准验证 → 裁决 → 用户
```

### 标准调用形态

让 Codex 在讨论阶段做只读技术调查（不改仓库）：

```bash
codex exec -C /d/godot/rpg -s read-only --ephemeral --color never \
  -m gpt-5.6-sol -c 'model_reasoning_effort="ultra"' \
  --output-last-message /tmp/codex_discussion.md \
  "只读调查 <主题>。说明现状、可选方案、工程代价、风险和验证建议；不要修改任何文件或外部状态。"
```

让 Codex 实现已由用户确认的活动任务：

```bash
codex exec -C /d/godot/rpg -s workspace-write --color never \
  -m gpt-5.6-sol -c 'model_reasoning_effort="high"' \
  --output-last-message /tmp/codex_impl.md \
  "执行 work-items/active/<task>.md，不重新设计或扩大范围。维护任务进度与证据；方向冲突时标记 Needs Discussion 并停止；完成执行后留给独立验证。"
```

复杂迁移、持久化、并发、疑难调试或安全工作可把执行档位显式提高为 `xhigh`；普通实现不得默认提高。

只读任务（Codex 调研，不改文件）：

```bash
codex exec -C /d/godot/rpg -s read-only --ephemeral --color never \
  --output-last-message /tmp/codex_out.md \
  "<只读任务描述>"
```

关键参数：
- `-C <dir>`：工作根目录，固定为仓库根。
- `-s <mode>`：沙箱。讨论调查用 `read-only`，执行实现用 `workspace-write`。除非用户明确要求，不使用 `danger-full-access`。
- `--output-last-message <file>`：把 Codex 最终结论写入文件供 Claude 读取与留痕。
- `--color never`：避免 ANSI 噪声污染输出。
- `--ephemeral`：只读任务不落 session 文件。
- `--output-schema <file>`：需要结构化结论时强制 JSON Schema 输出。
- `-m` 与 `model_reasoning_effort`：讨论调查使用 Sol + Ultra；普通执行使用 Sol + High；高风险执行才使用 xhigh。

注意：项目 `.codex/config.toml` 默认 `sandbox_mode = "danger-full-access"`。Claude 调用时必须显式用 `-s` 覆盖为最小必要权限，不依赖默认。

## 硬性约束

这些规则防止两个 AI 互相破坏，必须遵守：

1. **职责不串**：Codex 负责只读技术调查与已确认活动任务的实现，Claude 负责用户讨论、任务成文、审查与验收。Claude 不替 Codex 重做实现；发现问题时把任务退回 `In Progress` 或 `Needs Discussion`，而不是自己改实现（除非用户另行指示）。

2. **文档解耦**：Codex 的交付必须有文档载体。Claude 的 review 针对文档与实现，结论也落文档/裁决，不靠口头默契。

3. **子 Agent 做深读 review**：Claude review 时派子 Agent 深读，主会话只收结论。避免主会话上下文被大段代码长期占用。

4. **验收闸不下放**：Codex 的实现一律由 Claude 独立验证后才算完成。验证项目由活动任务风险和 acceptance criteria 决定：代码或运行时改动通常包括低并发构建与相关回归；纯文档任务不强制运行无关战斗回归。只有相关失败才把任务退回 `In Progress`；需要改变方向或权威时退回 `Needs Discussion`。无关既有失败应记录，但不得误判为本任务失败。

5. **单写者原则**：同一时刻只有一方写文件。Codex 实现期间 Claude 不碰同批文件；Codex 返回后 Claude 只读取、审查、验收。禁止并发编辑同一文件。

6. **最小权限沙箱**：讨论和只读调查必须使用 `read-only`；已确认活动任务的执行默认使用 `workspace-write`（限仓库内）。更高权限只可在确有必要且用户已授权的范围内使用，不得因项目默认配置较宽而省略显式沙箱覆盖。

7. **可追溯**：活动任务文档是完整进度、执行证据、恢复条件和最终结果的唯一任务记录；每次 Codex 调用仍用 `--output-last-message` 提供简短交接，但不得形成平行任务档案。

8. **遵循项目治理**：Codex 的调查与实现必须符合 `AGENTS.md` 的两阶段门禁、活动任务生命周期、拒绝临时代码、单一权威和显式失败原则。涉及玩法规则、架构、持久化或运行时归属的改动，必须先由用户确认讨论结论，并在实现前同步相关权威文档。Claude review 时把这些治理规则作为审查清单的一部分。

9. **一次一个 requirement**：与项目纪律一致，每个任务对应一个清晰的工作切片，不打包多个不相关改动。

## 典型流程（讨论 → 确认 → 实现 → 验收）

```text
1. 用户给 Claude 一个目标
2. Claude 与用户循环讨论；需要代码事实时调用 Codex 做只读调查
3. 用户明确确认方向，Claude 在 `work-items/active/` 创建或更新自包含任务
4. 长期玩法或架构结论先同步到对应权威文档
5. Claude 让 Codex 以 High 按活动任务实现；高风险任务才用 xhigh
6. Codex 实现并维护任务进度与证据，完成后设为 `Awaiting Verification`；发现方向冲突则设为 `Needs Discussion` 并返回步骤 2
7. Claude 读任务 + git diff → 派子 Agent 审查实现 → 收结论
8. Claude 按活动任务风险与 acceptance criteria 验收；代码/运行时改动通常跑 build + 相关回归，纯文档任务执行对应静态检查
   相关失败 → 在活动任务范围内退回 Codex 修复；需要改方向则回到步骤 2；无关既有失败只记录
   通过 → Claude 确认守护测试与文档同步 → 将任务设为 `Completed` 并归档 → 提交
```

## 审查清单（Claude review 时对照）

- 是否存在用户确认的活动任务，且实现没有重新设计或扩大范围？
- 玩法、架构、持久化或运行时归属发生变化时，相关权威文档是否已在实现前同步？
- 实现是否守住分层边界（Runtime 单一真相、Presentation 只读观测/提交意图）？
- 是否确定性（tie-break 完整、无随机/时间依赖泄漏进战斗解析）？
- 是否有越界 mutate、空引用、边界条件、命名问题？
- 任务涉及代码或相应运行风险时，build 是否 0 警告 0 错误，相关回归测试是否全绿？
- 任务涉及需要防复发的代码或风险时，改动是否配了相应守护测试？
