using TiezhuToolbox.Modules.Recommend;

namespace TiezhuToolbox;

internal sealed class HeroConfigControl : UserControl
{
    private static readonly IReadOnlyDictionary<string, string> AttributeNames = new Dictionary<string, string>
    {
        ["fire"] = "火焰", ["ice"] = "寒气", ["wind"] = "自然", ["light"] = "光明", ["dark"] = "黑暗",
    };

    private static readonly IReadOnlyDictionary<string, string> JobNames = new Dictionary<string, string>
    {
        ["warrior"] = "战士", ["knight"] = "骑士", ["assassin"] = "盗贼",
        ["ranger"] = "射手", ["mage"] = "魔导士", ["manauser"] = "精灵师",
    };

    private readonly AntdUI.Input _searchInput = new();
    private readonly FlowLayoutPanel _attributeFilters = new();
    private readonly FlowLayoutPanel _jobFilters = new();
    private readonly WheelListBox _heroList = new();
    private readonly FlowLayoutPanel _editor = new();
    private readonly Label _heroTitle = new();
    private readonly Label _heroMeta = new();
    private readonly Label _sourceInfo = new();
    private readonly Label _comboInfo = new();
    private Panel _setSection = null!;
    private FlowLayoutPanel _setOptions = null!;
    private Panel _comboSection = null!;
    private readonly AntdUI.Button _updateButton = new();
    private readonly AntdUI.Button _cancelUpdateButton = new();
    private readonly AntdUI.Button _exportButton = new();
    private readonly AntdUI.Button _importButton = new();
    private readonly ProgressBar _updateProgress = new();
    private readonly AntdUI.Button _resetHeroButton = new();
    private readonly AntdUI.Button _resetAllButton = new();
    private readonly AntdUI.Checkbox _participatesInMatchingCheck = new();
    private readonly Dictionary<string, AntdUI.Checkbox> _attributeFilterChecks = new();
    private readonly Dictionary<string, AntdUI.Checkbox> _jobFilterChecks = new();
    private readonly Dictionary<string, AntdUI.Checkbox> _usefulStatChecks = new();
    private readonly Dictionary<string, AntdUI.Checkbox> _setChecks = new();
    private readonly Dictionary<string, AntdUI.Checkbox> _necklaceChecks = new();
    private readonly Dictionary<string, AntdUI.Checkbox> _ringChecks = new();
    private readonly Dictionary<string, AntdUI.Checkbox> _bootsChecks = new();
    private readonly List<(Panel Section, FlowLayoutPanel Options)> _checkboxSections = new();
    private readonly Dictionary<string, Image> _avatarCache = new();
    private List<HeroProfile> _visibleProfiles = new();
    private string? _selectedHeroCode;
    private bool _loadingEditor;
    private int _layoutDpi = 96;

    public event EventHandler? UpdateRequested;
    public event EventHandler? CancelUpdateRequested;

    public HeroConfigControl()
    {
        BackColor = Color.FromArgb(245, 246, 248);
        Dock = DockStyle.Fill;
        BuildInterface();
        RefreshData();
    }

    internal void ApplyInitialDpiScale(int dpi)
    {
        dpi = Math.Max(96, dpi);
        if (dpi == _layoutDpi)
            return;

        var factor = dpi / (float)_layoutDpi;
        _layoutDpi = dpi;
        SuspendLayout();
        Scale(new SizeF(factor, factor));
        _heroList.ItemHeight = ScalePixel(58);
        ResumeLayout(performLayout: true);
        ResizeEditorChildren();
    }

    internal void PrepareForDpiChange(int dpi)
    {
        _layoutDpi = Math.Max(96, dpi);
    }

    internal void CompleteDpiChange()
    {
        _heroList.ItemHeight = ScalePixel(58);
        ResizeEditorChildren();
    }

    private int ScalePixel(int logicalPixel)
        => (int)Math.Round(logicalPixel * _layoutDpi / 96D);

    private void BuildInterface()
    {
        var filters = new Panel
        {
            Dock = DockStyle.Top,
            Height = 126,
            Padding = new Padding(14, 10, 14, 6),
            BackColor = Color.White,
        };

        var firstRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 38,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
        };
        _searchInput.PlaceholderText = "搜索英雄名称";
        _searchInput.Size = new Size(220, 34);
        _searchInput.Margin = new Padding(0, 0, 10, 0);
        _searchInput.TextChanged += (_, _) => ApplyFilters();

        _sourceInfo.AutoSize = false;
        _sourceInfo.Size = new Size(220, 34);
        _sourceInfo.TextAlign = ContentAlignment.MiddleLeft;
        _sourceInfo.ForeColor = Color.FromArgb(95, 99, 104);

        ConfigureButton(_updateButton, "更新官方数据", 112);
        _updateButton.Click += (_, _) => UpdateRequested?.Invoke(this, EventArgs.Empty);
        ConfigureButton(_exportButton, "导出配置", 84);
        _exportButton.Click += (_, _) => ExportConfiguration();
        ConfigureButton(_importButton, "导入配置", 84);
        _importButton.Click += (_, _) => ImportConfiguration();
        ConfigureButton(_cancelUpdateButton, "取消更新", 88);
        _cancelUpdateButton.Visible = false;
        _cancelUpdateButton.Click += (_, _) => CancelUpdateRequested?.Invoke(this, EventArgs.Empty);
        _updateProgress.Size = new Size(150, 20);
        _updateProgress.Margin = new Padding(8, 7, 0, 0);
        _updateProgress.Visible = false;

        firstRow.Controls.Add(_searchInput);
        firstRow.Controls.Add(_sourceInfo);
        firstRow.Controls.Add(_updateButton);
        firstRow.Controls.Add(_exportButton);
        firstRow.Controls.Add(_importButton);
        firstRow.Controls.Add(_cancelUpdateButton);
        firstRow.Controls.Add(_updateProgress);

        ConfigureFilterPanel(_attributeFilters, new Point(14, 50), "属性", AttributeNames, _attributeFilterChecks);
        ConfigureFilterPanel(_jobFilters, new Point(14, 84), "职业", JobNames, _jobFilterChecks);
        filters.Controls.Add(_jobFilters);
        filters.Controls.Add(_attributeFilters);
        filters.Controls.Add(firstRow);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Size = new Size(1000, 500),
            SplitterDistance = 330,
            SplitterWidth = 1,
            IsSplitterFixed = true,
            BackColor = Color.White,
            Panel1MinSize = 260,
            Panel2MinSize = 420,
        };
        split.Panel1.Padding = new Padding(12);
        split.Panel1.BackColor = Color.White;
        split.Panel2.Padding = new Padding(12);
        split.Panel2.BackColor = Color.White;

        _heroList.Dock = DockStyle.Fill;
        _heroList.BorderStyle = BorderStyle.None;
        _heroList.DrawMode = DrawMode.OwnerDrawFixed;
        _heroList.ItemHeight = 58;
        _heroList.Font = new Font("Microsoft YaHei UI", 10F);
        _heroList.DrawItem += DrawHeroItem;
        _heroList.SelectedIndexChanged += (_, _) => SelectCurrentHero();
        split.Panel1.Controls.Add(_heroList);

        _editor.Dock = DockStyle.Fill;
        _editor.AutoScroll = true;
        _editor.FlowDirection = FlowDirection.TopDown;
        _editor.WrapContents = false;
        _editor.Padding = new Padding(4, 4, 4, 28);
        _editor.Resize += (_, _) => ResizeEditorChildren();

        var header = new Panel { Height = 76, BackColor = Color.White };
        _heroTitle.Font = new Font("Microsoft YaHei UI", 17F, FontStyle.Bold);
        _heroTitle.Location = new Point(4, 4);
        _heroTitle.AutoSize = true;
        _heroMeta.Location = new Point(6, 42);
        _heroMeta.AutoSize = true;
        _heroMeta.ForeColor = Color.FromArgb(95, 99, 104);
        _participatesInMatchingCheck.Text = "参与装备匹配";
        _participatesInMatchingCheck.Size = new Size(132, 34);
        _participatesInMatchingCheck.CheckedChanged += (_, _) => SaveEditor();
        ConfigureButton(_resetHeroButton, "恢复当前英雄", 112);
        _resetHeroButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _resetHeroButton.Click += (_, _) => ResetSelectedHero();
        ConfigureButton(_resetAllButton, "恢复全部英雄", 112);
        _resetAllButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _resetAllButton.Click += (_, _) => ResetAllHeroes();
        header.Controls.Add(_heroTitle);
        header.Controls.Add(_heroMeta);
        header.Controls.Add(_participatesInMatchingCheck);
        header.Controls.Add(_resetHeroButton);
        header.Controls.Add(_resetAllButton);
        _editor.Controls.Add(header);

        _editor.Controls.Add(CreateCheckboxSection("有效属性", EquipmentRules.UsefulStats, _usefulStatChecks));
        _editor.Controls.Add(CreateSetSection());
        _editor.Controls.Add(CreateCheckboxSection("项链主属性", EquipmentRules.NecklaceMainStats, _necklaceChecks));
        _editor.Controls.Add(CreateCheckboxSection("戒指主属性", EquipmentRules.RingMainStats, _ringChecks));
        _editor.Controls.Add(CreateCheckboxSection("鞋子主属性", EquipmentRules.BootsMainStats, _bootsChecks));

        _comboSection = new Panel { Height = 106, Padding = new Padding(10), BackColor = Color.FromArgb(248, 249, 250) };
        var comboTitle = new Label
        {
            Text = "官方主流组合（只读）",
            Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 28,
        };
        _comboInfo.Dock = DockStyle.Fill;
        _comboInfo.ForeColor = Color.FromArgb(70, 72, 76);
        _comboInfo.AutoEllipsis = false;
        _comboSection.Controls.Add(_comboInfo);
        _comboSection.Controls.Add(comboTitle);
        _editor.Controls.Add(_comboSection);
        _editor.Controls.Add(new Panel
        {
            Height = 32,
            BackColor = Color.White,
            Margin = Padding.Empty,
            AccessibleName = "详情底部留白",
        });
        split.Panel2.Controls.Add(CreateClippedScrollHost(_editor));

        Controls.Add(split);
        Controls.Add(filters);
    }

    private static void ConfigureButton(AntdUI.Button button, string text, int width)
    {
        button.Text = text;
        button.Size = new Size(width, 34);
        button.Radius = 6;
        button.Margin = new Padding(4, 0, 0, 0);
    }

    private static Panel CreateClippedScrollHost(Control scrollContent)
    {
        var host = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        void LayoutScrollContent()
        {
            // 让原生滚动条落在宿主裁剪区域之外。控件本身仍保留完整滚动机制，
            // 无需在滚动过程中反复调用 ShowScrollBar，避免闪烁。
            scrollContent.Bounds = new Rectangle(
                0, 0,
                host.ClientSize.Width + SystemInformation.VerticalScrollBarWidth + 2,
                host.ClientSize.Height + SystemInformation.HorizontalScrollBarHeight + 2);
        }

        scrollContent.Dock = DockStyle.None;
        host.Controls.Add(scrollContent);
        host.Resize += (_, _) => LayoutScrollContent();
        LayoutScrollContent();
        return host;
    }

    private void ConfigureFilterPanel(
        FlowLayoutPanel panel,
        Point location,
        string title,
        IReadOnlyDictionary<string, string> values,
        IDictionary<string, AntdUI.Checkbox> target)
    {
        panel.Location = location;
        panel.Size = new Size(930, 30);
        panel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        panel.WrapContents = false;
        panel.Controls.Add(new Label
        {
            Text = title,
            Size = new Size(54, 30),
            Margin = Padding.Empty,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(95, 99, 104),
        });
        foreach (var (code, name) in values)
        {
            var check = new AntdUI.Checkbox
            {
                Text = name,
                AutoSize = false,
                Size = new Size(name.Length >= 3 ? 104 : 86, 30),
                Margin = Padding.Empty,
            };
            check.CheckedChanged += (_, _) => ApplyFilters();
            target[code] = check;
            panel.Controls.Add(check);
        }
    }

    private Panel CreateCheckboxSection(string title, IEnumerable<string> values, IDictionary<string, AntdUI.Checkbox> target)
    {
        var panel = new Panel { Height = 82, Padding = new Padding(10), BackColor = Color.FromArgb(248, 249, 250) };
        var label = new Label
        {
            Text = title,
            Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 28,
        };
        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(0, 2, 0, 0) };
        foreach (var value in values)
        {
            var check = new AntdUI.Checkbox { Text = value, AutoSize = true, Margin = new Padding(0, 0, 14, 4) };
            check.CheckedChanged += (_, _) => SaveEditor();
            target[value] = check;
            options.Controls.Add(check);
        }
        _checkboxSections.Add((panel, options));
        panel.SizeChanged += (_, _) => ResizeCheckboxSection(panel, options);
        panel.Controls.Add(options);
        panel.Controls.Add(label);
        return panel;
    }

    private Panel CreateSetSection()
    {
        _setSection = new Panel { Height = 220, Padding = new Padding(10), BackColor = Color.FromArgb(248, 249, 250) };
        var label = new Label
        {
            Text = "可用套装",
            Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 28,
        };
        _setOptions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = false, WrapContents = true };
        foreach (var (code, name) in HeroDatabase.Instance.SetNames.OrderBy(kv => kv.Value))
        {
            var item = new Panel { Size = new Size(126, 38), Margin = new Padding(0, 0, 6, 6), BackColor = Color.White };
            var icon = new PictureBox
            {
                Location = new Point(4, 5), Size = new Size(28, 28), SizeMode = PictureBoxSizeMode.Zoom,
                Image = LoadImageNoLock(HeroDatabase.GetSetIconPath(code)),
            };
            var check = new AntdUI.Checkbox
            {
                Text = name.Replace("套装", string.Empty),
                Location = new Point(35, 5), Size = new Size(88, 28),
            };
            check.CheckedChanged += (_, _) => SaveEditor();
            _setChecks[code] = check;
            item.Controls.Add(icon);
            item.Controls.Add(check);
            _setOptions.Controls.Add(item);
        }
        _setSection.Controls.Add(_setOptions);
        _setSection.Controls.Add(label);
        return _setSection;
    }

    public void RefreshData(string? preserveHeroCode = null)
    {
        preserveHeroCode ??= _selectedHeroCode;
        var db = HeroDatabase.Instance;
        _sourceInfo.Text = db.IsLoaded
            ? $"{(db.UsesUserData ? "用户更新" : "内置数据")} · {db.SeasonCode} · {db.UpdatedAt}"
            : "英雄数据未加载";
        ApplyFilters(preserveHeroCode);
    }

    private void ApplyFilters(string? preserveHeroCode = null)
    {
        preserveHeroCode ??= _selectedHeroCode;
        var query = _searchInput.Text.Trim();
        var attributes = _attributeFilterChecks.Where(x => x.Value.Checked).Select(x => x.Key).ToHashSet();
        var jobs = _jobFilterChecks.Where(x => x.Value.Checked).Select(x => x.Key).ToHashSet();
        _visibleProfiles = HeroDatabase.Instance.Profiles
            .Where(h => string.IsNullOrEmpty(query) || h.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .Where(h => attributes.Count == 0 || attributes.Contains(h.Attribute))
            .Where(h => jobs.Count == 0 || jobs.Contains(h.Job))
            .Select(h => h.Clone())
            .ToList();

        _heroList.BeginUpdate();
        _heroList.Items.Clear();
        foreach (var profile in _visibleProfiles)
            _heroList.Items.Add(profile);
        _heroList.EndUpdate();

        var index = preserveHeroCode == null ? -1 : _visibleProfiles.FindIndex(h => h.Code == preserveHeroCode);
        if (index < 0 && _visibleProfiles.Count > 0)
            index = 0;
        _heroList.SelectedIndex = index;
        if (index < 0)
            ClearEditor();
    }

    private void SelectCurrentHero()
    {
        if (_heroList.SelectedItem is not HeroProfile profile)
        {
            ClearEditor();
            return;
        }

        _selectedHeroCode = profile.Code;
        _loadingEditor = true;
        try
        {
            _heroTitle.Text = profile.Name;
            _heroMeta.Text = $"{GetName(AttributeNames, profile.Attribute)} · {GetName(JobNames, profile.Job)} · {profile.Grade}星"
                + (profile.HasGradeData ? " · 前排分段默认数据" : " · 暂无默认战绩数据")
                + (profile.IsExcluded ? " · 已屏蔽匹配" : string.Empty);
            _participatesInMatchingCheck.Checked = !profile.IsExcluded;
            SetChecks(_usefulStatChecks, profile.UsefulStats);
            SetChecks(_setChecks, profile.AllowedSets);
            SetChecks(_necklaceChecks, profile.NecklaceMainStats);
            SetChecks(_ringChecks, profile.RingMainStats);
            SetChecks(_bootsChecks, profile.BootsMainStats);
            _comboInfo.Text = profile.SetCombos.Count == 0
                ? "暂无官方主流套装组合"
                : string.Join(Environment.NewLine, profile.SetCombos.Select(combo =>
                    $"{string.Join(" + ", combo.Sets.Select(code => HeroDatabase.Instance.SetNames.GetValueOrDefault(code, code)))}"
                    + $"　使用率 {combo.Rate:0.##}% · 胜率 {combo.WinRate:0.##}%"));
            ResizeComboSection();
        }
        finally
        {
            _loadingEditor = false;
        }
    }

    private void SaveEditor()
    {
        if (_loadingEditor || _selectedHeroCode == null)
            return;
        var profile = HeroDatabase.Instance.GetProfile(_selectedHeroCode);
        if (profile == null)
            return;
        profile.IsExcluded = !_participatesInMatchingCheck.Checked;
        profile.UsefulStats = CheckedKeys(_usefulStatChecks);
        profile.AllowedSets = CheckedKeys(_setChecks);
        profile.NecklaceMainStats = CheckedKeys(_necklaceChecks);
        profile.RingMainStats = CheckedKeys(_ringChecks);
        profile.BootsMainStats = CheckedKeys(_bootsChecks);
        HeroDatabase.Instance.SaveOverride(profile);

        // 列表项是筛选时创建的快照。保存后同步为数据库中的规范化结果，
        // 否则切换英雄再返回时会从旧快照恢复编辑前的勾选状态。
        var saved = HeroDatabase.Instance.GetProfile(profile.Code);
        var visible = _visibleProfiles.FirstOrDefault(item => item.Code == profile.Code);
        if (saved != null && visible != null)
        {
            visible.IsExcluded = saved.IsExcluded;
            visible.UsefulStats = saved.UsefulStats.ToList();
            visible.AllowedSets = saved.AllowedSets.ToList();
            visible.NecklaceMainStats = saved.NecklaceMainStats.ToList();
            visible.RingMainStats = saved.RingMainStats.ToList();
            visible.BootsMainStats = saved.BootsMainStats.ToList();
            _heroList.Invalidate();
            _heroMeta.Text = _heroMeta.Text.Replace(" · 已屏蔽匹配", string.Empty)
                + (saved.IsExcluded ? " · 已屏蔽匹配" : string.Empty);
        }
    }

    private void ExportConfiguration()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "导出英雄装备需求",
            Filter = "英雄配置存档 (*.json)|*.json",
            FileName = $"铁柱工具箱-英雄配置-{DateTime.Now:yyyyMMdd}.json",
            AddExtension = true,
            DefaultExt = "json",
        };
        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        try
        {
            HeroDatabase.Instance.ExportOverrides(dialog.FileName);
            MessageBox.Show("英雄装备需求和屏蔽状态已导出。", "导出完成",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出失败：{ex.Message}", "导出配置",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ImportConfiguration()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "导入英雄装备需求",
            Filter = "英雄配置存档 (*.json)|*.json|所有文件 (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog() != DialogResult.OK)
            return;
        if (MessageBox.Show("导入会替换当前全部英雄配置，是否继续？", "导入配置",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            return;

        try
        {
            HeroDatabase.Instance.ImportOverrides(dialog.FileName);
            RefreshData();
            MessageBox.Show("英雄配置已恢复，装备需求和屏蔽状态立即生效。", "导入完成",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导入失败：{ex.Message}", "导入配置",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ResetSelectedHero()
    {
        if (_selectedHeroCode == null)
            return;
        var code = _selectedHeroCode;
        HeroDatabase.Instance.ResetOverride(code);
        RefreshData(code);
    }

    private void ResetAllHeroes()
    {
        if (MessageBox.Show("确定恢复全部英雄的默认配置吗？", "恢复默认",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            return;
        HeroDatabase.Instance.ResetAllOverrides();
        RefreshData();
    }

    public void BeginUpdate()
    {
        _updateButton.Enabled = false;
        _exportButton.Enabled = false;
        _importButton.Enabled = false;
        _cancelUpdateButton.Visible = true;
        _updateProgress.Visible = true;
        _updateProgress.Style = ProgressBarStyle.Marquee;
        _sourceInfo.Text = "正在准备官方数据更新...";
    }

    public void ReportUpdate(HeroDataUpdateProgress value)
    {
        _sourceInfo.Text = value.Message;
        if (value.Total > 0)
        {
            _updateProgress.Style = ProgressBarStyle.Continuous;
            _updateProgress.Maximum = Math.Max(1, value.Total);
            _updateProgress.Value = Math.Clamp(value.Current, 0, _updateProgress.Maximum);
        }
    }

    public void EndUpdate()
    {
        _updateButton.Enabled = true;
        _exportButton.Enabled = true;
        _importButton.Enabled = true;
        _cancelUpdateButton.Visible = false;
        _updateProgress.Visible = false;
        RefreshData();
    }

    private void DrawHeroItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _visibleProfiles.Count)
            return;
        var hero = _visibleProfiles[e.Index];
        e.DrawBackground();
        var selected = (e.State & DrawItemState.Selected) != 0;
        var textColor = selected ? Color.White : Color.FromArgb(32, 33, 36);
        var secondary = selected ? Color.FromArgb(235, 241, 255) : Color.FromArgb(95, 99, 104);
        var avatar = GetAvatar(hero.Code);
        if (avatar != null)
            e.Graphics.DrawImage(avatar, new Rectangle(e.Bounds.Left + 6, e.Bounds.Top + 7, 44, 44));
        else
            e.Graphics.FillEllipse(Brushes.LightGray, e.Bounds.Left + 10, e.Bounds.Top + 11, 36, 36);

        using var nameBrush = new SolidBrush(textColor);
        using var metaBrush = new SolidBrush(secondary);
        using var nameFont = new Font(Font, FontStyle.Bold);
        e.Graphics.DrawString(hero.Name, nameFont, nameBrush, e.Bounds.Left + 58, e.Bounds.Top + 9);
        var meta = $"{GetName(AttributeNames, hero.Attribute)} · {GetName(JobNames, hero.Job)}"
            + (hero.HasGradeData ? string.Empty : " · 无战绩默认")
            + (hero.IsExcluded ? " · 已屏蔽" : string.Empty);
        e.Graphics.DrawString(meta, Font, metaBrush, e.Bounds.Left + 58, e.Bounds.Top + 32);
    }

    private Image? GetAvatar(string code)
    {
        if (_avatarCache.TryGetValue(code, out var cached))
            return cached;
        var image = LoadImageNoLock(HeroDatabase.GetAvatarPath(code));
        if (image != null)
            _avatarCache[code] = image;
        return image;
    }

    private static Image? LoadImageNoLock(string? path)
    {
        if (path == null || !File.Exists(path))
            return null;
        try
        {
            var bytes = File.ReadAllBytes(path);
            using var stream = new MemoryStream(bytes);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    private void ResizeEditorChildren()
    {
        // 为原生纵向滚动区域和控件边距预留空间，避免产生横向滚动条。
        var visibleWidth = _editor.Parent?.ClientSize.Width ?? _editor.ClientSize.Width;
        var width = Math.Max(ScalePixel(380), visibleWidth
            - _editor.Padding.Horizontal
            - ScalePixel(8));
        foreach (Control child in _editor.Controls)
            child.Width = width;
        ResizeCheckboxSections();
        ResizeSetSection();
        ResizeComboSection();
        if (_editor.Controls.Count > 0)
        {
            var header = _editor.Controls[0];
            _resetAllButton.Location = new Point(
                header.Width - _resetAllButton.Width - ScalePixel(4), ScalePixel(4));
            _resetHeroButton.Location = new Point(
                _resetAllButton.Left - _resetHeroButton.Width - ScalePixel(8), ScalePixel(4));
            _participatesInMatchingCheck.Location = new Point(
                Math.Max(ScalePixel(210), _resetHeroButton.Left), ScalePixel(38));
        }
    }

    private void ResizeCheckboxSections()
    {
        foreach (var (section, options) in _checkboxSections)
            ResizeCheckboxSection(section, options);
    }

    private void ResizeCheckboxSection(Panel section, FlowLayoutPanel options)
    {
        var titleHeight = ScalePixel(28);
        var minimumOptionsHeight = ScalePixel(30);
        var availableWidth = Math.Max(1, section.ClientSize.Width - section.Padding.Horizontal);
        var rowWidth = options.Padding.Left;
        var rowHeight = 0;
        var contentHeight = options.Padding.Top;

        foreach (Control option in options.Controls)
        {
            var preferredSize = option.GetPreferredSize(Size.Empty);
            var itemWidth = preferredSize.Width + option.Margin.Horizontal;
            var itemHeight = preferredSize.Height + option.Margin.Vertical;
            if (rowWidth > options.Padding.Left && rowWidth + itemWidth + options.Padding.Right > availableWidth)
            {
                contentHeight += rowHeight;
                rowWidth = options.Padding.Left;
                rowHeight = 0;
            }

            rowWidth += itemWidth;
            rowHeight = Math.Max(rowHeight, itemHeight);
        }

        contentHeight += rowHeight + options.Padding.Bottom;
        var requiredHeight = section.Padding.Vertical + titleHeight
                             + Math.Max(minimumOptionsHeight, contentHeight);
        if (section.Height != requiredHeight)
            section.Height = requiredHeight;
    }

    private void ResizeSetSection()
    {
        if (_setSection == null || _setOptions == null)
            return;
        var itemWidth = ScalePixel(132);
        var itemHeight = ScalePixel(44);
        var availableWidth = Math.Max(itemWidth, _setSection.Width - _setSection.Padding.Horizontal);
        var columns = Math.Max(1, availableWidth / itemWidth);
        var rows = (int)Math.Ceiling(_setOptions.Controls.Count / (double)columns);
        _setSection.Height = ScalePixel(28) + _setSection.Padding.Vertical + rows * itemHeight + ScalePixel(4);
    }

    private void ResizeComboSection()
    {
        if (_comboSection == null || _comboInfo == null)
            return;
        var textWidth = Math.Max(ScalePixel(200), _comboSection.Width - _comboSection.Padding.Horizontal);
        var textHeight = _comboInfo.GetPreferredSize(new Size(textWidth, int.MaxValue)).Height;
        _comboSection.Height = Math.Max(
            ScalePixel(106),
            ScalePixel(28) + _comboSection.Padding.Vertical + textHeight + ScalePixel(6));
    }

    private void ClearEditor()
    {
        _selectedHeroCode = null;
        _heroTitle.Text = "未选择英雄";
        _heroMeta.Text = string.Empty;
        _participatesInMatchingCheck.Checked = false;
        _comboInfo.Text = string.Empty;
        _loadingEditor = true;
        foreach (var dictionary in new[] { _usefulStatChecks, _setChecks, _necklaceChecks, _ringChecks, _bootsChecks })
            foreach (var check in dictionary.Values)
                check.Checked = false;
        _loadingEditor = false;
    }

    private static void SetChecks(IReadOnlyDictionary<string, AntdUI.Checkbox> controls, IEnumerable<string> selected)
    {
        var selectedSet = selected.ToHashSet(StringComparer.Ordinal);
        foreach (var (key, check) in controls)
            check.Checked = selectedSet.Contains(key);
    }

    private static List<string> CheckedKeys(IReadOnlyDictionary<string, AntdUI.Checkbox> controls)
        => controls.Where(item => item.Value.Checked).Select(item => item.Key).ToList();

    private static string GetName(IReadOnlyDictionary<string, string> names, string code)
        => names.TryGetValue(code, out var name) ? name : string.IsNullOrWhiteSpace(code) ? "未知" : code;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var image in _avatarCache.Values)
                image.Dispose();
            _avatarCache.Clear();
        }
        base.Dispose(disposing);
    }
}

internal sealed class WheelListBox : ListBox
{
    private const int WsVScroll = 0x00200000;
    private int _wheelDelta;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.Style &= ~WsVScroll;
            return parameters;
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        _wheelDelta += e.Delta;
        var detents = _wheelDelta / SystemInformation.MouseWheelScrollDelta;
        if (detents != 0 && Items.Count > 0)
        {
            _wheelDelta -= detents * SystemInformation.MouseWheelScrollDelta;
            var lines = SystemInformation.MouseWheelScrollLines;
            if (lines < 0)
                lines = Math.Max(1, ClientSize.Height / Math.Max(1, ItemHeight));
            TopIndex = Math.Clamp(TopIndex - detents * Math.Max(1, lines), 0, Items.Count - 1);
        }
        base.OnMouseWheel(e);
    }
}
