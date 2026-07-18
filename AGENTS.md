# AGENTS.md

## 项目简介

第七史诗（Epic Seven）打铁助手：Windows 桌面小工具，通过 **ADB 连接安卓模拟器**（MuMu 12 等）截取游戏画面，用 **PaddleOCR** 识别装备信息（等级、强化等级、名称、品质、主/副属性、套装、装备分数）。

## 技术栈

- **.NET 9 / C#（net9.0-windows）+ WinForms**：主程序，单文件自包含发布（win-x64）
- **OpenCvSharp4**：图像处理（裁剪、掩码、连通域、模板匹配）
- **Microsoft.ML.OnnxRuntime + PaddleOCR PP-OCRv4 ONNX**：文本检测（det）+ 单行识别（rec），唯一的 OCR 引擎
- **ADB（外部进程）**：截图，不依赖任何 adb 托管库
- 无测试框架；回归验证靠 `tools/OcrTest` 控制台工具跑样本截图

## 目录结构

```
src/TiezhuToolbox/            主程序（WinForms）
├── Program.cs                入口
├── MainForm.cs / .Designer.cs 主界面：设备下拉框 + 地址框 + 连接/刷新/截图/目录/识别
├── AdbHelper.cs              adb.exe 定位（程序目录→PATH→SDK 环境变量→Android Studio 默认路径）、
│                             设备枚举（devices -l）、connect、exec-out screencap 截图
├── ScreenshotHelper.cs       截图保存（exe 同目录 screenshots/，兼容单文件发布）
├── Modules/Ocr/
│   ├── OcrEngine.cs          识别流程编排：面板文本 → 行分组 → 结构锚点（"装备分数"行）解析；
│   │                         等级/强化等级走"图标区定位 → 橙色徽章 → 金色数字条"识别链
│   ├── PaddleOcrEngine.cs    PP-OCRv4 det+rec 封装（ONNX Runtime）
│   ├── DigitTemplateMatcher.cs 数字/掩码模板匹配（MatchTemplate，多 _mask 模板竞争取最优）
│   ├── ImagePreprocessor.cs  裁剪、数字区域预处理、Mat→Bitmap
│   └── EquipmentInfo.cs      识别结果模型
├── Assets/PaddleOCR/         ONNX 模型 + 字典（构建时拷贝到输出目录，勿删）
└── Assets/Templates/digits/  数字模板（88.png、85_mask.png、88_mask.png 等）

tools/OcrTest/                OCR 回归工具（引用主项目；args 传截图绝对路径，无参跑默认老样本）
tools/TemplateGenerator/      从截图提取数字模板的脚手架（按需改坐标用）
```

## 构建与验证

```bash
dotnet build src/TiezhuToolbox/TiezhuToolbox.csproj     # 编译
dotnet run --project src/TiezhuToolbox                  # 运行主程序
cd tools/OcrTest && dotnet run -- <截图路径...>          # OCR 回归（改识别逻辑后必跑）
dotnet publish src/TiezhuToolbox -c Release             # 单文件自包含发布
```

样本截图：主程序 `bin/.../screenshots/` 下有多张历史 ADB 截图（85/+0、85/+3、85/+6、85/+9、88/+3、88/+9 各档），回归时一起跑。

## 约定与注意事项

- 注释、提交信息用中文；提交信息采用 `feat: / fix:` 等前缀。
- **PaddleOCR 输入必须是 3 通道 BGR**：`PaddleOcrEngine.ToNormalizedTensor` 内部会把灰度/BGRA 转 BGR（直接按 3 通道读会越界崩溃，已踩过坑），调用方无需额外处理。
- 截图走 `adb exec-out screencap -p` 并以**二进制**读取；不要改成 `adb shell screencap`（Windows 下 CRLF 转换会损坏 PNG）。
- 等级数字是图标绶带上的金色美术字：二值化掩码会丢笔画特征，**彩色原条带 + Paddle 最稳**，掩码仅用于模板匹配和定位。
- 新增等级模板：把识别时自动保存的 `*_level.png`（干净的数字掩码裁剪）复制到 `Assets/Templates/digits/` 并命名为 `XX_mask.png`（如 `90_mask.png`）即可，多模板竞争自动生效。
- MuMu 12 默认 ADB 地址 `127.0.0.1:16384`，需在模拟器"设置中心 → 其他 → ADB 调试"中开启。
- `screenshots/`、`bin/`、`obj/` 已在 .gitignore；运行时会额外生成 `*_debug.png`（识别框标注）和 `*_level.png`（等级数字裁剪）调试图。
