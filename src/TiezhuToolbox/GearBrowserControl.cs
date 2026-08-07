using TiezhuToolbox.Modules.Account;

namespace TiezhuToolbox;

public sealed class GearBrowserControl : UserControl
{
    private readonly AccountWorkspace _workspace;
    private readonly AntdUI.Table _table = new() { Dock = DockStyle.Fill, VirtualMode = true };
    private readonly AntdUI.Input _search = new() { PlaceholderText = "搜索装备/持有英雄", Width = 190 };
    private readonly AntdUI.Select _set = CreateSelect(130);
    private readonly AntdUI.Select _slot = CreateSelect(105);
    private readonly AntdUI.Select _main = CreateSelect(170);
    private readonly AntdUI.Select _owner = CreateSelect(120);
    private readonly Label _summary = new() { Width = 250, Height = 34, TextAlign = ContentAlignment.MiddleRight };
    private List<GearRow> _rows = [];
    private bool _isRefreshing;

    public GearBrowserControl(AccountWorkspace workspace)
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
        _slot.Items.AddRange(new object[] { "全部部位", "Weapon", "Helmet", "Armor", "Necklace", "Ring", "Boots" });
        _main.Items.Add("全部主属性");
        foreach (var value in Enum.GetNames<GearStatType>())
            _main.Items.Add(value);
        _owner.Items.AddRange(new object[] { "全部持有状态", "未穿戴", "已穿戴", "仓库" });
        _slot.SelectedIndex = _main.SelectedIndex = _owner.SelectedIndex = 0;
        toolbar.Controls.AddRange([_search, _set, _slot, _main, _owner, _summary]);
        Controls.Add(_table);
        Controls.Add(toolbar);

        _search.TextChanged += (_, _) => RefreshRows();
        _set.SelectedIndexChanged += (_, _) => RefreshRows();
        _slot.SelectedIndexChanged += (_, _) => RefreshRows();
        _main.SelectedIndexChanged += (_, _) => RefreshRows();
        _owner.SelectedIndexChanged += (_, _) => RefreshRows();
        _workspace.Changed += WorkspaceChanged;
        Disposed += (_, _) => _workspace.Changed -= WorkspaceChanged;
        RefreshRows();
    }

    public int DisplayedGearCount => _rows.Count;

    private void BuildColumns()
    {
        AddColumn("Name", "装备", "120");
        AddColumn("Set", "套装", "88");
        AddColumn("Slot", "部位", "82");
        AddColumn("LevelEnhance", "等级", "74");
        AddColumn("Main", "主属性", "145");
        AddColumn("Attack", "固定攻", "70");
        AddColumn("AttackPercent", "攻击%", "68");
        AddColumn("Health", "固定生", "70");
        AddColumn("HealthPercent", "生命%", "68");
        AddColumn("Defense", "固定防", "70");
        AddColumn("DefensePercent", "防御%", "68");
        AddColumn("Speed", "速度", "62");
        AddColumn("CriticalChance", "暴击", "62");
        AddColumn("CriticalDamage", "暴伤", "62");
        AddColumn("Effectiveness", "命中", "62");
        AddColumn("Resistance", "抗性", "62");
        AddColumn("GearScore", "装备分", "72");
        AddColumn("Owner", "持有者", "110");
        AddColumn("Storage", "仓库", "55");
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
        if (_isRefreshing)
            return;
        _isRefreshing = true;
        try
        {
        var snapshot = _workspace.Snapshot;
        if (snapshot == null)
        {
            _rows = [];
            _table.DataSource = _rows;
            _summary.Text = "尚未导入账号数据";
            return;
        }
        var ownerNames = snapshot.Heroes.ToDictionary(value => value.Id, value => value.Name, StringComparer.Ordinal);
        var sets = snapshot.Items.Select(value => value.Set).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(EquipmentSetCatalog.DisplayName).ToArray();
        var previousSet = SelectedText(_set);
        _set.Items.Clear();
        _set.Items.Add("全部套装");
        foreach (var value in sets)
            _set.Items.Add(value);
        _set.SelectedIndex = Math.Max(0, Array.FindIndex(sets, value => value == previousSet) + 1);

        var search = _search.Text.Trim();
        var selectedSet = SelectedText(_set);
        var selectedSlot = SelectedText(_slot);
        var selectedMain = SelectedText(_main);
        var selectedOwner = SelectedText(_owner);
        _rows = snapshot.Items.Select(item => CreateRow(item, ownerNames))
            .Where(row => string.IsNullOrEmpty(search) || row.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || row.Owner.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Where(row => selectedSet is "" or "全部套装" || row.Item.Set == selectedSet)
            .Where(row => selectedSlot is "" or "全部部位" || row.Item.Slot.ToString() == selectedSlot)
            .Where(row => selectedMain is "" or "全部主属性" || row.Item.Main.Type.ToString() == selectedMain)
            .Where(row => selectedOwner switch
            {
                "未穿戴" => string.IsNullOrEmpty(row.Item.EquippedHeroId) && !row.Item.Storage,
                "已穿戴" => !string.IsNullOrEmpty(row.Item.EquippedHeroId),
                "仓库" => row.Item.Storage,
                _ => true,
            }).ToList();
        _table.DataSource = _rows;
        _summary.Text = $"显示 {_rows.Count} / {snapshot.Items.Count} 件装备";
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private static GearRow CreateRow(AccountGear item, IReadOnlyDictionary<string, string> ownerNames)
    {
        var totals = Enum.GetValues<GearStatType>().ToDictionary(value => value, _ => 0D);
        foreach (var stat in item.Substats.Prepend(item.Main))
            totals[stat.Type] += stat.Value;
        var score = totals[GearStatType.AttackPercent] + totals[GearStatType.HealthPercent]
                    + totals[GearStatType.DefensePercent] + totals[GearStatType.Speed] * 2
                    + totals[GearStatType.CriticalHitChancePercent] * 1.5
                    + totals[GearStatType.CriticalHitDamagePercent] * 1.125
                    + totals[GearStatType.EffectivenessPercent] + totals[GearStatType.EffectResistancePercent];
        return new GearRow
        {
            Item = item,
            Name = item.Name,
            Set = EquipmentSetCatalog.DisplayName(item.Set),
            Slot = DisplaySlot(item.Slot),
            LevelEnhance = $"{item.Level} / +{item.Enhance}",
            Main = $"{DisplayStat(item.Main.Type)} {item.Main.Value:0.#}",
            Attack = totals[GearStatType.Attack], AttackPercent = totals[GearStatType.AttackPercent],
            Health = totals[GearStatType.Health], HealthPercent = totals[GearStatType.HealthPercent],
            Defense = totals[GearStatType.Defense], DefensePercent = totals[GearStatType.DefensePercent],
            Speed = totals[GearStatType.Speed], CriticalChance = totals[GearStatType.CriticalHitChancePercent],
            CriticalDamage = totals[GearStatType.CriticalHitDamagePercent],
            Effectiveness = totals[GearStatType.EffectivenessPercent], Resistance = totals[GearStatType.EffectResistancePercent],
            GearScore = Math.Round(score, 1),
            Owner = ownerNames.TryGetValue(item.EquippedHeroId, out var name) ? name : string.IsNullOrEmpty(item.EquippedHeroId) ? "未穿戴" : item.EquippedHeroId,
            Storage = item.Storage ? "是" : string.Empty,
        };
    }

    private void WorkspaceChanged(object? sender, EventArgs e)
    {
        if (IsDisposed)
            return;
        if (InvokeRequired) BeginInvoke(RefreshRows); else RefreshRows();
    }

    private static AntdUI.Select CreateSelect(int width) => new() { Width = width, Height = 34, List = true, ReadOnly = false };
    private static string SelectedText(AntdUI.Select value) => value.SelectedValue?.ToString() ?? value.Text;
    private static string DisplaySlot(GearSlot value) => value switch
    {
        GearSlot.Weapon => "武器", GearSlot.Helmet => "头盔", GearSlot.Armor => "铠甲",
        GearSlot.Necklace => "项链", GearSlot.Ring => "戒指", GearSlot.Boots => "鞋子", _ => value.ToString(),
    };
    public static string DisplayStat(GearStatType value) => value switch
    {
        GearStatType.Attack => "攻击", GearStatType.AttackPercent => "攻击%", GearStatType.Health => "生命",
        GearStatType.HealthPercent => "生命%", GearStatType.Defense => "防御", GearStatType.DefensePercent => "防御%",
        GearStatType.Speed => "速度", GearStatType.CriticalHitChancePercent => "暴击", GearStatType.CriticalHitDamagePercent => "暴伤",
        GearStatType.EffectivenessPercent => "命中", GearStatType.EffectResistancePercent => "抗性", _ => value.ToString(),
    };

    private sealed class GearRow
    {
        public required AccountGear Item { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Set { get; init; } = string.Empty;
        public string Slot { get; init; } = string.Empty;
        public string LevelEnhance { get; init; } = string.Empty;
        public string Main { get; init; } = string.Empty;
        public double Attack { get; init; }
        public double AttackPercent { get; init; }
        public double Health { get; init; }
        public double HealthPercent { get; init; }
        public double Defense { get; init; }
        public double DefensePercent { get; init; }
        public double Speed { get; init; }
        public double CriticalChance { get; init; }
        public double CriticalDamage { get; init; }
        public double Effectiveness { get; init; }
        public double Resistance { get; init; }
        public double GearScore { get; init; }
        public string Owner { get; init; } = string.Empty;
        public string Storage { get; init; } = string.Empty;
    }
}
