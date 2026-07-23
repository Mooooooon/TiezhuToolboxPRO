# AGENTS.md

## 项目简介

第七史诗（Epic Seven）打铁助手：Windows 桌面小工具，通过 **ADB 连接安卓模拟器**（MuMu 12 等）截取游戏画面，用 **PaddleOCR** 识别装备信息（等级、强化等级、名称、品质、主/副属性、套装），按民间算法由副属性计算装备分数，并按人工维护的“套装→属性子类→英雄配装”数据推荐用途。

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
├── MainForm.cs / .Designer.cs 主界面与装备强化逻辑；AntdUI 页签包含装备强化、自动强化、需求分析、软件设置。
├── MainForm.Tabs.cs          页签装配、设置持久化和页签间识别启停。
├── DemandBrowserControl.cs   只读套装需求浏览器，展示子类权重和英雄完整配装组合。
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
│   ├── DemandDatabase.cs     只读加载并校验内置 demand-profiles.json，不联网、不使用用户覆盖。
│   ├── DemandDataModels.cs   套装、属性子类、英雄完整配装和八维权重模型。
│   ├── EquipmentRules.cs     装备部位识别和右三主属性规范化。
│   ├── SetProfileMatcher.cs  装备→套装子类推荐：匹配度=有效率×属性覆盖率×JSD配点契合度；
│   │                         仅一条可修改歪副属性按初始值20%+歪强化全罚，其余歪属性全罚；
│   │                         右三主属性用满值量化。
│   ├── SetProfileRecommendation.cs 子类和英雄配装两级推荐结果。
│   └── EnhancementAdvisor.cs 强化建议：85级分数阶梯（+3 前分数≥左右件阈值，之后每 3 级 +6，
│                             +15 时预计重铸≥65建议重铸）；88级使用独立阈值（默认28、每跳+7，
│                             +15达标建议保留且不重铸）+ 赌速度阶梯（速度≥3/6/9/12/12 可继续，
│                             +15 时 ≥15；85级建议重铸、88级建议保留）；
│                             部位取自 Quality 文本（如"传说武器"），左三件直接走阶梯，右三件先淘汰
│                             固定攻/防/血主属性，且只有项链/戒指可赌速度（鞋子低分直接放弃）；
│                             默认开启速度套必须带速度、暴击率/暴伤高权重项链仅接受对应主属性两条硬规则
├── Assets/PaddleOCR/         ONNX 模型 + 字典（构建时拷贝到输出目录，勿删）
├── Assets/Templates/digits/  数字模板（88.png、85_mask.png、88_mask.png 等）
└── Assets/HeroData/          人工维护需求数据与静态图片：
    ├── demand-profiles.json  唯一需求数据源（23套装、属性子类、英雄配装、八维权重）
    ├── heroes/{code}.png     角色头像
    └── sets/{set}.png        套装图标

tools/OcrTest/                OCR 回归工具；--demand-data 校验静态数据，--synthetic 验证匹配与强化算法
tools/TemplateGenerator/      从截图提取数字模板的脚手架（按需改坐标用）
```

## 构建与验证

```bash
dotnet build src/TiezhuToolbox/TiezhuToolbox.csproj     # 编译
dotnet run --project src/TiezhuToolbox                  # 运行主程序
cd tools/OcrTest && dotnet run -- <截图路径...>          # OCR 回归（改识别逻辑后必跑）
cd tools/OcrTest && dotnet run -- --demand-data          # 23套装/171子类/644配装/100英雄及隐私字段校验
cd tools/OcrTest && dotnet run -- --synthetic            # 权重匹配、右三满值、固定主属性和强化建议自检
cd tools/OcrTest && dotnet run -- --ui-smoke             # 四页签、需求浏览器、离开装备页暂停持续识别
cd tools/OcrTest && dotnet run -- --config-smoke         # 软件设置持久化/恢复默认
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
- `demand-profiles.json` 是唯一需求真源，后续只通过仓库提交人工维护；禁止重新加入运行时联网覆盖或用户英雄配置。
- 右三主属性按满值参与用途匹配但不进入装备分/重铸分：百分比、命中、抗性65，暴击60×1.5，暴伤70×1.125，速度45×2；85按90预估，88/90同档。
- 套装名必须与游戏内简体中文一致（破灭/守护/生命值/抵抗/夹攻等），否则无法匹配 OCR 的 SetName。
- `screenshots/`、`bin/`、`obj/` 已在 .gitignore；运行时会额外生成 `*_debug.png`（识别框标注）和 `*_level.png`（等级数字裁剪）调试图。
