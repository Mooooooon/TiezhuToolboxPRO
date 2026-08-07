using System.Globalization;
using TiezhuToolbox.Modules.Account;
using TiezhuToolbox.Modules.Optimizer;

namespace TiezhuToolbox;

public sealed class OptimizerControl : UserControl
{
    private static readonly (GearStatType Type, string Name)[] PanelStats =
    [
        (GearStatType.Attack, "攻击"), (GearStatType.Health, "生命"), (GearStatType.Defense, "防御"),
        (GearStatType.Speed, "速度"), (GearStatType.CriticalHitChancePercent, "暴击"),
        (GearStatType.CriticalHitDamagePercent, "暴伤"), (GearStatType.EffectivenessPercent, "命中"),
        (GearStatType.EffectResistancePercent, "抗性"),
    ];

    private readonly AccountWorkspace _workspace;
    private readonly BuildOptimizer _optimizer;
    private readonly OptimizerPresetRepository _presetRepository = new();
    private readonly OptimizerPresetDocument _presetDocument;
    private readonly AntdUI.Select _hero = CreateSelect(210);
    private readonly AntdUI.Select _occupation = CreateSelect(170);
    private readonly AntdUI.Select[] _sets = [CreateSelect(120), CreateSelect(120), CreateSelect(120)];
    private readonly Dictionary<GearStatType, RangeInputs> _ranges = [];
    private readonly CheckedListBox _necklace = CreateMainList();
    private readonly CheckedListBox _ring = CreateMainList();
    private readonly CheckedListBox _boots = CreateMainList();
    private readonly AntdUI.Button _search = new() { Text = "开始精确搜索", Width = 124, Height = 34, Type = AntdUI.TTypeMini.Primary };
    private readonly AntdUI.Button _cancel = new() { Text = "取消", Width = 76, Height = 34, Enabled = false };
    private readonly AntdUI.Input _presetName = new() { Width = 120, Height = 34, PlaceholderText = "预设名称" };
    private readonly AntdUI.Select _preset = CreateSelect(130);
    private readonly AntdUI.Button _savePreset = new() { Text = "保存预设", Width = 82, Height = 34 };
    private readonly AntdUI.Button _loadPreset = new() { Text = "载入", Width = 62, Height = 34 };
    private readonly AntdUI.Button _deletePreset = new() { Text = "删除", Width = 62, Height = 34 };
    private readonly Label _progress = new() { AutoSize = false, Width = 500, Height = 34, TextAlign = ContentAlignment.MiddleLeft };
    private readonly AntdUI.Table _results = new() { Dock = DockStyle.Fill, VirtualMode = true };
    private List<AccountHero> _heroOptions = [];
    private CancellationTokenSource? _cancellation;

    public OptimizerControl(AccountWorkspace workspace)
    {
        _workspace = workspace;
        _optimizer = new BuildOptimizer(workspace.Calculator);
        _presetDocument = _presetRepository.Load();
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(245, 246, 248);
        Padding = new Padding(16);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, FixedPanel = FixedPanel.Panel1,
            SplitterDistance = 390, BackColor = BackColor,
        };
        split.Panel1.AutoScroll = true;
        split.Panel1.BackColor = Color.White;
        split.Panel2.BackColor = Color.White;
        var config = BuildConfigPanel();
        split.Panel1.Controls.Add(config);
        BuildResultColumns();
        split.Panel2.Controls.Add(_results);
        Controls.Add(split);

        _occupation.Items.AddRange(new object[] { "全部装备（标记冲突）", "保护高优先级英雄", "仅未穿戴和目标装备" });
        _occupation.SelectedIndex = 0;
        _search.Click += async (_, _) => await StartSearchAsync();
        _cancel.Click += (_, _) => _cancellation?.Cancel();
        _savePreset.Click += (_, _) => SavePreset();
        _loadPreset.Click += (_, _) => LoadPreset();
        _deletePreset.Click += (_, _) => DeletePreset();
        _workspace.Changed += WorkspaceChanged;
        Disposed += (_, _) => { _workspace.Changed -= WorkspaceChanged; _cancellation?.Cancel(); };
        RefreshOptions();
    }

    public int ResultCount => (_results.DataSource as Array)?.Length ?? 0;

    private Control BuildConfigPanel()
    {
        var host = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var header = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8, 7, 8, 4), WrapContents = false };
        header.Controls.AddRange([LabelFor("目标英雄", 72), _hero, LabelFor("装备占用", 72), _occupation,
            LabelFor("必需套装", 72), _sets[0], _sets[1], _sets[2]]);

        var statGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Left, Width = 580, Padding = new Padding(8), ColumnCount = 4, RowCount = 9,
        };
        statGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        statGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        statGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        statGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        statGrid.Controls.Add(HeaderLabel("属性"), 0, 0);
        statGrid.Controls.Add(HeaderLabel("最小值（空=不限）"), 1, 0);
        statGrid.Controls.Add(HeaderLabel("最大值（空=不限）"), 2, 0);
        statGrid.Controls.Add(HeaderLabel("权重"), 3, 0);
        for (var index = 0; index < PanelStats.Length; index++)
        {
            var pair = PanelStats[index];
            var minimum = new AntdUI.Input { Width = 135, Height = 31, PlaceholderText = "不限" };
            var maximum = new AntdUI.Input { Width = 135, Height = 31, PlaceholderText = "不限" };
            var weight = new AntdUI.InputNumber { Width = 110, Height = 31, Minimum = 0, Maximum = 100, Value = 1 };
            _ranges[pair.Type] = new RangeInputs(minimum, maximum, weight);
            statGrid.Controls.Add(LabelFor(pair.Name, 70), 0, index + 1);
            statGrid.Controls.Add(minimum, 1, index + 1);
            statGrid.Controls.Add(maximum, 2, index + 1);
            statGrid.Controls.Add(weight, 3, index + 1);
        }

        var mainPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        var mainTitle = new Label { Text = "右三主属性（不勾选表示不限，可多选）", Dock = DockStyle.Top, Height = 28, Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold) };
        var mainLists = new TableLayoutPanel { Dock = DockStyle.Top, Height = 118, ColumnCount = 3, RowCount = 2 };
        for (var index = 0; index < 3; index++)
            mainLists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        mainLists.Controls.Add(HeaderLabel("项链"), 0, 0);
        mainLists.Controls.Add(HeaderLabel("戒指"), 1, 0);
        mainLists.Controls.Add(HeaderLabel("鞋子"), 2, 0);
        mainLists.Controls.Add(_necklace, 0, 1);
        mainLists.Controls.Add(_ring, 1, 1);
        mainLists.Controls.Add(_boots, 2, 1);
        PopulateMainLists();
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(0, 7, 0, 4), WrapContents = false };
        actions.Controls.AddRange([_search, _cancel, _presetName, _savePreset, _preset, _loadPreset, _deletePreset, _progress]);
        var todo = new Label
        {
            Text = "V1：基础八维精确匹配。伤害、肉度/EHP、技能伤害、神器/专属技能效果将在后续版本实现。",
            Dock = DockStyle.Top, Height = 42, ForeColor = Color.FromArgb(95, 99, 104),
        };
        mainPanel.Controls.Add(todo);
        mainPanel.Controls.Add(actions);
        mainPanel.Controls.Add(mainLists);
        mainPanel.Controls.Add(mainTitle);

        var body = new Panel { Dock = DockStyle.Fill };
        body.Controls.Add(mainPanel);
        body.Controls.Add(statGrid);
        host.Controls.Add(body);
        host.Controls.Add(header);
        return host;
    }

    private void PopulateMainLists()
    {
        AddMainOptions(_necklace, GearStatType.Attack, GearStatType.Health, GearStatType.Defense,
            GearStatType.AttackPercent, GearStatType.HealthPercent, GearStatType.DefensePercent,
            GearStatType.CriticalHitChancePercent, GearStatType.CriticalHitDamagePercent);
        AddMainOptions(_ring, GearStatType.Attack, GearStatType.Health, GearStatType.Defense,
            GearStatType.AttackPercent, GearStatType.HealthPercent, GearStatType.DefensePercent,
            GearStatType.EffectivenessPercent, GearStatType.EffectResistancePercent);
        AddMainOptions(_boots, GearStatType.Attack, GearStatType.Health, GearStatType.Defense,
            GearStatType.AttackPercent, GearStatType.HealthPercent, GearStatType.DefensePercent, GearStatType.Speed);
    }

    private static void AddMainOptions(CheckedListBox target, params GearStatType[] values)
    {
        foreach (var value in values)
            target.Items.Add(new MainStatOption(value, GearBrowserControl.DisplayStat(value)));
    }

    private void BuildResultColumns()
    {
        AddResultColumn("Score", "加权分", "78");
        AddResultColumn("Sets", "套装", "150");
        AddResultColumn("Attack", "攻击", "75");
        AddResultColumn("Health", "生命", "80");
        AddResultColumn("Defense", "防御", "70");
        AddResultColumn("Speed", "速度", "62");
        AddResultColumn("CriticalChance", "暴击", "62");
        AddResultColumn("CriticalDamage", "暴伤", "62");
        AddResultColumn("Effectiveness", "命中", "62");
        AddResultColumn("Resistance", "抗性", "62");
        AddResultColumn("Conflicts", "冲突", "58");
        AddResultColumn("Weapon", "武器", "110");
        AddResultColumn("Helmet", "头盔", "110");
        AddResultColumn("Armor", "铠甲", "110");
        AddResultColumn("Necklace", "项链", "110");
        AddResultColumn("Ring", "戒指", "110");
        AddResultColumn("Boots", "鞋子", "110");
    }

    private void AddResultColumn(string key, string title, string width)
    {
        var column = new AntdUI.Column(key, title);
        column.SetWidth(width);
        column.SetSortOrder(true);
        _results.Columns.Add(column);
    }

    private void RefreshOptions()
    {
        var snapshot = _workspace.Snapshot;
        _heroOptions = snapshot?.Heroes.OrderBy(value => _workspace.GetPreference(value.Id)?.Priority ?? int.MaxValue).ToList() ?? [];
        _hero.Items.Clear();
        foreach (var value in _heroOptions)
            _hero.Items.Add($"{_workspace.GetPreference(value.Id)?.Priority}. {value.Name}");
        if (_hero.Items.Count > 0)
            _hero.SelectedIndex = 0;

        var availableSets = snapshot?.Items.Select(value => value.Set).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(EquipmentSetCatalog.DisplayName).ToArray() ?? [];
        foreach (var select in _sets)
        {
            select.Items.Clear();
            select.Items.Add("不限");
            foreach (var value in availableSets)
                select.Items.Add(value);
            select.SelectedIndex = 0;
        }
        _progress.Text = snapshot == null ? "请先扫描或导入账号数据" : $"可搜索 {snapshot.Items.Count} 件装备";
        RefreshPresetOptions();
    }

    private void RefreshPresetOptions(string? selected = null)
    {
        selected ??= SelectedText(_preset);
        _preset.Items.Clear();
        foreach (var value in _presetDocument.Presets.OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase))
            _preset.Items.Add(value.Name);
        _preset.SelectedIndex = FindItemIndex(_preset, selected);
    }

    private void SavePreset()
    {
        if (_workspace.Snapshot == null || _hero.SelectedIndex < 0 || _hero.SelectedIndex >= _heroOptions.Count)
            return;
        var name = _presetName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            _progress.Text = "请先填写预设名称";
            return;
        }
        try
        {
            var request = BuildRequest(_heroOptions[_hero.SelectedIndex]);
            var preset = new OptimizerPreset
            {
                Name = name,
                HeroId = request.Hero.Id,
                OccupationMode = request.OccupationMode,
                StatRanges = request.StatRanges,
                Weights = request.Weights,
                RequiredSets = request.RequiredSets,
                AllowedMainStats = request.AllowedMainStats,
            };
            var oldIndex = _presetDocument.Presets.FindIndex(value => value.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (oldIndex >= 0)
                _presetDocument.Presets[oldIndex] = preset;
            else
                _presetDocument.Presets.Add(preset);
            _presetRepository.Save(_presetDocument);
            RefreshPresetOptions(name);
            _progress.Text = $"已保存配装预设：{name}";
        }
        catch (Exception ex)
        {
            _progress.Text = "保存预设失败：" + ex.Message;
        }
    }

    private void LoadPreset()
    {
        var name = SelectedText(_preset);
        var preset = _presetDocument.Presets.FirstOrDefault(value => value.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (preset == null)
            return;
        var heroIndex = _heroOptions.FindIndex(value => value.Id == preset.HeroId);
        if (heroIndex >= 0)
            _hero.SelectedIndex = heroIndex;
        _occupation.SelectedIndex = (int)preset.OccupationMode;
        foreach (var (type, inputs) in _ranges)
        {
            preset.StatRanges.TryGetValue(type, out var range);
            inputs.Minimum.Text = range.Minimum?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
            inputs.Maximum.Text = range.Maximum?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
            inputs.Weight.Value = (decimal)WeightOf(preset.Weights, type);
        }
        for (var index = 0; index < _sets.Length; index++)
            _sets[index].SelectedIndex = index < preset.RequiredSets.Count
                ? Math.Max(0, FindItemIndex(_sets[index], preset.RequiredSets[index]))
                : 0;
        RestoreChecked(_necklace, preset.AllowedMainStats.GetValueOrDefault(GearSlot.Necklace));
        RestoreChecked(_ring, preset.AllowedMainStats.GetValueOrDefault(GearSlot.Ring));
        RestoreChecked(_boots, preset.AllowedMainStats.GetValueOrDefault(GearSlot.Boots));
        _presetName.Text = preset.Name;
        _progress.Text = $"已载入配装预设：{preset.Name}";
    }

    private void DeletePreset()
    {
        var name = SelectedText(_preset);
        var removed = _presetDocument.Presets.RemoveAll(value => value.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (removed == 0)
            return;
        _presetRepository.Save(_presetDocument);
        RefreshPresetOptions(string.Empty);
        _progress.Text = $"已删除配装预设：{name}";
    }

    private async Task StartSearchAsync()
    {
        if (_workspace.Snapshot == null || _hero.SelectedIndex < 0 || _hero.SelectedIndex >= _heroOptions.Count)
            return;
        try
        {
            var request = BuildRequest(_heroOptions[_hero.SelectedIndex]);
            _cancellation?.Dispose();
            _cancellation = new CancellationTokenSource();
            _search.Enabled = false;
            _cancel.Enabled = true;
            _results.DataSource = Array.Empty<ResultRow>();
            var reporter = new Progress<OptimizationProgress>(value =>
                _progress.Text = $"已检查 {value.CheckedCombinations:N0} · 剪枝 {value.PrunedBranches:N0} · {value.Elapsed.TotalSeconds:0.0}s");
            var result = await _optimizer.SearchAsync(request, reporter, _cancellation.Token);
            _results.DataSource = result.Results.Select(CreateResultRow).ToArray();
            _progress.Text = $"{(result.IsComplete ? "完成" : "已取消，结果不完整")}：{result.Results.Count} 组 · " +
                             $"检查 {result.Progress.CheckedCombinations:N0} · 剪枝 {result.Progress.PrunedBranches:N0} · " +
                             $"{result.Progress.Elapsed.TotalSeconds:0.00}s";
        }
        catch (Exception ex)
        {
            _progress.Text = "搜索失败：" + ex.Message;
        }
        finally
        {
            _search.Enabled = true;
            _cancel.Enabled = false;
        }
    }

    private OptimizationRequest BuildRequest(AccountHero hero)
    {
        var ranges = new Dictionary<GearStatType, StatRange>();
        foreach (var (type, inputs) in _ranges)
        {
            var minimum = ParseOptional(inputs.Minimum.Text);
            var maximum = ParseOptional(inputs.Maximum.Text);
            if (minimum.HasValue || maximum.HasValue)
                ranges[type] = new StatRange(minimum, maximum);
        }
        var requiredSets = _sets.Select(SelectedText).Where(value => value is not "" and not "不限").ToList();
        var allowed = new Dictionary<GearSlot, HashSet<GearStatType>>
        {
            [GearSlot.Necklace] = CheckedValues(_necklace), [GearSlot.Ring] = CheckedValues(_ring), [GearSlot.Boots] = CheckedValues(_boots),
        };
        return new OptimizationRequest
        {
            Hero = hero,
            HeroPreference = _workspace.GetPreference(hero.Id),
            Equipment = _workspace.Snapshot!.Items,
            HeroPriorities = _workspace.Preferences.Active,
            OccupationMode = (EquipmentOccupationMode)Math.Clamp(_occupation.SelectedIndex, 0, 2),
            StatRanges = ranges,
            Weights = new HeroStats(
                Weight(GearStatType.Attack), Weight(GearStatType.Health), Weight(GearStatType.Defense), Weight(GearStatType.Speed),
                Weight(GearStatType.CriticalHitChancePercent), Weight(GearStatType.CriticalHitDamagePercent),
                Weight(GearStatType.EffectivenessPercent), Weight(GearStatType.EffectResistancePercent)),
            RequiredSets = requiredSets,
            AllowedMainStats = allowed,
            ResultLimit = 200,
        };
    }

    private double Weight(GearStatType type) => (double)_ranges[type].Weight.Value;

    private static double WeightOf(HeroStats value, GearStatType type) => type switch
    {
        GearStatType.Attack => value.Attack, GearStatType.Health => value.Health, GearStatType.Defense => value.Defense,
        GearStatType.Speed => value.Speed, GearStatType.CriticalHitChancePercent => value.CriticalChance,
        GearStatType.CriticalHitDamagePercent => value.CriticalDamage, GearStatType.EffectivenessPercent => value.Effectiveness,
        GearStatType.EffectResistancePercent => value.Resistance, _ => 0,
    };

    private static void RestoreChecked(CheckedListBox list, HashSet<GearStatType>? selected)
    {
        for (var index = 0; index < list.Items.Count; index++)
            list.SetItemChecked(index, list.Items[index] is MainStatOption option && selected?.Contains(option.Type) == true);
    }

    private static int FindItemIndex(AntdUI.Select select, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return -1;
        for (var index = 0; index < select.Items.Count; index++)
            if (string.Equals(select.Items[index]?.ToString(), text, StringComparison.OrdinalIgnoreCase))
                return index;
        return -1;
    }

    private static ResultRow CreateResultRow(OptimizationResult value)
    {
        var slots = value.Equipment.ToDictionary(item => item.Slot);
        var stats = value.Panel.RawStats;
        string Item(GearSlot slot) => slots.TryGetValue(slot, out var item) ? $"{EquipmentSetCatalog.DisplayName(item.Set)} #{item.Id}" : string.Empty;
        return new ResultRow
        {
            Score = Math.Round(value.Score, 2), Sets = string.Join(" + ", value.Panel.ActiveSets.Select(EquipmentSetCatalog.DisplayName)),
            Attack = stats.Attack, Health = stats.Health, Defense = stats.Defense, Speed = stats.Speed,
            CriticalChance = stats.CriticalChance, CriticalDamage = stats.CriticalDamage,
            Effectiveness = stats.Effectiveness, Resistance = stats.Resistance, Conflicts = value.ConflictCount,
            Weapon = Item(GearSlot.Weapon), Helmet = Item(GearSlot.Helmet), Armor = Item(GearSlot.Armor),
            Necklace = Item(GearSlot.Necklace), Ring = Item(GearSlot.Ring), Boots = Item(GearSlot.Boots),
        };
    }

    private void WorkspaceChanged(object? sender, EventArgs e)
    {
        _cancellation?.Cancel();
        if (IsDisposed)
            return;
        if (InvokeRequired) BeginInvoke(RefreshOptions); else RefreshOptions();
    }

    private static double? ParseOptional(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out var value)
            && !double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            throw new ArgumentException($"无法解析属性值：{text}");
        return value;
    }

    private static HashSet<GearStatType> CheckedValues(CheckedListBox list) => list.CheckedItems
        .OfType<MainStatOption>().Select(value => value.Type).ToHashSet();
    private static string SelectedText(AntdUI.Select value) => value.SelectedValue?.ToString() ?? value.Text;
    private static AntdUI.Select CreateSelect(int width) => new() { Width = width, Height = 34, List = true, ReadOnly = false };
    private static CheckedListBox CreateMainList() => new() { Dock = DockStyle.Fill, CheckOnClick = true, BorderStyle = BorderStyle.FixedSingle, Height = 88 };
    private static Label LabelFor(string text, int width) => new() { Text = text, AutoSize = false, Width = width, Height = 34, TextAlign = ContentAlignment.MiddleRight };
    private static Label HeaderLabel(string text) => new() { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold) };

    private sealed record RangeInputs(AntdUI.Input Minimum, AntdUI.Input Maximum, AntdUI.InputNumber Weight);
    private sealed record MainStatOption(GearStatType Type, string Text) { public override string ToString() => Text; }
    private sealed class ResultRow
    {
        public double Score { get; init; }
        public string Sets { get; init; } = string.Empty;
        public double Attack { get; init; }
        public double Health { get; init; }
        public double Defense { get; init; }
        public double Speed { get; init; }
        public double CriticalChance { get; init; }
        public double CriticalDamage { get; init; }
        public double Effectiveness { get; init; }
        public double Resistance { get; init; }
        public int Conflicts { get; init; }
        public string Weapon { get; init; } = string.Empty;
        public string Helmet { get; init; } = string.Empty;
        public string Armor { get; init; } = string.Empty;
        public string Necklace { get; init; } = string.Empty;
        public string Ring { get; init; } = string.Empty;
        public string Boots { get; init; } = string.Empty;
    }
}
