using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

public partial class SettingsForm : Form
{
    private static class PairCardLayout
    {
        public const int CardPaddingTop = 7;
        public const int CardPaddingBottom = 7;
        public const int RowHeight = 25;
        public const int RowVerticalGap = 6;
        public const int CardListGap = 6;

        public const int ComboOffsetY = 0;
        public const int ButtonOffsetY = 0;
        public const int SwatchOffsetY = 0;
        public const int LabelOffsetY = 4;
        public const int CheckBoxOffsetY = 2;

        public static int RowStep => RowHeight + RowVerticalGap;
        public static int ModeRowY => 0;
        public static int Source1RowY => RowStep;
        public static int TopBlockHeight => Source1RowY + RowHeight + 1;
        public static int DeleteButtonHeight => (Source1RowY + RowHeight) - ButtonOffsetY;

        public static int GetRow2PanelTop(bool isSingle) =>
            isSingle
                ? (CardPaddingTop + Source1RowY)
                : (CardPaddingTop + Source1RowY + RowStep);

        public static int GetCardHeight(bool isSingle)
        {
            int contentBottom = isSingle
                ? (CardPaddingTop + Source1RowY + RowHeight)
                : (GetRow2PanelTop(false) + RowHeight);
            return contentBottom + CardPaddingBottom;
        }
    }

    private Win11CardPanel _collectionsMasterCard = null!;
    private ModernButton _btnAddCollection = null!;
    private BufferedPanel _collectionsSlotsHost = null!;
    private bool _isCollectionAnimating = false;

    private sealed class CollectionCardContext
    {
        public PairCollection Collection = null!;
        public Win11CardPanel Card = null!;
        public TextBox NameBox = null!;
        public ModernButton AddPairBtn = null!;
        public ModernButton DeleteCollectionBtn = null!;
        public BufferedPanel PairsHost = null!;

        public BufferedPanel KeysHost = null!;
        public BufferedPanel KeysHeader = null!;
        public BufferedPanel KeysShutter = null!;
        public bool KeysExpanded = false;
        public bool KeysHovered = false;
        public float KeysAnimProgress = 0f;
        public int KeysExpandedHeight = 182;
        public System.Windows.Forms.Timer? KeysAnimTimer;

        public BufferedPanel LookHost = null!;
        public BufferedPanel LookHeader = null!;
        public BufferedPanel LookShutter = null!;
        public bool LookExpanded = false;
        public bool LookHovered = false;
        public float LookAnimProgress = 0f;
        public int LookExpandedHeight = 240;
        public System.Windows.Forms.Timer? LookAnimTimer;
        public Action? RefreshLookLayout;
    }

    private DarkScrollPanel CreateGeneralPage()
    {
        DarkScrollPanel page = new DarkScrollPanel { Dock = DockStyle.Fill };

        _collectionsMasterCard = new Win11CardPanel
        {
            Left = 18,
            Top = 4,
            Width = 468,
            Height = 56
        };

        Label lblTitle = new Label
        {
            Text = "Pair Collections",
            Left = 14,
            Top = 10,
            Width = 280,
            Height = 20,
            Font = new Font("Segoe UI Semibold", 9.75f, FontStyle.Bold),
            ForeColor = Color.FromArgb(96, 205, 255)
        };
        _collectionsMasterCard.Controls.Add(lblTitle);

        Label lblSubtitle = new Label
        {
            Text = "Create multiple carousels or single-pair setups with independent styles & hotkeys",
            Left = 14,
            Top = 30,
            Width = 315,
            Height = 16,
            Font = new Font("Segoe UI", 8.0f, FontStyle.Regular),
            ForeColor = Color.FromArgb(165, 165, 165)
        };
        _collectionsMasterCard.Controls.Add(lblSubtitle);

        _btnAddCollection = CreateModernButton("+ New Collection", 334, 11, 120, 26, true);
        _btnAddCollection.Click += (s, e) =>
        {
            if (_isCollectionAnimating || _isPairListAnimating) return;
            _draftSettings.EnsureCollectionsInitialized();
            var newCol = new PairCollection
            {
                Name = $"Collection {_draftSettings.Collections.Count + 1}",
                Pairs = new List<CrossfadeCyclePair>
                {
                    new CrossfadeCyclePair
                    {
                        Source1 = "",
                        Color1Hex = "#0E0466",
                        Source2 = "",
                        Color2Hex = "#DA043A",
                        ControlMode = "Volume"
                    }
                }
            };
            AnimateAddCollectionCard(newCol);
        };
        _collectionsMasterCard.Controls.Add(_btnAddCollection);

        _collectionsSlotsHost = new BufferedPanel
        {
            Left = 12,
            Top = 52,
            Width = 444,
            Height = 0,
            BackColor = Color.FromArgb(39, 39, 39)
        };
        _collectionsMasterCard.Controls.Add(_collectionsSlotsHost);
        page.InnerControls.Add(_collectionsMasterCard);

        return page;
    }

    private List<Win11CardPanel> GetOrderedCollectionCards()
    {
        if (_collectionsSlotsHost == null) return new List<Win11CardPanel>();
        return _collectionsSlotsHost.Controls
            .OfType<Win11CardPanel>()
            .OrderBy(c =>
            {
                if (c.Tag is CollectionCardContext ctx)
                {
                    int idx = _draftSettings.Collections.IndexOf(ctx.Collection);
                    if (idx >= 0) return idx;
                }
                return int.MaxValue;
            })
            .ToList();
    }

    private void PopulateCycleListUI()
    {
        if (_collectionsSlotsHost == null) return;
        _draftSettings.EnsureCollectionsInitialized();

        _collectionsSlotsHost.SuspendLayout();
        _collectionsSlotsHost.Controls.Clear();

        int hostW = Math.Max(442, _collectionsSlotsHost.Width);
        List<string> options = GetAvailableSourceOptions();
        int y = 0;

        foreach (var col in _draftSettings.Collections)
        {
            if (col.Pairs.Count == 0)
            {
                col.Pairs.Add(new CrossfadeCyclePair { ControlMode = "Volume" });
            }

            Win11CardPanel colCard = BuildSingleCollectionCard(col, options, hostW);
            colCard.Top = y;
            _collectionsSlotsHost.Controls.Add(colCard);
            y += colCard.Height + 10;
        }

        RefreshAllPairButtonsState();
        _collectionsSlotsHost.ResumeLayout();
        ReflowGeneralPage();
    }

    private Win11CardPanel BuildSingleCollectionCard(PairCollection col, List<string> options, int hostW)
    {
        var ctx = new CollectionCardContext { Collection = col };

        Win11CardPanel card = new Win11CardPanel
        {
            Left = 0,
            Top = 0,
            Width = hostW,
            FillColor = Color.FromArgb(43, 43, 45),
            BorderColor = Color.FromArgb(64, 64, 68),
            CornerRadius = 8,
            Padding = new Padding(0),
            Tag = ctx
        };
        ctx.Card = card;

        Label lblCol = new Label
        {
            Text = "Collection:",
            Left = 10,
            Top = 12,
            Width = 64,
            Height = 18,
            Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(96, 205, 255)
        };
        card.Controls.Add(lblCol);

        int btnDelW = 74;
        int btnAddPairW = 94;
        int btnDelLeft = hostW - 10 - btnDelW;
        int btnAddLeft = btnDelLeft - 6 - btnAddPairW;
        int nameBoxLeft = 76;
        int nameBoxW = Math.Max(100, btnAddLeft - nameBoxLeft - 8);

        TextBox txtName = new TextBox
        {
            Text = col.Name,
            Left = nameBoxLeft,
            Top = 9,
            Width = nameBoxW,
            Height = 25,
            BackColor = Color.FromArgb(34, 34, 36),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold)
        };
        txtName.TextChanged += (s, e) =>
        {
            if (_isInitializing) return;
            col.Name = txtName.Text.Trim();
            MarkDirty();
        };
        card.Controls.Add(txtName);
        ctx.NameBox = txtName;

        ModernButton btnAddPair = CreateModernButton("+ Add Pair", btnAddLeft, 8, btnAddPairW, 25, true);
        btnAddPair.Click += (s, e) =>
        {
            if (_isPairListAnimating || _isCollectionAnimating) return;
            var newPair = new CrossfadeCyclePair
            {
                Source1 = "",
                Color1Hex = "#0E0466",
                Source2 = "",
                Color2Hex = "#DA043A",
                ControlMode = "Volume"
            };
            AnimateAddPairCard(ctx, newPair);
        };
        card.Controls.Add(btnAddPair);
        ctx.AddPairBtn = btnAddPair;

        ModernButton btnDeleteCol = CreateModernButton("Delete", btnDelLeft, 8, btnDelW, 25, false, isDanger: true);
        btnDeleteCol.Click += (s, e) =>
        {
            if (_isCollectionAnimating || _isPairListAnimating) return;
            if (_draftSettings.Collections.Count <= 1)
            {
                MessageBox.Show("You must keep at least one pair collection.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string displayTitle = string.IsNullOrWhiteSpace(col.Name) ? "this collection" : $"\"{col.Name}\"";
            var confirm = MessageBox.Show(
                this,
                $"Are you sure you want to delete {displayTitle} and all of its pairs?",
                "Delete Collection?",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (confirm == DialogResult.Yes)
            {
                AnimateRemoveCollectionCard(col);
            }
        };
        card.Controls.Add(btnDeleteCol);
        ctx.DeleteCollectionBtn = btnDeleteCol;

        int innerW = hostW - 16;
        ctx.PairsHost = new BufferedPanel
        {
            Left = 8,
            Top = 40,
            Width = innerW,
            Height = 0,
            BackColor = Color.FromArgb(43, 43, 45)
        };

        int py = 0;
        for (int i = 0; i < col.Pairs.Count; i++)
        {
            Win11CardPanel pairCard = BuildSinglePairCard(ctx, col.Pairs[i], options, innerW);
            pairCard.Top = py;
            ctx.PairsHost.Controls.Add(pairCard);
            py += pairCard.Height + PairCardLayout.CardListGap;
        }
        ctx.PairsHost.Height = py;
        card.Controls.Add(ctx.PairsHost);

        BuildCollectionHotkeysShutter(ctx, innerW);
        card.Controls.Add(ctx.KeysHost);

        BuildCollectionAppearanceShutter(ctx, innerW);
        card.Controls.Add(ctx.LookHost);

        ReflowSingleCollectionCard(ctx);
        return card;
    }

    private void BuildCollectionHotkeysShutter(CollectionCardContext ctx, int innerW)
    {
        var col = ctx.Collection;
        ctx.KeysHost = new BufferedPanel
        {
            Left = 8,
            Top = 40,
            Width = innerW,
            Height = GlobalKeysHeaderHeight,
            BackColor = Color.FromArgb(43, 43, 45)
        };

        ctx.KeysHeader = new BufferedPanel
        {
            Left = 0,
            Top = 0,
            Width = innerW,
            Height = GlobalKeysHeaderHeight,
            BackColor = Color.FromArgb(43, 43, 45),
            Cursor = Cursors.Hand
        };
        ctx.KeysHeader.MouseEnter += (s, e) => { ctx.KeysHovered = true; ctx.KeysHeader.Invalidate(); };
        ctx.KeysHeader.MouseLeave += (s, e) => { ctx.KeysHovered = false; ctx.KeysHeader.Invalidate(); };
        ctx.KeysHeader.Click += (s, e) => ToggleCollectionShutter(ctx, isKeysShutter: true);
        ctx.KeysHeader.Paint += (s, e) => PaintShutterHeaderBar(
            e.Graphics,
            ctx.KeysHeader.Width,
            ctx.KeysHeader.Height,
            ctx.KeysExpanded ? "Collection Hotkeys (Click to bind, Esc to clear)" : "Collection Hotkeys",
            ctx.KeysExpanded,
            ctx.KeysHovered);

        ctx.KeysShutter = new BufferedPanel
        {
            Left = 0,
            Top = GlobalKeysHeaderHeight,
            Width = innerW,
            BackColor = Color.FromArgb(43, 43, 45)
        };

        int kbW = Math.Max(200, innerW - 142);
        int ky = 4;

        AddAlignedLabel(ctx.KeysShutter, "Fade Left / Down:", 2, ky);
        var kbDown = AddKeybindBoxControl(ctx.KeysShutter, 136, kbW, ref ky, val => col.KeyDown = val);
        kbDown.Binding = col.KeyDown;
        kbDown.Tag = "ColStretchControl";

        AddAlignedLabel(ctx.KeysShutter, "Fade Right / Up:", 2, ky);
        var kbUp = AddKeybindBoxControl(ctx.KeysShutter, 136, kbW, ref ky, val => col.KeyUp = val);
        kbUp.Binding = col.KeyUp;
        kbUp.Tag = "ColStretchControl";

        AddAlignedLabel(ctx.KeysShutter, "Mute Toggle:", 2, ky);
        var kbMute = AddKeybindBoxControl(ctx.KeysShutter, 136, kbW, ref ky, val => col.KeyMute = val);
        kbMute.Binding = col.KeyMute;
        kbMute.Tag = "ColStretchControl";

        AddAlignedLabel(ctx.KeysShutter, "Mute Behavior:", 2, ky);
        var cmbMute = new ModernComboBox
        {
            Left = 136,
            Top = ky,
            Width = kbW,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Tag = "ColStretchControl"
        };
        cmbMute.Items.AddRange(GlobalMuteModeOptions);
        cmbMute.SelectedItem = string.IsNullOrWhiteSpace(col.MuteMode) ? "Cycle R → L" : col.MuteMode;
        if (cmbMute.SelectedIndex == -1) cmbMute.SelectedIndex = 0;
        cmbMute.SelectedIndexChanged += (s, e) =>
        {
            if (_isInitializing) return;
            col.MuteMode = cmbMute.SelectedItem?.ToString() ?? "Cycle R → L";
            MarkDirty();
        };
        ctx.KeysShutter.Controls.Add(cmbMute);
        ky += 31;

        AddAlignedLabel(ctx.KeysShutter, "Next Pair:", 2, ky);
        var kbNext = AddKeybindBoxControl(ctx.KeysShutter, 136, kbW, ref ky, val => col.CycleNextKey = val);
        kbNext.Binding = col.CycleNextKey;
        kbNext.Tag = "ColStretchControl";

        AddAlignedLabel(ctx.KeysShutter, "Previous Pair:", 2, ky);
        var kbPrev = AddKeybindBoxControl(ctx.KeysShutter, 136, kbW, ref ky, val => col.CyclePrevKey = val);
        kbPrev.Binding = col.CyclePrevKey;
        kbPrev.Tag = "ColStretchControl";

        ctx.KeysExpandedHeight = ky + 4;
        ctx.KeysShutter.Height = ctx.KeysExpandedHeight;
        ctx.KeysShutter.Top = GlobalKeysHeaderHeight - ctx.KeysExpandedHeight;
        ctx.KeysShutter.Visible = false;

        ctx.KeysHost.Controls.Add(ctx.KeysShutter);
        ctx.KeysHost.Controls.Add(ctx.KeysHeader);
        ctx.KeysHeader.BringToFront();
    }

    private void BuildCollectionAppearanceShutter(CollectionCardContext ctx, int innerW)
    {
        var col = ctx.Collection;
        ctx.LookHost = new BufferedPanel
        {
            Left = 8,
            Top = 70,
            Width = innerW,
            Height = GlobalKeysHeaderHeight,
            BackColor = Color.FromArgb(43, 43, 45)
        };

        ctx.LookHeader = new BufferedPanel
        {
            Left = 0,
            Top = 0,
            Width = innerW,
            Height = GlobalKeysHeaderHeight,
            BackColor = Color.FromArgb(43, 43, 45),
            Cursor = Cursors.Hand
        };
        ctx.LookHeader.MouseEnter += (s, e) => { ctx.LookHovered = true; ctx.LookHeader.Invalidate(); };
        ctx.LookHeader.MouseLeave += (s, e) => { ctx.LookHovered = false; ctx.LookHeader.Invalidate(); };
        ctx.LookHeader.Click += (s, e) => ToggleCollectionShutter(ctx, isKeysShutter: false);
        ctx.LookHeader.Paint += (s, e) => PaintShutterHeaderBar(
            e.Graphics,
            ctx.LookHeader.Width,
            ctx.LookHeader.Height,
            "Carousel Appearance && Behavior",
            ctx.LookExpanded,
            ctx.LookHovered);

        ctx.LookShutter = new BufferedPanel
        {
            Left = 0,
            Top = GlobalKeysHeaderHeight,
            Width = innerW,
            BackColor = Color.FromArgb(43, 43, 45)
        };

        int rightW = Math.Max(200, innerW - 142);
        int ly = 4;

        Label lblMode = AddAlignedLabel(ctx.LookShutter, "Carousel Mode:", 2, ly);
        ModernComboBox cmbMode = new ModernComboBox
        {
            Left = 136,
            Top = ly,
            Width = rightW,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Tag = "ColStretchControl"
        };
        cmbMode.Items.AddRange(new object[] { "None", "Full", "Compact", "Minimal" });
        cmbMode.SelectedItem = string.IsNullOrWhiteSpace(col.CarouselMode) ? "Full" : col.CarouselMode;
        if (cmbMode.SelectedIndex == -1) cmbMode.SelectedIndex = 1;
        ctx.LookShutter.Controls.Add(cmbMode);
        ly += 31;

        Label lblStyle = AddAlignedLabel(ctx.LookShutter, "Switch Style:", 2, ly);
        ModernComboBox cmbStyle = new ModernComboBox
        {
            Left = 136,
            Top = ly,
            Width = rightW,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Tag = "ColStretchControl"
        };
        ctx.LookShutter.Controls.Add(cmbStyle);
        ly += 31;

        CheckBox chkAlwaysPreviews = new CheckBox
        {
            Text = "Always show idle previews",
            Checked = col.AlwaysShowPreviews,
            Left = 4,
            Top = ly,
            Width = 200,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        ctx.LookShutter.Controls.Add(chkAlwaysPreviews);

        CheckBox chkAnim = new CheckBox
        {
            Text = "Smooth switch animations",
            Checked = col.EnableCarouselAnimation,
            Left = 210,
            Top = ly,
            Width = 210,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        ctx.LookShutter.Controls.Add(chkAnim);
        ly += 26;

        CheckBox chkBg = new CheckBox
        {
            Text = "Carousel backdrop",
            Checked = col.CarouselBackgroundEnabled,
            Left = 4,
            Top = ly,
            Width = 130,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        ctx.LookShutter.Controls.Add(chkBg);

        Label lblBgCol = new Label
        {
            Text = "Backdrop Color:",
            Left = 136,
            Top = ly + 2,
            Width = 96,
            Height = 20,
            ForeColor = Color.FromArgb(205, 205, 205)
        };
        ctx.LookShutter.Controls.Add(lblBgCol);

        RoundedColorSwatch swatchBackdrop = new RoundedColorSwatch
        {
            Left = 236,
            Top = ly,
            Width = Math.Max(100, innerW - 242),
            Height = 22,
            CornerRadius = 6,
            SwatchColor = SafeParseHexColor(col.CarouselBackdropColor, Color.FromArgb(15, 15, 18)),
            Tag = "ColBackdropSwatch"
        };
        swatchBackdrop.Click += (s, e) =>
        {
            Color originalColor = swatchBackdrop.SwatchColor;
            string originalHex = ColorTranslator.ToHtml(originalColor);
            bool isFirstOpen = true;

            void PreviewCollectionBackdrop(Color liveCol, bool keepPinned)
            {
                string liveHex = $"#{liveCol.R:X2}{liveCol.G:X2}{liveCol.B:X2}";
                swatchBackdrop.SwatchColor = liveCol;
                swatchBackdrop.Invalidate();
                col.CarouselBackdropColor = liveHex;
                TriggerCollectionLivePreview(col, forceTrigger: true, keepPinned: keepPinned, animateTransition: isFirstOpen);
                isFirstOpen = false;
            }

            using var dlg = new ModernColorPickerDialog(originalColor, _draftSettings, liveCol => PreviewCollectionBackdrop(liveCol, keepPinned: true));
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                swatchBackdrop.SwatchColor = dlg.SelectedColor;
                swatchBackdrop.Invalidate();
                col.CarouselBackdropColor = dlg.SelectedHex;
                MarkDirty();
                PreviewCollectionBackdrop(dlg.SelectedColor, keepPinned: false);
            }
            else
            {
                swatchBackdrop.SwatchColor = originalColor;
                swatchBackdrop.Invalidate();
                col.CarouselBackdropColor = originalHex;
                PreviewCollectionBackdrop(originalColor, keepPinned: false);
            }
        };
        ctx.LookShutter.Controls.Add(swatchBackdrop);
        ly += 28;

        Label lblOpenSpeed = AddAlignedLabel(ctx.LookShutter, "Open Speed:", 2, ly);
        ModernSlider sldOpenSpeed = new ModernSlider
        {
            Minimum = 5,
            Maximum = 100,
            Value = Math.Clamp(col.CarouselOpenSpeed, 5, 100),
            Left = 136,
            Top = ly,
            Width = rightW,
            Tag = "ColStretchControl"
        };
        ctx.LookShutter.Controls.Add(sldOpenSpeed);
        ly += 29;

        Label lblScrollSpeed = AddAlignedLabel(ctx.LookShutter, "Scroll Speed:", 2, ly);
        ModernSlider sldScrollSpeed = new ModernSlider
        {
            Minimum = 5,
            Maximum = 100,
            Value = Math.Clamp(col.CarouselAnimationSpeed, 5, 100),
            Left = 136,
            Top = ly,
            Width = rightW,
            Tag = "ColStretchControl"
        };
        ctx.LookShutter.Controls.Add(sldScrollSpeed);
        ly += 29;

        CheckBox chkInvertScroll = new CheckBox
        {
            Text = "Invert carousel scroll direction",
            Checked = col.InvertCarouselScroll,
            Left = 4,
            Top = ly,
            Width = 300,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        ctx.LookShutter.Controls.Add(chkInvertScroll);
        ly += 26;

        Label lblCardOrder = AddAlignedLabel(ctx.LookShutter, "Card Order:", 2, ly);
        ModernComboBox cmbOrder = new ModernComboBox
        {
            Left = 136,
            Top = ly,
            Width = rightW,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Tag = "ColStretchControl"
        };
        cmbOrder.Items.AddRange(new object[] { "Ascending", "Descending" });
        cmbOrder.SelectedItem = string.IsNullOrWhiteSpace(col.CardOrderDirection) ? "Ascending" : col.CardOrderDirection;
        if (cmbOrder.SelectedIndex == -1) cmbOrder.SelectedIndex = 0;
        ctx.LookShutter.Controls.Add(cmbOrder);

        void UpdateLookShutterLayout()
        {
            string mode = cmbMode.SelectedItem?.ToString() ?? "Full";
            bool isNone = mode == "None";
            bool isFull = mode == "Full";
            bool isCompact = mode == "Compact";

            bool prevInit = _isInitializing;
            _isInitializing = true;

            if (!isNone)
            {
                cmbStyle.Items.Clear();
                if (isFull)
                {
                    cmbStyle.Items.AddRange(CarouselStyleStateMachine.FullDisplayNames);
                    cmbStyle.SelectedItem = string.IsNullOrWhiteSpace(col.LastFullCarouselStyle) ? "3D Floating Pills" : col.LastFullCarouselStyle;
                }
                else if (isCompact)
                {
                    cmbStyle.Items.AddRange(CarouselStyleStateMachine.CompactDisplayNames);
                    cmbStyle.SelectedItem = string.IsNullOrWhiteSpace(col.LastCompactCarouselStyle) ? "Top/Bottom Preview Morph" : col.LastCompactCarouselStyle;
                }
                else
                {
                    cmbStyle.Items.AddRange(CarouselStyleStateMachine.MinimalDisplayNames);
                    cmbStyle.SelectedItem = string.IsNullOrWhiteSpace(col.LastMinimalCarouselStyle) ? "Side Preview Morph" : col.LastMinimalCarouselStyle;
                }
                if (cmbStyle.SelectedIndex == -1 && cmbStyle.Items.Count > 0)
                    cmbStyle.SelectedIndex = 0;

                var st = CarouselStyleStateMachine.ParseState(cmbStyle.SelectedItem?.ToString(), mode);
                string stName = CarouselStyleStateMachine.ToDisplayName(st);
                col.CarouselMotionStyle = stName;
                if (isFull) col.LastFullCarouselStyle = stName;
                else if (isCompact) col.LastCompactCarouselStyle = stName;
                else col.LastMinimalCarouselStyle = stName;

                bool canAnim = !CarouselStyleStateMachine.IsInstantCut(st);
                chkAnim.Enabled = canAnim;
                sldOpenSpeed.Enabled = canAnim && chkAnim.Checked;
                sldScrollSpeed.Enabled = canAnim && chkAnim.Checked;
            }
            _isInitializing = prevInit;

            lblStyle.Visible = !isNone;
            cmbStyle.Visible = !isNone;
            chkAlwaysPreviews.Visible = !isNone;
            chkAnim.Visible = !isNone;
            chkBg.Visible = isFull;
            lblBgCol.Visible = !isNone;
            swatchBackdrop.Visible = !isNone;
            lblOpenSpeed.Visible = isFull;
            sldOpenSpeed.Visible = isFull;
            lblScrollSpeed.Visible = !isNone;
            sldScrollSpeed.Visible = !isNone;
            chkInvertScroll.Visible = !isNone;
            lblCardOrder.Visible = !isNone;
            cmbOrder.Visible = !isNone;

            int ry = cmbMode.Bottom + 6;
            if (!isNone)
            {
                lblStyle.Top = ry + 3;
                cmbStyle.Top = ry;
                ry += 31;

                chkAlwaysPreviews.Top = ry;
                chkAnim.Top = ry;
                ry += 26;

                if (isFull)
                {
                    chkBg.Top = ry;
                    lblBgCol.Left = 136;
                    lblBgCol.Top = ry + 2;
                    swatchBackdrop.Left = 236;
                    swatchBackdrop.Width = Math.Max(100, ctx.LookShutter.Width - 242);
                    swatchBackdrop.Top = ry;
                }
                else
                {
                    lblBgCol.Left = 2;
                    lblBgCol.Top = ry + 2;
                    swatchBackdrop.Left = 136;
                    swatchBackdrop.Width = Math.Max(100, ctx.LookShutter.Width - 142);
                    swatchBackdrop.Top = ry;
                }
                ry += 28;

                if (isFull)
                {
                    lblOpenSpeed.Top = ry + 3;
                    sldOpenSpeed.Top = ry;
                    ry += 29;
                }

                lblScrollSpeed.Top = ry + 3;
                sldScrollSpeed.Top = ry;
                ry += 29;

                chkInvertScroll.Top = ry;
                ry += 26;

                lblCardOrder.Top = ry + 3;
                cmbOrder.Top = ry;
                ry += 29;
            }

            ctx.LookExpandedHeight = ry + 4;
            ctx.LookShutter.Height = ctx.LookExpandedHeight;
            if (ctx.LookExpanded)
            {
                ctx.LookShutter.Top = GlobalKeysHeaderHeight;
                ctx.LookHost.Height = GlobalKeysHeaderHeight + ctx.LookExpandedHeight;
            }
            else
            {
                ctx.LookShutter.Top = GlobalKeysHeaderHeight - ctx.LookExpandedHeight;
                ctx.LookHost.Height = GlobalKeysHeaderHeight;
            }

            ReflowSingleCollectionCard(ctx);
            ReflowGeneralPage();
        }

        ctx.RefreshLookLayout = UpdateLookShutterLayout;

        cmbMode.SelectedIndexChanged += (s, e) =>
        {
            if (_isInitializing) return;
            col.CarouselMode = cmbMode.SelectedItem?.ToString() ?? "Full";
            UpdateLookShutterLayout();
            MarkDirty();
            TriggerCollectionLivePreview(col);
        };

        cmbStyle.SelectedIndexChanged += (s, e) =>
        {
            if (_isInitializing) return;
            string mode = cmbMode.SelectedItem?.ToString() ?? "Full";
            var st = CarouselStyleStateMachine.ParseState(cmbStyle.SelectedItem?.ToString(), mode);
            string stName = CarouselStyleStateMachine.ToDisplayName(st);
            col.CarouselMotionStyle = stName;
            if (mode == "Full") col.LastFullCarouselStyle = stName;
            else if (mode == "Compact") col.LastCompactCarouselStyle = stName;
            else if (mode == "Minimal") col.LastMinimalCarouselStyle = stName;

            bool canAnim = !CarouselStyleStateMachine.IsInstantCut(st);
            chkAnim.Enabled = canAnim;
            sldOpenSpeed.Enabled = canAnim && chkAnim.Checked;
            sldScrollSpeed.Enabled = canAnim && chkAnim.Checked;
            MarkDirty();
            TriggerCollectionLivePreview(col, forceTrigger: true);
        };

        chkAlwaysPreviews.CheckedChanged += (s, e) =>
        {
            if (_isInitializing) return;
            col.AlwaysShowPreviews = chkAlwaysPreviews.Checked;
            MarkDirty();
            TriggerCollectionLivePreview(col);
        };

        chkAnim.CheckedChanged += (s, e) =>
        {
            if (_isInitializing) return;
            col.EnableCarouselAnimation = chkAnim.Checked;
            string mode = cmbMode.SelectedItem?.ToString() ?? "Full";
            var st = CarouselStyleStateMachine.ParseState(cmbStyle.SelectedItem?.ToString(), mode);
            bool canAnim = chkAnim.Checked && !CarouselStyleStateMachine.IsInstantCut(st);
            sldOpenSpeed.Enabled = canAnim;
            sldScrollSpeed.Enabled = canAnim;
            MarkDirty();
            TriggerCollectionLivePreview(col);
        };

        chkBg.CheckedChanged += (s, e) =>
        {
            if (_isInitializing) return;
            col.CarouselBackgroundEnabled = chkBg.Checked;
            MarkDirty();
            TriggerCollectionLivePreview(col);
        };

        sldOpenSpeed.ValueChanged += (s, e) =>
        {
            if (_isInitializing) return;
            col.CarouselOpenSpeed = sldOpenSpeed.Value;
            MarkDirty();
            TriggerCollectionLivePreview(col);
        };

        sldScrollSpeed.ValueChanged += (s, e) =>
        {
            if (_isInitializing) return;
            col.CarouselAnimationSpeed = sldScrollSpeed.Value;
            MarkDirty();
            TriggerCollectionLivePreview(col);
        };

        chkInvertScroll.CheckedChanged += (s, e) =>
        {
            if (_isInitializing) return;
            col.InvertCarouselScroll = chkInvertScroll.Checked;
            MarkDirty();
            TriggerCollectionLivePreview(col);
        };

        cmbOrder.SelectedIndexChanged += (s, e) =>
        {
            if (_isInitializing) return;
            col.CardOrderDirection = cmbOrder.SelectedItem?.ToString() ?? "Ascending";
            MarkDirty();
            TriggerCollectionLivePreview(col);
        };

        UpdateLookShutterLayout();
        ctx.LookShutter.Visible = false;

        ctx.LookHost.Controls.Add(ctx.LookShutter);
        ctx.LookHost.Controls.Add(ctx.LookHeader);
        ctx.LookHeader.BringToFront();
    }

    private static void PaintShutterHeaderBar(Graphics g, int width, int height, string titleText, bool isExpanded, bool isHovered)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(43, 43, 45));

        using Pen sepPen = new Pen(Color.FromArgb(64, 64, 68), 1);
        g.DrawLine(sepPen, 2, 2, width - 4, 2);

        if (isHovered)
        {
            using var hoverPath = Win11CardPanel.CreateRoundRect(new Rectangle(0, 5, width - 1, height - 6), 5);
            using var hoverBrush = new SolidBrush(Color.FromArgb(52, 52, 55));
            g.FillPath(hoverBrush, hoverPath);
        }

        Color titleColor = isHovered
            ? Color.FromArgb(130, 220, 255)
            : Color.FromArgb(96, 205, 255);

        using Font titleFont = new Font("Segoe UI Semibold", 9.0f, FontStyle.Bold);
        TextRenderer.DrawText(
            g,
            titleText,
            titleFont,
            new Rectangle(4, 6, width - 36, 22),
            titleColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

        float cx = width - 14f;
        float cy = 17f;
        using SolidBrush arrowBrush = new SolidBrush(titleColor);
        PointF[] triangle = isExpanded
            ? new PointF[] { new PointF(cx - 5f, cy + 2.5f), new PointF(cx + 5f, cy + 2.5f), new PointF(cx, cy - 3.5f) }
            : new PointF[] { new PointF(cx - 5f, cy - 2.5f), new PointF(cx + 5f, cy - 2.5f), new PointF(cx, cy + 3.5f) };
        g.FillPolygon(arrowBrush, triangle);
    }

    private void ToggleCollectionShutter(CollectionCardContext ctx, bool isKeysShutter)
    {
        CancelRecording();

        if (isKeysShutter)
        {
            ctx.KeysExpanded = !ctx.KeysExpanded;
            ctx.KeysHeader.Invalidate();

            float startProg = ctx.KeysAnimProgress;
            float targetProg = ctx.KeysExpanded ? 1f : 0f;
            ctx.KeysShutter.Visible = true;
            ctx.KeysHeader.BringToFront();

            ctx.KeysAnimTimer?.Stop();
            ctx.KeysAnimTimer?.Dispose();

            const double durationMs = 210.0;
            Stopwatch sw = Stopwatch.StartNew();
            ctx.KeysAnimTimer = new System.Windows.Forms.Timer { Interval = 15 };
            ctx.KeysAnimTimer.Tick += (s, e) =>
            {
                if (ctx.Card.IsDisposed)
                {
                    ctx.KeysAnimTimer.Stop();
                    ctx.KeysAnimTimer.Dispose();
                    return;
                }
                double t = Math.Clamp(sw.Elapsed.TotalMilliseconds / durationMs, 0.0, 1.0);
                float ease = (float)(1.0 - Math.Pow(1.0 - t, 3.0));

                if (t >= 1.0)
                {
                    ctx.KeysAnimProgress = targetProg;
                    ctx.KeysShutter.Top = GlobalKeysHeaderHeight - (int)Math.Round(ctx.KeysExpandedHeight * (1f - targetProg));
                    ctx.KeysHost.Height = GlobalKeysHeaderHeight + (int)Math.Round(ctx.KeysExpandedHeight * targetProg);
                    ctx.KeysShutter.Visible = ctx.KeysExpanded;
                    ctx.KeysAnimTimer.Stop();
                    ctx.KeysAnimTimer.Dispose();
                }
                else
                {
                    ctx.KeysAnimProgress = startProg + (targetProg - startProg) * ease;
                    ctx.KeysShutter.Top = GlobalKeysHeaderHeight - (int)Math.Round(ctx.KeysExpandedHeight * (1f - ctx.KeysAnimProgress));
                    ctx.KeysHost.Height = GlobalKeysHeaderHeight + (int)Math.Round(ctx.KeysExpandedHeight * ctx.KeysAnimProgress);
                }

                ReflowSingleCollectionCard(ctx);
                ReflowGeneralPage();
            };
            ctx.KeysAnimTimer.Start();
        }
        else
        {
            ctx.LookExpanded = !ctx.LookExpanded;
            ctx.LookHeader.Invalidate();

            float startProg = ctx.LookAnimProgress;
            float targetProg = ctx.LookExpanded ? 1f : 0f;
            ctx.LookShutter.Visible = true;
            ctx.LookHeader.BringToFront();

            ctx.LookAnimTimer?.Stop();
            ctx.LookAnimTimer?.Dispose();

            const double durationMs = 210.0;
            Stopwatch sw = Stopwatch.StartNew();
            ctx.LookAnimTimer = new System.Windows.Forms.Timer { Interval = 15 };
            ctx.LookAnimTimer.Tick += (s, e) =>
            {
                if (ctx.Card.IsDisposed)
                {
                    ctx.LookAnimTimer.Stop();
                    ctx.LookAnimTimer.Dispose();
                    return;
                }
                double t = Math.Clamp(sw.Elapsed.TotalMilliseconds / durationMs, 0.0, 1.0);
                float ease = (float)(1.0 - Math.Pow(1.0 - t, 3.0));

                if (t >= 1.0)
                {
                    ctx.LookAnimProgress = targetProg;
                    ctx.LookShutter.Top = GlobalKeysHeaderHeight - (int)Math.Round(ctx.LookExpandedHeight * (1f - targetProg));
                    ctx.LookHost.Height = GlobalKeysHeaderHeight + (int)Math.Round(ctx.LookExpandedHeight * targetProg);
                    ctx.LookShutter.Visible = ctx.LookExpanded;
                    ctx.LookAnimTimer.Stop();
                    ctx.LookAnimTimer.Dispose();
                }
                else
                {
                    ctx.LookAnimProgress = startProg + (targetProg - startProg) * ease;
                    ctx.LookShutter.Top = GlobalKeysHeaderHeight - (int)Math.Round(ctx.LookExpandedHeight * (1f - ctx.LookAnimProgress));
                    ctx.LookHost.Height = GlobalKeysHeaderHeight + (int)Math.Round(ctx.LookExpandedHeight * ctx.LookAnimProgress);
                }

                ReflowSingleCollectionCard(ctx);
                ReflowGeneralPage();
            };
            ctx.LookAnimTimer.Start();
        }
    }

    private void ReflowSingleCollectionCard(CollectionCardContext ctx)
    {
        ctx.PairsHost.Top = 40;
        ctx.KeysHost.Top = ctx.PairsHost.Bottom + 2;
        ctx.LookHost.Top = ctx.KeysHost.Bottom + 2;
        ctx.Card.Height = ctx.LookHost.Bottom + 8;
    }

    private Win11CardPanel BuildSinglePairCard(
        CollectionCardContext ctx,
        CrossfadeCyclePair pair,
        List<string> options,
        int hostW)
    {
        var targetList = ctx.Collection.Pairs;
        bool isSingle = pair.IsSingleMode;

        int comboW = Math.Max(140, hostW - 234);
        int btnActionLeft = 56 + comboW + 6;
        int btnArrowLeft = btnActionLeft + 94 + 4;
        int btnDelLeft = btnArrowLeft + 30 + 4;

        int cardH = PairCardLayout.GetCardHeight(isSingle);

        Win11CardPanel pairCard = new Win11CardPanel
        {
            Left = 0,
            Top = 0,
            Width = hostW,
            Height = cardH,
            FillColor = Color.FromArgb(52, 52, 55),
            BorderColor = Color.FromArgb(70, 70, 74),
            CornerRadius = 6,
            Padding = new Padding(0),
            Tag = pair
        };

        BufferedPanel topBlockPanel = new BufferedPanel
        {
            Left = 2,
            Top = PairCardLayout.CardPaddingTop,
            Width = hostW - 4,
            Height = PairCardLayout.TopBlockHeight,
            BackColor = Color.FromArgb(52, 52, 55),
            Tag = "TopBlockPanel"
        };

        int r1Y = PairCardLayout.ModeRowY;

        Label lblMode = new Label
        {
            Text = "Mode:",
            Left = 6,
            Top = r1Y + PairCardLayout.LabelOffsetY,
            Width = 46,
            Height = 18,
            Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(190, 190, 190)
        };
        topBlockPanel.Controls.Add(lblMode);

        int invertW = 62;
        int modeComboW = Math.Max(95, comboW - invertW - 6);

        ModernComboBox cmbMode = new ModernComboBox
        {
            Left = 56,
            Top = r1Y + PairCardLayout.ComboOffsetY,
            Width = modeComboW,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Tag = "ModeCombo"
        };
        cmbMode.Items.AddRange(new object[] { "Volume", "Balance", "Crossfade" });
        cmbMode.SelectedItem = pair.IsSingleMode ? "Volume" : (pair.IsCrossfadeMode ? "Crossfade" : "Balance");
        topBlockPanel.Controls.Add(cmbMode);

        CheckBox chkInvert = new CheckBox
        {
            Text = "Invert",
            Checked = pair.InvertDirection,
            Left = 56 + modeComboW + 6,
            Top = r1Y + PairCardLayout.CheckBoxOffsetY,
            Width = invertW,
            Height = 22,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Regular),
            ForeColor = Color.FromArgb(215, 215, 215),
            Tag = "InvertChk"
        };
        _toolTip.SetToolTip(chkInvert, "Invert Volume Up / Down direction for this pair");
        chkInvert.CheckedChanged += (s, e) =>
        {
            if (_isInitializing) return;
            pair.InvertDirection = chkInvert.Checked;
            MarkDirty();
        };
        topBlockPanel.Controls.Add(chkInvert);

        ModernButton btnUp = CreateModernButton("▲", btnArrowLeft, r1Y + PairCardLayout.ButtonOffsetY, 30, 25, false);
        btnUp.Tag = "UpBtn";
        btnUp.Click += (s, e) =>
        {
            int curIdx = targetList.IndexOf(pair);
            if (_isPairListAnimating || curIdx <= 0) return;
            AnimateSwapPairCards(ctx.PairsHost, targetList, curIdx, curIdx - 1);
        };
        topBlockPanel.Controls.Add(btnUp);

        int r2Y = PairCardLayout.Source1RowY;

        Label lblS1 = new Label
        {
            Text = isSingle ? "S:" : "L:",
            Left = 6,
            Top = r2Y + PairCardLayout.LabelOffsetY,
            Width = 18,
            Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(190, 190, 190)
        };
        topBlockPanel.Controls.Add(lblS1);

        RoundedColorSwatch swatch1 = CreateCompactColorSwatch(
            26,
            r2Y + PairCardLayout.SwatchOffsetY,
            24,
            24,
            pair.Color1Hex,
            hex => pair.Color1Hex = hex,
            pair,
            ctx.Collection);
        topBlockPanel.Controls.Add(swatch1);

        ModernComboBox cmb1 = new ModernComboBox
        {
            Left = 56,
            Top = r2Y + PairCardLayout.ComboOffsetY,
            Width = comboW,
            DropDownStyle = ComboBoxStyle.DropDown,
            Tag = "Source1Combo",
            BadgeResolver = ResolveDropdownBadge,
            IconResolver = ResolveDropdownIcon
        };
        cmb1.Items.AddRange(options.Cast<object>().ToArray());
        cmb1.SelectedItem = pair.Source1;
        if (cmb1.SelectedIndex == -1) cmb1.Text = pair.Source1;
        cmb1.DropDown += (s, e) =>
        {
            _detectedAudio = AudioProcessDetector.Scan();
            PopulateSingleComboOptions(cmb1, includeGroups: true);
        };
        cmb1.SelectedIndexChanged += (s, e) =>
        {
            if (_isInitializing) return;
            pair.Source1 = ReadComboText(cmb1);
            MarkDirty();
            TriggerCollectionLivePreview(ctx.Collection, targetPair: pair);
        };
        cmb1.TextChanged += (s, e) =>
        {
            if (_isInitializing) return;
            pair.Source1 = ReadComboText(cmb1);
            MarkDirty();
        };
        topBlockPanel.Controls.Add(cmb1);

        ModernButton btnBrowse1 = CreateModernButton("Browse", btnActionLeft, r2Y + PairCardLayout.ButtonOffsetY, 94, 25, false);
        btnBrowse1.Tag = "Browse1Btn";
        btnBrowse1.Click += (s, e) =>
        {
            BrowseForExecutable(cmb1);
            pair.Source1 = ReadComboText(cmb1);
            TriggerCollectionLivePreview(ctx.Collection, targetPair: pair);
        };
        topBlockPanel.Controls.Add(btnBrowse1);

        ModernButton btnDown = CreateModernButton("▼", btnArrowLeft, r2Y + PairCardLayout.ButtonOffsetY, 30, 25, false);
        btnDown.Tag = "DownBtn";
        btnDown.Click += (s, e) =>
        {
            int curIdx = targetList.IndexOf(pair);
            if (_isPairListAnimating || curIdx < 0 || curIdx >= targetList.Count - 1) return;
            AnimateSwapPairCards(ctx.PairsHost, targetList, curIdx, curIdx + 1);
        };
        topBlockPanel.Controls.Add(btnDown);

        ModernButton btnDel = CreateModernButton(
            "✕",
            btnDelLeft,
            r1Y + PairCardLayout.ButtonOffsetY,
            32,
            PairCardLayout.DeleteButtonHeight,
            false,
            isDanger: true);
        btnDel.Tag = "DeleteBtn";
        btnDel.Click += (s, e) =>
        {
            int curIdx = targetList.IndexOf(pair);
            if (_isPairListAnimating || curIdx < 0) return;
            if (targetList.Count <= 1)
            {
                MessageBox.Show("A collection must have at least one source pair. Use the Delete button at the top of the collection to remove the entire collection.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            AnimateRemovePairCard(ctx, curIdx);
        };
        topBlockPanel.Controls.Add(btnDel);

        BufferedPanel row2Panel = new BufferedPanel
        {
            Left = 2,
            Top = PairCardLayout.GetRow2PanelTop(isSingle),
            Width = hostW - 4,
            Height = PairCardLayout.RowHeight + 2,
            Visible = !isSingle,
            BackColor = Color.FromArgb(52, 52, 55),
            Tag = "Row2Panel"
        };

        Label lblS2 = new Label
        {
            Text = "R:",
            Left = 6,
            Top = PairCardLayout.LabelOffsetY,
            Width = 18,
            Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(190, 190, 190)
        };
        row2Panel.Controls.Add(lblS2);

        RoundedColorSwatch swatch2 = CreateCompactColorSwatch(
            26,
            PairCardLayout.SwatchOffsetY,
            24,
            24,
            pair.Color2Hex,
            hex => pair.Color2Hex = hex,
            pair,
            ctx.Collection);
        row2Panel.Controls.Add(swatch2);

        ModernComboBox cmb2 = new ModernComboBox
        {
            Left = 56,
            Top = PairCardLayout.ComboOffsetY,
            Width = comboW,
            DropDownStyle = ComboBoxStyle.DropDown,
            Tag = "Source2Combo",
            BadgeResolver = ResolveDropdownBadge,
            IconResolver = ResolveDropdownIcon
        };
        cmb2.Items.AddRange(options.Cast<object>().ToArray());
        cmb2.SelectedItem = pair.Source2;
        if (cmb2.SelectedIndex == -1) cmb2.Text = pair.Source2;
        cmb2.DropDown += (s, e) =>
        {
            _detectedAudio = AudioProcessDetector.Scan();
            PopulateSingleComboOptions(cmb2, includeGroups: true);
        };
        cmb2.SelectedIndexChanged += (s, e) =>
        {
            if (_isInitializing) return;
            pair.Source2 = ReadComboText(cmb2);
            MarkDirty();
            TriggerCollectionLivePreview(ctx.Collection, targetPair: pair);
        };
        cmb2.TextChanged += (s, e) =>
        {
            if (_isInitializing) return;
            pair.Source2 = ReadComboText(cmb2);
            MarkDirty();
        };
        row2Panel.Controls.Add(cmb2);

        ModernButton btnBrowse2 = CreateModernButton("Browse", btnActionLeft, PairCardLayout.ButtonOffsetY, 94, 25, false);
        btnBrowse2.Tag = "Browse2Btn";
        btnBrowse2.Click += (s, e) =>
        {
            BrowseForExecutable(cmb2);
            pair.Source2 = ReadComboText(cmb2);
            TriggerCollectionLivePreview(ctx.Collection, targetPair: pair);
        };
        row2Panel.Controls.Add(btnBrowse2);

        var modeFsm = new StateMachine<ControlModeState>(pair.ModeState);
        modeFsm.ConfigureState(ControlModeState.Volume, onEnter: () =>
        {
            pair.ModeState = ControlModeState.Volume;
            lblS1.Text = "S:";
            AnimatePairCardModeChange(ctx, pairCard, topBlockPanel, row2Panel, true);
        });
        modeFsm.ConfigureState(ControlModeState.Balance, onEnter: () =>
        {
            pair.ModeState = ControlModeState.Balance;
            lblS1.Text = "L:";
            AnimatePairCardModeChange(ctx, pairCard, topBlockPanel, row2Panel, false);
        });
        modeFsm.ConfigureState(ControlModeState.Crossfade, onEnter: () =>
        {
            pair.ModeState = ControlModeState.Crossfade;
            lblS1.Text = "L:";
            AnimatePairCardModeChange(ctx, pairCard, topBlockPanel, row2Panel, false);
        });

        cmbMode.SelectedIndexChanged += (s, e) =>
        {
            if (_isInitializing) return;
            ControlModeState nextMode = ControlModeStateMachine.ParseState(cmbMode.SelectedItem?.ToString());
            if (modeFsm.TransitionTo(nextMode))
            {
                MarkDirty();
                TriggerCollectionLivePreview(ctx.Collection, targetPair: pair);
            }
        };

        pairCard.Controls.Add(row2Panel);
        pairCard.Controls.Add(topBlockPanel);
        topBlockPanel.BringToFront();

        return pairCard;
    }

    private void TriggerCollectionLivePreview(
        PairCollection col,
        CrossfadeCyclePair? targetPair = null,
        bool forceTrigger = false,
        bool keepPinned = false,
        bool animateTransition = true)
    {
        if (_isInitializing) return;
        SyncDraftPersonalizationFields();
        if (!forceTrigger && !keepPinned && !_draftSettings.KeepOverlayOnWhileSettingsOpen)
            return;

        var previewDraft = _draftSettings.Clone();
        previewDraft.ApplyCollectionToActiveOverlayFields(col, targetPair);
        _context.TriggerOverlayPreview(
            previewDraft,
            isColorPickerLive: keepPinned,
            visualOnly: true,
            animateTransition: animateTransition);
    }

    private void ResizeExistingCollectionCards(int cardW)
    {
        if (_collectionsMasterCard == null || _collectionsSlotsHost == null) return;

        _collectionsMasterCard.Width = cardW;
        _btnAddCollection.Left = cardW - 14 - _btnAddCollection.Width;

        int hostW = Math.Max(442, cardW - 24);
        _collectionsSlotsHost.Width = hostW;

        int btnDelW = 74;
        int btnAddPairW = 94;
        int btnDelLeft = hostW - 10 - btnDelW;
        int btnAddLeft = btnDelLeft - 6 - btnAddPairW;
        int nameBoxLeft = 76;
        int nameBoxW = Math.Max(100, btnAddLeft - nameBoxLeft - 8);
        int innerW = hostW - 16;

        foreach (Win11CardPanel colCard in GetOrderedCollectionCards())
        {
            if (colCard.Tag is not CollectionCardContext ctx) continue;
            colCard.Width = hostW;
            ctx.NameBox.Left = nameBoxLeft;
            ctx.NameBox.Width = nameBoxW;
            ctx.AddPairBtn.Left = btnAddLeft;
            ctx.DeleteCollectionBtn.Left = btnDelLeft;

            ctx.PairsHost.Width = innerW;
            ResizeExistingPairCards(ctx.PairsHost);

            ctx.KeysHost.Width = innerW;
            ctx.KeysHeader.Width = innerW;
            ctx.KeysHeader.Invalidate();
            ctx.KeysShutter.Width = innerW;
            int rightW = Math.Max(200, innerW - 142);
            foreach (Control c in ctx.KeysShutter.Controls)
            {
                if (c.Tag as string == "ColStretchControl") c.Width = rightW;
            }

            ctx.LookHost.Width = innerW;
            ctx.LookHeader.Width = innerW;
            ctx.LookHeader.Invalidate();
            ctx.LookShutter.Width = innerW;
            foreach (Control c in ctx.LookShutter.Controls)
            {
                if (c.Tag as string == "ColStretchControl") c.Width = rightW;
            }
            ctx.RefreshLookLayout?.Invoke();
            ReflowSingleCollectionCard(ctx);
        }

        ReflowGeneralPage();
    }

    private void ResizeExistingPairCards(Panel? host)
    {
        if (host == null) return;
        host.SuspendLayout();
        try
        {
            int hostW = Math.Max(420, host.Width);
            int comboW = Math.Max(140, hostW - 234);
            int btnActionLeft = 56 + comboW + 6;
            int btnArrowLeft = btnActionLeft + 94 + 4;
            int btnDelLeft = btnArrowLeft + 30 + 4;
            int invertW = 62;
            int modeComboW = Math.Max(95, comboW - invertW - 6);

            foreach (Control ctrl in host.Controls)
            {
                if (ctrl is not Win11CardPanel pairCard) continue;
                pairCard.Width = hostW;
                foreach (Control subPanel in pairCard.Controls)
                {
                    if (subPanel is not Panel p) continue;
                    p.Width = hostW - 4;
                    foreach (Control child in p.Controls)
                    {
                        if (child.Tag is not string tag) continue;
                        switch (tag)
                        {
                            case "Source1Combo":
                            case "Source2Combo":
                                child.Width = comboW;
                                break;
                            case "ModeCombo":
                                child.Width = modeComboW;
                                break;
                            case "InvertChk":
                                child.Left = 56 + modeComboW + 6;
                                break;
                            case "Browse1Btn":
                            case "Browse2Btn":
                                child.Left = btnActionLeft;
                                break;
                            case "UpBtn":
                            case "DownBtn":
                                child.Left = btnArrowLeft;
                                break;
                            case "DeleteBtn":
                                child.Left = btnDelLeft;
                                break;
                        }
                    }
                }
            }
        }
        finally
        {
            host.ResumeLayout(true);
        }
    }

    private void AnimateAddCollectionCard(PairCollection newCol)
    {
        if (_collectionsSlotsHost == null) return;

        var existingOrdered = GetOrderedCollectionCards();
        int finalTop = 0;
        foreach (var c in existingOrdered)
        {
            finalTop = Math.Max(finalTop, c.Bottom + 10);
        }

        _draftSettings.Collections.Add(newCol);
        int hostW = Math.Max(442, _collectionsSlotsHost.Width);
        List<string> options = GetAvailableSourceOptions();

        Win11CardPanel newCard = BuildSingleCollectionCard(newCol, options, hostW);
        int finalCardH = newCard.Height;
        int startHostH = finalTop;
        int finalHostH = finalTop + finalCardH + 10;

        _collectionsSlotsHost.SuspendLayout();
        newCard.Top = finalTop;
        newCard.Height = 0;
        _collectionsSlotsHost.Controls.Add(newCard);
        _collectionsSlotsHost.Height = Math.Max(1, startHostH);
        _collectionsSlotsHost.Visible = true;
        RefreshAllPairButtonsState();
        _collectionsSlotsHost.ResumeLayout(false);

        MarkDirty();
        ReflowGeneralPage();

        _isCollectionAnimating = true;
        const double durationMs = 200.0;
        Stopwatch sw = Stopwatch.StartNew();
        var timer = new System.Windows.Forms.Timer { Interval = 15 };

        timer.Tick += (s, e) =>
        {
            if (newCard.IsDisposed || _collectionsSlotsHost.IsDisposed)
            {
                _isCollectionAnimating = false;
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
                _collectionsSlotsHost.Height = finalHostH;
                _isCollectionAnimating = false;
                timer.Stop();
                timer.Dispose();
            }
            else
            {
                newCard.Top = finalTop;
                newCard.Height = Math.Max(1, (int)Math.Round(finalCardH * ease));
                _collectionsSlotsHost.Height = startHostH + (int)Math.Round((finalHostH - startHostH) * ease);
            }

            ReflowGeneralPage();
        };
        timer.Start();
    }

    private void AnimateRemoveCollectionCard(PairCollection colToRemove)
    {
        if (_collectionsSlotsHost == null) return;
        var orderedCards = GetOrderedCollectionCards();
        int removeIndex = _draftSettings.Collections.IndexOf(colToRemove);
        if (removeIndex < 0) return;

        Win11CardPanel? removingCard = orderedCards.FirstOrDefault(c => c.Tag is CollectionCardContext ctx && ReferenceEquals(ctx.Collection, colToRemove));
        if (removingCard == null) return;

        int startCardH = removingCard.Height;
        int shiftDelta = startCardH + 10;

        List<(Win11CardPanel ctrl, int startY, int targetY)> trailingCards = new();
        for (int i = removeIndex + 1; i < orderedCards.Count; i++)
        {
            var tc = orderedCards[i];
            trailingCards.Add((tc, tc.Top, tc.Top - shiftDelta));
        }

        int startHostH = _collectionsSlotsHost.Height;
        int targetHostH = Math.Max(0, startHostH - shiftDelta);

        _isCollectionAnimating = true;
        const double durationMs = 200.0;
        Stopwatch sw = Stopwatch.StartNew();
        var timer = new System.Windows.Forms.Timer { Interval = 15 };

        timer.Tick += (s, e) =>
        {
            if (_collectionsSlotsHost.IsDisposed)
            {
                _isCollectionAnimating = false;
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

                _collectionsSlotsHost.SuspendLayout();
                _draftSettings.Collections.Remove(colToRemove);
                _collectionsSlotsHost.Controls.Remove(removingCard);
                removingCard.Dispose();

                foreach (var item in trailingCards)
                    item.ctrl.Top = item.targetY;

                _collectionsSlotsHost.Height = targetHostH;
                RefreshAllPairButtonsState();
                _collectionsSlotsHost.ResumeLayout(false);

                _isCollectionAnimating = false;
                ReflowGeneralPage();
                MarkDirty();
                PushLivePreviewIfActive();
                return;
            }

            removingCard.Height = Math.Max(0, (int)Math.Round(startCardH * (1f - ease)));
            foreach (var item in trailingCards)
                item.ctrl.Top = item.startY + (int)Math.Round((item.targetY - item.startY) * ease);

            _collectionsSlotsHost.Height = startHostH + (int)Math.Round((targetHostH - startHostH) * ease);
            ReflowGeneralPage();
        };
        timer.Start();
    }

    private void AnimatePairCardModeChange(
        CollectionCardContext ctx,
        Win11CardPanel pairCard,
        Panel topBlockPanel,
        Panel row2Panel,
        bool toSingleMode)
    {
        topBlockPanel.BringToFront();

        int startCardH = pairCard.Height;
        int targetCardH = PairCardLayout.GetCardHeight(toSingleMode);
        int startRow2Top = row2Panel.Top;
        int targetRow2Top = PairCardLayout.GetRow2PanelTop(toSingleMode);

        row2Panel.Visible = true;

        const double durationMs = 120.0;
        Stopwatch sw = Stopwatch.StartNew();
        var animTimer = new System.Windows.Forms.Timer { Interval = 15 };
        animTimer.Tick += (s, e) =>
        {
            if (pairCard.IsDisposed)
            {
                animTimer.Stop();
                animTimer.Dispose();
                return;
            }

            double t = Math.Clamp(sw.Elapsed.TotalMilliseconds / durationMs, 0.0, 1.0);
            float ease = (float)(1.0 - Math.Pow(1.0 - t, 3.0));

            if (t >= 1.0)
            {
                pairCard.Height = targetCardH;
                row2Panel.Top = targetRow2Top;
                row2Panel.Visible = !toSingleMode;
                animTimer.Stop();
                animTimer.Dispose();
            }
            else
            {
                pairCard.Height = startCardH + (int)Math.Round((targetCardH - startCardH) * ease);
                row2Panel.Top = startRow2Top + (int)Math.Round((targetRow2Top - startRow2Top) * ease);
            }

            int curY = 0;
            foreach (Control sibling in ctx.PairsHost.Controls.Cast<Control>().OrderBy(c => c.Top))
            {
                sibling.Top = curY;
                curY += sibling.Height + PairCardLayout.CardListGap;
            }
            ctx.PairsHost.Height = curY;
            ReflowSingleCollectionCard(ctx);
            ReflowGeneralPage();
        };
        animTimer.Start();
    }

    private void AnimateAddPairCard(CollectionCardContext ctx, CrossfadeCyclePair newPair)
    {
        Panel host = ctx.PairsHost;
        var targetList = ctx.Collection.Pairs;
        targetList.Add(newPair);

        int hostW = Math.Max(420, host.Width);
        List<string> options = GetAvailableSourceOptions();

        Control? prevCard = null;
        int finalTop = 0;
        foreach (Control c in host.Controls)
        {
            if (prevCard == null || c.Top >= prevCard.Top) prevCard = c;
            if (c.Bottom + 6 > finalTop) finalTop = c.Bottom + 6;
        }

        Win11CardPanel newCard = BuildSinglePairCard(ctx, newPair, options, hostW);
        int finalCardH = newCard.Height;
        int startTop = prevCard != null ? prevCard.Top : -finalCardH;
        int startHostH = Math.Max(0, finalTop);
        int finalHostH = finalTop + finalCardH + 6;

        host.SuspendLayout();
        newCard.Top = startTop;
        host.Controls.Add(newCard);
        if (prevCard != null) prevCard.BringToFront();
        host.Height = startHostH;
        RefreshAllPairButtonsState();
        host.ResumeLayout();

        MarkDirty();
        ReflowSingleCollectionCard(ctx);
        ReflowGeneralPage();

        _isPairListAnimating = true;
        const double durationMs = 220.0;
        Stopwatch sw = Stopwatch.StartNew();
        var timer = new System.Windows.Forms.Timer { Interval = 15 };

        timer.Tick += (s, e) =>
        {
            if (newCard.IsDisposed || host.IsDisposed)
            {
                _isPairListAnimating = false;
                timer.Stop();
                timer.Dispose();
                return;
            }

            double t = Math.Clamp(sw.Elapsed.TotalMilliseconds / durationMs, 0.0, 1.0);
            float ease = (float)(1.0 - Math.Pow(1.0 - t, 3.0));

            if (t >= 1.0)
            {
                newCard.Top = finalTop;
                host.Height = finalHostH;
                _isPairListAnimating = false;
                timer.Stop();
                timer.Dispose();
            }
            else
            {
                newCard.Top = startTop + (int)Math.Round((finalTop - startTop) * ease);
                host.Height = startHostH + (int)Math.Round((finalHostH - startHostH) * ease);
            }

            ReflowSingleCollectionCard(ctx);
            ReflowGeneralPage();
        };
        timer.Start();
    }

    private void AnimateRemovePairCard(CollectionCardContext ctx, int removeIndex)
    {
        Panel host = ctx.PairsHost;
        var targetList = ctx.Collection.Pairs;
        if (removeIndex < 0 || removeIndex >= targetList.Count) return;

        CrossfadeCyclePair pairToRemove = targetList[removeIndex];
        CrossfadeCyclePair? pairAbove = removeIndex > 0 ? targetList[removeIndex - 1] : null;

        Control? removingCard = host.Controls.Cast<Control>().FirstOrDefault(c => ReferenceEquals(c.Tag, pairToRemove));
        if (removingCard == null) return;

        Control? cardAbove = pairAbove != null
            ? host.Controls.Cast<Control>().FirstOrDefault(c => ReferenceEquals(c.Tag, pairAbove))
            : null;

        if (cardAbove != null) cardAbove.BringToFront();
        else removingCard.SendToBack();

        int shiftDelta = removingCard.Height + 6;
        int startRemoveTop = removingCard.Top;
        int targetRemoveTop = cardAbove != null ? cardAbove.Top : -removingCard.Height;

        List<(Control ctrl, int startY, int targetY)> trailingCards = new();
        for (int i = removeIndex + 1; i < targetList.Count; i++)
        {
            var trailingPair = targetList[i];
            Control? c = host.Controls.Cast<Control>().FirstOrDefault(ctrl => ReferenceEquals(ctrl.Tag, trailingPair));
            if (c != null) trailingCards.Add((c, c.Top, c.Top - shiftDelta));
        }

        int startHostH = host.Height;
        int targetHostH = Math.Max(0, startHostH - shiftDelta);

        _isPairListAnimating = true;
        const double durationMs = 210.0;
        Stopwatch sw = Stopwatch.StartNew();
        var timer = new System.Windows.Forms.Timer { Interval = 15 };

        timer.Tick += (s, e) =>
        {
            if (host.IsDisposed)
            {
                _isPairListAnimating = false;
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

                host.SuspendLayout();
                targetList.RemoveAt(removeIndex);
                host.Controls.Remove(removingCard);
                removingCard.Dispose();

                foreach (var item in trailingCards) item.ctrl.Top = item.targetY;
                host.Height = targetHostH;
                RefreshAllPairButtonsState();
                host.ResumeLayout();

                _isPairListAnimating = false;
                ReflowSingleCollectionCard(ctx);
                ReflowGeneralPage();
                MarkDirty();
                TriggerCollectionLivePreview(ctx.Collection);
                return;
            }

            removingCard.Top = startRemoveTop + (int)Math.Round((targetRemoveTop - startRemoveTop) * ease);
            foreach (var item in trailingCards)
                item.ctrl.Top = item.startY + (int)Math.Round((item.targetY - item.startY) * ease);
            host.Height = startHostH + (int)Math.Round((targetHostH - startHostH) * ease);
            ReflowSingleCollectionCard(ctx);
            ReflowGeneralPage();
        };
        timer.Start();
    }

    private void AnimateSwapPairCards(Panel? host, List<CrossfadeCyclePair> targetList, int indexA, int indexB)
    {
        if (host == null || indexA < 0 || indexB < 0 ||
            indexA >= targetList.Count || indexB >= targetList.Count || indexA == indexB)
            return;

        int upperIdx = Math.Min(indexA, indexB);
        int lowerIdx = Math.Max(indexA, indexB);

        CrossfadeCyclePair upperPair = targetList[upperIdx];
        CrossfadeCyclePair lowerPair = targetList[lowerIdx];
        CrossfadeCyclePair initiatedPair = targetList[indexA];

        Control? upperCard = host.Controls.Cast<Control>().FirstOrDefault(c => ReferenceEquals(c.Tag, upperPair));
        Control? lowerCard = host.Controls.Cast<Control>().FirstOrDefault(c => ReferenceEquals(c.Tag, lowerPair));
        Control? initiatedCard = host.Controls.Cast<Control>().FirstOrDefault(c => ReferenceEquals(c.Tag, initiatedPair));

        if (upperCard == null || lowerCard == null || initiatedCard == null) return;

        initiatedCard.BringToFront();

        int upperStartTop = upperCard.Top;
        int lowerStartTop = lowerCard.Top;
        int upperTargetTop = upperStartTop + lowerCard.Height + 6;
        int lowerTargetTop = upperStartTop;

        _isPairListAnimating = true;
        const double durationMs = 220.0;
        Stopwatch sw = Stopwatch.StartNew();
        var timer = new System.Windows.Forms.Timer { Interval = 15 };

        timer.Tick += (s, e) =>
        {
            if (host.IsDisposed || upperCard.IsDisposed || lowerCard.IsDisposed)
            {
                _isPairListAnimating = false;
                timer.Stop();
                timer.Dispose();
                return;
            }

            double t = Math.Clamp(sw.Elapsed.TotalMilliseconds / durationMs, 0.0, 1.0);
            float ease = (float)(1.0 - Math.Pow(1.0 - t, 3.0));

            if (t >= 1.0)
            {
                upperCard.Top = upperTargetTop;
                lowerCard.Top = lowerTargetTop;
                timer.Stop();
                timer.Dispose();

                (targetList[indexA], targetList[indexB]) = (targetList[indexB], targetList[indexA]);
                RefreshAllPairButtonsState();
                _isPairListAnimating = false;
                MarkDirty();
                PushLivePreviewIfActive();
                return;
            }

            upperCard.Top = upperStartTop + (int)Math.Round((upperTargetTop - upperStartTop) * ease);
            lowerCard.Top = lowerStartTop + (int)Math.Round((lowerTargetTop - lowerStartTop) * ease);
            host.Invalidate();
        };
        timer.Start();
    }

    private void ReflowGeneralPage()
    {
        if (_collectionsMasterCard == null || _collectionsSlotsHost == null) return;

        if (!_isCollectionAnimating)
        {
            int y = 0;
            foreach (Win11CardPanel colCard in GetOrderedCollectionCards())
            {
                colCard.Top = y;
                y += colCard.Height + 10;
            }
            _collectionsSlotsHost.Height = y;
        }

        _collectionsSlotsHost.Visible = (_collectionsSlotsHost.Controls.Count > 0 && _collectionsSlotsHost.Height > 0);
        _collectionsMasterCard.Height = !_collectionsSlotsHost.Visible ? 56 : (54 + _collectionsSlotsHost.Height + 6);
        _pageGeneral?.RecalculateScroll();
    }

    private bool ValidateAndNormalizePairList(List<CrossfadeCyclePair> list, string listDisplayName)
    {
        for (int i = 0; i < list.Count; i++)
        {
            var pair = list[i];
            if (!string.IsNullOrWhiteSpace(pair.Source1) && !pair.Source1.StartsWith("Group: ", StringComparison.OrdinalIgnoreCase))
            {
                pair.Source1 = AppSettings.NormalizeExeName(pair.Source1);
                _draftSettings.RecordProgramHistory(pair.Source1);
            }
            if (!pair.IsSingleMode && !string.IsNullOrWhiteSpace(pair.Source2) && !pair.Source2.StartsWith("Group: ", StringComparison.OrdinalIgnoreCase))
            {
                pair.Source2 = AppSettings.NormalizeExeName(pair.Source2);
                _draftSettings.RecordProgramHistory(pair.Source2);
            }

            if (!pair.IsSingleMode && !string.IsNullOrWhiteSpace(pair.Source1) && !string.IsNullOrWhiteSpace(pair.Source2))
            {
                if (pair.Source1.Equals(pair.Source2, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show($"{listDisplayName} Pair #{i + 1} has the same source ({pair.Source1}) on both sides.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                var pairOverlaps = _draftSettings.GetOverlappingExecutables(pair.Source1, pair.Source2);
                if (pairOverlaps.Count > 0)
                {
                    string dupList = string.Join("\n• ", pairOverlaps);
                    MessageBox.Show(
                        $"{listDisplayName} Pair #{i + 1} ({pair.Source1} ⇄ {pair.Source2}) contains duplicate software on both sides:\n\n• {dupList}",
                        "Duplicate Software in Source Pair",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return false;
                }
            }
        }
        return true;
    }
}