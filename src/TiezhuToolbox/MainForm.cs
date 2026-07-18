using System.Diagnostics;

namespace TiezhuToolbox;

public partial class MainForm : Form
{
    private static readonly Color AccentColor = Color.FromArgb(26, 115, 232);
    private static readonly Color TextDarkColor = Color.FromArgb(32, 33, 36);
    private static readonly Font HeroNameFont = new("Microsoft YaHei UI", 9.75F, FontStyle.Bold);
    private static readonly Font HeroScoreFont = new("Microsoft YaHei UI", 9F);

    // 强化建议徽章配色：继续强化=绿，赌速度=橙，重铸=紫，放弃=红，无法判断=灰
    private static readonly Color AdviceContinueColor = Color.FromArgb(52, 168, 83);
    private static readonly Color AdviceGambleColor = Color.FromArgb(245, 124, 0);
    private static readonly Color AdviceReforgeColor = Color.FromArgb(142, 36, 170);
    private static readonly Color AdviceGiveUpColor = Color.FromArgb(217, 48, 37);
    private static readonly Color AdviceNoneColor = Color.FromArgb(95, 99, 104);

    private string? _lastScreenshotPath;
    private Modules.Ocr.EquipmentInfo? _lastInfo;

    public MainForm()
    {
        InitializeComponent();
        DoubleBuffered = true;
    }

    private void MainForm_Load(object sender, EventArgs e)
    {
        RefreshDeviceList();
    }

    private void RefreshDeviceList()
    {
        if (AdbHelper.FindAdbPath() == null)
        {
            comboDevices.DataSource = null;
            UpdateStatus("未找到 adb.exe，请将 platform-tools 的 adb.exe 放到程序目录或加入 PATH");
            return;
        }

        try
        {
            var devices = AdbHelper.GetDevices();
            comboDevices.DataSource = null;
            comboDevices.DataSource = devices;

            if (comboDevices.Items.Count > 0)
                comboDevices.SelectedIndex = 0;

            UpdateStatus(devices.Count > 0
                ? $"已加载 {devices.Count} 个设备"
                : "未发现设备，请确认模拟器已开启 ADB 调试（MuMu：设置中心→其他→ADB 调试），或输入地址后点击连接");
        }
        catch (Exception ex)
        {
            UpdateStatus($"获取设备列表失败：{ex.Message}");
        }
    }

    private void btnRefresh_Click(object sender, EventArgs e)
    {
        RefreshDeviceList();
    }

    private void btnConnect_Click(object sender, EventArgs e)
    {
        var address = txtAddress.Text.Trim();
        if (string.IsNullOrEmpty(address))
        {
            UpdateStatus("请输入模拟器 ADB 地址（如 127.0.0.1:16384）");
            return;
        }

        if (AdbHelper.FindAdbPath() == null)
        {
            UpdateStatus("未找到 adb.exe，请将 platform-tools 的 adb.exe 放到程序目录或加入 PATH");
            return;
        }

        UpdateStatus($"正在连接 {address} ...");
        Application.DoEvents();

        try
        {
            var result = AdbHelper.Connect(address);
            UpdateStatus(string.IsNullOrEmpty(result) ? "连接命令已执行" : result);
        }
        catch (Exception ex)
        {
            UpdateStatus($"连接失败：{ex.Message}");
            return;
        }

        RefreshDeviceList();
    }

    /// <summary>
    /// 截图 + 识别一步完成：截取当前设备画面并保存，随后 OCR 识别并刷新装备信息与角色推荐。
    /// </summary>
    private async void btnCaptureRecognize_Click(object sender, EventArgs e)
    {
        if (comboDevices.SelectedItem is not AdbDeviceInfo device)
        {
            UpdateStatus("请先选择一个设备");
            return;
        }

        btnCaptureRecognize.Enabled = false;
        try
        {
            UpdateStatus("正在截图...");
            var bitmap = await Task.Run(() => AdbHelper.ScreenshotPng(device.Serial));

            pictureBox.Image?.Dispose();
            pictureBox.Image = bitmap;

            var baseName = "adb_" + string.Join("_", device.Serial.Split(Path.GetInvalidFileNameChars()));
            _lastScreenshotPath = ScreenshotHelper.SaveBitmap(bitmap, baseName);

            UpdateStatus("正在识别...");
            var templateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Templates");
            using var engine = new Modules.Ocr.OcrEngine(templateDir);
            var info = await engine.RecognizeAsync(_lastScreenshotPath);

            ShowEquipmentInfo(info);
            UpdateStatus($"识别完成：{info.Name}，装备分数 {info.Score:0.##}");

            WriteDebugLog($"识别成功\n截图路径: {_lastScreenshotPath}\n原始文本:\n{info.RawText}");
        }
        catch (Exception ex)
        {
            UpdateStatus($"操作失败：{ex.Message}");
            WriteDebugLog($"截图识别失败：{ex}");
        }
        finally
        {
            btnCaptureRecognize.Enabled = true;
        }
    }

    private void btnOpenFolder_Click(object sender, EventArgs e)
    {
        var dir = ScreenshotHelper.GetScreenshotDirectory();
        Directory.CreateDirectory(dir);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true,
                Verb = "open"
            });
        }
        catch (Exception ex)
        {
            UpdateStatus($"无法打开截图目录：{ex.Message}");
        }
    }

    /// <summary>展开/收起底部截图预览面板。</summary>
    private void btnToggleScreenshot_Click(object sender, EventArgs e)
    {
        pnlScreenshot.Visible = !pnlScreenshot.Visible;
        btnToggleShot.Text = pnlScreenshot.Visible ? "收起截图" : "查看截图";
    }

    private void ShowEquipmentInfo(Modules.Ocr.EquipmentInfo info)
    {
        _lastInfo = info;

        lblName.Text = string.IsNullOrEmpty(info.Name) ? "（未识别到装备名称）" : info.Name;

        var meta = $"等级 {info.Level} · 强化 +{info.EnhanceLevel}";
        if (!string.IsNullOrEmpty(info.Quality))
            meta += $" · {info.Quality}";
        lblMeta.Text = meta;

        lblScoreValue.Text = info.Score.ToString("0.##");
        lblMainStat.Text = $"主属性：{info.MainStatName} {info.MainStatValue}";

        listSubStats.Items.Clear();
        foreach (var sub in info.SubStats)
        {
            var text = $"{sub.Name} {sub.Value}";
            if (!string.IsNullOrEmpty(sub.EnhanceValue))
                text += $" ({sub.EnhanceValue})";
            listSubStats.Items.Add(text);
        }

        lblSet.Text = $"套装：{info.SetName}";

        UpdateAdvice();
        ShowHeroRecommendations(info);
    }

    /// <summary>按当前阈值计算并展示强化建议（识别完成或阈值变更时调用）。</summary>
    private void UpdateAdvice()
    {
        if (_lastInfo == null)
            return;

        var result = Modules.Recommend.EnhancementAdvisor.Analyze(
            _lastInfo, (double)numLeftThreshold.Value, (double)numRightThreshold.Value);

        lblAdviceBadge.Text = result.Text;
        lblAdviceBadge.BackColor = result.Advice switch
        {
            Modules.Recommend.EnhanceAdvice.Continue => AdviceContinueColor,
            Modules.Recommend.EnhanceAdvice.GambleSpeed => AdviceGambleColor,
            Modules.Recommend.EnhanceAdvice.Reforge => AdviceReforgeColor,
            Modules.Recommend.EnhanceAdvice.GiveUp => AdviceGiveUpColor,
            Modules.Recommend.EnhanceAdvice.GiveUpFixedMain => AdviceGiveUpColor,
            _ => AdviceNoneColor,
        };
        lblAdviceDetail.Text = result.Detail;
    }

    private void numThreshold_ValueChanged(object sender, EventArgs e)
    {
        UpdateAdvice();
    }

    /// <summary>
    /// 根据识别出的装备信息，展示官方战绩（传说分段）匹配出的适用角色（头像 + 名字 + 匹配度）。
    /// </summary>
    private void ShowHeroRecommendations(Modules.Ocr.EquipmentInfo info)
    {
        // 清空旧的推荐项并释放头像图片
        var oldItems = flowHeroes.Controls.Cast<Control>().ToList();
        flowHeroes.Controls.Clear();
        foreach (var control in oldItems)
            control.Dispose();

        if (!Modules.Recommend.HeroDatabase.Instance.IsLoaded)
        {
            lblHeroesTitle.Text = "适用角色（缺少 heroes.json，请先运行 tools/HeroDataCollector）";
            return;
        }

        var recommendations = Modules.Recommend.HeroRecommender.Recommend(info);
        lblHeroesTitle.Text = recommendations.Count > 0 ? "适用角色" : "适用角色（无匹配）";

        foreach (var rec in recommendations)
        {
            var item = new Panel
            {
                Width = 172,
                Height = 62,
                Margin = new Padding(0, 0, 10, 10),
                BackColor = Color.FromArgb(248, 249, 250),
            };

            var avatar = new PictureBox
            {
                Location = new Point(9, 9),
                Size = new Size(44, 44),
                SizeMode = PictureBoxSizeMode.Zoom,
            };
            if (rec.AvatarPath != null)
                avatar.Image = LoadImageNoLock(rec.AvatarPath);

            var nameLabel = new Label
            {
                Location = new Point(62, 11),
                Size = new Size(104, 20),
                Font = HeroNameFont,
                ForeColor = TextDarkColor,
                Text = rec.Name,
                AutoEllipsis = true,
            };

            var scoreLabel = new Label
            {
                Location = new Point(62, 33),
                Size = new Size(104, 20),
                Font = HeroScoreFont,
                ForeColor = AccentColor,
                Text = $"匹配度 {rec.Score:0.#}%",
                AutoEllipsis = true,
            };

            if (rec.MatchedStats.Count > 0)
            {
                var tip = $"匹配副属性：{string.Join("、", rec.MatchedStats)}";
                toolTip.SetToolTip(item, tip);
                toolTip.SetToolTip(avatar, tip);
                toolTip.SetToolTip(nameLabel, tip);
                toolTip.SetToolTip(scoreLabel, tip);
            }

            item.Controls.Add(avatar);
            item.Controls.Add(nameLabel);
            item.Controls.Add(scoreLabel);
            flowHeroes.Controls.Add(item);
        }
    }

    /// <summary>读文件加载图片且不占用文件句柄（避免锁住 Assets 下的头像）。</summary>
    private static Image LoadImageNoLock(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return new Bitmap(new MemoryStream(bytes));
    }

    /// <summary>
    /// 把调试信息（原始识别文本、异常堆栈等）追加写入程序目录 logs/debug.log，不在界面显示。
    /// </summary>
    private void WriteDebugLog(string message)
    {
        try
        {
            var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "debug.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            // 日志写入失败不影响主流程
        }
    }

    private void UpdateStatus(string message)
    {
        toolStripStatusLabel.Text = message;
    }
}
