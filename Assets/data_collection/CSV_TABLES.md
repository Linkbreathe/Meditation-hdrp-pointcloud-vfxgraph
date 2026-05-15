# Meditation Experiment CSV 数据字典

本文档说明 `data_collection/<sessionId>/` 下七张 CSV 表的设计初衷、记录初衷和字段含义。表结构来自 `MeditationExperimentCsvLogger`、`PaintingRotationController`、`StarryNightRhoneVfxAutoAnimator` 和 `EyeTrackingDataLogger` 当前实现。

## 整体设计

每次实验开始后会创建一个独立的 session 文件夹，文件夹名与 `sessionId` 一致。一个 session 文件夹内包含七张表：

| 文件 | 粒度 | 主要用途 |
|---|---|---|
| `session.csv` | 每次实验 1 行 | 记录实验会话身份、开始时间、启动输入来源和 schema 版本。 |
| `paintings.csv` | 每幅画每次完整播放 1 行 | 记录一幅画从开始到完成的整体运行信息。 |
| `stage_values.csv` | 每次粒子参数写入 1 行 | 记录 VFX 阶段中每一次随机生成并应用的 intensity/frequency。 |
| `choices.csv` | 每次选择流程结果 1 行 | 记录一次 orb 选择的完整结果，包括选择、超时或跳过。 |
| `events.csv` | 每个关键事件 1 行 | 保留完整时间线，作为审计、debug 和重建流程的原始事件流。 |
| `summary.csv` | 每个阶段的选择结果 1 行 | 提供更适合统计分析的阶段结果汇总。 |
| `eye_tracking.csv` | 每个眼动采样点 1 行 | 记录 session 内持续采样的左右眼和中心视角数据。 |

拆成七张表的目的不是让数据变复杂，而是把同一件事的不同分析视角分开：

- `events.csv` 保留所有关键事件，适合排查时间线和实验流程是否正常。
- `stage_values.csv` 与 `eye_tracking.csv` 可能数据量较大，单独放置可以避免污染低频行为表。
- `choices.csv` 和 `summary.csv` 面向后续统计，减少分析时反复从事件流中过滤和拼接。
- `paintings.csv` 与 `session.csv` 提供稳定的上层索引，方便按被试、session、作品或播放轮次分组。

## 格式约定

- 文件编码为 UTF-8。
- 字段分隔符为逗号 `,`。
- CSV version 3 起，新文件可在第一行写入 `sep=,`，帮助 Excel 在使用分号作为系统列表分隔符的机器上正确分列。程序读取时如果第一行正好是 `sep=,`，应跳过该行再读取表头。旧的 version 2 文件可能没有这一行。
- 表内索引字段对外记录为 1-based，例如 `paintingIndex=1` 表示配置列表中的第一幅画。内部不存在或不适用时会写为空。
- 时间字段有两类：
  - `*Local` 使用本地时间字符串，格式为 `yyyy-MM-dd HH:mm:ss.fff zzz`。
  - `*UnixMs` 使用 Unix epoch 毫秒。
- `elapsedMs` 一般表示相对 session 开始的毫秒数。
- `stageElapsedMs` 表示相对当前 stage 开始的毫秒数。
- `promptToSelectionMs`、`promptElapsedMs`、`choiceWaitMs` 等字段单位均为毫秒。
- 布尔值写为 `1` 或 `0`。
- 粒子参数、置信度、颜色强度一般是 `0..1` 范围内的浮点数。
- 空字段表示当前事件不适用、数据缺失，或脚本中对应值为 `NaN` / 未设置。

## 关联键

常用关联方式：

| 关联层级 | 推荐字段 |
|---|---|
| 会话 | `sessionId` |
| 一幅画的一次播放 | `sessionId + paintingRunIndex` |
| 配置列表中的画 | `sessionId + paintingIndex` 或 `paintingId` |
| 某幅画播放中的某个 stage | `sessionId + paintingRunIndex + stageIndex` |
| 某个 stage 的具体参数更新 | `sessionId + paintingRunIndex + stageIndex + stageValueIndex` |
| 某个 stage 的选择结果 | `sessionId + paintingRunIndex + stageIndex` |

## `session.csv`

### 设计初衷

`session.csv` 是整个 session 的入口表。它只需要一行，用来回答“这批数据属于哪一次实验、什么时候开始、由什么输入触发、用的是哪个 CSV schema”。

### 记录初衷

当实验会话启动时立即写入。后续所有表都通过 `sessionId` 回到这一行。

### 字段

| 字段 | 含义 |
|---|---|
| `sessionId` | 本次实验会话唯一 ID。由 session 前缀和本地开始时间组成，例如 `meditation_experiment_20260516_002946_263`。 |
| `sessionStartLocal` | session 开始的本地时间，带时区偏移。 |
| `sessionStartUnixMs` | session 开始的 Unix 毫秒时间戳。 |
| `inputSource` | 触发 session 开始的输入来源，例如键盘、Unity XR primary button、OVRInput A button，以及当时的 controller 描述。 |
| `csvVersion` | CSV schema 版本。当前脚本为 `3`。旧文件可能为 `2`。 |
| `sessionFolderPath` | 本次 session 的输出文件夹路径。 |

## `paintings.csv`

### 设计初衷

`paintings.csv` 是作品播放层级的事实表。它把一幅画的一次完整运行压缩成一行，适合分析每幅画的总观看/播放时长、播放顺序和 stage preset 顺序。

### 记录初衷

一幅画开始时会写入 `events.csv` 的 `painting_started`。一幅画完成时，脚本才会向 `paintings.csv` 写入完整行，因为此时开始和结束时间都已知。

### 字段

| 字段 | 含义 |
|---|---|
| `sessionId` | 所属 session。 |
| `paintingRunIndex` | 本 session 内第几次播放画作。每开始一幅画递增一次。 |
| `paintingIndex` | 画作在 `_paintings` 配置列表中的 1-based 位置。 |
| `paintingId` | 画作配置中的稳定 ID，适合跨 session 关联同一作品。 |
| `paintingName` | 画作 GameObject 名称。 |
| `paintingStartLocal` | 这次画作播放开始的本地时间。 |
| `paintingStartUnixMs` | 这次画作播放开始的 Unix 毫秒时间戳。 |
| `paintingEndLocal` | 这次画作播放完成的本地时间。 |
| `paintingEndUnixMs` | 这次画作播放完成的 Unix 毫秒时间戳。 |
| `paintingDurationMs` | `paintingEndUnixMs - paintingStartUnixMs`，即这次画作运行总时长。 |
| `stageOrder` | 本次播放实际使用的 stage preset 顺序，逗号分隔且 1-based。由于 stage 顺序会打乱，此字段用于还原实际播放顺序。 |
| `notes` | 说明文本，例如 `Painting run completed.`。 |

## `stage_values.csv`

### 设计初衷

`stage_values.csv` 是 VFX 参数变化表。它只记录真正应用到粒子系统的随机参数值，避免在完整事件流中筛选大量 stage value 事件。

### 记录初衷

每个 stage 会根据当前 stage 的随机范围生成若干次 `particleIntensity` 和 `particleFrequency`，并按 `_updateInterval` 应用到 VFX。每应用一次，写一行。

### 字段

| 字段 | 含义 |
|---|---|
| `sessionId` | 所属 session。 |
| `paintingRunIndex` | 本 session 内第几次播放画作。 |
| `paintingIndex` | 画作在 `_paintings` 配置列表中的 1-based 位置。 |
| `paintingId` | 画作稳定 ID。 |
| `paintingName` | 画作 GameObject 名称。 |
| `stageIndex` | 当前播放流程中的第几个 stage，1-based。注意这是打乱后的播放位置，不是原始 preset 编号。 |
| `stagePresetIndex` | 当前 stage 使用的随机范围 preset 编号，1-based。 |
| `stageValueIndex` | 当前 stage 内第几次参数更新，1-based。 |
| `stageValueCount` | 当前 stage 计划应用的参数更新总次数。 |
| `eventLocal` | 这次参数写入发生的本地时间。 |
| `eventUnixMs` | 这次参数写入发生的 Unix 毫秒时间戳。 |
| `elapsedMs` | 从 session 开始到这次参数写入的毫秒数。 |
| `stageElapsedMs` | 从当前 stage 开始到这次参数写入的毫秒数。 |
| `stageRangeMinimum` | 当前 stage preset 随机范围的最小值。 |
| `stageRangeMaximum` | 当前 stage preset 随机范围的最大值。 |
| `particleIntensity` | 本次写入 VFX 的粒子强度，通常在 `0..1`。 |
| `particleFrequency` | 本次写入 VFX 的粒子频率，通常在 `0..1`。 |

## `choices.csv`

### 设计初衷

`choices.csv` 是选择流程事实表。它记录一次 choice prompt 从开始、触发选择，到选择效果完成的完整过程。相比 `summary.csv`，它保留更多过程时间字段。

### 记录初衷

每个 stage 完成后，系统可能显示 orb 选择提示。选择结果可能是：

- `choice_completed`：眼动停留达到阈值并完成选择效果。
- `choice_timeout`：等待超过设定时间，没有完成选择。
- `choice_prompt_skipped`：选择反馈组件不存在，无法显示选择提示。

这些结果都会写入 `choices.csv`，并同步写入 `summary.csv` 与 `events.csv`。

### 字段

| 字段 | 含义 |
|---|---|
| `sessionId` | 所属 session。 |
| `paintingRunIndex` | 本 session 内第几次播放画作。 |
| `paintingIndex` | 画作在 `_paintings` 配置列表中的 1-based 位置。 |
| `paintingId` | 画作稳定 ID。 |
| `paintingName` | 画作 GameObject 名称。 |
| `stageIndex` | 当前播放流程中的第几个 stage，1-based。 |
| `stagePresetIndex` | 当前 stage 使用的随机范围 preset 编号，1-based。 |
| `eventType` | 选择结果类型，例如 `choice_completed`、`choice_timeout` 或 `choice_prompt_skipped`。 |
| `promptStartLocal` | 选择提示开始的本地时间。若没有单独记录 prompt start，则使用当前事件时间。 |
| `promptStartUnixMs` | 选择提示开始的 Unix 毫秒时间戳。 |
| `selectionTriggeredLocal` | 眼动选择被触发的本地时间。仅 `choice_completed` 通常有值。 |
| `selectionTriggeredUnixMs` | 眼动选择被触发的 Unix 毫秒时间戳。 |
| `selectionTriggeredElapsedMs` | 从 session 开始到选择触发的毫秒数。 |
| `promptToSelectionMs` | 从选择提示开始到选择触发的毫秒数。 |
| `selectionTriggeredFrame` | 选择触发时的 Unity frame。 |
| `choiceCompleteLocal` | 选择流程写入结果时的本地时间。`choice_completed` 时为选择效果完成时间。 |
| `choiceCompleteUnixMs` | 选择流程写入结果时的 Unix 毫秒时间戳。 |
| `elapsedMs` | 从 session 开始到选择流程结果写入时间的毫秒数。 |
| `promptElapsedMs` | 从选择提示开始到选择流程结果写入时间的毫秒数。 |
| `choiceWaitMs` | `choice_completed` 时为 prompt 到 trigger 的等待时长；`choice_timeout` 时为等待到超时的时长；跳过时通常为 `0`。 |
| `orbIndex` | 被选中的 orb 编号，1-based。未选中时为空。 |
| `orbLabel` | 被选中的 orb 名称，例如 `MgicOrb_lit_green`。未选中时为空。 |
| `selectionDwellMs` | 眼动在 orb 上持续停留并触发选择所需的时长。 |
| `selectionEffectMs` | 选择触发后，orb 选择效果播放到完成的时长。 |
| `finalIntensity` | stage 结束时保留下来的粒子强度。 |
| `finalFrequency` | stage 结束时保留下来的粒子频率。 |
| `orbColorIntensity` | 记录选择流程中 orb 颜色强度的配置值。完成或超时后通常是 normal color intensity。 |
| `notes` | 说明文本，例如选择完成、超时原因或组件缺失原因。 |

## `events.csv`

### 设计初衷

`events.csv` 是最完整的事件流。它的目标是“不丢任何关键流程节点”，即使其他分析表已经提供了更方便的视图，仍可以用这张表还原 session 时间线。

### 记录初衷

以下事件会进入 `events.csv`：

- `session_started`
- `painting_started`
- `painting_completed`
- `stage_started`
- `stage_value_applied`
- `stage_completed`
- `choice_prompt_started`
- `choice_completed`
- `choice_timeout`
- `choice_prompt_skipped`

### 字段

| 字段 | 含义 |
|---|---|
| `sessionId` | 所属 session。 |
| `sessionStartLocal` | session 开始的本地时间。 |
| `sessionStartUnixMs` | session 开始的 Unix 毫秒时间戳。 |
| `eventLocal` | 当前事件发生或被记录的本地时间。 |
| `eventUnixMs` | 当前事件发生或被记录的 Unix 毫秒时间戳。 |
| `elapsedMs` | 从 session 开始到当前事件的毫秒数。 |
| `frame` | 写入当前事件时的 Unity frame。 |
| `eventType` | 事件类型。 |
| `inputSource` | 启动 session 的输入来源。通常只有 `session_started` 行有值。 |
| `paintingRunIndex` | 本 session 内第几次播放画作。 |
| `paintingIndex` | 画作在 `_paintings` 配置列表中的 1-based 位置。 |
| `paintingId` | 画作稳定 ID。 |
| `paintingName` | 画作 GameObject 名称。 |
| `stageIndex` | 当前播放流程中的第几个 stage，1-based。 |
| `stagePresetIndex` | 当前 stage 使用的随机范围 preset 编号，1-based。 |
| `stageCount` | 当前画作播放包含的 stage 总数。 |
| `stageValueIndex` | 当前 stage 内第几次参数更新。仅 `stage_value_applied` 通常有值。 |
| `stageValueCount` | 当前 stage 计划应用的参数更新总次数。 |
| `stageElapsedMs` | 从当前 stage 开始到当前事件的毫秒数。 |
| `promptElapsedMs` | 从选择提示开始到当前事件的毫秒数。主要用于 choice result 事件。 |
| `selectionTriggeredLocal` | 眼动选择被触发的本地时间。 |
| `selectionTriggeredUnixMs` | 眼动选择被触发的 Unix 毫秒时间戳。 |
| `selectionTriggeredElapsedMs` | 从 session 开始到选择触发的毫秒数。 |
| `promptToSelectionMs` | 从选择提示开始到选择触发的毫秒数。 |
| `selectionTriggeredFrame` | 选择触发时的 Unity frame。 |
| `stageRangeMinimum` | 当前 stage preset 随机范围的最小值。 |
| `stageRangeMaximum` | 当前 stage preset 随机范围的最大值。 |
| `particleIntensity` | 当前事件对应的粒子强度。对于 `stage_value_applied` 是本次写入值；对于 stage/choice 结果通常是 stage 最后值。 |
| `particleFrequency` | 当前事件对应的粒子频率。 |
| `orbIndex` | 被选中的 orb 编号，1-based。 |
| `orbLabel` | 被选中的 orb 名称。 |
| `orbColorIntensity` | 当前事件关联的 orb 颜色强度配置值。`choice_prompt_started` 使用 prompt color intensity，结果事件通常使用 normal color intensity。 |
| `selectionDwellMs` | 眼动停留触发选择的时长。 |
| `selectionEffectMs` | 选择效果播放时长。 |
| `choiceWaitMs` | 选择等待时长或超时等待时长。 |
| `notes` | 当前事件的说明文本。 |

## `summary.csv`

### 设计初衷

`summary.csv` 是给统计分析准备的结果表。它按 stage choice outcome 汇总，把最常用的上下文、最终粒子参数和选择结果放在同一行。

### 记录初衷

每次 `choices.csv` 写入时同步写入 `summary.csv`。通常一行代表一个 stage 的最终选择结果，适合直接计算“哪个 range / intensity / frequency 更容易被选择”、“选择等待时长分布”等指标。

### 字段

| 字段 | 含义 |
|---|---|
| `sessionId` | 所属 session。 |
| `paintingRunIndex` | 本 session 内第几次播放画作。 |
| `paintingIndex` | 画作在 `_paintings` 配置列表中的 1-based 位置。 |
| `paintingId` | 画作稳定 ID。 |
| `paintingName` | 画作 GameObject 名称。 |
| `stageIndex` | 当前播放流程中的第几个 stage，1-based。 |
| `stagePresetIndex` | 当前 stage 使用的随机范围 preset 编号，1-based。 |
| `stageValueCount` | 当前 stage 计划应用的参数更新总次数。 |
| `stageRangeMinimum` | 当前 stage preset 随机范围的最小值。 |
| `stageRangeMaximum` | 当前 stage preset 随机范围的最大值。 |
| `finalIntensity` | stage 结束时保留下来的粒子强度。 |
| `finalFrequency` | stage 结束时保留下来的粒子频率。 |
| `choiceEventType` | 选择结果类型，例如 `choice_completed`、`choice_timeout` 或 `choice_prompt_skipped`。 |
| `orbIndex` | 被选中的 orb 编号，1-based。未选中时为空。 |
| `orbLabel` | 被选中的 orb 名称。未选中时为空。 |
| `selectionTriggeredLocal` | 眼动选择被触发的本地时间。 |
| `selectionTriggeredUnixMs` | 眼动选择被触发的 Unix 毫秒时间戳。 |
| `selectionTriggeredElapsedMs` | 从 session 开始到选择触发的毫秒数。 |
| `promptToSelectionMs` | 从选择提示开始到选择触发的毫秒数。 |
| `selectionTriggeredFrame` | 选择触发时的 Unity frame。 |
| `choiceWaitMs` | 选择等待时长或超时等待时长。 |
| `selectionDwellMs` | 眼动停留触发选择的时长。 |
| `selectionEffectMs` | 选择效果播放时长。 |
| `orbColorIntensity` | 选择流程结果写入时的 orb 颜色强度配置值。 |
| `eventLocal` | 选择结果事件的本地时间。 |
| `eventUnixMs` | 选择结果事件的 Unix 毫秒时间戳。 |
| `elapsedMs` | 从 session 开始到选择结果事件的毫秒数。 |
| `notes` | 选择结果说明、超时原因或跳过原因。 |

## `eye_tracking.csv`

### 设计初衷

`eye_tracking.csv` 是高频眼动采样表。它与行为事件分离，避免 `events.csv` 变得过大，同时保留后续做 gaze trajectory、注视稳定性和选择前眼动分析所需的原始采样数据。

### 记录初衷

当 `EyeTrackingDataLogger` 配置为写入实验 session，且 `MeditationExperimentCsvLogger` 已经启动 session 时，每个采样间隔写一行。采样间隔由 `_sampleInterval` 控制。

### 字段

| 字段 | 含义 |
|---|---|
| `sessionId` | 所属 session。 |
| `sampleUnixMs` | 本次采样时间的 Unix 毫秒时间戳。 |
| `elapsedMs` | 从 session 开始到本次采样的毫秒数。 |
| `frame` | 本次采样对应的 Unity frame。 |
| `leftValid` | 左眼样本是否可用。若 OVR raw sample valid，或 OVREyeGaze component confidence 大于 0，则为 `1`。 |
| `leftConfidence` | 左眼置信度。优先使用 OVREyeGaze component confidence；否则使用 OVR raw confidence。 |
| `leftOriginX` | 左眼 gaze ray 起点 X。优先来自左眼 gaze transform 的世界坐标；否则来自 raw sample。 |
| `leftOriginY` | 左眼 gaze ray 起点 Y。 |
| `leftOriginZ` | 左眼 gaze ray 起点 Z。 |
| `leftForwardX` | 左眼 gaze ray 方向 X，Unity world-space forward。 |
| `leftForwardY` | 左眼 gaze ray 方向 Y。 |
| `leftForwardZ` | 左眼 gaze ray 方向 Z。 |
| `rightValid` | 右眼样本是否可用。逻辑同 `leftValid`。 |
| `rightConfidence` | 右眼置信度。逻辑同 `leftConfidence`。 |
| `rightOriginX` | 右眼 gaze ray 起点 X。 |
| `rightOriginY` | 右眼 gaze ray 起点 Y。 |
| `rightOriginZ` | 右眼 gaze ray 起点 Z。 |
| `rightForwardX` | 右眼 gaze ray 方向 X。 |
| `rightForwardY` | 右眼 gaze ray 方向 Y。 |
| `rightForwardZ` | 右眼 gaze ray 方向 Z。 |
| `centerEyeX` | `CenterEyeAnchor` 世界坐标 X。可用来表示头显中心眼位置。 |
| `centerEyeY` | `CenterEyeAnchor` 世界坐标 Y。 |
| `centerEyeZ` | `CenterEyeAnchor` 世界坐标 Z。 |
| `centerForwardX` | `CenterEyeAnchor` forward 方向 X。 |
| `centerForwardY` | `CenterEyeAnchor` forward 方向 Y。 |
| `centerForwardZ` | `CenterEyeAnchor` forward 方向 Z。 |

## 分析建议

- 做严格时间线排查时，以 `events.csv` 为准。
- 做 stage 参数分析时，优先使用 `stage_values.csv`。
- 做选择结果统计时，优先使用 `summary.csv`；需要 prompt 到 selection 的过程细节时再看 `choices.csv`。
- 做眼动分析时，用 `eye_tracking.csv`，再通过 `sampleUnixMs` 或 `elapsedMs` 与 `events.csv` 的事件窗口对齐。
- 对旧 CSV 文件，如果 Excel 双击打开后所有内容在一列，可以在文件第一行添加 `sep=,`，或在 Excel 中通过“数据 -> 从文本/CSV”导入并选择逗号分隔。

## 当前实现注意事项

- `stageIndex` 表示打乱后的播放位置，`stagePresetIndex` 才表示使用了哪个原始随机范围 preset。
- `paintings.csv` 只在画作完成后写入完整行；画作开始事件请看 `events.csv`。
- `choice_timeout` 或 `choice_prompt_skipped` 没有实际选中的 orb，因此 `orbIndex`、`orbLabel`、`selectionTriggered*`、`selectionDwellMs` 和 `selectionEffectMs` 通常为空。
- 版本 3 增加了 Excel separator directive 支持，但数据列结构仍应按本文字段读取。
