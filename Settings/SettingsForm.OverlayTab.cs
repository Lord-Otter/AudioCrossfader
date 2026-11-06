using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

public partial class SettingsForm : Form
{
    private DarkScrollPanel CreateOverlayPage()
    {
        DarkScrollPanel page = new DarkScrollPanel { Dock = DockStyle.Fill };
        int y = 4;

        _previewCard = new Win11CardPanel
        {
            Left = 18,
            Top = y,
            Width = 468
        };

        int py = 10;
        AddSectionHeader(_previewCard, "Preview", 14, ref py);

        ModernButton btnShowPreview = CreateModernButton("Test Overlay", 14, py, 155, 28, true);
        btnShowPreview.Click += (s, e) => PushLivePreviewIfActive(forceTrigger: true);
        _previewCard.Controls.Add(btnShowPreview);

        _chkKeepOverlayAlwaysOn = new CheckBox
        {
            Text = "Pin overlay while settings open",
            Checked = _draftSettings.KeepOverlayOnWhileSettingsOpen,
            Left = 185,
            Top = py + 3,
            Width = 265,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        _chkKeepOverlayAlwaysOn.CheckedChanged += (s, e) =>
        {
            _draftSettings.KeepOverlayOnWhileSettingsOpen = _chkKeepOverlayAlwaysOn.Checked;
            MarkDirty();
            _context.NotifySettingsWindowState(true, _draftSettings);
        };
        _previewCard.Controls.Add(_chkKeepOverlayAlwaysOn);

        _previewCard.FitHeightToContent(12);
        page.InnerControls.Add(_previewCard);
        y = _previewCard.Bottom + 10;

        _overlayStyleCard = new Win11CardPanel
        {
            Left = 18,
            Top = y,
            Width = 468
        };

        int sy = 10;
        AddSectionHeader(_overlayStyleCard, "Global Card Style", 14, ref sy);

        Label lblIdMode = new Label
        {
            Text = "Header Style:",
            Left = 14,
            Top = sy + 4,
            Width = 165,
            Height = 20,
            ForeColor = Color.FromArgb(205, 205, 205)
        };
        _overlayStyleCard.Controls.Add(lblIdMode);

        _cmbIdentityMode = new ModernComboBox
        {
            Left = 185,
            Top = sy,
            Width = 265,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbIdentityMode.Items.AddRange(new object[] { "Both", "Name", "Icon" });
        _cmbIdentityMode.SelectedItem = _draftSettings.OverlayIdentityMode;
        if (_cmbIdentityMode.SelectedIndex == -1) _cmbIdentityMode.SelectedIndex = 0;
        _cmbIdentityMode.SelectedIndexChanged += (s, e) =>
        {
            _draftSettings.OverlayIdentityMode = _cmbIdentityMode.SelectedItem?.ToString() ?? "Both";
            MarkDirty();
            PushLivePreviewIfActive();
        };
        _overlayStyleCard.Controls.Add(_cmbIdentityMode);
        sy += 32;

        Label lblLvlMode = new Label
        {
            Text = "Level Indicator:",
            Left = 14,
            Top = sy + 4,
            Width = 165,
            Height = 20,
            ForeColor = Color.FromArgb(205, 205, 205)
        };
        _overlayStyleCard.Controls.Add(lblLvlMode);

        _cmbLevelMode = new ModernComboBox
        {
            Left = 185,
            Top = sy,
            Width = 265,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbLevelMode.Items.AddRange(new object[] { "Both", "Bar", "Percentage" });
        _cmbLevelMode.SelectedItem = _draftSettings.OverlayLevelMode;
        if (_cmbLevelMode.SelectedIndex == -1) _cmbLevelMode.SelectedIndex = 0;
        _cmbLevelMode.SelectedIndexChanged += (s, e) =>
        {
            _draftSettings.OverlayLevelMode = _cmbLevelMode.SelectedItem?.ToString() ?? "Both";
            MarkDirty();
            PushLivePreviewIfActive();
        };
        _overlayStyleCard.Controls.Add(_cmbLevelMode);
        sy += 32;

        Label lblOpacity = new Label
        {
            Text = "Overlay Opacity:",
            Left = 14,
            Top = sy + 3,
            Width = 165,
            Height = 20,
            ForeColor = Color.FromArgb(205, 205, 205)
        };
        _overlayStyleCard.Controls.Add(lblOpacity);

        _sldOverlayOpacity = new ModernSlider
        {
            Minimum = 10,
            Maximum = 100,
            Value = Math.Clamp(_draftSettings.OverlayOpacity, 10, 100),
            ValueSuffix = "%",
            Left = 185,
            Top = sy,
            Width = 265
        };
        _sldOverlayOpacity.ValueChanged += (s, e) =>
        {
            if (_isInitializing) return;
            MarkDirty();
            PushLivePreviewIfActive();
        };
        _overlayStyleCard.Controls.Add(_sldOverlayOpacity);
        sy += 30;

        _unmutedColorBox = AddColorPickerRow(_overlayStyleCard, "Active Bar Color:", _draftSettings.UnmutedColor, (hex) =>
        {
            _draftSettings.UnmutedColor = hex;
            MarkDirty();
            PushLivePreviewIfActive();
        }, ref sy);

        _mutedColorBox = AddColorPickerRow(_overlayStyleCard, "Muted Bar Color:", _draftSettings.MutedColor, (hex) =>
        {
            _draftSettings.MutedColor = hex;
            MarkDirty();
            PushLivePreviewIfActive();
        }, ref sy);

        _chkUseSourceBgColors = new CheckBox
        {
            Text = "Tint background with source colors",
            Checked = _draftSettings.UseSourceBackgroundColors,
            Left = 14,
            Top = sy + 2,
            Width = 430,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        _chkUseSourceBgColors.CheckedChanged += (s, e) =>
        {
            _draftSettings.UseSourceBackgroundColors = _chkUseSourceBgColors.Checked;
            if (_chkFadeBgWithVolume != null) _chkFadeBgWithVolume.Enabled = _chkUseSourceBgColors.Checked;
            MarkDirty();
            PushLivePreviewIfActive();
        };
        _overlayStyleCard.Controls.Add(_chkUseSourceBgColors);
        sy += 26;

        _chkFadeBgWithVolume = new CheckBox
        {
            Text = "Dim background as volume lowers",
            Checked = _draftSettings.FadeBgColorWithVolume,
            Enabled = _draftSettings.UseSourceBackgroundColors,
            Left = 14,
            Top = sy + 2,
            Width = 430,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        _chkFadeBgWithVolume.CheckedChanged += (s, e) =>
        {
            _draftSettings.FadeBgColorWithVolume = _chkFadeBgWithVolume.Checked;
            MarkDirty();
            PushLivePreviewIfActive();
        };
        _overlayStyleCard.Controls.Add(_chkFadeBgWithVolume);

        _overlayStyleCard.FitHeightToContent(12);
        page.InnerControls.Add(_overlayStyleCard);

        page.RecalculateScroll();
        return page;
    }

    private void SyncDraftPersonalizationFields()
    {
        if (_isInitializing) return;
        int ParseInt(string text, int defaultVal) => int.TryParse(text, out int val) ? val : defaultVal;

        _draftSettings.OverlayPosition = _cmbPosition?.SelectedItem?.ToString() ?? _draftSettings.OverlayPosition;
        _draftSettings.PopupDurationMs = Math.Clamp(ParseInt(_txtPopupDuration?.Text ?? "2500", 2500), 500, 10000);
        _draftSettings.OverlayOpacity = _sldOverlayOpacity?.Value ?? _draftSettings.OverlayOpacity;
        _draftSettings.OverlayWidth = Math.Clamp(ParseInt(_txtWidth?.Text ?? "360", 360), 120, 2000);
        _draftSettings.OverlayHeight = Math.Clamp(ParseInt(_txtHeight?.Text ?? "80", 80), 50, 1000);
        _draftSettings.EnableInterpolation = _chkInterpolation?.Checked ?? true;
        _draftSettings.InterpolationSpeed = _sldInterpSpeed?.Value ?? _draftSettings.InterpolationSpeed;
        _draftSettings.StepSize = _sldStepSize?.Value ?? _draftSettings.StepSize;

        var firstCol = _draftSettings.Collections.FirstOrDefault();
        if (firstCol != null)
        {
            _draftSettings.ApplyCollectionToActiveOverlayFields(firstCol);
        }
    }

    private void PushLivePreviewIfActive(bool forceTrigger = false)
    {
        if (_isInitializing) return;
        SyncDraftPersonalizationFields();
        if (forceTrigger || _draftSettings.KeepOverlayOnWhileSettingsOpen)
        {
            _context.TriggerOverlayPreview(_draftSettings, isColorPickerLive: false, visualOnly: false, animateTransition: forceTrigger);
        }
    }

    private void StretchPersonalizationControls(int cardW)
    {
        if (_previewCard == null) return;

        _previewCard.Width = cardW;
        _overlayStyleCard.Width = cardW;
        _layoutCard.Width = cardW;
        _behaviorCard.Width = cardW;

        int rightStretchW = Math.Max(265, cardW - 185 - 18);

        _cmbIdentityMode.Width = rightStretchW;
        _cmbLevelMode.Width = rightStretchW;
        _sldOverlayOpacity.Width = rightStretchW;
        _unmutedColorBox.Width = rightStretchW;
        _mutedColorBox.Width = rightStretchW;
        _cmbPosition.Width = rightStretchW;
        if (_sldStepSize != null) _sldStepSize.Width = rightStretchW;
        if (_sldInterpSpeed != null) _sldInterpSpeed.Width = rightStretchW;

        if (_monitorRowsPanel != null)
        {
            int panelW = cardW - 32;
            _monitorRowsPanel.Width = panelW;
            int monComboW = Math.Max(160, panelW - 236);
            foreach (Control c in _monitorRowsPanel.Controls)
            {
                if (c is ModernComboBox cmb)
                {
                    cmb.Left = 236;
                    cmb.Width = monComboW;
                }
            }
        }
    }

    private void ReflowPersonalizationPage()
    {
        if (_previewCard != null && _overlayStyleCard != null)
        {
            _previewCard.Top = 4;
            _previewCard.FitHeightToContent(12);

            _overlayStyleCard.Top = _previewCard.Bottom + 10;
            _overlayStyleCard.FitHeightToContent(12);

            _pageOverlay?.RecalculateScroll();
        }

        if (_layoutCard != null && _behaviorCard != null)
        {
            _layoutCard.Top = 4;
            _layoutCard.FitHeightToContent(12);

            _behaviorCard.Top = _layoutCard.Bottom + 10;
            _behaviorCard.FitHeightToContent(12);

            _pageBehavior?.RecalculateScroll();
        }
    }
}