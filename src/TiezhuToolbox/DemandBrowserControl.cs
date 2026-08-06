using TiezhuToolbox.Modules.Recommend;

namespace TiezhuToolbox;

/// <summary>套装需求浏览器；内置内容只读，用户可另行维护手动属性子类并控制匹配开关。</summary>
internal sealed class DemandBrowserControl : UserControl
{
    private readonly ListBox _setList = new();
    private readonly FlowLayoutPanel _profiles = new();
    private readonly Label _sourceInfo = new();
    private readonly AntdUI.Button _addProfileButton = new();
    private readonly Func<string, bool> _isProfileEnabled;
    private readonly Action<string, bool> _setProfileEnabled;
    private readonly Action _profilesChanged;
    private int _layoutDpi = 96;

    public DemandBrowserControl(
        Func<string, bool>? isProfileEnabled = null,
        Action<string, bool>? setProfileEnabled = null,
        Action? profilesChanged = null)
    {
        _isProfileEnabled = isProfileEnabled ?? (_ => true);
        _setProfileEnabled = setProfileEnabled ?? ((_, _) => { });
        _profilesChanged = profilesChanged ?? (() => { });
        BackColor = Color.FromArgb(245, 246, 248);
        Dock = DockStyle.Fill;
        BuildInterface();
        LoadSets();
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
        _setList.ItemHeight = ScalePixel(48);
        ResumeLayout(performLayout: true);
        ResizeProfileCards();
    }

    internal void PrepareForDpiChange(int dpi) => _layoutDpi = Math.Max(96, dpi);

    internal void CompleteDpiChange()
    {
        _setList.ItemHeight = ScalePixel(48);
        ResizeProfileCards();
    }

    internal void RefreshProfiles() => ShowSelectedSet();

    private int ScalePixel(int value) => (int)Math.Round(value * _layoutDpi / 96D);

    private void BuildInterface()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 64,
            Padding = new Padding(18, 10, 18, 8),
            BackColor = Color.White,
        };
        var title = new Label
        {
            Text = "套装需求分析",
            Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(18, 15),
        };
        _sourceInfo.AutoSize = true;
        _sourceInfo.ForeColor = Color.FromArgb(95, 99, 104);
        _sourceInfo.Location = new Point(220, 23);
        _addProfileButton.Text = "手动添加";
        _addProfileButton.Type = AntdUI.TTypeMini.Primary;
        _addProfileButton.Size = new Size(96, 34);
        _addProfileButton.Radius = 6;
        _addProfileButton.Enabled = false;
        _addProfileButton.Click += (_, _) => AddCustomProfile();
        header.Resize += (_, _) => _addProfileButton.Location = new Point(
            Math.Max(18, header.ClientSize.Width - _addProfileButton.Width - 18), 15);
        header.Controls.Add(title);
        header.Controls.Add(_sourceInfo);
        header.Controls.Add(_addProfileButton);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Size = new Size(1000, 500),
            SplitterDistance = 245,
            SplitterWidth = 1,
            IsSplitterFixed = true,
            Panel1MinSize = 210,
            Panel2MinSize = 480,
            BackColor = Color.FromArgb(225, 227, 230),
        };
        split.Panel1.BackColor = Color.White;
        split.Panel1.Padding = new Padding(12);
        split.Panel2.BackColor = Color.FromArgb(245, 246, 248);
        split.Panel2.Padding = new Padding(14);

        _setList.Dock = DockStyle.Fill;
        _setList.BorderStyle = BorderStyle.None;
        _setList.DrawMode = DrawMode.OwnerDrawFixed;
        _setList.ItemHeight = 48;
        _setList.Font = new Font("Microsoft YaHei UI", 10.5F);
        _setList.DrawItem += DrawSetItem;
        _setList.SelectedIndexChanged += (_, _) => ShowSelectedSet();
        split.Panel1.Controls.Add(_setList);

        _profiles.Dock = DockStyle.Fill;
        _profiles.AutoScroll = true;
        _profiles.FlowDirection = FlowDirection.TopDown;
        _profiles.WrapContents = false;
        _profiles.Padding = new Padding(4);
        _profiles.BackColor = Color.FromArgb(245, 246, 248);
        _profiles.Resize += (_, _) => ResizeProfileCards();
        split.Panel2.Controls.Add(_profiles);

        Controls.Add(split);
        Controls.Add(header);
    }

    private void LoadSets()
    {
        var database = DemandDatabase.Instance;
        var customStore = CustomDemandProfileStore.Instance;
        _sourceInfo.Text = database.IsLoaded
            ? $"内置数据 · 更新于 {database.UpdatedAt}　手动添加 {customStore.Profiles.Count} 条"
            : $"需求数据未加载：{database.ErrorMessage}";
        _setList.Items.Clear();
        if (!database.IsLoaded)
            return;

        foreach (var set in database.Sets)
            _setList.Items.Add(set);
        if (_setList.Items.Count > 0)
            _setList.SelectedIndex = 0;
        _addProfileButton.Enabled = _setList.SelectedItem is DemandSet;
    }

    private void DrawSetItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _setList.Items.Count)
            return;
        var set = (DemandSet)_setList.Items[e.Index]!;
        e.DrawBackground();
        var selected = (e.State & DrawItemState.Selected) != 0;
        var foreground = selected ? Color.White : Color.FromArgb(32, 33, 36);
        var secondary = selected ? Color.FromArgb(225, 235, 255) : Color.FromArgb(95, 99, 104);
        var iconPath = DemandDatabase.GetSetIconPath(set.Code);
        if (iconPath != null)
        {
            using var image = LoadImageNoLock(iconPath);
            if (image != null)
                e.Graphics.DrawImage(image, e.Bounds.Left + 5, e.Bounds.Top + 8, 32, 32);
        }

        using var foregroundBrush = new SolidBrush(foreground);
        using var secondaryBrush = new SolidBrush(secondary);
        using var bold = new Font(e.Font ?? Font, FontStyle.Bold);
        e.Graphics.DrawString(set.Name, bold, foregroundBrush, e.Bounds.Left + 46, e.Bounds.Top + 6);
        var customProfiles = CustomDemandProfileStore.Instance.GetProfiles(set.Code);
        var enabledCount = set.Profiles.Count(profile =>
            _isProfileEnabled(SetProfileMatcher.CreateProfileKey(set.Code, profile.Id)));
        enabledCount += customProfiles.Count(profile => profile.Enabled);
        var totalCount = set.Profiles.Count + customProfiles.Count;
        var profileSummary = totalCount == 0
            ? "暂无需求数据"
            : enabledCount == totalCount
                ? $"{totalCount} 个属性子类"
                : $"{enabledCount}/{totalCount} 个参与匹配";
        e.Graphics.DrawString(
            profileSummary,
            e.Font ?? Font, secondaryBrush, e.Bounds.Left + 46, e.Bounds.Top + 27);
        e.DrawFocusRectangle();
    }

    private void ShowSelectedSet()
    {
        foreach (var control in _profiles.Controls.Cast<Control>().ToList())
            control.Dispose();
        _profiles.Controls.Clear();

        if (_setList.SelectedItem is not DemandSet set)
        {
            _addProfileButton.Enabled = false;
            return;
        }
        _addProfileButton.Enabled = true;
        var customProfiles = CustomDemandProfileStore.Instance.GetProfiles(set.Code);
        if (set.Profiles.Count == 0 && customProfiles.Count == 0)
        {
            _profiles.Controls.Add(new Label
            {
                Text = $"{set.Name}暂无内置需求数据",
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 12F),
                ForeColor = Color.FromArgb(95, 99, 104),
                Margin = new Padding(12, 18, 0, 0),
            });
            return;
        }

        foreach (var profile in customProfiles.OrderBy(
                     profile => profile.Name, StringComparer.CurrentCulture))
        {
            _profiles.Controls.Add(CreateCustomProfileCard(set, profile));
        }
        foreach (var profile in set.Profiles
                     .OrderByDescending(profile => profile.DemandWeight)
                     .ThenBy(profile => profile.Name, StringComparer.CurrentCulture))
        {
            _profiles.Controls.Add(CreateProfileCard(set, profile));
        }
        ResizeProfileCards();
    }

    private Panel CreateCustomProfileCard(DemandSet set, CustomDemandProfile profile)
    {
        var cardHeight = ScalePixel(86);
        var card = new Panel
        {
            Width = ScalePixel(600),
            Height = cardHeight,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, ScalePixel(12)),
            Tag = "profile-card",
        };
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Width = card.Width,
            Height = ScalePixel(56),
        };
        var content = new Panel
        {
            Dock = DockStyle.Left,
        };
        var actions = new Panel
        {
            Dock = DockStyle.Right,
            Width = ScalePixel(322),
        };
        void LayoutHeader()
        {
            content.Width = Math.Max(ScalePixel(80), header.ClientSize.Width - actions.Width);
        }
        header.Resize += (_, _) => LayoutHeader();
        LayoutHeader();
        var title = new Label
        {
            Text = profile.Name,
            Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
            Location = new Point(ScalePixel(14), ScalePixel(11)),
            Size = new Size(Math.Max(ScalePixel(80), card.Width - actions.Width - ScalePixel(28)), ScalePixel(25)),
            AutoEllipsis = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };
        var source = new Label
        {
            Text = "手动添加 · 无英雄下级",
            ForeColor = Color.FromArgb(95, 99, 104),
            Location = new Point(ScalePixel(14), ScalePixel(37)),
            Size = new Size(ScalePixel(220), ScalePixel(20)),
        };
        var weights = new Label
        {
            Text = "属性权重：" + string.Join("　", profile.Stats.Select(stat =>
                $"{stat} {profile.Weights.GetValueOrDefault(stat):0.#}")),
            ForeColor = Color.FromArgb(45, 89, 178),
            Location = new Point(ScalePixel(14), ScalePixel(60)),
            Size = new Size(card.Width - ScalePixel(28), ScalePixel(20)),
            AutoEllipsis = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };
        var enabledText = new Label
        {
            Location = new Point(0, ScalePixel(12)),
            Size = new Size(ScalePixel(84), ScalePixel(22)),
            TextAlign = ContentAlignment.MiddleRight,
        };
        var enabledSwitch = new AntdUI.Switch
        {
            Checked = profile.Enabled,
            Location = new Point(ScalePixel(92), ScalePixel(10)),
            Size = new Size(ScalePixel(48), ScalePixel(26)),
            Tag = profile.ProfileKey,
        };
        var edit = new AntdUI.Button
        {
            Text = "编辑",
            Location = new Point(ScalePixel(152), ScalePixel(8)),
            Size = new Size(ScalePixel(72), ScalePixel(30)),
            Radius = 6,
        };
        var delete = new AntdUI.Button
        {
            Text = "删除",
            Location = new Point(ScalePixel(236), ScalePixel(8)),
            Size = new Size(ScalePixel(72), ScalePixel(30)),
            Radius = 6,
        };

        void UpdateEnabledStyle()
        {
            enabledText.Text = enabledSwitch.Checked ? "参与匹配" : "已停用";
            enabledText.ForeColor = enabledSwitch.Checked
                ? Color.FromArgb(52, 130, 76)
                : Color.FromArgb(150, 80, 80);
            title.ForeColor = enabledSwitch.Checked
                ? Color.FromArgb(32, 33, 36)
                : Color.FromArgb(125, 128, 132);
            weights.ForeColor = enabledSwitch.Checked
                ? Color.FromArgb(45, 89, 178)
                : Color.FromArgb(145, 148, 152);
        }

        enabledSwitch.CheckedChanged += (_, _) =>
        {
            try
            {
                CustomDemandProfileStore.Instance.SetEnabled(set.Code, profile.Id, enabledSwitch.Checked);
                UpdateEnabledStyle();
                _setList.Invalidate();
                UpdateSourceInfo();
                _profilesChanged();
            }
            catch (Exception ex)
            {
                enabledSwitch.Checked = !enabledSwitch.Checked;
                ShowSaveError(ex);
            }
        };
        edit.Click += (_, _) => EditCustomProfile(set, profile);
        delete.Click += (_, _) => DeleteCustomProfile(set, profile);
        UpdateEnabledStyle();
        content.Controls.Add(title);
        content.Controls.Add(source);
        actions.Controls.Add(enabledText);
        actions.Controls.Add(enabledSwitch);
        actions.Controls.Add(edit);
        actions.Controls.Add(delete);
        header.Controls.Add(content);
        header.Controls.Add(actions);
        actions.BringToFront();
        card.Controls.Add(weights);
        card.Controls.Add(header);
        return card;
    }

    private Panel CreateProfileCard(DemandSet set, DemandProfile profile)
    {
        const int collapsedLogicalHeight = 86;
        var collapsedHeight = ScalePixel(collapsedLogicalHeight);
        var heroLineHeight = ScalePixel(46);
        var profileKey = SetProfileMatcher.CreateProfileKey(set.Code, profile.Id);
        var card = new Panel
        {
            Width = ScalePixel(600),
            Height = collapsedHeight,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, ScalePixel(12)),
            Tag = "profile-card",
        };
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Width = card.Width,
            Height = collapsedHeight,
            Cursor = Cursors.Hand,
        };
        var title = new Label
        {
            Text = profile.Name,
            Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
            Location = new Point(ScalePixel(14), ScalePixel(12)),
            Size = new Size(card.Width - ScalePixel(250), ScalePixel(25)),
            AutoEllipsis = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };
        var demand = new Label
        {
            Text = $"需求权重 {profile.DemandWeight:0.##} · {profile.Heroes.Count} 条英雄配装",
            ForeColor = Color.FromArgb(95, 99, 104),
            Location = new Point(ScalePixel(14), ScalePixel(38)),
            Size = new Size(card.Width - ScalePixel(28), ScalePixel(20)),
            AutoEllipsis = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };
        var weights = new Label
        {
            Text = "属性权重：" + string.Join("　", profile.Stats.Select(stat =>
                $"{stat} {profile.Weights.GetValueOrDefault(stat):0.#}")),
            ForeColor = Color.FromArgb(45, 89, 178),
            Location = new Point(ScalePixel(14), ScalePixel(59)),
            Size = new Size(card.Width - ScalePixel(28), ScalePixel(20)),
            AutoEllipsis = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };
        var toggle = new Label
        {
            Text = "▼",
            Location = new Point(card.Width - ScalePixel(123), ScalePixel(12)),
            Size = new Size(ScalePixel(22), ScalePixel(22)),
            ForeColor = Color.FromArgb(95, 99, 104),
            TextAlign = ContentAlignment.MiddleCenter,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        var enabledText = new Label
        {
            Location = new Point(card.Width - ScalePixel(225), ScalePixel(12)),
            Size = new Size(ScalePixel(98), ScalePixel(22)),
            TextAlign = ContentAlignment.MiddleRight,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        var enabledSwitch = new AntdUI.Switch
        {
            Checked = _isProfileEnabled(profileKey),
            Location = new Point(card.Width - ScalePixel(63), ScalePixel(10)),
            Size = new Size(ScalePixel(48), ScalePixel(26)),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Tag = profileKey,
        };
        header.Controls.Add(title);
        header.Controls.Add(demand);
        header.Controls.Add(weights);
        header.Controls.Add(toggle);
        header.Controls.Add(enabledText);
        header.Controls.Add(enabledSwitch);

        var builds = new Panel
        {
            Dock = DockStyle.Fill,
            Size = new Size(card.Width, ScalePixel(350)),
            Visible = false,
            AutoScroll = profile.Heroes.Count * heroLineHeight > ScalePixel(350),
            BackColor = Color.FromArgb(250, 251, 252),
        };
        var y = 0;
        foreach (var hero in profile.Heroes
                     .OrderByDescending(hero => hero.DemandContribution)
                     .ThenBy(hero => hero.Name, StringComparer.CurrentCulture))
        {
            var row = new Panel
            {
                Location = new Point(ScalePixel(14), y),
                Size = new Size(card.Width - ScalePixel(28), heroLineHeight),
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            };
            var summary = new Label
            {
                Text = $"{hero.Name}｜{hero.ComboName}｜样本 {hero.SampleShare:P1}｜需求 {hero.DemandContribution:0.###}",
                Location = new Point(0, ScalePixel(2)),
                Size = new Size(row.Width, ScalePixel(20)),
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(60, 62, 66),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            };
            var heroWeights = new Label
            {
                Text = "属性：" + string.Join("　", profile.Stats.Select(stat =>
                    $"{stat} {hero.Weights.GetValueOrDefault(stat):0.#}")),
                Location = new Point(0, ScalePixel(23)),
                Size = new Size(row.Width, ScalePixel(20)),
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(75, 105, 155),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            };
            row.Controls.Add(summary);
            row.Controls.Add(heroWeights);
            builds.Controls.Add(row);
            y += heroLineHeight;
        }

        void UpdateEnabledStyle()
        {
            var enabled = enabledSwitch.Checked;
            enabledText.Text = enabled ? "参与匹配" : "已停用";
            enabledText.ForeColor = enabled
                ? Color.FromArgb(52, 130, 76)
                : Color.FromArgb(150, 80, 80);
            title.ForeColor = enabled
                ? Color.FromArgb(32, 33, 36)
                : Color.FromArgb(125, 128, 132);
            weights.ForeColor = enabled
                ? Color.FromArgb(45, 89, 178)
                : Color.FromArgb(145, 148, 152);
        }

        void ToggleExpanded(object? _, EventArgs __)
        {
            builds.Visible = !builds.Visible;
            toggle.Text = builds.Visible ? "▲" : "▼";
            card.Height = builds.Visible
                ? collapsedHeight + Math.Min(Math.Max(heroLineHeight, y), ScalePixel(350))
                : collapsedHeight;
            _profiles.PerformLayout();
        }

        enabledSwitch.CheckedChanged += (_, _) =>
        {
            UpdateEnabledStyle();
            _setProfileEnabled(profileKey, enabledSwitch.Checked);
            _setList.Invalidate();
        };
        header.Click += ToggleExpanded;
        foreach (var control in new Control[] { title, demand, weights, toggle, enabledText })
        {
            control.Cursor = Cursors.Hand;
            control.Click += ToggleExpanded;
        }
        UpdateEnabledStyle();
        card.Controls.Add(builds);
        card.Controls.Add(header);
        return card;
    }

    private void AddCustomProfile()
    {
        if (_setList.SelectedItem is not DemandSet set)
            return;
        using var dialog = new CustomDemandProfileDialog(set);
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK || dialog.ResultProfile == null)
            return;
        try
        {
            CustomDemandProfileStore.Instance.Upsert(dialog.ResultProfile);
            AfterCustomProfileChanged();
        }
        catch (Exception ex)
        {
            ShowSaveError(ex);
        }
    }

    private void EditCustomProfile(DemandSet set, CustomDemandProfile profile)
    {
        using var dialog = new CustomDemandProfileDialog(set, profile);
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK || dialog.ResultProfile == null)
            return;
        try
        {
            CustomDemandProfileStore.Instance.Upsert(dialog.ResultProfile);
            AfterCustomProfileChanged();
        }
        catch (Exception ex)
        {
            ShowSaveError(ex);
        }
    }

    private void DeleteCustomProfile(DemandSet set, CustomDemandProfile profile)
    {
        var answer = MessageBox.Show(
            FindForm(),
            $"确定删除手动属性子类“{profile.Name}”吗？\r\n删除后无法恢复。",
            "删除手动属性子类",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes)
            return;
        try
        {
            CustomDemandProfileStore.Instance.Remove(set.Code, profile.Id);
            AfterCustomProfileChanged();
        }
        catch (Exception ex)
        {
            ShowSaveError(ex);
        }
    }

    private void AfterCustomProfileChanged()
    {
        UpdateSourceInfo();
        ShowSelectedSet();
        _setList.Invalidate();
        _profilesChanged();
    }

    private void UpdateSourceInfo()
    {
        var database = DemandDatabase.Instance;
        if (!database.IsLoaded)
            return;
        _sourceInfo.Text = $"内置数据 · 更新于 {database.UpdatedAt}　手动添加 "
                           + $"{CustomDemandProfileStore.Instance.Profiles.Count} 条";
    }

    private void ShowSaveError(Exception ex) => MessageBox.Show(
        FindForm(), $"保存手动属性子类失败：{ex.Message}", "保存失败",
        MessageBoxButtons.OK, MessageBoxIcon.Error);

    private void ResizeProfileCards()
    {
        var width = Math.Max(ScalePixel(420),
            _profiles.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - ScalePixel(12));
        foreach (Control card in _profiles.Controls)
        {
            if (Equals(card.Tag, "profile-card"))
            {
                card.Width = width;
                foreach (Control child in card.Controls)
                {
                    if (child is Label { AutoEllipsis: true })
                        child.Width = Math.Max(ScalePixel(200), width - ScalePixel(28));
                }
            }
        }
    }

    private static Image? LoadImageNoLock(string path)
    {
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
}
