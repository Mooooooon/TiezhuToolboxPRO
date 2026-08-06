using TiezhuToolbox.Modules.Recommend;

namespace TiezhuToolbox;

/// <summary>添加或编辑手动套装属性子类。</summary>
internal sealed class CustomDemandProfileDialog : Form
{
    private sealed record StatRow(AntdUI.Checkbox Selected, AntdUI.InputNumber Weight);

    private readonly DemandSet _set;
    private readonly CustomDemandProfile? _existing;
    private readonly Dictionary<string, StatRow> _rows = new(StringComparer.Ordinal);

    public CustomDemandProfile? ResultProfile { get; private set; }

    public CustomDemandProfileDialog(DemandSet set, CustomDemandProfile? existing = null)
    {
        _set = set;
        _existing = existing;
        Text = existing == null ? $"添加 {set.Name}属性子类" : $"编辑 {set.Name}属性子类";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(510, 560);
        BackColor = Color.White;
        Font = new Font("Microsoft YaHei UI", 9.5F);
        BuildInterface();
    }

    private void BuildInterface()
    {
        var title = new Label
        {
            Text = "选择有效属性并设置相对权重",
            Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold),
            Location = new Point(24, 20),
            Size = new Size(460, 30),
        };
        var description = new Label
        {
            Text = "权重只比较已选择属性之间的侧重，范围 0.1～10；名称将按所选属性自动生成。",
            ForeColor = Color.FromArgb(95, 99, 104),
            Location = new Point(24, 56),
            Size = new Size(460, 40),
        };
        var setLabel = new Label
        {
            Text = $"套装：{_set.Name}",
            Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold),
            Location = new Point(24, 99),
            Size = new Size(300, 25),
        };
        var propertyHeader = new Label
        {
            Text = "有效属性",
            ForeColor = Color.FromArgb(70, 72, 76),
            Location = new Point(32, 131),
            Size = new Size(180, 24),
        };
        var weightHeader = new Label
        {
            Text = "属性权重",
            ForeColor = Color.FromArgb(70, 72, 76),
            Location = new Point(337, 131),
            Size = new Size(100, 24),
        };

        var rowsPanel = new Panel
        {
            Location = new Point(24, 157),
            Size = new Size(462, 320),
            BackColor = Color.FromArgb(247, 249, 252),
        };
        for (var i = 0; i < EquipmentRules.UsefulStats.Length; i++)
        {
            var stat = EquipmentRules.UsefulStats[i];
            var isSelected = _existing?.Stats.Contains(stat, StringComparer.Ordinal) == true;
            var selected = new AntdUI.Checkbox
            {
                Text = stat,
                Checked = isSelected,
                Location = new Point(14, 5 + i * 39),
                Size = new Size(220, 34),
            };
            var weight = new AntdUI.InputNumber
            {
                Minimum = 0.1M,
                Maximum = 10M,
                Value = isSelected ? (decimal)_existing!.Weights.GetValueOrDefault(stat, 1) : 1M,
                Enabled = isSelected,
                Location = new Point(307, 5 + i * 39),
                Size = new Size(125, 32),
                Radius = 6,
            };
            selected.CheckedChanged += (_, _) => weight.Enabled = selected.Checked;
            _rows[stat] = new StatRow(selected, weight);
            rowsPanel.Controls.Add(selected);
            rowsPanel.Controls.Add(weight);
        }

        var save = new AntdUI.Button
        {
            Text = _existing == null ? "添加" : "保存",
            Type = AntdUI.TTypeMini.Primary,
            Location = new Point(304, 503),
            Size = new Size(82, 36),
            Radius = 6,
        };
        var cancel = new AntdUI.Button
        {
            Text = "取消",
            Location = new Point(398, 503),
            Size = new Size(82, 36),
            Radius = 6,
        };
        save.Click += (_, _) => SaveAndClose();
        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        Controls.Add(title);
        Controls.Add(description);
        Controls.Add(setLabel);
        Controls.Add(propertyHeader);
        Controls.Add(weightHeader);
        Controls.Add(rowsPanel);
        Controls.Add(save);
        Controls.Add(cancel);
    }

    private void SaveAndClose()
    {
        var stats = EquipmentRules.UsefulStats
            .Where(stat => _rows[stat].Selected.Checked)
            .ToList();
        if (stats.Count == 0)
        {
            MessageBox.Show(this, "请至少选择一种有效属性。", "无法保存",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ResultProfile = new CustomDemandProfile
        {
            Id = _existing?.Id ?? string.Empty,
            SetCode = _set.Code,
            Name = string.Join("·", stats),
            Stats = stats,
            Weights = stats.ToDictionary(
                stat => stat,
                stat => (double)_rows[stat].Weight.Value,
                StringComparer.Ordinal),
            Enabled = _existing?.Enabled ?? true,
        };
        DialogResult = DialogResult.OK;
        Close();
    }
}
