using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

public partial class SettingsForm : Form
{
    private DarkScrollPanel CreateBehaviorPage()
    {
        DarkScrollPanel page = new DarkScrollPanel { Dock = DockStyle.Fill };
        int y = 4;

        _layoutCard = new Win11CardPanel
        {
            Left = 18,
            Top = y,
            Width = 468
        };

        int ay = 10;
        AddSectionHeader(_layoutCard, "Position && Size", 14, ref ay);

        Label lblPos = new Label
        {
            Text = "Screen Position:",
            Left = 14,
            Top = ay + 4,
            Width = 165,
            Height = 20,
            ForeColor = Color.FromArgb(205, 205, 205)
        };
        _layoutCard.Controls.Add(lblPos);

        _cmbPosition = new ModernComboBox
        {
            Left = 185,
            Top = ay,
            Width = 265,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbPosition.Items.AddRange(new object[] { "Bottom Center", "Top Center", "Bottom Right", "Bottom Left", "Top Right", "Top Left" });
        _cmbPosition.SelectedItem = _draftSettings.OverlayPosition;
        if (_cmbPosition.SelectedIndex == -1) _cmbPosition.SelectedIndex = 0;
        _cmbPosition.SelectedIndexChanged += (s, e) =>
        {
            MarkDirty();
            PushLivePreviewIfActive();
        };
        _layoutCard.Controls.Add(_cmbPosition);
        ay += 32;

        CheckBox chkFollowMonitor = new CheckBox
        {
            Text = "Follow active monitor",
            Checked = _draftSettings.FollowActiveMonitor,
            Left = 14,
            Top = ay,
            Width = 430,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        _layoutCard.Controls.Add(chkFollowMonitor);
        ay += 28;

        _monitorRowsPanel = new Panel
        {
            Left = 14,
            Top = ay,
            Width = 436,
            AutoSize = false,
            Enabled = !_draftSettings.FollowActiveMonitor
        };

        Screen[] allScreens = Screen.AllScreens;
        int my = 0;
        for (int i = 0; i < allScreens.Length; i++)
        {
            Screen scr = allScreens[i];
            var existingCfg = _draftSettings.MonitorConfigs.FirstOrDefault(m => m.DeviceName.Equals(scr.DeviceName, StringComparison.OrdinalIgnoreCase));
            if (existingCfg == null)
            {
                existingCfg = new MonitorOverlayConfig
                {
                    DeviceName = scr.DeviceName,
                    Enabled = scr.Primary,
                    Position = _draftSettings.OverlayPosition
                };
                _draftSettings.MonitorConfigs.Add(existingCfg);
            }

            var (physW, physH) = GetPhysicalMonitorResolution(scr);
            string monLabel = $"Monitor {i + 1}{(scr.Primary ? " (Primary)" : "")} [{physW}×{physH}]";
            CheckBox chkMon = new CheckBox
            {
                Text = monLabel,
                Checked = existingCfg.Enabled,
                Left = 0,
                Top = my + 3,
                Width = 230,
                Height = 22,
                ForeColor = Color.FromArgb(215, 215, 215)
            };

            ModernComboBox cmbMonPos = new ModernComboBox
            {
                Left = 236,
                Top = my,
                Width = 200,
                Enabled = existingCfg.Enabled,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbMonPos.Items.AddRange(new object[] { "Bottom Center", "Top Center", "Bottom Right", "Bottom Left", "Top Right", "Top Left" });
            cmbMonPos.SelectedItem = existingCfg.Position;
            if (cmbMonPos.SelectedIndex == -1) cmbMonPos.SelectedIndex = 0;

            chkMon.CheckedChanged += (s, e) =>
            {
                existingCfg.Enabled = chkMon.Checked;
                cmbMonPos.Enabled = chkMon.Checked;
                MarkDirty();
                PushLivePreviewIfActive();
            };
            cmbMonPos.SelectedIndexChanged += (s, e) =>
            {
                existingCfg.Position = cmbMonPos.SelectedItem?.ToString() ?? "Bottom Center";
                if (scr.Primary) _draftSettings.OverlayPosition = existingCfg.Position;
                MarkDirty();
                PushLivePreviewIfActive();
            };

            _monitorRowsPanel.Controls.Add(chkMon);
            _monitorRowsPanel.Controls.Add(cmbMonPos);
            my += 30;
        }
        _monitorRowsPanel.Height = Math.Max(30, my);
        _layoutCard.Controls.Add(_monitorRowsPanel);
        ay += _monitorRowsPanel.Height + 6;

        chkFollowMonitor.CheckedChanged += (s, e) =>
        {
            _draftSettings.FollowActiveMonitor = chkFollowMonitor.Checked;
            _monitorRowsPanel.Enabled = !chkFollowMonitor.Checked;
            MarkDirty();
            PushLivePreviewIfActive();
        };

        Label lblSize = new Label
        {
            Text = "Dimensions (W × H):",
            Left = 14,
            Top = ay + 3,
            Width = 165,
            Height = 20,
            ForeColor = Color.FromArgb(205, 205, 205)
        };
        _layoutCard.Controls.Add(lblSize);

        _txtWidth = new TextBox
        {
            Text = _draftSettings.OverlayWidth.ToString(),
            Left = 185,
            Top = ay,
            Width = 90,
            BackColor = Color.FromArgb(48, 48, 48),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        _txtWidth.TextChanged += (s, e) =>
        {
            MarkDirty();
            PushLivePreviewIfActive();
        };
        _layoutCard.Controls.Add(_txtWidth);

        Label lblBy = new Label
        {
            Text = "×",
            Left = 280,
            Top = ay + 3,
            Width = 18,
            Height = 20,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(180, 180, 180)
        };
        _layoutCard.Controls.Add(lblBy);

        _txtHeight = new TextBox
        {
            Text = _draftSettings.OverlayHeight.ToString(),
            Left = 302,
            Top = ay,
            Width = 90,
            BackColor = Color.FromArgb(48, 48, 48),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        _txtHeight.TextChanged += (s, e) =>
        {
            MarkDirty();
            PushLivePreviewIfActive();
        };
        _layoutCard.Controls.Add(_txtHeight);
        ay += 30;

        Label lblDur = new Label
        {
            Text = "Hide Delay (ms):",
            Left = 14,
            Top = ay + 3,
            Width = 165,
            Height = 20,
            ForeColor = Color.FromArgb(205, 205, 205)
        };
        _layoutCard.Controls.Add(lblDur);

        _txtPopupDuration = new TextBox
        {
            Text = _draftSettings.PopupDurationMs.ToString(),
            Left = 185,
            Top = ay,
            Width = 90,
            BackColor = Color.FromArgb(48, 48, 48),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        _txtPopupDuration.TextChanged += (s, e) => MarkDirty();
        _layoutCard.Controls.Add(_txtPopupDuration);

        _layoutCard.FitHeightToContent(12);
        page.InnerControls.Add(_layoutCard);
        y = _layoutCard.Bottom + 10;

        _behaviorCard = new Win11CardPanel
        {
            Left = 18,
            Top = y,
            Width = 468
        };

        int by = 10;
        AddSectionHeader(_behaviorCard, "Audio && System", 14, ref by);

        Label lblStep = new Label
        {
            Text = "Knob Step Size:",
            Left = 14,
            Top = by + 3,
            Width = 165,
            Height = 20,
            ForeColor = Color.FromArgb(205, 205, 205)
        };
        _behaviorCard.Controls.Add(lblStep);

        _sldStepSize = new ModernSlider
        {
            Minimum = 1,
            Maximum = 50,
            Value = Math.Clamp(_draftSettings.StepSize, 1, 50),
            ValueSuffix = "%",
            Left = 185,
            Top = by,
            Width = 265
        };
        _sldStepSize.ValueChanged += (s, e) => { if (!_isInitializing) MarkDirty(); };
        _behaviorCard.Controls.Add(_sldStepSize);
        by += 30;

        Label lblSpeed = new Label
        {
            Text = "Volume Smoothing Speed:",
            Left = 14,
            Top = by + 3,
            Width = 165,
            Height = 20,
            ForeColor = Color.FromArgb(205, 205, 205)
        };
        _behaviorCard.Controls.Add(lblSpeed);

        _sldInterpSpeed = new ModernSlider
        {
            Minimum = 5,
            Maximum = 100,
            Value = Math.Clamp(_draftSettings.InterpolationSpeed, 5, 100),
            Enabled = _draftSettings.EnableInterpolation,
            Left = 185,
            Top = by,
            Width = 265
        };
        _sldInterpSpeed.ValueChanged += (s, e) => { if (!_isInitializing) MarkDirty(); };
        _behaviorCard.Controls.Add(_sldInterpSpeed);
        by += 30;

        _chkInterpolation = new CheckBox
        {
            Text = "Smooth volume interpolation",
            Checked = _draftSettings.EnableInterpolation,
            Left = 14,
            Top = by,
            Width = 430,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        _chkInterpolation.CheckedChanged += (s, e) =>
        {
            if (_sldInterpSpeed != null) _sldInterpSpeed.Enabled = _chkInterpolation.Checked;
            MarkDirty();
            PushLivePreviewIfActive();
        };
        _behaviorCard.Controls.Add(_chkInterpolation);
        by += 26;

        _chkBuffer = new CheckBox
        {
            Text = "Center detent (pause at 50/50)",
            Checked = _draftSettings.EnableEqualizationBuffer,
            Left = 14,
            Top = by,
            Width = 430,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        _chkBuffer.CheckedChanged += (s, e) => MarkDirty();
        _behaviorCard.Controls.Add(_chkBuffer);
        by += 26;

        _chkStartup = new CheckBox
        {
            Text = "Run at Windows startup",
            Checked = _draftSettings.RunOnStartup,
            Left = 14,
            Top = by,
            Width = 430,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        _chkStartup.CheckedChanged += (s, e) => MarkDirty();
        _behaviorCard.Controls.Add(_chkStartup);

        _behaviorCard.FitHeightToContent(12);
        page.InnerControls.Add(_behaviorCard);

        page.RecalculateScroll();
        return page;
    }
}