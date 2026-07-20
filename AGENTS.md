# AGENTS.md

## 项目简介

第七史诗（Epic Seven）打铁助手：Windows 桌面小工具，通过 **ADB 连接安卓模拟器**（MuMu 12 等）截取游戏画面，用 **PaddleOCR** 识别装备信息（等级、强化等级、名称、品质、主/副属性、套装），按民间算法由副属性计算装备分数，并结合**官方战绩数据（前排分段）**推荐适用角色。

## 技术栈

- **.NET 9 / C#（net9.0-windows）+ WinForms**：主程序，单文件自包含发布（win-x64）
- **AntdUI 2.4.3**：现代化控件库，顶部工具栏（Select/Input/Button）与底部设置区（InputNumber/Select/Checkbox）全部使用 AntdUI 控件；注意其事件签名为自定义委托（IntEventHandler/BoolEventHandler/DecimalEventHandler），Button.Type 用 TTypeMini 枚举
- **OpenCvSharp4**：图像处理（裁剪、掩码、连通域、模板匹配）
- **Microsoft.ML.OnnxRuntime + PaddleOCR PP-OCRv4 ONNX**：文本检测（det）+ 单行识别（rec），唯一的 OCR 引擎
- **ADB（外部进程）**：截图，不依赖任何 adb 托管库
- 无测试框架；回归验证靠 `tools/OcrTest` 控制台工具跑样本截图

## 目录结构

```
src/TiezhuToolbox/            主程序（WinForms）
├── Program.cs                入口
├── MainForm.cs / .Designer.cs 主界面与装备强化逻辑；AntdUI 页签分为装备强化、英雄配置、软件设置。
├── MainForm.Tabs.cs          页签装配、设置持久化、页签间识别启停和应用内官方数据更新。
├── HeroConfigControl.cs      英雄名称/属性/职业筛选，编辑有效属性、可用套装和右三件主属性。
├── AppPaths.cs / AppSettings.cs 用户目录、原子写入和版本化软件设置（LocalAppData/TiezhuToolbox）。
├── AdbHelper.cs              adb.exe 定位（程序目录→PATH→SDK 环境变量→Android Studio 默认路径）、
│                             设备枚举（devices -l）、connect、exec-out screencap 截图
├── ScreenshotHelper.cs       截图保存（exe 同目录 screenshots/，兼容单文件发布）
├── Modules/Ocr/
│   ├── OcrEngine.cs          识别流程编排：面板文本 → 行分组 → 结构锚点（"装备分数"行）解析；
│   │                         等级/强化等级走"图标区定位 → 橙色徽章 → 金色数字条"识别链
│   ├── PaddleOcrEngine.cs    PP-OCRv4 det+rec 封装（ONNX Runtime）
│   ├── DigitTemplateMatcher.cs 数字/掩码模板匹配（MatchTemplate，多 _mask 模板竞争取最优）
│   ├── ImagePreprocessor.cs  裁剪、数字区域预处理、Mat→Bitmap
│   ├── EquipmentScoreCalculator.cs 装备分数计算（民间算法，只统计副属性）
│   └── EquipmentInfo.cs      识别结果模型
├── Modules/Recommend/
│   ├── HeroDatabase.cs       三级角色数据库：用户覆盖 > 用户更新数据 > 内置 heroes.json，支持热重载。
│   ├── HeroDataModels.cs     官方基础数据、解析后英雄配置与用户覆盖模型。
│   ├── HeroDataUpdateService.cs 主程序/采集工具共用的官方元数据、传说战绩和图片更新服务。
│   ├── EquipmentRules.cs     装备部位识别、右三件主属性规范化和默认主属性推导。
│   ├── HeroRecommender.cs    装备→适用角色推荐：主属性（须为有用属性）与套装（须属主流搭配）为
│   │                         硬门槛，不符直接淘汰；右三件按项链/戒指/鞋独立主属性配置过滤；
│   │                         角色需求速度时，装备主/副属性必须带速度，否则直接淘汰；
│   │                         匹配度按“当前部位可出现的有效属性覆盖率 × 强化分配质量”计算；角色需求
│   │                         少于四种或右三件主属性占用需求时动态缩小目标，强化全跳无用属性仍会显著降分，
│   │                         左三件固定主属性与未识别出的主属性/套装不过滤
│   ├── HeroRecommendation.cs 推荐结果模型
│   └── EnhancementAdvisor.cs 强化建议：85级分数阶梯（+3 前分数≥左右件阈值，之后每 3 级 +6，
│                             +15 时预计重铸≥65建议重铸）；88级使用独立阈值（默认28、每跳+7，
│                             +15达标建议保留且不重铸）+ 赌速度阶梯（速度≥3/6/9/12/12 可继续，
│                             +15 时 ≥15；85级建议重铸、88级建议保留）；
│                             部位取自 Quality 文本（如"传说武器"），左三件直接走阶梯，右三件先淘汰
│                             固定攻/防/血主属性，且只有项链/戒指可赌速度（鞋子低分直接放弃）
├── Assets/PaddleOCR/         ONNX 模型 + 字典（构建时拷贝到输出目录，勿删）
├── Assets/Templates/digits/  数字模板（88.png、85_mask.png、88_mask.png 等）
└── Assets/HeroData/          官方战绩数据（采集工具生成，提交进 git）：
    ├── heroes.json           前排分段角色的有用属性 + 主流套装搭配 + 套装名表
    ├── heroes/{code}.png     角色头像
    └── sets/{set}.png        套装图标

tools/OcrTest/                OCR 回归工具（引用主项目；args 传截图绝对路径，无参跑默认老样本，
                              --synthetic 跑合成装备样例验证推荐算法）
tools/TemplateGenerator/      从截图提取数字模板的脚手架（按需改坐标用）
tools/HeroDataCollector/      官方数据采集工具：共享主程序的采集服务，读取官网简体英雄元数据，
                              合并当前前排分段统计，下载全部可选英雄头像/套装图标并生成内置数据。
```

## 构建与验证

```bash
dotnet build src/TiezhuToolbox/TiezhuToolbox.csproj     # 编译
dotnet run --project src/TiezhuToolbox                  # 运行主程序
cd tools/OcrTest && dotnet run -- <截图路径...>          # OCR 回归（改识别逻辑后必跑）
cd tools/OcrTest && dotnet run -- --synthetic            # 推荐+强化建议+右三件主属性配置自检（速度套速度鞋→c5154 应 100%；暴击套暴击项链→应淘汰 c5154；
                                                         # 强化全跳暴击→c5154 应大降；6 个强化建议样例结论见注释"应：xxx"）
cd tools/OcrTest && dotnet run -- --ui-smoke             # 三页签、全部英雄加载、离开装备页暂停持续识别
cd tools/OcrTest && dotnet run -- --config-smoke         # 英雄覆盖与软件设置持久化/恢复默认
dotnet run --project tools/HeroDataCollector             # 重新采集官方角色数据（每赛季/版本更新后跑一次）
dotnet publish src/TiezhuToolbox -c Release              # 单文件自包含发布
```

样本截图：主程序 `bin/.../screenshots/` 下有多张历史 ADB 截图（85/+0、85/+3、85/+6、85/+9、88/+3、88/+9 各档），回归时一起跑。

## 约定与注意事项

- 注释、提交信息用中文；提交信息采用 `feat: / fix:` 等前缀。
- **PaddleOCR 输入必须是 3 通道 BGR**：`PaddleOcrEngine.ToNormalizedTensor` 内部会把灰度/BGRA 转 BGR（直接按 3 通道读会越界崩溃，已踩过坑），调用方无需额外处理。
- 截图走 `adb exec-out screencap -p` 并以**二进制**读取；不要改成 `adb shell screencap`（Windows 下 CRLF 转换会损坏 PNG）。
- 等级数字是图标绶带上的金色美术字：二值化掩码会丢笔画特征，**彩色原条带 + Paddle 最稳**，掩码仅用于模板匹配和定位。
- 新增等级模板：把识别时自动保存的 `*_level.png`（干净的数字掩码裁剪）复制到 `Assets/Templates/digits/` 并命名为 `XX_mask.png`（如 `90_mask.png`）即可，多模板竞争自动生效。
- MuMu 12 默认 ADB 地址 `127.0.0.1:16384`，需在模拟器"设置中心 → 其他 → ADB 调试"中开启。
- 官方战绩数据：STOVE GG `e7api.onstove.com/gameApi`（POST + query 传参，无需登录），**采集 `grade_code=emperor` 并统一显示为“前排分段”**，避免暴露具体段位称谓；`Assets/HeroData/` 提交进 git，每个新赛季重跑一次 `tools/HeroDataCollector` 更新。
- 有用属性推导：属性直方图 0+1 号低桶占比 < 15%，或峰值桶 ≥ 4 号桶 ⇒ 玩家普遍在堆此属性；主流套装：使用率 ≥ 10% 才保留，主流组合包含速度套时强制补入速度属性。
- 套装名必须与游戏内简体中文一致（破灭/守护/生命值/抵抗/夹攻…见 heroes.json 的 sets 表），否则无法匹配 OCR 的 SetName。
- `screenshots/`、`bin/`、`obj/` 已在 .gitignore；运行时会额外生成 `*_debug.png`（识别框标注）和 `*_level.png`（等级数字裁剪）调试图。
