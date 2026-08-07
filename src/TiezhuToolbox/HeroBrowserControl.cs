using TiezhuToolbox.Modules.Account;
using TiezhuToolbox.Modules.Recommend;

namespace TiezhuToolbox;

public sealed class HeroBrowserControl : UserControl
{
    private readonly AccountWorkspace _workspace;
    private readonly AntdUI.Table _table = new() { Dock = DockStyle.Fill, VirtualMode = true };
    private readonly AntdUI.Input _search = new() { PlaceholderText = "搜索英雄", Width = 190 };
    private readonly AntdUI.Select _attribute = CreateSelect(110);
    private readonly AntdUI.Select _role = CreateSelect(120);
    private readonly Label _summary = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleRight };
    private readonly AntdUI.InputNumber _priority = new() { Minimum = 1, Maximum = 999, Width = 76 };
    private readonly AntdUI.Select _imprint = CreateSelect(105);
    private readonly AntdUI.Select _exclusive = CreateSelect(170);
    private readonly AntdUI.Select _artifact = CreateSelect(220);
    private readonly AntdUI.InputNumber _artifactLevel = new() { Minimum = 1, Maximum = 30, Value = 30, Width = 72 };
    private readonly AntdUI.Checkbox _specialty = new() { Text = "满转职树", Checked = true, Width = 122 };
    private List<HeroRow> _rows = [];
    private List<ArtifactCatalogEntry> _artifactOptions = [];
    private AccountHero? _selectedHero;
    private readonly Dictionary<string, Image?> _avatarCache = new(StringComparer.OrdinalIgnoreCase);

    public HeroBrowserControl(AccountWorkspace workspace)
    {
        _workspace = workspace;
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(245, 246, 248);
        Padding = new Padding(18);
        BuildColumns();

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 48, WrapContents = false, Padding = new Padding(0, 5, 0, 5),
        };
        _attribute.Items.Add("全部属性");
        foreach (var pair in HeroAttributeCatalog.Options)
            _attribute.Items.Add(pair.Value);
        _role.Items.Add("全部职业");
        foreach (var pair in HeroRoleCatalog.Options)
            _role.Items.Add(pair.Value);
        _attribute.SelectedIndex = 0;
        _role.SelectedIndex = 0;
        var import = new AntdUI.Button { Text = "导入 gear.txt", Width = 116, Height = 34, Type = AntdUI.TTypeMini.Primary };
        import.Click += (_, _) => ImportGearText();
        _summary.Width = 260;
        toolbar.Controls.AddRange([_search, _attribute, _role, import, _summary]);

        var detail = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 54, WrapContents = false, Padding = new Padding(0, 8, 0, 6),
            BackColor = Color.White,
        };
        var save = new AntdUI.Button { Text = "保存英雄设置", Width = 118, Height = 34, Type = AntdUI.TTypeMini.Primary };
        save.Click += (_, _) => SaveSelection();
        detail.Controls.AddRange([
            LabelFor("优先级"), _priority, LabelFor("自身阵型"), _imprint,
            LabelFor("专属装备"), _exclusive, LabelFor("神器"), _artifact,
            LabelFor("等级"), _artifactLevel, _specialty, save,
        ]);

        Controls.Add(_table);
        Controls.Add(detail);
        Controls.Add(toolbar);
        _search.TextChanged += (_, _) => RefreshRows();
        _attribute.SelectedIndexChanged += (_, _) => RefreshRows();
        _role.SelectedIndexChanged += (_, _) => RefreshRows();
        _table.CellClick += (_, args) => SelectRow(args.Record as HeroRow);
        _workspace.Changed += WorkspaceChanged;
        Disposed += (_, _) =>
        {
            _workspace.Changed -= WorkspaceChanged;
            foreach (var image in _avatarCache.Values)
                image?.Dispose();
            _avatarCache.Clear();
        };
        RefreshRows();
    }

    public int DisplayedHeroCount => _rows.Count;

    private void BuildColumns()
    {
        AddColumn("Avatar", "头像", "52");
        AddColumn("Priority", "优先级", "72");
        AddColumn("Name", "英雄", "150");
        AddColumn("Attribute", "属性", "70");
        AddColumn("Role", "职业", "82");
        AddColumn("Progress", "养成", "95");
        AddColumn("Sets", "套装", "150");
        AddColumn("Attack", "攻击", "78");
        AddColumn("Health", "生命", "82");
        AddColumn("Defense", "防御", "72");
        AddColumn("Speed", "速度", "66");
        AddColumn("CriticalChance", "暴击", "68");
        AddColumn("CriticalDamage", "暴伤", "68");
        AddColumn("Effectiveness", "命中", "68");
        AddColumn("Resistance", "抗性", "68");
        AddColumn("Warning", "状态", "180");
    }

    private void AddColumn(string key, string title, string width)
    {
        var column = new AntdUI.Column(key, title);
        column.SetWidth(width);
        column.SetSortOrder(true);
        _table.Columns.Add(column);
    }

    private void RefreshRows()
    {
        var snapshot = _workspace.Snapshot;
        if (snapshot == null)
        {
            _rows = [];
            _table.DataSource = _rows;
            _summary.Text = "尚未导入账号数据";
            return;
        }
        var search = _search.Text.Trim();
        var attribute = _attribute.SelectedIndex > 0 ? HeroAttributeCatalog.Options[_attribute.SelectedIndex - 1].Key : null;
        var role = _role.SelectedIndex > 0 ? HeroRoleCatalog.Options[_role.SelectedIndex - 1].Key : null;
        _rows = snapshot.Heroes.Select(hero => CreateRow(hero))
            .Where(row => string.IsNullOrEmpty(search) || row.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || row.Hero.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || row.Code.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Where(row => attribute == null || row.AttributeKey == attribute)
            .Where(row => role == null || row.RoleKey == role)
            .OrderBy(row => row.Priority).ToList();
        _table.DataSource = _rows;
        _summary.Text = $"显示 {_rows.Count} / {snapshot.Heroes.Count} 名英雄";
    }

    private HeroRow CreateRow(AccountHero hero)
    {
        var preference = _workspace.GetPreference(hero.Id);
        var panel = _workspace.GetPanel(hero);
        _workspace.GameData.TryGetHero(hero.Code, out var catalog);
        return new HeroRow
        {
            Hero = hero,
            Avatar = CreateAvatarCell(hero.Code),
            Code = hero.Code,
            Priority = preference?.Priority ?? int.MaxValue,
            Name = _workspace.GameData.DisplayHeroName(hero.Code, hero.Name),
            Attribute = catalog != null ? HeroAttributeCatalog.DisplayName(catalog.Attribute) : "未知",
            AttributeKey = catalog?.Attribute ?? string.Empty,
            Role = catalog != null ? HeroRoleCatalog.DisplayName(catalog.Role) : "未知",
            RoleKey = catalog?.Role ?? string.Empty,
            Progress = $"{hero.Level}级 {hero.Stars}星/{hero.Awaken}觉",
            Sets = string.Join(" + ", panel.ActiveSets.Select(EquipmentSetCatalog.DisplayName)),
            Attack = panel.RawStats.Attack,
            Health = panel.RawStats.Health,
            Defense = panel.RawStats.Defense,
            Speed = panel.RawStats.Speed,
            CriticalChance = FormatCapped(panel.RawStats.CriticalChance, panel.CriticalChanceOverflow),
            CriticalDamage = FormatCapped(panel.RawStats.CriticalDamage, panel.CriticalDamageOverflow),
            Effectiveness = panel.RawStats.Effectiveness,
            Resistance = panel.RawStats.Resistance,
            Warning = panel.Warning ?? string.Empty,
        };
    }

    private AntdUI.CellImage? CreateAvatarCell(string code)
    {
        if (!_avatarCache.TryGetValue(code, out var image))
        {
            image = LoadAvatar(code);
            _avatarCache[code] = image;
        }
        return image == null ? null : new AntdUI.CellImage(image);
    }

    /// <summary>读文件加载头像且不占用文件句柄（避免锁住 Assets 下的图片）。</summary>
    private static Image? LoadAvatar(string code)
    {
        var path = DemandDatabase.GetAvatarPath(code);
        if (path == null)
            return null;
        try
        {
            var bytes = File.ReadAllBytes(path);
            return new Bitmap(new MemoryStream(bytes));
        }
        catch
        {
            return null;
        }
    }

    private void SelectRow(HeroRow? row)
    {
        if (row == null)
            return;
        _selectedHero = row.Hero;
        var preference = _workspace.GetPreference(row.Hero.Id);
        if (preference == null)
            return;
        _priority.Value = preference.Priority;
        _specialty.Checked = preference.MaxSpecialtyTree;
        _imprint.Items.Clear();
        _imprint.Items.Add("无");
        _exclusive.Items.Clear();
        _exclusive.Items.Add("无");
        if (_workspace.GameData.TryGetHero(row.Hero.Code, out var catalog))
        {
            foreach (var grade in catalog.ImprintGrades.Keys)
                _imprint.Items.Add(grade);
            foreach (var value in catalog.ExclusiveEquipment)
                _exclusive.Items.Add($"{DisplayExternalStat(value.Type)} +{FormatExternalStat(value)}");
        }
        _specialty.Enabled = catalog?.SpecialtyTreeDataAvailable == true;
        _specialty.Text = _specialty.Enabled ? "满转职树" : "转职树未覆盖";
        _imprint.SelectedIndex = Math.Max(0, _imprint.Items.IndexOf(preference.ImprintGrade ?? "无"));
        _exclusive.SelectedIndex = Math.Clamp(preference.ExclusiveEquipmentIndex + 1, 0, _exclusive.Items.Count - 1);

        _artifactOptions = _workspace.GameData.Artifacts
            .Where(value => catalog == null || string.IsNullOrEmpty(value.Role) || value.Role == catalog.Role)
            .OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase).ToList();
        _artifact.Items.Clear();
        _artifact.Items.Add("无");
        foreach (var value in _artifactOptions)
            _artifact.Items.Add(value.Name);
        var artifactIndex = _artifactOptions.FindIndex(value => value.Code == preference.ArtifactCode);
        _artifact.SelectedIndex = artifactIndex + 1;
        _artifactLevel.Value = Math.Clamp(preference.ArtifactLevel, 1, 30);
    }

    private void SaveSelection()
    {
        if (_selectedHero == null)
            return;
        var preference = _workspace.GetPreference(_selectedHero.Id);
        if (preference == null)
            return;
        var requestedPriority = (int)_priority.Value;
        preference.ImprintGrade = _imprint.SelectedIndex > 0 ? SelectedText(_imprint) : null;
        preference.ExclusiveEquipmentIndex = _exclusive.SelectedIndex - 1;
        preference.ArtifactCode = _artifact.SelectedIndex > 0 && _artifact.SelectedIndex - 1 < _artifactOptions.Count
            ? _artifactOptions[_artifact.SelectedIndex - 1].Code : null;
        preference.ArtifactLevel = (int)_artifactLevel.Value;
        if (_specialty.Enabled)
            preference.MaxSpecialtyTree = _specialty.Checked;
        _workspace.UpdatePreference(preference);
        _workspace.MovePriority(preference.HeroId, requestedPriority);
    }

    private void ImportGearText()
    {
        using var dialog = new OpenFileDialog { Filter = "Fribbels 装备数据 (gear.txt)|gear.txt|JSON 文件 (*.json)|*.json|所有文件|*.*" };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
            return;
        try
        {
            var text = File.ReadAllText(dialog.FileName);
            var preview = AccountImportService.Parse(text, "gear.txt 预验证");
            var confirm = MessageBox.Show(FindForm(),
                $"文件验证通过：{preview.Items.Count} 件装备、{preview.Heroes.Count} 名英雄。\n" +
                "导入后将替换当前账号快照，英雄偏好会按实例 ID 保留。是否继续？",
                "确认导入 gear.txt", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
                return;
            var snapshot = _workspace.Import(text, "gear.txt:" + Path.GetFileName(dialog.FileName));
            MessageBox.Show(FindForm(), $"已导入 {snapshot.Items.Count} 件装备、{snapshot.Heroes.Count} 名英雄。",
                "导入完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(FindForm(), ex.Message, "导入失败，原数据未改变", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void WorkspaceChanged(object? sender, EventArgs e)
    {
        if (IsDisposed)
            return;
        if (InvokeRequired)
            BeginInvoke(RefreshRows);
        else
            RefreshRows();
    }

    private static string SelectedText(AntdUI.Select value) => value.SelectedValue?.ToString() ?? value.Text;
    private static AntdUI.Select CreateSelect(int width) => new() { Width = width, Height = 34, List = true, ReadOnly = false };
    private static Label LabelFor(string text) => new() { Text = text, AutoSize = false, Width = 68, Height = 34, TextAlign = ContentAlignment.MiddleRight };
    private static string DisplayExternalStat(string value) => value switch
    {
        "att" or "att_rate" => "攻击", "max_hp" or "max_hp_rate" => "生命", "def" or "def_rate" => "防御",
        "speed" => "速度", "cri" => "暴击", "cri_dmg" => "暴伤", "acc" => "命中", "res" => "抗性", _ => value,
    };
    private static string FormatExternalStat(ExclusiveEquipmentStat value) =>
        value.Value is > -1 and < 1 && value.Type is not "speed" and not "att" and not "max_hp" and not "def"
            ? $"{value.Value * 100:0.#}%" : $"{value.Value:0.#}";
    private static string FormatCapped(double raw, double overflow) => overflow > 0
        ? $"{raw:0.#} (+{overflow:0.#})" : $"{raw:0.#}";

    private sealed class HeroRow
    {
        public required AccountHero Hero { get; init; }
        public AntdUI.CellImage? Avatar { get; init; }
        public string Code { get; init; } = string.Empty;
        public int Priority { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Attribute { get; init; } = string.Empty;
        public string AttributeKey { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public string RoleKey { get; init; } = string.Empty;
        public string Progress { get; init; } = string.Empty;
        public string Sets { get; init; } = string.Empty;
        public double Attack { get; init; }
        public double Health { get; init; }
        public double Defense { get; init; }
        public double Speed { get; init; }
        public string CriticalChance { get; init; } = string.Empty;
        public string CriticalDamage { get; init; } = string.Empty;
        public double Effectiveness { get; init; }
        public double Resistance { get; init; }
        public string Warning { get; init; } = string.Empty;
    }
}
