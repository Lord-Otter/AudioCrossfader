using System.Drawing;

public enum SettingsTabState
{
    Sources = 0,
    Groups = 1,
    Overlay = 2,
    Behavior = 3
}

public enum ControlModeState
{
    Volume,
    Balance,
    Crossfade
}

public sealed class StateMachine<TState> where TState : struct, Enum
{
    private readonly Dictionary<TState, Action?> _onEnter = new();
    private readonly Dictionary<TState, Action?> _onExit = new();

    public TState CurrentState { get; private set; }

    public StateMachine(TState initialState)
    {
        CurrentState = initialState;
    }

    public void ConfigureState(TState state, Action? onEnter = null, Action? onExit = null)
    {
        _onEnter[state] = onEnter;
        _onExit[state] = onExit;
    }

    public bool TransitionTo(TState nextState, bool force = false)
    {
        if (!force && EqualityComparer<TState>.Default.Equals(CurrentState, nextState))
            return false;

        if (_onExit.TryGetValue(CurrentState, out var exitAction))
            exitAction?.Invoke();

        CurrentState = nextState;

        if (_onEnter.TryGetValue(CurrentState, out var enterAction))
            enterAction?.Invoke();

        return true;
    }
}

public static class ControlModeStateMachine
{
    public static ControlModeState ParseState(string? rawMode)
    {
        if (string.Equals(rawMode, "Volume", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rawMode, "Standard Volume", StringComparison.OrdinalIgnoreCase))
        {
            return ControlModeState.Volume;
        }

        if (string.Equals(rawMode, "Crossfade", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rawMode, "True Crossfade", StringComparison.OrdinalIgnoreCase))
        {
            return ControlModeState.Crossfade;
        }

        return ControlModeState.Balance;
    }

    public static string ToDisplayName(ControlModeState state) => state switch
    {
        ControlModeState.Volume => "Volume",
        ControlModeState.Crossfade => "Crossfade",
        _ => "Balance"
    };

    public static bool IsSingleSource(ControlModeState state) => state == ControlModeState.Volume;

    public static float GetBaseDirectionSign(ControlModeState state) =>
        state == ControlModeState.Volume ? 1.0f : -1.0f;

    public static int GetTargetCardHeight(ControlModeState state, bool isCarouselList) => state switch
    {
        ControlModeState.Volume => isCarouselList ? 68 : 144,
        _ => isCarouselList ? 100 : 176
    };

    public static (float vol1, float vol2) EvaluateVolumes(ControlModeState state, float position)
    {
        float pos = Math.Clamp(position, 0.0f, 1.0f);
        return state switch
        {
            ControlModeState.Volume => (pos, 0.0f),
            ControlModeState.Crossfade => (pos, 1.0f - pos),
            _ => (
                pos >= 0.5f ? 1.0f : (pos * 2.0f),
                pos >= 0.5f ? (1.0f - ((pos - 0.5f) * 2.0f)) : 1.0f
            )
        };
    }
}

public enum CarouselStyleState
{
    // Full Carousel Styles (Open Full Vertical Carousel)
    FloatingPills3D,
    FluidMovingDeck,
    CoverflowArc3D,
    StackedRolodex,
    CylinderFacets3D,

    // Compact Carousel Styles (Top & Bottom Peeking Previews + Vertical Transitions)
    CompactTopBottomMorph,
    CompactCardFlip,
    CompactVerticalClockFlip,
    CompactVerticalSwoosh,
    CompactVerticalLaserWipe,
    CompactVerticalSquashStretch,
    CompactVerticalInstantCut,

    // Minimal Carousel Styles (Left & Right Side Previews + In-Place Transitions)
    MinimalSidePreviewMorph,
    MinimalCardFlip,
    MinimalDirectionalSwoosh,
    MinimalLaserWipe,
    MinimalSquashAndStretch,
    MinimalInstantCut
}

public static class CarouselStyleStateMachine
{
    public static readonly object[] FullDisplayNames = new object[]
    {
        "3D Floating Pills",
        "Fluid Moving Deck",
        "3D Perspective Arc",
        "Stacked Rolodex",
        "3D Faceted Cylinder"
    };

    public static readonly object[] CompactDisplayNames = new object[]
    {
        "Top/Bottom Preview Morph",
        "Card Flip",
        "Split-Flap Clock Flip",
        "Vertical Swoosh",
        "Vertical Laser Wipe",
        "Vertical Squash && Stretch",
        "Instant Cut"
    };

    public static readonly object[] MinimalDisplayNames = new object[]
    {
        "Side Preview Morph",
        "Card Flip",
        "Directional Swoosh",
        "Laser Wipe Reveal",
        "Squash && Stretch Push",
        "Instant Cut"
    };

    public static CarouselStyleState ParseState(string? rawStyle, string? modeHint = null)
    {
        string mode = modeHint?.Trim() ?? "";
        bool preferCompact = mode.StartsWith("Compact", StringComparison.OrdinalIgnoreCase);
        bool preferMinimal = mode.StartsWith("Minimal", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(rawStyle))
        {
            if (preferCompact) return CarouselStyleState.CompactTopBottomMorph;
            if (preferMinimal) return CarouselStyleState.MinimalSidePreviewMorph;
            return CarouselStyleState.FloatingPills3D;
        }

        string s = rawStyle.Trim();

        // Full styles
        if (s.Contains("Floating Pills", StringComparison.OrdinalIgnoreCase))
            return CarouselStyleState.FloatingPills3D;
        if (s.Contains("Fluid Moving Deck", StringComparison.OrdinalIgnoreCase))
            return CarouselStyleState.FluidMovingDeck;
        if (s.Contains("Perspective Arc", StringComparison.OrdinalIgnoreCase) || s.Contains("Coverflow", StringComparison.OrdinalIgnoreCase))
            return CarouselStyleState.CoverflowArc3D;
        if (s.Contains("Stacked Rolodex", StringComparison.OrdinalIgnoreCase))
            return CarouselStyleState.StackedRolodex;
        if (s.Contains("Faceted Cylinder", StringComparison.OrdinalIgnoreCase) || s.Contains("Cylinder", StringComparison.OrdinalIgnoreCase))
            return CarouselStyleState.CylinderFacets3D;

        // Compact-specific vertical styles
        if (s.Contains("Top/Bottom", StringComparison.OrdinalIgnoreCase))
            return CarouselStyleState.CompactTopBottomMorph;
        if (s.Contains("Split-Flap", StringComparison.OrdinalIgnoreCase) || s.Contains("Clock Flip", StringComparison.OrdinalIgnoreCase))
            return preferMinimal ? CarouselStyleState.MinimalCardFlip : CarouselStyleState.CompactVerticalClockFlip;
        if (s.Contains("Vertical Swoosh", StringComparison.OrdinalIgnoreCase))
            return CarouselStyleState.CompactVerticalSwoosh;
        if (s.Contains("Vertical Laser", StringComparison.OrdinalIgnoreCase))
            return CarouselStyleState.CompactVerticalLaserWipe;
        if (s.Contains("Vertical Squash", StringComparison.OrdinalIgnoreCase))
            return CarouselStyleState.CompactVerticalSquashStretch;

        // Shared names between Compact and Minimal ("Card Flip", "Instant Cut")
        if (s.Contains("Card Flip", StringComparison.OrdinalIgnoreCase))
            return preferCompact ? CarouselStyleState.CompactCardFlip : CarouselStyleState.MinimalCardFlip;
        if (s.Contains("Instant Cut", StringComparison.OrdinalIgnoreCase))
            return preferCompact ? CarouselStyleState.CompactVerticalInstantCut : CarouselStyleState.MinimalInstantCut;

        // Minimal styles
        if (s.Contains("Side Preview Morph", StringComparison.OrdinalIgnoreCase) || s.Contains("Preview Morph", StringComparison.OrdinalIgnoreCase))
            return CarouselStyleState.MinimalSidePreviewMorph;
        if (s.Contains("Directional Swoosh", StringComparison.OrdinalIgnoreCase) || s.Contains("Swoosh", StringComparison.OrdinalIgnoreCase))
            return CarouselStyleState.MinimalDirectionalSwoosh;
        if (s.Contains("Laser Wipe", StringComparison.OrdinalIgnoreCase) || s.Contains("Line Wipe", StringComparison.OrdinalIgnoreCase))
            return CarouselStyleState.MinimalLaserWipe;
        if (s.Contains("Squash", StringComparison.OrdinalIgnoreCase) || s.Contains("Stretch", StringComparison.OrdinalIgnoreCase))
            return CarouselStyleState.MinimalSquashAndStretch;

        return preferCompact
            ? CarouselStyleState.CompactTopBottomMorph
            : (preferMinimal ? CarouselStyleState.MinimalSidePreviewMorph : CarouselStyleState.FloatingPills3D);
    }

    public static string ToDisplayName(CarouselStyleState state) => state switch
    {
        CarouselStyleState.FluidMovingDeck => "Fluid Moving Deck",
        CarouselStyleState.CoverflowArc3D => "3D Perspective Arc",
        CarouselStyleState.StackedRolodex => "Stacked Rolodex",
        CarouselStyleState.CylinderFacets3D => "3D Faceted Cylinder",

        CarouselStyleState.CompactTopBottomMorph => "Top/Bottom Preview Morph",
        CarouselStyleState.CompactCardFlip => "Card Flip",
        CarouselStyleState.CompactVerticalClockFlip => "Split-Flap Clock Flip",
        CarouselStyleState.CompactVerticalSwoosh => "Vertical Swoosh",
        CarouselStyleState.CompactVerticalLaserWipe => "Vertical Laser Wipe",
        CarouselStyleState.CompactVerticalSquashStretch => "Vertical Squash && Stretch",
        CarouselStyleState.CompactVerticalInstantCut => "Instant Cut",

        CarouselStyleState.MinimalSidePreviewMorph => "Side Preview Morph",
        CarouselStyleState.MinimalCardFlip => "Card Flip",
        CarouselStyleState.MinimalDirectionalSwoosh => "Directional Swoosh",
        CarouselStyleState.MinimalLaserWipe => "Laser Wipe Reveal",
        CarouselStyleState.MinimalSquashAndStretch => "Squash && Stretch Push",
        CarouselStyleState.MinimalInstantCut => "Instant Cut",

        _ => "3D Floating Pills"
    };

    public static bool IsWideCarousel(CarouselStyleState state) => state switch
    {
        CarouselStyleState.FloatingPills3D => true,
        CarouselStyleState.FluidMovingDeck => true,
        CarouselStyleState.CoverflowArc3D => true,
        CarouselStyleState.StackedRolodex => true,
        CarouselStyleState.CylinderFacets3D => true,
        _ => false
    };

    public static bool IsCompactCarousel(CarouselStyleState state) => state switch
    {
        CarouselStyleState.CompactTopBottomMorph => true,
        CarouselStyleState.CompactCardFlip => true,
        CarouselStyleState.CompactVerticalClockFlip => true,
        CarouselStyleState.CompactVerticalSwoosh => true,
        CarouselStyleState.CompactVerticalLaserWipe => true,
        CarouselStyleState.CompactVerticalSquashStretch => true,
        CarouselStyleState.CompactVerticalInstantCut => true,
        _ => false
    };

    public static bool IsMinimalCarousel(CarouselStyleState state) =>
        !IsWideCarousel(state) && !IsCompactCarousel(state);

    public static bool IsInstantCut(CarouselStyleState state) =>
        state == CarouselStyleState.CompactVerticalInstantCut ||
        state == CarouselStyleState.MinimalInstantCut;
}

public readonly struct BadgeInfo
{
    public string Text { get; }
    public Color Color { get; }

    public BadgeInfo(string text, Color color)
    {
        Text = text;
        Color = color;
    }
}

public class ProgramGroup
{
    public string GroupName { get; set; } = "";
    public string CustomIconPath { get; set; } = "";
    public List<string> Executables { get; set; } = new List<string>();

    public ProgramGroup Clone()
    {
        return new ProgramGroup
        {
            GroupName = this.GroupName,
            CustomIconPath = this.CustomIconPath,
            Executables = new List<string>(this.Executables)
        };
    }
}

public class MonitorOverlayConfig
{
    public string DeviceName { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string Position { get; set; } = "Bottom Center";

    public MonitorOverlayConfig Clone()
    {
        return new MonitorOverlayConfig
        {
            DeviceName = this.DeviceName,
            Enabled = this.Enabled,
            Position = this.Position
        };
    }
}

public class CrossfadeCyclePair
{
    public string Source1 { get; set; } = "";
    public string Color1Hex { get; set; } = "#0E0466";
    public string Source2 { get; set; } = "";
    public string Color2Hex { get; set; } = "#68021C";

    public string MuteMode { get; set; } = "Use Global";
    public bool UseCustomKeybinds { get; set; } = false;
    public int KeyUp { get; set; } = 0;
    public int KeyDown { get; set; } = 0;
    public int KeyMute { get; set; } = 0;

    public string ControlMode { get; set; } = "Volume";
    public bool InvertDirection { get; set; } = false;

    [System.Text.Json.Serialization.JsonIgnore]
    public ControlModeState ModeState
    {
        get => ControlModeStateMachine.ParseState(ControlMode);
        set => ControlMode = ControlModeStateMachine.ToDisplayName(value);
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsVolumeMode => ModeState == ControlModeState.Volume;

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsSingleMode => IsVolumeMode;

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsBalanceMode => ModeState == ControlModeState.Balance;

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsCrossfadeMode => ModeState == ControlModeState.Crossfade;

    public CrossfadeCyclePair Clone()
    {
        return new CrossfadeCyclePair
        {
            Source1 = this.Source1,
            Color1Hex = this.Color1Hex,
            Source2 = this.Source2,
            Color2Hex = this.Color2Hex,
            MuteMode = this.MuteMode,
            UseCustomKeybinds = this.UseCustomKeybinds,
            KeyUp = this.KeyUp,
            KeyDown = this.KeyDown,
            KeyMute = this.KeyMute,
            ControlMode = this.ControlMode,
            InvertDirection = this.InvertDirection
        };
    }
}

public readonly struct CycleDisplayItem
{
    public string Label1 { get; }
    public string RawSource1 { get; }
    public string Color1Hex { get; }
    public string Label2 { get; }
    public string RawSource2 { get; }
    public string Color2Hex { get; }
    public bool IsSingleSource { get; }

    public CycleDisplayItem(
        string label1, string rawSource1, string color1Hex,
        string label2, string rawSource2, string color2Hex,
        bool isSingleSource = false)
    {
        Label1 = label1;
        RawSource1 = rawSource1;
        Color1Hex = color1Hex;
        Label2 = label2;
        RawSource2 = rawSource2;
        Color2Hex = color2Hex;
        IsSingleSource = isSingleSource;
    }
}

public static class KeybindHelper
{
    public const int VkMask = 0x0000FFFF;

    public static int CreateBinding(Keys keyCode, Keys modifiers)
    {
        return (int)(keyCode & Keys.KeyCode) | (int)(modifiers & Keys.Modifiers);
    }

    public static int GetPrimaryVk(int binding) => binding & VkMask;

    public static bool HasCtrl(int binding) => ((Keys)binding & Keys.Control) == Keys.Control;
    public static bool HasAlt(int binding) => ((Keys)binding & Keys.Alt) == Keys.Alt;
    public static bool HasShift(int binding) => ((Keys)binding & Keys.Shift) == Keys.Shift;

    public static string FormatKeybind(int binding)
    {
        int vk = GetPrimaryVk(binding);
        if (vk == 0) return "None";

        List<string> parts = new List<string>();
        if (HasCtrl(binding)) parts.Add("Ctrl");
        if (HasAlt(binding)) parts.Add("Alt");
        if (HasShift(binding)) parts.Add("Shift");

        Keys key = (Keys)vk;
        string keyLabel = key switch
        {
            Keys.Up => "Up Arrow",
            Keys.Down => "Down Arrow",
            Keys.Left => "Left Arrow",
            Keys.Right => "Right Arrow",
            Keys.Space => "Space",
            Keys.Return => "Enter",
            Keys.Back => "Backspace",
            Keys.Tab => "Tab",
            Keys.Escape => "Escape",
            Keys.Prior => "Page Up",
            Keys.Next => "Page Down",
            Keys.Oemplus => "Plus (+)",
            Keys.OemMinus => "Minus (-)",
            _ => key.ToString()
        };

        parts.Add(keyLabel);
        return string.Join(" + ", parts);
    }

    public static string FormatLiveModifiers(Keys modifiers)
    {
        List<string> parts = new List<string>();
        if ((modifiers & Keys.Control) == Keys.Control) parts.Add("Ctrl");
        if ((modifiers & Keys.Alt) == Keys.Alt) parts.Add("Alt");
        if ((modifiers & Keys.Shift) == Keys.Shift) parts.Add("Shift");
        if (parts.Count == 0) return "Press keys... (Esc = Clear)";
        return string.Join(" + ", parts) + " + ...";
    }
}

public class AppSettings
{
    public const int MaxHistoryCount = 10;

    public string Source1 { get; set; } = "chrome.exe";
    public string Source1Color { get; set; } = "#1B3B5A";
    public string Source2 { get; set; } = "firefox.exe";
    public string Source2Color { get; set; } = "#4A2040";

    public bool FollowActiveMonitor { get; set; } = false;
    public List<MonitorOverlayConfig> MonitorConfigs { get; set; } = new List<MonitorOverlayConfig>();

    public List<ProgramGroup> Groups { get; set; } = new List<ProgramGroup>();

    public List<CrossfadeCyclePair> CrossfadeCycleList { get; set; } = new List<CrossfadeCyclePair>
    {
        new CrossfadeCyclePair { Source1 = "chrome.exe", Color1Hex = "#1B3B5A", Source2 = "firefox.exe", Color2Hex = "#4A2040" }
    };

    public List<CrossfadeCyclePair> StandalonePairs { get; set; } = new List<CrossfadeCyclePair>();

    public List<string> RecentPrograms { get; set; } = new List<string> { "chrome.exe", "firefox.exe" };
    public Dictionary<string, string> KnownExecutablePaths { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public bool RunOnStartup { get; set; } = false;

    public string GlobalMuteMode { get; set; } = "Cycle R → L";
    public int CrossfadeKeyUp { get; set; } = 0x7E;    // VK_F15
    public int CrossfadeKeyMute { get; set; } = 0x7F;  // VK_F16
    public int CrossfadeKeyDown { get; set; } = 0x80;  // VK_F17

    public int QuickSwapKey { get; set; } = 0;
    public int CycleNextKey { get; set; } = 0;
    public int CyclePrevKey { get; set; } = 0;

    public string UnmutedColor { get; set; } = "#00A8FF";
    public string MutedColor { get; set; } = "#FF4757";
    public List<string> PinnedCustomColors { get; set; } = new List<string>();
    public List<string> RecentCustomColors { get; set; } = new List<string>();

    public void RecordCustomColorHistory(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return;
        string norm = hex.Trim().ToUpperInvariant();
        if (!norm.StartsWith("#")) norm = "#" + norm;

        if (PinnedCustomColors.Any(c => c.Equals(norm, StringComparison.OrdinalIgnoreCase)))
            return;

        RecentCustomColors.RemoveAll(c => c.Equals(norm, StringComparison.OrdinalIgnoreCase));
        RecentCustomColors.Insert(0, norm);

        int maxRecent = Math.Max(0, 20 - PinnedCustomColors.Count);
        if (RecentCustomColors.Count > maxRecent)
        {
            RecentCustomColors.RemoveRange(maxRecent, RecentCustomColors.Count - maxRecent);
        }
    }

    public bool UseSourceBackgroundColors { get; set; } = true;
    public bool FadeBgColorWithVolume { get; set; } = true;
    public bool KeepOverlayOnWhileSettingsOpen { get; set; } = false;


    public string CarouselMode { get; set; } = "Full Carousel"; // "None", "Full Carousel", "Compact Carousel", "Minimal Carousel"
    public string CarouselMotionStyle { get; set; } = "3D Floating Pills";
    public string LastFullCarouselStyle { get; set; } = "3D Floating Pills";
    public string LastCompactCarouselStyle { get; set; } = "Top/Bottom Preview Morph";
    public string LastMinimalCarouselStyle { get; set; } = "Side Preview Morph";
    public bool InvertCarouselScroll { get; set; } = false;
    public string CardOrderDirection { get; set; } = "Ascending";
    public float CarouselVisibleNeighbors { get; set; } = 1.5f;
    public bool CarouselBlendColors { get; set; } = true;
    public bool EnableCarouselAnimation { get; set; } = true;
    public int CarouselOpenSpeed { get; set; } = 55;
    public int CarouselAnimationSpeed { get; set; } = 55;
    public bool CarouselBackgroundEnabled { get; set; } = true;
    public bool CompactShowNeighborPreviews { get; set; } = true;
    public string NeighborPreviewVisibility { get; set; } = "While Switching";

    public string OverlayPosition { get; set; } = "Bottom Center";
    public int PopupDurationMs { get; set; } = 2500;
    public int OverlayOpacity { get; set; } = 100;

    public int OverlayWidth { get; set; } = 360;
    public int OverlayHeight { get; set; } = 80;
    public bool EnableInterpolation { get; set; } = true;
    public int InterpolationSpeed { get; set; } = 45;
    public int StepSize { get; set; } = 10;
    public bool EnableEqualizationBuffer { get; set; } = true;

    public string OverlayIdentityMode { get; set; } = "Both";
    public string OverlayLevelMode { get; set; } = "Both";

    public int GetEffectiveCycleNextKey() => CycleNextKey != 0 ? CycleNextKey : QuickSwapKey;

    public string ActiveControlMode { get; set; } = "Volume";

    public IEnumerable<CrossfadeCyclePair> GetAllConfiguredPairs()
    {
        return CrossfadeCycleList.Concat(StandalonePairs);
    }

    public AppSettings Clone()
    {
        return new AppSettings
        {
            Source1 = this.Source1,
            Source1Color = this.Source1Color,
            Source2 = this.Source2,
            Source2Color = this.Source2Color,
            Groups = this.Groups.Select(g => g.Clone()).ToList(),
            CrossfadeCycleList = this.CrossfadeCycleList.Select(c => c.Clone()).ToList(),
            StandalonePairs = this.StandalonePairs.Select(c => c.Clone()).ToList(),
            RecentPrograms = new List<string>(this.RecentPrograms),
            KnownExecutablePaths = new Dictionary<string, string>(this.KnownExecutablePaths, StringComparer.OrdinalIgnoreCase),
            RunOnStartup = this.RunOnStartup,
            GlobalMuteMode = this.GlobalMuteMode,
            CrossfadeKeyUp = this.CrossfadeKeyUp,
            CrossfadeKeyMute = this.CrossfadeKeyMute,
            CrossfadeKeyDown = this.CrossfadeKeyDown,
            QuickSwapKey = this.QuickSwapKey,
            CycleNextKey = this.CycleNextKey,
            CyclePrevKey = this.CyclePrevKey,
            UnmutedColor = this.UnmutedColor,
            MutedColor = this.MutedColor,
            PinnedCustomColors = new List<string>(this.PinnedCustomColors),
            RecentCustomColors = new List<string>(this.RecentCustomColors),
            UseSourceBackgroundColors = this.UseSourceBackgroundColors,
            FadeBgColorWithVolume = this.FadeBgColorWithVolume,
            KeepOverlayOnWhileSettingsOpen = this.KeepOverlayOnWhileSettingsOpen,
            CarouselVisibleNeighbors = this.CarouselVisibleNeighbors,
            CarouselBlendColors = this.CarouselBlendColors,
            CarouselMotionStyle = this.CarouselMotionStyle,
            CarouselMode = this.CarouselMode,
            InvertCarouselScroll = this.InvertCarouselScroll,
            CardOrderDirection = this.CardOrderDirection,
            EnableCarouselAnimation = this.EnableCarouselAnimation,
            LastFullCarouselStyle = this.LastFullCarouselStyle,
            LastCompactCarouselStyle = this.LastCompactCarouselStyle,
            LastMinimalCarouselStyle = this.LastMinimalCarouselStyle,
            CarouselOpenSpeed = this.CarouselOpenSpeed,
            CarouselAnimationSpeed = this.CarouselAnimationSpeed,
            CarouselBackgroundEnabled = this.CarouselBackgroundEnabled,
            CompactShowNeighborPreviews = this.CompactShowNeighborPreviews,
            NeighborPreviewVisibility = this.NeighborPreviewVisibility,
            OverlayPosition = this.OverlayPosition,
            PopupDurationMs = this.PopupDurationMs,
            OverlayOpacity = this.OverlayOpacity,
            OverlayWidth = this.OverlayWidth,
            OverlayHeight = this.OverlayHeight,
            EnableInterpolation = this.EnableInterpolation,
            InterpolationSpeed = this.InterpolationSpeed,
            StepSize = this.StepSize,
            EnableEqualizationBuffer = this.EnableEqualizationBuffer,
            OverlayIdentityMode = this.OverlayIdentityMode,
            OverlayLevelMode = this.OverlayLevelMode,
            FollowActiveMonitor = this.FollowActiveMonitor,
            MonitorConfigs = this.MonitorConfigs.Select(m => m.Clone()).ToList(),
            ActiveControlMode = this.ActiveControlMode
        };
    }

    public static string NormalizeExeName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        string trimmed = raw.Trim();
        if (trimmed.StartsWith("Group: ", StringComparison.OrdinalIgnoreCase)) return trimmed;

        if (trimmed.Contains(Path.DirectorySeparatorChar) || trimmed.Contains(Path.AltDirectorySeparatorChar))
        {
            trimmed = Path.GetFileName(trimmed);
        }

        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? trimmed.ToLowerInvariant()
            : $"{trimmed.ToLowerInvariant()}.exe";
    }

    public static string NormalizeProcessKey(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        string trimmed = NormalizeExeName(raw);
        if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(0, trimmed.Length - 4);
        }
        return trimmed.Trim();
    }

    public void RecordProgramHistory(string? candidate, string? fullPath = null)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return;
        string trimmed = candidate.Trim();
        if (trimmed.StartsWith("Group: ", StringComparison.OrdinalIgnoreCase)) return;

        string normalized = NormalizeExeName(trimmed);
        if (string.IsNullOrEmpty(normalized)) return;

        if (!string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath))
        {
            KnownExecutablePaths[normalized] = fullPath;
            AppIconCache.RegisterKnownPath(normalized, fullPath);
        }

        RecentPrograms.RemoveAll(p => p.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        RecentPrograms.Insert(0, normalized);

        if (RecentPrograms.Count > MaxHistoryCount)
        {
            RecentPrograms.RemoveRange(MaxHistoryCount, RecentPrograms.Count - MaxHistoryCount);
        }
    }

    public List<string> ResolveExecutables(string targetIdentifier)
    {
        List<string> results = new List<string>();
        if (string.IsNullOrWhiteSpace(targetIdentifier)) return results;

        if (targetIdentifier.StartsWith("Group: ", StringComparison.OrdinalIgnoreCase))
        {
            string groupName = targetIdentifier.Substring(7).Trim();
            var group = Groups.FirstOrDefault(g => g.GroupName.Equals(groupName, StringComparison.OrdinalIgnoreCase));
            if (group != null)
            {
                foreach (var exe in group.Executables)
                {
                    string norm = NormalizeExeName(exe);
                    if (!string.IsNullOrEmpty(norm)) results.Add(norm);
                }
            }
        }
        else
        {
            string norm = NormalizeExeName(targetIdentifier);
            if (!string.IsNullOrEmpty(norm)) results.Add(norm);
        }

        return results.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public List<string> GetOverlappingExecutables(string source1, string source2)
    {
        var list1 = ResolveExecutables(source1);
        var list2 = ResolveExecutables(source2);

        var set1Keys = new HashSet<string>(list1.Select(NormalizeProcessKey), StringComparer.OrdinalIgnoreCase);

        return list2
            .Where(exe => set1Keys.Contains(NormalizeProcessKey(exe)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}