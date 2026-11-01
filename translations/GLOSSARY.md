# XTU 汉化术语表（必须遵守）

产品与专有名词保持原样：Intel、XTU、Windows、BIOS、VBS、HWBOT、AI Assist、XMP、Speed Optimizer（但可用"速度优化器"）、Intel® SpeedStep、Turbo Boost（可译"睿频加速"）、Cinebench、Prime95、linpack、AVX、AMX、TMUL、DDR4/DDR5、PCIe。

## 核心术语
| English | 中文 |
|---|---|
| tuning / tune | 调校 |
| Advanced Tuning | 高级调校 |
| overclocking | 超频 |
| profile | 配置文件 |
| Profile Pairing / Hotkey-Profile Pairing | 配置文件配对 / 热键-配置文件配对 |
| App-Profile Pairing | 应用-配置文件配对 |
| benchmark / benchmarking | 基准测试 |
| stress test | 压力测试 |
| Speed Optimizer | 速度优化器 |
| AI Assist | AI 助手 |
| core | 核心 |
| P-core / Performance Core | 性能核 |
| E-core / Efficient Cores | 能效核 |
| processor | 处理器 |
| cache | 缓存 |
| voltage | 电压 |
| offset | 偏移 |
| adaptive | 自适应 |
| static | 静态 |
| ratio / multiplier | 倍频 |
| frequency | 频率 |
| memory / DRAM | 内存 |
| graphics | 显卡 |
| system agent | 系统代理 |
| watchdog | 看门狗 |
| power limit | 功耗限制 |
| thermal throttling / throttled | 温度降频 / 已降频 |
| undervolt | 降压 |
| undervolt protection | 降压保护 |
| monitoring / monitor | 监控 / 监控项 |
| sensor | 传感器 |
| trend / trendline | 趋势 / 趋势线 |
| compact view | 紧凑视图 |
| advanced view | 高级视图 |
| system information | 系统信息 |
| default | 默认 |
| apply | 应用 |
| cancel | 取消 |
| discard | 放弃 |
| reset | 重置 |
| restore | 恢复 |
| save | 保存 |
| delete | 删除 |
| close | 关闭 |
| OK | 确定 |
| settings | 设置 |
| help | 帮助 |
| about | 关于 |
| license | 许可协议 |
| report an issue | 报告问题 |
| enabled / disabled | 已启用 / 已禁用 |
| on / off | 开 / 关 |
| error / warning / notice | 错误 / 警告 / 提示 |
| failed / success | 失败 / 成功 |
| battery / AC power | 电池 / 交流电源 |
| reboot / shutdown | 重启 / 关机 |
| channel | 通道 |
| rail | 供电轨 |
| assertion | 断言 |
| characterization | 特性分析 |
| hotkey | 热键 |
| telemetry | 遥测 |
| benchmark score | 跑分 |
| reference clock / BCLK | 基准时钟 / BCLK |
| AVX offset | AVX 偏移 |
| XMP profile | XMP 配置文件 |

## 格式规则（重要）
1. 输出必须是 `id<TAB>译文`，每个输入 id 恰好一行，顺序一致，不得增删行，不要表头、不要代码块围栏、不要解释。
2. **保留行首/行尾空格**（原文用于字符串拼接）。例如原文 `" has been ignored."`（开头有一个空格）译文也必须在开头保留一个空格。
3. 保留占位符 `{0}` `{1}` `{2}` 及格式说明（如 `{0:F2}`、`{0:P0}`）原样不动，位置按中文语序合理放置。
4. 保留 `\n`（如果出现在文本里）、`%`、数字与单位符号（GHz、MHz、V、W、°C、GT/s、x）。
5. 术语表优先于个人习惯；同一术语全篇统一。
6. 界面文案简洁（按钮/标签尽量 2-4 字）；长句（说明/描述）用完整中文，句末用"。"；纯片段（原文小写开头或无句号）译文也不要加句号。
7. 原文是纯技术串（如版本号、路径、`n/a`、单位）就原样返回。
8. 不要翻译引号内的英文品牌名。

## 风格
- 简体中文，面向普通 PC 用户，专业但不生硬。
- 例句："This program requires administrative privileges to run. Please right click on the application tile and choose "Run as administrator"" → "此程序需要管理员权限才能运行。请右键单击应用磁贴并选择"以管理员身份运行""。
