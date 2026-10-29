using System.Drawing;

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
    public List<string> Executables { get; set; } = new List<string>();

    public ProgramGroup Clone()
    {
        return new ProgramGroup
        {
            GroupName = this.GroupName,
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
    public string Color1Hex { get; set; } = "#1B3B5A";
    public string Source2 { get; set; } = "";
    public string Color2Hex { get; set; } = "#4A2040";

    public CrossfadeCyclePair Clone()
    {
        return new CrossfadeCyclePair
        {
            Source1 = this.Source1,
            Color1Hex = this.Color1Hex,
            Source2 = this.Source2,
            Color2Hex = this.Color2Hex
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

    public CycleDisplayItem(string label1, string rawSource1, string color1Hex, string label2, string rawSource2, string color2Hex)
    {
        Label1 = label1;
        RawSource1 = rawSource1;
        Color1Hex = color1Hex;
        Label2 = label2;
        RawSource2 = rawSource2;
        Color2Hex = color2Hex;
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
        if (parts.Count == 0) return "Press keys...";
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

    public List<string> RecentPrograms { get; set; } = new List<string> { "chrome.exe", "firefox.exe" };
    public Dictionary<string, string> KnownExecutablePaths { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public bool RunOnStartup { get; set; } = false;

    public int CrossfadeKeyUp { get; set; } = 0x7E;    // VK_F15
    public int CrossfadeKeyMute { get; set; } = 0x7F;  // VK_F16
    public int CrossfadeKeyDown { get; set; } = 0x80;  // VK_F17

    public int QuickSwapKey { get; set; } = 0;
    public int CycleNextKey { get; set; } = 0;
    public int CyclePrevKey { get; set; } = 0;

    public string UnmutedColor { get; set; } = "#00A8FF";
    public string MutedColor { get; set; } = "#FF4757";
    public bool UseSourceBackgroundColors { get; set; } = true;
    public bool FadeBgColorWithVolume { get; set; } = true;
    public bool KeepOverlayOnWhileSettingsOpen { get; set; } = false;

    public float CarouselVisibleNeighbors { get; set; } = 1.5f;
    public bool CarouselBlendColors { get; set; } = true;
    public string CarouselMotionStyle { get; set; } = "3D Floating Pills";
    public bool EnableCarouselAnimation { get; set; } = true;
    public bool CarouselBackgroundEnabled { get; set; } = true;

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
            RecentPrograms = new List<string>(this.RecentPrograms),
            KnownExecutablePaths = new Dictionary<string, string>(this.KnownExecutablePaths, StringComparer.OrdinalIgnoreCase),
            RunOnStartup = this.RunOnStartup,
            CrossfadeKeyUp = this.CrossfadeKeyUp,
            CrossfadeKeyMute = this.CrossfadeKeyMute,
            CrossfadeKeyDown = this.CrossfadeKeyDown,
            QuickSwapKey = this.QuickSwapKey,
            CycleNextKey = this.CycleNextKey,
            CyclePrevKey = this.CyclePrevKey,
            UnmutedColor = this.UnmutedColor,
            MutedColor = this.MutedColor,
            UseSourceBackgroundColors = this.UseSourceBackgroundColors,
            FadeBgColorWithVolume = this.FadeBgColorWithVolume,
            KeepOverlayOnWhileSettingsOpen = this.KeepOverlayOnWhileSettingsOpen,
            CarouselVisibleNeighbors = this.CarouselVisibleNeighbors,
            CarouselBlendColors = this.CarouselBlendColors,
            CarouselMotionStyle = this.CarouselMotionStyle,
            EnableCarouselAnimation = this.EnableCarouselAnimation,
            CarouselBackgroundEnabled = this.CarouselBackgroundEnabled,
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