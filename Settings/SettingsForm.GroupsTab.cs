using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

public partial class SettingsForm : Form
{
    private DarkScrollPanel CreateGroupsPage()
    {
        DarkScrollPanel page = new DarkScrollPanel { Dock = DockStyle.Fill };

        _groupsMasterCard = new Win11CardPanel
        {
            Left = 18,
            Top = 4,
            Width = 468,
            Height = 56
        };

        Label lblTitle = new Label
        {
            Text = "Application Groups",
            Left = 14,
            Top = 10,
            Width = 280,
            Height = 20,
            Font = new Font("Segoe UI Semibold", 9.75f, FontStyle.Bold),
            ForeColor = Color.FromArgb(96, 205, 255)
        };
        _groupsMasterCard.Controls.Add(lblTitle);

        Label lblSubtitle = new Label
        {
            Text = "Bundle multiple apps to control them together as a single source",
            Left = 14,
            Top = 30,
            Width = 320,
            Height = 16,
            Font = new Font("Segoe UI", 8.0f, FontStyle.Regular),
            ForeColor = Color.FromArgb(165, 165, 165)
        };
        _groupsMasterCard.Controls.Add(lblSubtitle);

        _btnAddGroupEntry = CreateModernButton("+ New Group", 334, 11, 120, 26, true);
        _btnAddGroupEntry.Click += (s, e) =>
        {
            if (_isGroupListAnimating) return;
            var newGroup = new ProgramGroup
            {
                GroupName = $"Group {_draftSettings.Groups.Count + 1}",
                Executables = new List<string> { "" }
            };
            AnimateAddGroupCard(newGroup);
        };
        _groupsMasterCard.Controls.Add(_btnAddGroupEntry);

        _groupsSlotsHost = new BufferedPanel
        {
            Left = 12,
            Top = 52,
            Width = 444,
            Height = 0,
            BackColor = Color.FromArgb(39, 39, 39)
        };
        _groupsMasterCard.Controls.Add(_groupsSlotsHost);
        page.InnerControls.Add(_groupsMasterCard);

        return page;
    }

    private void RefreshGroupsTabUI()
    {
        PopulateGroupsFlowContent();
    }

    private void PopulateGroupsFlowContent()
    {
        if (_groupsSlotsHost == null) return;
        _groupsSlotsHost.SuspendLayout();
        _groupsSlotsHost.Controls.Clear();

        int hostW = Math.Max(442, _groupsSlotsHost.Width);
        List<string> exeSuggestions = GetExecutableSuggestions();
        int y = 0;

        foreach (var group in _draftSettings.Groups)
        {
            if (group.Executables.Count == 0)
                group.Executables.Add("");

            Win11CardPanel groupCard = BuildSingleGroupCard(group, exeSuggestions, hostW);
            groupCard.Top = y;
            _groupsSlotsHost.Controls.Add(groupCard);
            y += groupCard.Height + 6;
        }

        _groupsSlotsHost.ResumeLayout();
        ReflowGroupsPage();
    }

    private Win11CardPanel BuildSingleGroupCard(ProgramGroup group, List<string> exeSuggestions, int hostW)
    {
        int btnDelW = 104;
        int btnDelGroupLeft = hostW - btnDelW - 8;
        int nameBoxLeft = 106;
        int nameBoxW = Math.Max(130, btnDelGroupLeft - nameBoxLeft - 8);

        int cardH = 100 + (group.Executables.Count * 31);

        Win11CardPanel card = new Win11CardPanel
        {
            Left = 0,
            Top = 0,
            Width = hostW,
            Height = cardH,
            FillColor = Color.FromArgb(48, 48, 48),
            BorderColor = Color.FromArgb(66, 66, 66),
            CornerRadius = 6,
            Padding = new Padding(0),
            Tag = group
        };

        BufferedPanel iconBox = new BufferedPanel
        {
            Left = 10,
            Top = 8,
            Width = 46,
            Height = 46,
            Cursor = Cursors.Hand,
            BackColor = Color.FromArgb(48, 48, 48)
        };
        _toolTip.SetToolTip(iconBox, "Click to choose a group icon (Default presets or Custom icon)");
        iconBox.Paint += (s, e) =>
        {
            try
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                Rectangle r = new Rectangle(0, 0, iconBox.Width - 1, iconBox.Height - 1);
                using var path = Win11CardPanel.CreateRoundRect(r, 8);
                using var bg = new SolidBrush(Color.FromArgb(36, 36, 40));
                using var pen = new Pen(Color.FromArgb(82, 82, 88), 1f);
                e.Graphics.FillPath(bg, path);
                e.Graphics.DrawPath(pen, path);

                Bitmap? grpIco = AppIconCache.GetIconForGroup(group);
                if (grpIco != null)
                {
                    e.Graphics.DrawImage(grpIco, new Rectangle(5, 5, 36, 36));
                }
            }
            catch { }
        };
        iconBox.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
            {
                ShowGroupIconPickerMenu(group, iconBox);
            }
        };
        card.Controls.Add(iconBox);

        Label lblGroupName = new Label
        {
            Text = "Group:",
            Left = 62,
            Top = 12,
            Width = 44,
            Height = 18,
            Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(190, 190, 190)
        };
        card.Controls.Add(lblGroupName);

        TextBox txtGroupName = new TextBox
        {
            Text = group.GroupName,
            Left = nameBoxLeft,
            Top = 8,
            Width = nameBoxW,
            Height = 25,
            BackColor = Color.FromArgb(38, 38, 38),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            Tag = "GroupNameTxt"
        };
        txtGroupName.TextChanged += (s, e) =>
        {
            if (_isInitializing) return;
            group.GroupName = txtGroupName.Text.Trim();
            MarkDirty();
        };
        card.Controls.Add(txtGroupName);

        ModernButton btnRemoveGroup = CreateModernButton("Delete Group", btnDelGroupLeft, 8, btnDelW, 25, false, isDanger: true);
        btnRemoveGroup.Tag = "DeleteGroupBtn";
        btnRemoveGroup.Click += (s, e) =>
        {
            if (_isGroupListAnimating) return;
            AnimateRemoveGroupCard(group);
        };
        card.Controls.Add(btnRemoveGroup);

        Label lblAppsHeader = new Label
        {
            Text = "Included Applications:",
            Left = 62,
            Top = 37,
            Width = 220,
            Height = 17,
            Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(155, 155, 155)
        };
        card.Controls.Add(lblAppsHeader);

        for (int i = 0; i < group.Executables.Count; i++)
        {
            BufferedPanel exeRow = CreateGroupExeRowControl(card, group, i, exeSuggestions, hostW);
            card.Controls.Add(exeRow);
        }

        int innerY = 60 + (group.Executables.Count * 31);
        ModernButton btnAddExe = CreateModernButton("+ Add App", 10, innerY + 3, 110, 25, false);
        btnAddExe.Tag = "AddExeBtn";
        btnAddExe.Click += (s, e) =>
        {
            if (_isGroupListAnimating) return;

            var orderedRows = card.Controls.Cast<Control>()
                .Where(c => c.Tag as string == "GroupExeRow")
                .OrderBy(c => c.Top)
                .ToList();

            int newIdx = group.Executables.Count;
            group.Executables.Add("");

            List<string> currentSuggestions = GetExecutableSuggestions();
            BufferedPanel newRow = CreateGroupExeRowControl(card, group, newIdx, currentSuggestions, card.Width);

            Control? prevRow = orderedRows.LastOrDefault();
            int finalRowTop = 60 + (newIdx * 31);
            int startRowTop = prevRow != null ? prevRow.Top : 34;

            newRow.Top = startRowTop;
            card.Controls.Add(newRow);
            if (prevRow != null) prevRow.BringToFront();
            btnAddExe.BringToFront();

            RebindGroupExeRows(card, group);
            MarkDirty();

            int targetCardH = 100 + (group.Executables.Count * 31);
            AnimateGroupCardHeightChange(
                card,
                targetCardH,
                newRow,
                startRowTop,
                finalRowTop,
                new List<(Control ctrl, int startY, int targetY)>());
        };
        card.Controls.Add(btnAddExe);

        return card;
    }

    private BufferedPanel CreateGroupExeRowControl(
        Win11CardPanel card,
        ProgramGroup group,
        int initialIndex,
        List<string> exeSuggestions,
        int hostW)
    {
        int exeComboW = Math.Max(180, hostW - 154);
        int btnBrowseLeft = 10 + exeComboW + 6;
        int btnRemExeLeft = btnBrowseLeft + 94 + 4;

        BufferedPanel exeRow = new BufferedPanel
        {
            Left = 2,
            Top = 60 + initialIndex * 31,
            Width = hostW - 4,
            Height = 27,
            BackColor = Color.FromArgb(48, 48, 48),
            Tag = "GroupExeRow"
        };

        int GetCurrentRowIndex()
        {
            var ordered = card.Controls.Cast<Control>()
                .Where(c => c.Tag as string == "GroupExeRow")
                .OrderBy(c => c.Top)
                .ToList();
            return ordered.IndexOf(exeRow);
        }

        ModernComboBox cmbExe = new ModernComboBox
        {
            Left = 8,
            Top = 1,
            Width = exeComboW,
            DropDownStyle = ComboBoxStyle.DropDown,
            BadgeResolver = ResolveDropdownBadge,
            IconResolver = ResolveDropdownIcon,
            Tag = "ExeCombo"
        };
        cmbExe.Items.AddRange(exeSuggestions.Cast<object>().ToArray());
        string initVal = (initialIndex >= 0 && initialIndex < group.Executables.Count) ? group.Executables[initialIndex] : "";
        cmbExe.SelectedItem = initVal;
        if (cmbExe.SelectedIndex == -1) cmbExe.Text = initVal;

        cmbExe.DropDown += (s, e) =>
        {
            _detectedAudio = AudioProcessDetector.Scan();
            PopulateSingleComboOptions(cmbExe, includeGroups: false);
        };
        cmbExe.SelectedIndexChanged += (s, e) =>
        {
            if (_isInitializing) return;
            int idx = GetCurrentRowIndex();
            if (idx >= 0 && idx < group.Executables.Count)
            {
                group.Executables[idx] = ReadComboText(cmbExe);
                MarkDirty();
            }
        };
        cmbExe.TextChanged += (s, e) =>
        {
            if (_isInitializing) return;
            int idx = GetCurrentRowIndex();
            if (idx >= 0 && idx < group.Executables.Count)
            {
                group.Executables[idx] = ReadComboText(cmbExe);
                MarkDirty();
            }
        };
        exeRow.Controls.Add(cmbExe);

        ModernButton btnBrowseExe = CreateModernButton("Browse", btnBrowseLeft, 1, 94, 25, false);
        btnBrowseExe.Tag = "BrowseExeBtn";
        btnBrowseExe.Click += (s, e) =>
        {
            BrowseForExecutable(cmbExe);
            int idx = GetCurrentRowIndex();
            if (idx >= 0 && idx < group.Executables.Count)
            {
                group.Executables[idx] = ReadComboText(cmbExe);
            }
        };
        exeRow.Controls.Add(btnBrowseExe);

        ModernButton btnRemoveExe = CreateModernButton("✕", btnRemExeLeft, 1, 32, 25, false, isDanger: true);
        btnRemoveExe.Tag = "RemoveExeBtn";
        btnRemoveExe.Enabled = group.Executables.Count > 1;
        btnRemoveExe.Click += (s, e) =>
        {
            if (_isGroupListAnimating) return;
            var orderedRows = card.Controls.Cast<Control>()
                .Where(c => c.Tag as string == "GroupExeRow")
                .OrderBy(c => c.Top)
                .ToList();

            int idx = orderedRows.IndexOf(exeRow);
            if (idx < 0) return;

            if (orderedRows.Count <= 1)
            {
                group.Executables[0] = "";
                cmbExe.Text = "";
                MarkDirty();
                return;
            }

            Control? rowAbove = idx > 0 ? orderedRows[idx - 1] : null;
            if (rowAbove != null) rowAbove.BringToFront();
            else exeRow.SendToBack();

            int startRowTop = exeRow.Top;
            int targetRowTop = rowAbove != null ? rowAbove.Top : 34;

            List<(Control ctrl, int startY, int targetY)> shiftingBelow = new();
            for (int i = idx + 1; i < orderedRows.Count; i++)
            {
                Control rBelow = orderedRows[i];
                shiftingBelow.Add((rBelow, rBelow.Top, rBelow.Top - 31));
            }

            int targetCardH = 100 + ((orderedRows.Count - 1) * 31);
            AnimateGroupCardHeightChange(
                card,
                targetCardH,
                exeRow,
                startRowTop,
                targetRowTop,
                shiftingBelow,
                onFinished: () =>
                {
                    if (idx < group.Executables.Count)
                        group.Executables.RemoveAt(idx);
                    card.Controls.Remove(exeRow);
                    exeRow.Dispose();
                    RebindGroupExeRows(card, group);
                    MarkDirty();
                });
        };
        exeRow.Controls.Add(btnRemoveExe);

        return exeRow;
    }

    private void ResizeExistingGroupCards(int cardW)
    {
        if (_groupsMasterCard == null || _groupsSlotsHost == null) return;

        _groupsMasterCard.Width = cardW;
        _btnAddGroupEntry.Left = cardW - 14 - _btnAddGroupEntry.Width;

        int hostW = Math.Max(442, cardW - 24);
        _groupsSlotsHost.Width = hostW;

        int btnDelW = 104;
        int btnDelGroupLeft = hostW - btnDelW - 8;
        int nameBoxLeft = 106;
        int nameBoxW = Math.Max(130, btnDelGroupLeft - nameBoxLeft - 8);

        int exeComboW = Math.Max(180, hostW - 154);
        int btnBrowseLeft = 10 + exeComboW + 6;
        int btnRemExeLeft = btnBrowseLeft + 94 + 4;

        foreach (Control c in _groupsSlotsHost.Controls)
        {
            if (c is not Win11CardPanel groupCard) continue;
            groupCard.Width = hostW;

            foreach (Control child in groupCard.Controls)
            {
                if (child.Tag is not string tag) continue;
                switch (tag)
                {
                    case "GroupNameTxt":
                        child.Left = nameBoxLeft;
                        child.Width = nameBoxW;
                        break;
                    case "DeleteGroupBtn":
                        child.Left = btnDelGroupLeft;
                        break;
                    case "GroupExeRow":
                        child.Width = hostW - 4;
                        foreach (Control rc in child.Controls)
                        {
                            if (rc.Tag as string == "ExeCombo") rc.Width = exeComboW;
                            else if (rc.Tag as string == "BrowseExeBtn") rc.Left = btnBrowseLeft;
                            else if (rc.Tag as string == "RemoveExeBtn") rc.Left = btnRemExeLeft;
                        }
                        break;
                }
            }
        }

        ReflowGroupsPage();
    }

    private void AnimateAddGroupCard(ProgramGroup newGroup)
    {
        if (_groupsSlotsHost == null) return;

        var existingOrdered = GetOrderedGroupCards();
        int finalTop = 0;
        foreach (var c in existingOrdered)
        {
            finalTop = Math.Max(finalTop, c.Bottom + 6);
        }

        _draftSettings.Groups.Add(newGroup);
        int hostW = Math.Max(442, _groupsSlotsHost.Width);
        List<string> exeSuggestions = GetExecutableSuggestions();

        Win11CardPanel newCard = BuildSingleGroupCard(newGroup, exeSuggestions, hostW);
        int finalCardH = newCard.Height;

        // Place the new group at its exact bottom slot below all existing groups,
        // and smoothly grow the card & host container downward out of the bottom!
        int startHostH = finalTop;
        int finalHostH = finalTop + finalCardH + 6;

        _groupsSlotsHost.SuspendLayout();
        newCard.Top = finalTop;
        newCard.Height = 0;
        _groupsSlotsHost.Controls.Add(newCard);
        _groupsSlotsHost.Height = Math.Max(1, startHostH);
        _groupsSlotsHost.Visible = true;
        _groupsSlotsHost.ResumeLayout(false);

        MarkDirty();
        ReflowGroupsPage();

        _isGroupListAnimating = true;
        const double durationMs = 190.0;
        Stopwatch sw = Stopwatch.StartNew();
        var timer = new System.Windows.Forms.Timer { Interval = 15 };

        timer.Tick += (s, e) =>
        {
            if (newCard.IsDisposed || _groupsSlotsHost.IsDisposed)
            {
                _isGroupListAnimating = false;
                timer.Stop();
                timer.Dispose();
                return;
            }

            double t = Math.Clamp(sw.Elapsed.TotalMilliseconds / durationMs, 0.0, 1.0);
            float ease = (float)(1.0 - Math.Pow(1.0 - t, 3.0));

            if (t >= 1.0)
            {
                newCard.Top = finalTop;
                newCard.Height = finalCardH;
                _groupsSlotsHost.Height = finalHostH;
                _isGroupListAnimating = false;
                timer.Stop();
                timer.Dispose();
            }
            else
            {
                newCard.Top = finalTop;
                newCard.Height = Math.Max(1, (int)Math.Round(finalCardH * ease));
                _groupsSlotsHost.Height = startHostH + (int)Math.Round((finalHostH - startHostH) * ease);
            }

            ReflowGroupsPage();
        };
        timer.Start();
    }

    private void AnimateRemoveGroupCard(ProgramGroup groupToRemove)
    {
        if (_groupsSlotsHost == null) return;
        var orderedCards = GetOrderedGroupCards();
        int removeIndex = _draftSettings.Groups.IndexOf(groupToRemove);
        if (removeIndex < 0) return;

        Win11CardPanel? removingCard = orderedCards.FirstOrDefault(c => ReferenceEquals(c.Tag, groupToRemove));
        if (removingCard == null) return;

        int startCardH = removingCard.Height;
        int shiftDelta = startCardH + 6;

        List<(Win11CardPanel ctrl, int startY, int targetY)> trailingCards = new();
        for (int i = removeIndex + 1; i < orderedCards.Count; i++)
        {
            var tc = orderedCards[i];
            trailingCards.Add((tc, tc.Top, tc.Top - shiftDelta));
        }

        int startHostH = _groupsSlotsHost.Height;
        int targetHostH = Math.Max(0, startHostH - shiftDelta);

        _isGroupListAnimating = true;
        const double durationMs = 190.0;
        Stopwatch sw = Stopwatch.StartNew();
        var timer = new System.Windows.Forms.Timer { Interval = 15 };

        timer.Tick += (s, e) =>
        {
            if (_groupsSlotsHost.IsDisposed)
            {
                _isGroupListAnimating = false;
                timer.Stop();
                timer.Dispose();
                return;
            }

            double t = Math.Clamp(sw.Elapsed.TotalMilliseconds / durationMs, 0.0, 1.0);
            float ease = (float)(1.0 - Math.Pow(1.0 - t, 3.0));

            if (t >= 1.0)
            {
                timer.Stop();
                timer.Dispose();

                _groupsSlotsHost.SuspendLayout();
                _draftSettings.Groups.Remove(groupToRemove);
                _groupsSlotsHost.Controls.Remove(removingCard);
                removingCard.Dispose();

                foreach (var item in trailingCards)
                    item.ctrl.Top = item.targetY;

                _groupsSlotsHost.Height = targetHostH;
                _groupsSlotsHost.ResumeLayout(false);

                _isGroupListAnimating = false;
                ReflowGroupsPage();
                MarkDirty();
                PushLivePreviewIfActive();
                return;
            }

            removingCard.Height = Math.Max(0, (int)Math.Round(startCardH * (1f - ease)));
            foreach (var item in trailingCards)
                item.ctrl.Top = item.startY + (int)Math.Round((item.targetY - item.startY) * ease);

            _groupsSlotsHost.Height = startHostH + (int)Math.Round((targetHostH - startHostH) * ease);
            ReflowGroupsPage();
        };
        timer.Start();
    }

    private void AnimateGroupCardHeightChange(
        Win11CardPanel card,
        int targetCardH,
        Control? slidingRow,
        int rowStartTop,
        int rowTargetTop,
        List<(Control ctrl, int startY, int targetY)> shiftingChildren,
        Action? onFinished = null)
    {
        if (_groupsSlotsHost == null) return;

        int startCardH = card.Height;
        int deltaH = targetCardH - startCardH;

        Control? btnAddExe = card.Controls.Cast<Control>().FirstOrDefault(c => c.Tag as string == "AddExeBtn");
        int btnStartTop = btnAddExe?.Top ?? 0;
        int btnTargetTop = targetCardH - 37;

        var orderedCards = GetOrderedGroupCards();
        int cardIdx = orderedCards.IndexOf(card);
        List<(Win11CardPanel ctrl, int startY, int targetY)> trailingGroupCards = new();
        for (int i = cardIdx + 1; i < orderedCards.Count; i++)
        {
            var tc = orderedCards[i];
            trailingGroupCards.Add((tc, tc.Top, tc.Top + deltaH));
        }

        int startHostH = _groupsSlotsHost.Height;
        int targetHostH = Math.Max(0, startHostH + deltaH);

        _isGroupListAnimating = true;
        const double durationMs = 175.0;
        Stopwatch sw = Stopwatch.StartNew();
        var timer = new System.Windows.Forms.Timer { Interval = 15 };

        timer.Tick += (s, e) =>
        {
            if (card.IsDisposed || _groupsSlotsHost.IsDisposed)
            {
                _isGroupListAnimating = false;
                timer.Stop();
                timer.Dispose();
                return;
            }

            double t = Math.Clamp(sw.Elapsed.TotalMilliseconds / durationMs, 0.0, 1.0);
            float ease = (float)(1.0 - Math.Pow(1.0 - t, 3.0));

            if (t >= 1.0)
            {
                timer.Stop();
                timer.Dispose();

                card.Height = targetCardH;
                if (slidingRow != null && !slidingRow.IsDisposed)
                    slidingRow.Top = rowTargetTop;
                foreach (var sc in shiftingChildren)
                    if (!sc.ctrl.IsDisposed) sc.ctrl.Top = sc.targetY;
                if (btnAddExe != null)
                {
                    btnAddExe.Top = btnTargetTop;
                    btnAddExe.BringToFront();
                }
                foreach (var tc in trailingGroupCards)
                    tc.ctrl.Top = tc.targetY;

                _groupsSlotsHost.Height = targetHostH;
                _isGroupListAnimating = false;
                onFinished?.Invoke();
                ReflowGroupsPage();
                return;
            }

            card.Height = startCardH + (int)Math.Round(deltaH * ease);
            if (slidingRow != null && !slidingRow.IsDisposed)
                slidingRow.Top = rowStartTop + (int)Math.Round((rowTargetTop - rowStartTop) * ease);
            foreach (var sc in shiftingChildren)
                if (!sc.ctrl.IsDisposed) sc.ctrl.Top = sc.startY + (int)Math.Round((sc.targetY - sc.startY) * ease);
            if (btnAddExe != null)
            {
                btnAddExe.Top = btnStartTop + (int)Math.Round((btnTargetTop - btnStartTop) * ease);
                btnAddExe.BringToFront();
            }
            foreach (var tc in trailingGroupCards)
                tc.ctrl.Top = tc.startY + (int)Math.Round((tc.targetY - tc.startY) * ease);

            _groupsSlotsHost.Height = startHostH + (int)Math.Round(deltaH * ease);
            ReflowGroupsPage();
        };
        timer.Start();
    }

    private void ShowGroupIconPickerMenu(ProgramGroup group, Control iconPreviewControl)
    {
        try
        {
            ContextMenuStrip menu = new ContextMenuStrip
            {
                Renderer = new DarkContextMenuRenderer(),
                BackColor = Color.FromArgb(32, 32, 32),
                ForeColor = Color.FromArgb(240, 240, 240),
                ShowImageMargin = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            string currentPath = string.IsNullOrWhiteSpace(group.CustomIconPath)
                ? "preset:cards"
                : group.CustomIconPath.Trim();

            foreach (var (key, displayName) in AppIconCache.DefaultGroupIconPresets)
            {
                Bitmap? bmp = AppIconCache.GetPresetIconPreview(key);
                var item = new ToolStripMenuItem(displayName, bmp)
                {
                    Checked = currentPath.Equals(key, StringComparison.OrdinalIgnoreCase)
                };
                string capturedKey = key;
                item.Click += (s, e) =>
                {
                    AppIconCache.InvalidateCustomIcon(group.CustomIconPath);
                    group.CustomIconPath = capturedKey;
                    iconPreviewControl.Invalidate();
                    PopulateCycleListUI();
                    MarkDirty();
                    PushLivePreviewIfActive(forceTrigger: true);
                };
                menu.Items.Add(item);
            }

            menu.Items.Add(new ToolStripSeparator());

            var browseItem = new ToolStripMenuItem("Browse Custom Icon (.ico, .exe, .png)...");
            browseItem.Click += (s, e) => BrowseForGroupIcon(group, iconPreviewControl);
            menu.Items.Add(browseItem);

            menu.Show(iconPreviewControl, new Point(0, iconPreviewControl.Height + 2));
        }
        catch { }
    }

    private void BrowseForGroupIcon(ProgramGroup group, Control iconPreviewControl)
    {
        try
        {
            using OpenFileDialog dlg = new OpenFileDialog
            {
                Title = "Select Custom Group Icon (.ico, .exe, .png, .jpg)",
                Filter = "Icons, Apps & Images (*.ico;*.exe;*.png;*.jpg;*.bmp)|*.ico;*.exe;*.png;*.jpg;*.bmp|All Files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                AppIconCache.InvalidateCustomIcon(group.CustomIconPath);
                group.CustomIconPath = dlg.FileName;
                AppIconCache.InvalidateCustomIcon(group.CustomIconPath);
                iconPreviewControl.Invalidate();
                PopulateCycleListUI();
                MarkDirty();
                PushLivePreviewIfActive(forceTrigger: true);
            }
        }
        catch { }
    }

    private void ReflowGroupsPage()
    {
        if (_groupsMasterCard == null || _groupsSlotsHost == null) return;

        if (!_isGroupListAnimating)
        {
            int y = 0;
            foreach (Win11CardPanel c in GetOrderedGroupCards())
            {
                c.Top = y;
                y += c.Height + 6;
            }
            _groupsSlotsHost.Height = y;
        }

        _groupsSlotsHost.Visible = (_groupsSlotsHost.Controls.Count > 0 && _groupsSlotsHost.Height > 0);
        _groupsMasterCard.Height = !_groupsSlotsHost.Visible ? 56 : (54 + _groupsSlotsHost.Height + 6);
        _pageGroups?.RecalculateScroll();
    }
}