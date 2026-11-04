using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

public static class AppIconCache
{
    private static readonly object _gdiLock = new();
    private static readonly ConcurrentDictionary<string, Bitmap?> _iconCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, Bitmap?> _customFileIconCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, Bitmap> _presetGroupIcons = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string> _knownExePaths = new(StringComparer.OrdinalIgnoreCase);

    public static readonly (string Key, string DisplayName)[] DefaultGroupIconPresets = new[]
    {
        ("preset:cards",   "Stacked Cards (Default)"),
        ("preset:gaming",  "Gaming & Games"),
        ("preset:music",   "Music & Audio Players"),
        ("preset:voice",   "Voice Chat & Comms"),
        ("preset:browser", "Web Browsers"),
        ("preset:video",   "Video & Streaming"),
        ("preset:speaker", "Speakers & Output"),
        ("preset:system",  "System & Utilities")
    };

    [ThreadStatic]
    private static Dictionary<string, Bitmap>? _threadLocalIcons;

    private static Dictionary<string, Bitmap> ThreadIcons =>
        _threadLocalIcons ??= new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);

    public static void RegisterKnownPath(string exeName, string? fullPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(exeName) || string.IsNullOrWhiteSpace(fullPath)) return;
            string normalized = AppSettings.NormalizeExeName(exeName);
            if (!string.IsNullOrEmpty(normalized) && File.Exists(fullPath))
            {
                _knownExePaths[normalized] = fullPath;
            }
        }
        catch { }
    }

    public static void InvalidateCustomIcon(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                _customFileIconCache.TryRemove(path, out _);
            }
            _threadLocalIcons?.Clear();
        }
        catch { }
    }

    public static void LoadPathsFromSettings(AppSettings settings)
    {
        try
        {
            foreach (var kvp in settings.KnownExecutablePaths)
            {
                RegisterKnownPath(kvp.Key, kvp.Value);
            }
        }
        catch { }
    }

    public static Bitmap? GetPresetIconPreview(string presetKey)
    {
        try
        {
            string cacheKey = $"GRP_PRESET||{presetKey}";
            if (ThreadIcons.TryGetValue(cacheKey, out Bitmap? localBmp))
                return localBmp;

            lock (_gdiLock)
            {
                Bitmap master = _presetGroupIcons.GetOrAdd(presetKey, CreatePresetGroupBitmap);
                Bitmap clone = (Bitmap)master.Clone();
                ThreadIcons[cacheKey] = clone;
                return clone;
            }
        }
        catch
        {
            return null;
        }
    }

    public static Bitmap? GetIconForGroup(ProgramGroup? group)
    {
        try
        {
            string rawPath = group?.CustomIconPath?.Trim() ?? "";
            if (string.IsNullOrEmpty(rawPath) || rawPath.StartsWith("preset:", StringComparison.OrdinalIgnoreCase))
            {
                string presetKey = string.IsNullOrEmpty(rawPath) ? "preset:cards" : rawPath.ToLowerInvariant();
                return GetPresetIconPreview(presetKey);
            }

            string cacheKey = $"GRP_CUSTOM||{rawPath}";
            if (ThreadIcons.TryGetValue(cacheKey, out Bitmap? localBmp))
                return localBmp;

            lock (_gdiLock)
            {
                Bitmap? master = null;
                if (File.Exists(rawPath))
                {
                    master = LoadCustomIconBitmap(rawPath);
                }
                master ??= _presetGroupIcons.GetOrAdd("preset:cards", CreatePresetGroupBitmap);
                Bitmap clone = (Bitmap)master.Clone();
                ThreadIcons[cacheKey] = clone;
                return clone;
            }
        }
        catch
        {
            return null;
        }
    }

    public static Bitmap? GetIconForSource(string sourceIdentifier, AppSettings settings)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sourceIdentifier)) return null;

            if (sourceIdentifier.StartsWith("Group: ", StringComparison.OrdinalIgnoreCase))
            {
                string grpName = sourceIdentifier.Substring(7).Trim();
                var grp = settings.Groups.FirstOrDefault(g => g.GroupName.Equals(grpName, StringComparison.OrdinalIgnoreCase));
                return GetIconForGroup(grp);
            }

            return GetIconForExecutable(sourceIdentifier);
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap? LoadCustomIconBitmap(string filePath)
    {
        return _customFileIconCache.GetOrAdd(filePath, path =>
        {
            try
            {
                if (!File.Exists(path)) return null;
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".exe" || ext == ".dll")
                {
                    using Icon? ico = Icon.ExtractAssociatedIcon(path);
                    if (ico != null) return new Bitmap(ico.ToBitmap(), new Size(32, 32));
                }
                else if (ext == ".ico")
                {
                    using Icon ico = new Icon(path, new Size(32, 32));
                    return new Bitmap(ico.ToBitmap(), new Size(32, 32));
                }
                else
                {
                    using Image img = Image.FromFile(path);
                    Bitmap bmp = new Bitmap(32, 32);
                    using Graphics g = Graphics.FromImage(bmp);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(img, new Rectangle(0, 0, 32, 32));
                    return bmp;
                }
            }
            catch { }
            return null;
        });
    }

    public static Bitmap? GetIconForExecutable(string exeName)
    {
        try
        {
            string norm = AppSettings.NormalizeExeName(exeName);
            if (string.IsNullOrEmpty(norm)) return null;

            if (ThreadIcons.TryGetValue(norm, out Bitmap? localBmp))
                return localBmp;

            lock (_gdiLock)
            {
                Bitmap? master = _iconCache.GetOrAdd(norm, key =>
                {
                    string? path = ResolveExecutablePath(key);
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        try
                        {
                            using Icon? ico = Icon.ExtractAssociatedIcon(path);
                            if (ico != null)
                            {
                                return new Bitmap(ico.ToBitmap(), new Size(32, 32));
                            }
                        }
                        catch { }
                    }
                    return CreateFallbackMonogramIcon(key);
                });

                if (master == null) return null;
                Bitmap clone = (Bitmap)master.Clone();
                ThreadIcons[norm] = clone;
                return clone;
            }
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveExecutablePath(string normalizedExeName)
    {
        if (_knownExePaths.TryGetValue(normalizedExeName, out string? cachedPath) && File.Exists(cachedPath))
            return cachedPath;

        string procKey = AppSettings.NormalizeProcessKey(normalizedExeName);
        try
        {
            var procs = Process.GetProcessesByName(procKey);
            foreach (var p in procs)
            {
                try
                {
                    string? fileName = p.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(fileName) && File.Exists(fileName))
                    {
                        _knownExePaths[normalizedExeName] = fileName;
                        return fileName;
                    }
                }
                catch { }
                finally { p.Dispose(); }
            }
        }
        catch { }

        try
        {
            using var regKey = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{normalizedExeName}");
            string? regPath = regKey?.GetValue(null)?.ToString()?.Trim('"');
            if (!string.IsNullOrEmpty(regPath) && File.Exists(regPath))
            {
                _knownExePaths[normalizedExeName] = regPath;
                return regPath;
            }
        }
        catch { }

        try
        {
            using var regKey = Registry.CurrentUser.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{normalizedExeName}");
            string? regPath = regKey?.GetValue(null)?.ToString()?.Trim('"');
            if (!string.IsNullOrEmpty(regPath) && File.Exists(regPath))
            {
                _knownExePaths[normalizedExeName] = regPath;
                return regPath;
            }
        }
        catch { }

        return null;
    }

    private static Bitmap CreateFallbackMonogramIcon(string exeName)
    {
        Bitmap bmp = new Bitmap(32, 32);
        using Graphics g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using GraphicsPath path = new GraphicsPath();
        path.AddEllipse(2, 2, 28, 28);
        using SolidBrush bg = new SolidBrush(Color.FromArgb(0, 110, 195));
        g.FillPath(bg, path);

        string letter = string.IsNullOrEmpty(exeName) ? "?" : exeName.Substring(0, 1).ToUpperInvariant();
        using Font f = new Font("Segoe UI", 13f, FontStyle.Bold);
        using StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(letter, f, Brushes.White, new RectangleF(0, 0, 32, 32), sf);
        return bmp;
    }

    private static Bitmap CreatePresetGroupBitmap(string presetKey)
    {
        Bitmap bmp = new Bitmap(32, 32);
        using Graphics g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        (Color c1, Color c2, string glyph) = presetKey.ToLowerInvariant() switch
        {
            "preset:gaming"  => (Color.FromArgb(16, 185, 129), Color.FromArgb(5, 122, 85),   "\U0001F3AE"), // 🎮
            "preset:music"   => (Color.FromArgb(236, 72, 153), Color.FromArgb(180, 24, 93),  "\U0001F3B5"), // 🎵
            "preset:voice"   => (Color.FromArgb(59, 130, 246), Color.FromArgb(29, 78, 216),  "\U0001F399"), // 🎙
            "preset:browser" => (Color.FromArgb(14, 165, 233), Color.FromArgb(3, 105, 161),  "\U0001F310"), // 🌐
            "preset:video"   => (Color.FromArgb(239, 68, 68),  Color.FromArgb(185, 28, 28),  "\U0001F3AC"), // 🎬
            "preset:speaker" => (Color.FromArgb(245, 158, 11), Color.FromArgb(180, 83, 9),   "\U0001F50A"), // 🔊
            "preset:system"  => (Color.FromArgb(100, 116, 139),Color.FromArgb(51, 65, 85),   "\u2699"),     // ⚙
            _                => (Color.FromArgb(155, 105, 245),Color.FromArgb(105, 65, 195), "")
        };

        using (GraphicsPath badgePath = new GraphicsPath())
        {
            int r = 10;
            badgePath.AddArc(2, 2, r, r, 180, 90);
            badgePath.AddArc(30 - r, 2, r, r, 270, 90);
            badgePath.AddArc(30 - r, 30 - r, r, r, 0, 90);
            badgePath.AddArc(2, 30 - r, r, r, 90, 90);
            badgePath.CloseFigure();

            using LinearGradientBrush bg = new LinearGradientBrush(
                new Rectangle(2, 2, 28, 28), c1, c2, LinearGradientMode.ForwardDiagonal);
            g.FillPath(bg, badgePath);
        }

        if (string.IsNullOrEmpty(glyph))
        {
            // Default Stacked Cards icon
            using (SolidBrush backCardBrush = new SolidBrush(Color.FromArgb(170, 255, 255, 255)))
            {
                g.FillRectangle(backCardBrush, 12, 7, 12, 10);
            }
            using (SolidBrush frontCardBrush = new SolidBrush(Color.White))
            using (Pen separatorPen = new Pen(c2, 1.5f))
            {
                Rectangle frontRect = new Rectangle(7, 13, 14, 11);
                g.FillRectangle(frontCardBrush, frontRect);
                g.DrawRectangle(separatorPen, frontRect);
            }
        }
        else
        {
            using Font f = new Font("Segoe UI Emoji", 12.5f, FontStyle.Regular);
            using StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(glyph, f, Brushes.White, new RectangleF(0, 1, 32, 31), sf);
        }

        return bmp;
    }
}

public class DetectedAudioPrograms
{
    public HashSet<string> PlayingAudio { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> ActiveAudioSessions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> RunningWindowedApps { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

public static class AudioProcessDetector
{
    private static readonly HashSet<string> IgnoredSystemProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer.exe", "shellexperiencehost.exe", "searchhost.exe", "startmenuexperiencehost.exe",
        "textinputhost.exe", "applicationframehost.exe", "systemsettings.exe", "taskmgr.exe",
        "svchost.exe", "dwm.exe", "audiodg.exe"
    };

    private static readonly object _cacheLock = new();
    private static DetectedAudioPrograms? _cachedResult;
    private static DateTime _lastScanUtc = DateTime.MinValue;
    private static int _isScanningBg = 0;

    public static DetectedAudioPrograms Scan(bool forceImmediate = false)
    {
        var now = DateTime.UtcNow;
        lock (_cacheLock)
        {
            if (!forceImmediate && _cachedResult != null)
            {
                if ((now - _lastScanUtc).TotalMilliseconds >= 2500.0 && Interlocked.CompareExchange(ref _isScanningBg, 1, 0) == 0)
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            var fresh = PerformScanInternal();
                            lock (_cacheLock)
                            {
                                _cachedResult = fresh;
                                _lastScanUtc = DateTime.UtcNow;
                            }
                        }
                        finally
                        {
                            Interlocked.Exchange(ref _isScanningBg, 0);
                        }
                    });
                }
                return _cachedResult;
            }
        }

        var immediate = PerformScanInternal();
        lock (_cacheLock)
        {
            _cachedResult = immediate;
            _lastScanUtc = DateTime.UtcNow;
        }
        return immediate;
    }

    private static DetectedAudioPrograms PerformScanInternal()
    {
        var result = new DetectedAudioPrograms();
        string currentProcName = AppSettings.NormalizeExeName(Process.GetCurrentProcess().ProcessName);

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            foreach (var device in devices)
            {
                try
                {
                    var sessions = device.AudioSessionManager.Sessions;
                    for (int i = 0; i < sessions.Count; i++)
                    {
                        var session = sessions[i];
                        uint pid = session.GetProcessID;
                        if (pid == 0) continue;

                        try
                        {
                            using var proc = Process.GetProcessById((int)pid);
                            string exeName = AppSettings.NormalizeExeName(proc.ProcessName);
                            if (string.IsNullOrEmpty(exeName) || exeName.Equals(currentProcName, StringComparison.OrdinalIgnoreCase))
                                continue;

                            try
                            {
                                AppIconCache.RegisterKnownPath(exeName, proc.MainModule?.FileName);
                            }
                            catch { }

                            bool isPlaying = false;
                            try
                            {
                                float peak = session.AudioMeterInformation.MasterPeakValue;
                                isPlaying = (peak > 0.0005f) || (session.State == AudioSessionState.AudioSessionStateActive);
                            }
                            catch
                            {
                                isPlaying = (session.State == AudioSessionState.AudioSessionStateActive);
                            }

                            if (isPlaying)
                                result.PlayingAudio.Add(exeName);
                            else
                                result.ActiveAudioSessions.Add(exeName);
                        }
                        catch { }
                    }
                }
                finally
                {
                    device.Dispose();
                }
            }
        }
        catch { }

        try
        {
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    if (proc.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(proc.MainWindowTitle))
                    {
                        string exeName = AppSettings.NormalizeExeName(proc.ProcessName);
                        if (!string.IsNullOrEmpty(exeName) &&
                            !exeName.Equals(currentProcName, StringComparison.OrdinalIgnoreCase) &&
                            !IgnoredSystemProcesses.Contains(exeName))
                        {
                            try { AppIconCache.RegisterKnownPath(exeName, proc.MainModule?.FileName); } catch { }

                            if (!result.PlayingAudio.Contains(exeName) && !result.ActiveAudioSessions.Contains(exeName))
                            {
                                result.RunningWindowedApps.Add(exeName);
                            }
                        }
                    }
                }
                catch { }
                finally
                {
                    proc.Dispose();
                }
            }
        }
        catch { }

        return result;
    }
}

// High-Performance CoreAudio Session Cache
public class AudioSessionCache : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new MMDeviceEnumerator();
    private readonly Dictionary<string, List<SimpleAudioVolume>> _sessionsByProcKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<uint, string> _pidToProcKey = new();
    private readonly List<MMDevice> _activeDevices = new();
    private readonly List<AudioSessionControl> _activeSessions = new();
    private DateTime _lastRefreshTime = DateTime.MinValue;
    private DateTime _lastMissingKeyProbeTime = DateTime.MinValue;
    private const double RefreshIntervalMs = 2000.0;

    public void SetVolumeForProcessKey(string procKey, float volume)
    {
        if (string.IsNullOrEmpty(procKey)) return;

        var now = DateTime.UtcNow;
        bool regularRefreshDue = (now - _lastRefreshTime).TotalMilliseconds >= RefreshIntervalMs;
        bool missingKeyProbeDue = !_sessionsByProcKey.ContainsKey(procKey) && (now - _lastMissingKeyProbeTime).TotalMilliseconds >= 350.0;

        if (regularRefreshDue || missingKeyProbeDue)
        {
            _lastMissingKeyProbeTime = now;
            RefreshSessions();
        }

        if (_sessionsByProcKey.TryGetValue(procKey, out var volumes))
        {
            bool faulted = false;
            for (int i = 0; i < volumes.Count; i++)
            {
                try
                {
                    volumes[i].Volume = volume;
                }
                catch
                {
                    faulted = true;
                }
            }

            if (faulted)
            {
                RefreshSessions();
                if (_sessionsByProcKey.TryGetValue(procKey, out var retryVolumes))
                {
                    for (int i = 0; i < retryVolumes.Count; i++)
                    {
                        try { retryVolumes[i].Volume = volume; } catch { }
                    }
                }
            }
        }
    }

    public void ForceRefresh() => RefreshSessions();

    private void RefreshSessions()
    {
        _lastRefreshTime = DateTime.UtcNow;
        _sessionsByProcKey.Clear();
        _pidToProcKey.Clear();
        _activeSessions.Clear();

        foreach (var oldDev in _activeDevices)
        {
            try { oldDev.Dispose(); } catch { }
        }
        _activeDevices.Clear();

        try
        {
            var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            foreach (var device in devices)
            {
                _activeDevices.Add(device);
                var sessions = device.AudioSessionManager.Sessions;
                for (int i = 0; i < sessions.Count; i++)
                {
                    var session = sessions[i];
                    uint pid = session.GetProcessID;
                    if (pid == 0) continue;

                    if (!_pidToProcKey.TryGetValue(pid, out string? procKey))
                    {
                        try
                        {
                            using var proc = Process.GetProcessById((int)pid);
                            procKey = proc.ProcessName.ToLowerInvariant().Trim();
                            _pidToProcKey[pid] = procKey;
                            try
                            {
                                AppIconCache.RegisterKnownPath($"{procKey}.exe", proc.MainModule?.FileName);
                            }
                            catch { }
                        }
                        catch
                        {
                            continue;
                        }
                    }

                    if (!string.IsNullOrEmpty(procKey))
                    {
                        if (!_sessionsByProcKey.TryGetValue(procKey, out var list))
                        {
                            list = new List<SimpleAudioVolume>();
                            _sessionsByProcKey[procKey] = list;
                        }
                        _activeSessions.Add(session);
                        list.Add(session.SimpleAudioVolume);
                    }
                }
            }
        }
        catch { }
    }

    public void Dispose()
    {
        _activeSessions.Clear();
        foreach (var dev in _activeDevices)
        {
            try { dev.Dispose(); } catch { }
        }
        _activeDevices.Clear();
        try { _enumerator.Dispose(); } catch { }
    }
}