using System.Diagnostics;
using System.Text.Json;

static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    static void Main()
    {
        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

        const string mutexName = "AudioCrossfader_SingleInstance_Mutex";
        _mutex = new Mutex(true, mutexName, out bool createdNew);

        if (!createdNew)
        {
            MessageBox.Show("Audio Crossfader is already running.", "Audio Crossfader", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayApplicationContext());

        _mutex.ReleaseMutex();
    }
}

public class TrayApplicationContext : ApplicationContext
{
    private NotifyIcon? _trayIcon;
    private readonly ContextMenuStrip _contextMenu;
    private AppSettings _activeSettings;
    private AudioCrossfaderWorker? _crossfaderWorker;
    private SettingsForm? _settingsForm;
    private readonly string _settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

    public TrayApplicationContext()
    {
        _activeSettings = LoadSettings();
        AppIconCache.LoadPathsFromSettings(_activeSettings);

        _contextMenu = new ContextMenuStrip
        {
            Renderer = new DarkContextMenuRenderer(),
            BackColor = Color.FromArgb(32, 32, 32),
            ForeColor = Color.FromArgb(240, 240, 240),
            ShowImageMargin = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Regular)
        };
        _contextMenu.Opening += (s, e) => PopulateTrayMenu();
        PopulateTrayMenu();

        Icon trayIconAsset = SystemIcons.Application;
        try
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (File.Exists(iconPath))
            {
                trayIconAsset = new Icon(iconPath);
            }
            else
            {
                trayIconAsset = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            }
        }
        catch { }

        _trayIcon = new NotifyIcon
        {
            Icon = trayIconAsset,
            ContextMenuStrip = _contextMenu,
            Visible = true,
            Text = "Audio Crossfader"
        };
        _trayIcon.DoubleClick += (s, e) => ShowSettings();

        StartWorker();
    }

    private void PopulateTrayMenu()
    {
        _contextMenu.Items.Clear();

        var swapItem = new ToolStripMenuItem($"Swap Sources ({_activeSettings.Source1} ↔ {_activeSettings.Source2})");
        swapItem.Click += (s, e) =>
        {
            var updated = _activeSettings.Clone();
            (updated.Source1, updated.Source2) = (updated.Source2, updated.Source1);
            (updated.Source1Color, updated.Source2Color) = (updated.Source2Color, updated.Source1Color);
            SaveSettings(updated);
        };
        _contextMenu.Items.Add(swapItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        var validPairs = _activeSettings.CrossfadeCycleList
            .Where(p => !string.IsNullOrWhiteSpace(p.Source1) && !string.IsNullOrWhiteSpace(p.Source2))
            .ToList();

        if (validPairs.Count > 0)
        {
            var header = new ToolStripMenuItem("Crossfade Source Pairs") { Enabled = false };
            _contextMenu.Items.Add(header);

            foreach (var pair in validPairs)
            {
                var item = new ToolStripMenuItem($"{pair.Source1}  ⇄  {pair.Source2}")
                {
                    Checked = _activeSettings.Source1.Equals(pair.Source1, StringComparison.OrdinalIgnoreCase) &&
                              _activeSettings.Source2.Equals(pair.Source2, StringComparison.OrdinalIgnoreCase)
                };
                item.Click += (s, e) =>
                {
                    var updated = _activeSettings.Clone();
                    updated.Source1 = pair.Source1;
                    updated.Source1Color = pair.Color1Hex;
                    updated.Source2 = pair.Source2;
                    updated.Source2Color = pair.Color2Hex;
                    SaveSettings(updated);
                };
                _contextMenu.Items.Add(item);
            }
        }
        else
        {
            var emptyItem = new ToolStripMenuItem("No Crossfade Pairs Configured") { Enabled = false };
            _contextMenu.Items.Add(emptyItem);
        }

        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add("Settings...", null, (s, e) => ShowSettings());
        _contextMenu.Items.Add("Exit", null, (s, e) => ExitApp());
    }

    private AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                string json = File.ReadAllText(_settingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                if (loaded.CycleNextKey == 0 && loaded.QuickSwapKey != 0)
                {
                    loaded.CycleNextKey = loaded.QuickSwapKey;
                }
                if (loaded.CrossfadeCycleList.Count > 0)
                {
                    loaded.Source1 = loaded.CrossfadeCycleList[0].Source1;
                    loaded.Source1Color = loaded.CrossfadeCycleList[0].Color1Hex;
                    loaded.Source2 = loaded.CrossfadeCycleList[0].Source2;
                    loaded.Source2Color = loaded.CrossfadeCycleList[0].Color2Hex;
                }
                loaded.RecordProgramHistory(loaded.Source1);
                loaded.RecordProgramHistory(loaded.Source2);
                return loaded;
            }
        }
        catch { }
        return new AppSettings();
    }

    public void SaveSettings(AppSettings newSettings)
    {
        _activeSettings = newSettings.Clone();
        PersistSettingsToDisk(_activeSettings);
        _crossfaderWorker?.UpdateSettings(_activeSettings, false);
        if (_settingsForm != null && !_settingsForm.IsDisposed)
        {
            _crossfaderWorker?.SetSettingsWindowOpen(true, _activeSettings);
        }
    }

    public void TriggerOverlayPreview(AppSettings draftSettings)
    {
        _crossfaderWorker?.PreviewOverlay(draftSettings);
    }

    public void NotifySettingsWindowState(bool isOpen, AppSettings? draftSettings = null)
    {
        _crossfaderWorker?.SetSettingsWindowOpen(isOpen, draftSettings);
    }

    private void PersistSettingsToDisk(AppSettings settingsToSave)
    {
        try
        {
            string json = JsonSerializer.Serialize(settingsToSave, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch { }
    }

    private void StartWorker()
    {
        _crossfaderWorker = new AudioCrossfaderWorker(_activeSettings, mutatedSettings =>
        {
            _activeSettings = mutatedSettings;
            PersistSettingsToDisk(_activeSettings);
            if (_settingsForm != null && !_settingsForm.IsDisposed && _settingsForm.IsHandleCreated)
            {
                _settingsForm.BeginInvoke(new Action(() => _settingsForm.SyncActiveSourcesFromCycle(mutatedSettings)));
            }
        });
        _crossfaderWorker.Start();
    }

    private void ShowSettings()
    {
        if (_settingsForm == null || _settingsForm.IsDisposed)
        {
            _settingsForm = new SettingsForm(_activeSettings.Clone(), this);
            _settingsForm.FormClosed += (s, e) => NotifySettingsWindowState(false);
            _settingsForm.Show();
            NotifySettingsWindowState(true, _activeSettings);
        }
        else
        {
            if (_settingsForm.WindowState == FormWindowState.Minimized)
                _settingsForm.WindowState = FormWindowState.Normal;
            _settingsForm.Activate();
            _settingsForm.BringToFront();
        }
    }

    private void ExitApp()
    {
        _crossfaderWorker?.Stop();
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        Application.Exit();
    }
}