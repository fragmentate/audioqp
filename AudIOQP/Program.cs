using System.Runtime.InteropServices;

namespace AudIOQP;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        }
        catch (InvalidOperationException)
        {
            // The manifest already selected Per-Monitor V2.
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        if (SynchronizationContext.Current == null)
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

        if (args.Any(a => string.Equals(a, "--self-test", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.Exit(SelfTest.Run(args));
            return;
        }

        using var mutex = new Mutex(true, @"Local\AudIOQP.SingleInstance", out var created);
        if (!created)
        {
            MessageBox.Show("AudIOQP is already running in the notification area.", "AudIOQP",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.ThreadException += (_, e) =>
        {
            Log.Error("Unhandled UI exception.", e.Exception);
            try
            {
                MessageBox.Show(e.Exception.Message, "AudIOQP", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
                // The UI thread is already in a bad state.
            }
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                Log.Error("Unhandled exception.", ex);
        };

        Application.Run(new TrayApplication());
        GC.KeepAlive(mutex);
    }
}

internal static class SelfTest
{
    [DllImport("kernel32.dll")]
    static extern bool AttachConsole(int processId);

    public static int Run(string[] args)
    {
        AttachConsole(-1);
        var directory = Path.Combine(Path.GetTempPath(), "audioqp-preview");
        Directory.CreateDirectory(directory);
        using var report = new StreamWriter(Path.Combine(directory, "report.txt"));
        var previousOut = Console.Out;
        Console.SetOut(report);
        var failures = new List<string>();
        try
        {
            CoreChecks.Run();
            Console.WriteLine("Core checks passed.");
        }
        catch (Exception ex)
        {
            failures.Add("Core checks: " + ex.Message);
            Console.WriteLine(ex);
        }

        try
        {
            RunAudio(failures);
        }
        catch (Exception ex)
        {
            failures.Add("Audio: " + ex);
            Console.WriteLine(ex);
        }

        try
        {
            RunHotkey(failures);
        }
        catch (Exception ex)
        {
            failures.Add("Hotkey: " + ex);
            Console.WriteLine(ex);
        }

        try
        {
            RunUi(failures, args);
        }
        catch (Exception ex)
        {
            failures.Add("UI: " + ex);
            Console.WriteLine(ex);
        }

        if (failures.Count == 0)
            Console.WriteLine("SELF-TEST PASSED");
        else
        {
            Console.WriteLine("SELF-TEST FAILED");
            foreach (var failure in failures)
                Console.WriteLine(" - " + failure);
        }

        report.Flush();
        Console.SetOut(previousOut);
        return failures.Count == 0 ? 0 : 1;
    }

    static void RunAudio(List<string> failures)
    {
        using var audio = new Audio.AudioDeviceService();
        Console.WriteLine("Playback devices: " + audio.Devices.Count);
        foreach (var device in audio.Devices)
            Console.WriteLine($"  [{device.Status}] {device.FriendlyName}  {device.Id}");
        var current = audio.DefaultDevice;
        Console.WriteLine("Default: " + (current?.FriendlyName ?? "(none)"));
        if (current == null)
        {
            Console.WriteLine("No default playback device. Skipped SetDefaultEndpoint.");
            return;
        }

        audio.SetDefault(current.Id, includeCommunications: false);
        audio.Refresh();
        if (!Audio.DeviceSelector.IdEquals(audio.DefaultDevice?.Id, current.Id))
            failures.Add("SetDefaultEndpoint changed the device away from " + current.FriendlyName);
        else
            Console.WriteLine("SetDefaultEndpoint kept " + current.FriendlyName);
    }

    static void RunHotkey(List<string> failures)
    {
        using var window = new Hotkeys.HotkeyWindow();
        window.Create();
        // F24 with all modifiers is unused on a normal system and proves RegisterHotKey works.
        var ok = Hotkeys.NativeHotkey.TryRegister(window.Handle, 41, 0x0002 | 0x0001 | 0x0004, 0x87, out var error);
        Hotkeys.NativeHotkey.Unregister(window.Handle, 41);
        if (!ok)
            failures.Add("RegisterHotKey failed with Windows error " + error);
        else
            Console.WriteLine("RegisterHotKey succeeded.");

        var defaults = new Hotkeys.HotkeyRegistrar(window.Handle);
        if (!defaults.TryApply(new Settings.AppSettings(), out var hotkeyError))
            Console.WriteLine("Default shortcuts were not registered: " + hotkeyError);
        else
            Console.WriteLine("Default shortcuts registered.");
        defaults.UnregisterAll();
    }

    static void RunUi(List<string> failures, string[] args)
    {
        var directory = Path.Combine(Path.GetTempPath(), "audioqp-preview");
        Directory.CreateDirectory(directory);
        using (var audio = new Audio.AudioDeviceService())
        {
            audio.StartWatching();
            Application.DoEvents();
            Console.WriteLine("Notification watch registered.");
        }

        var devices = new List<Audio.AudioEndpoint>
        {
            new("id-realtek", "Speakers (Realtek Audio)", Audio.EndpointStatus.Active),
            new("id-captain", "Headphones (Captain 300)", Audio.EndpointStatus.Active),
            new("id-kt", "Speakers (KT-08UP)", Audio.EndpointStatus.Active)
        };
        var screen = Screen.PrimaryScreen ?? Screen.AllScreens.First();
        using var picker = new UI.DevicePickerForm(devices, "id-realtek", screen, focusOnShow: false);
        if (!Capture(picker, Path.Combine(directory, "picker.png")))
            failures.Add("Device picker bitmap looked blank.");

        using var preferences = new UI.PreferencesForm(new Settings.AppSettings
        {
            Initialized = true,
            Whitelist =
            [
                new Audio.DeviceRef("id-realtek", "Speakers (Realtek Audio)"),
                new Audio.DeviceRef("id-captain", "Headphones (Captain 300)")
            ],
            HomeDeviceId = "id-realtek",
            HomeDeviceName = "Speakers (Realtek Audio)"
        }, devices);
        if (!Capture(preferences, Path.Combine(directory, "preferences.png")))
            failures.Add("Preferences bitmap looked blank.");

        var cursor = UI.ScreenPlacement.ScreenAtCursor();
        var active = UI.ScreenPlacement.TargetScreens(false);
        if (active.Count != 1 || active[0].DeviceName != cursor.DeviceName)
            failures.Add("Active-screen mode should target only the monitor under the pointer.");
        var all = UI.ScreenPlacement.TargetScreens(true);
        if (all.Count != Screen.AllScreens.Length)
            failures.Add($"All-screens mode returned {all.Count} monitors, expected {Screen.AllScreens.Length}.");
        if (all.Count == 0 || all[^1].DeviceName != cursor.DeviceName)
            failures.Add("The monitor under the pointer should be shown last.");

        var placed = new List<UI.DevicePickerForm>();
        try
        {
            foreach (var target in all)
            {
                var form = new UI.DevicePickerForm(
                    devices,
                    "id-realtek",
                    target,
                    focusOnShow: target.DeviceName == cursor.DeviceName)
                {
                    Opacity = 0,
                    ShowInTaskbar = false
                };
                placed.Add(form);
            }

            foreach (var form in placed)
                form.Show();
            Application.DoEvents();
            for (var i = 0; i < placed.Count; i++)
            {
                var form = placed[i];
                var target = all[i];
                var center = new Point(form.Left + form.Width / 2, form.Top + form.Height / 2);
                if (!target.WorkingArea.Contains(center))
                    failures.Add("Device picker was not centered on " + target.DeviceName);
            }

            var focused = placed.Where(f => f.ContainsFocus).Select(f => f.Location).ToList();
            if (placed.Count > 1 && focused.Count != 1)
                Console.WriteLine("Focus check: " + focused.Count + " picker windows reported focus.");
        }
        finally
        {
            foreach (var form in placed)
                form.Dispose();
        }

        UI.OverlayForm.ShowMessage("Headphones (Captain 300)", "Audio output");
        Application.DoEvents();
        var overlay = Application.OpenForms.OfType<UI.OverlayForm>().FirstOrDefault();
        if (overlay == null)
            failures.Add("Overlay did not open.");
        else if (!Capture(overlay, Path.Combine(directory, "overlay.png"), requireOneLine: true))
            failures.Add("Overlay bitmap looked blank or the device name was clipped.");

        UI.OverlayForm.ShowMessage("Realtek HD Audio 2nd output (Realtek(R) Audio)", "Audio output");
        Application.DoEvents();
        overlay = Application.OpenForms.OfType<UI.OverlayForm>().FirstOrDefault();
        if (overlay == null)
            failures.Add("Overlay did not reopen for a long device name.");
        else if (!Capture(overlay, Path.Combine(directory, "overlay-long.png"), requireOneLine: true))
            failures.Add("Long device name was clipped on the overlay.");
        overlay?.Hide();

        var iconPath = Path.Combine(directory, "icon.png");
        using (var icon = UI.AppIcon.Create())
        using (var bitmap = icon.ToBitmap())
            bitmap.Save(iconPath);
        Console.WriteLine("Preview images: " + directory);
        if (args.Any(a => a == "--write-icon"))
        {
            var ico = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "app.ico"));
            UI.AppIcon.WriteIcoFile(ico);
            Console.WriteLine("Wrote " + ico);
        }
    }

    static bool Capture(Form form, string path, bool requireOneLine = false)
    {
        form.Opacity = 0;
        form.ShowInTaskbar = false;
        form.Show();
        form.Refresh();
        Application.DoEvents();
        using var bitmap = new Bitmap(Math.Max(1, form.Width), Math.Max(1, form.Height));
        form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        form.Hide();
        form.Opacity = 1;
        if (!HasContent(bitmap))
            return false;
        if (!requireOneLine)
            return true;
        var bands = ContentBands(bitmap);
        var clearBottom = BottomIsClear(bitmap);
        Console.WriteLine($"{Path.GetFileName(path)} {bitmap.Width}x{bitmap.Height} bands={bands} bottomClear={clearBottom}");
        return bands == 2 && clearBottom;
    }

    static int ContentBands(Bitmap bitmap)
    {
        var bands = 0;
        var inside = false;
        for (var y = 0; y < bitmap.Height; y++)
        {
            var hit = RowHasInk(bitmap, y, 30);
            if (hit && !inside)
                bands++;
            inside = hit;
        }

        return bands;
    }

    static bool BottomIsClear(Bitmap bitmap)
    {
        var start = Math.Max(0, bitmap.Height - 8);
        for (var y = start; y < bitmap.Height; y++)
        {
            if (RowHasInk(bitmap, y, 30))
                return false;
        }

        return true;
    }

    static bool RowHasInk(Bitmap bitmap, int y, int xStart)
    {
        for (var x = xStart; x < bitmap.Width; x += 2)
        {
            var color = bitmap.GetPixel(x, y);
            if (color.R > 140 || color.G > 140 || color.B > 170)
                return true;
        }

        return false;
    }

    static bool HasContent(Bitmap bitmap)
    {
        var colors = new HashSet<int>();
        for (var y = 0; y < bitmap.Height; y += 5)
        {
            for (var x = 0; x < bitmap.Width; x += 5)
                colors.Add(bitmap.GetPixel(x, y).ToArgb());
        }

        return colors.Count > 3;
    }
}

internal static class CoreChecks
{
    public static void Run()
    {
        var modal = Settings.Hotkey.ModalDefault();
        var cycle = Settings.Hotkey.CycleDefault();
        var home = Settings.Hotkey.HomeDefault();
        Equal("Ctrl+Pause", modal.Display, "modal display");
        Equal("Ctrl+Shift+]", cycle.Display, "cycle display");
        Equal("Ctrl+Shift+Home", home.Display, "home display");
        Equal(0x03u, modal.RegisterVirtualKey, "Ctrl+Pause registers as Break");
        Equal(0x13u, new Settings.Hotkey { VirtualKey = 0x13 }.RegisterVirtualKey, "Pause alone stays Pause");
        if (!modal.ConflictsWith(new Settings.Hotkey { Control = true, VirtualKey = 0x03 }))
            throw new InvalidOperationException("Ctrl+Pause should conflict with Ctrl+Break.");
        if (modal.ConflictsWith(cycle) || cycle.ConflictsWith(home))
            throw new InvalidOperationException("Default shortcuts should not conflict.");

        var realtek = new Audio.AudioEndpoint("old-id", "Speakers (Realtek Audio)", Audio.EndpointStatus.Active);
        var captainOld = new Audio.AudioEndpoint("captain-old", "Headphones (Captain 300)", Audio.EndpointStatus.Unplugged);
        var captain = new Audio.AudioEndpoint("captain-new", "Headphones (Captain 300)", Audio.EndpointStatus.Active);
        var board = new Audio.AudioEndpoint("kt", "Speakers (KT-08UP)", Audio.EndpointStatus.Active);
        var devices = new List<Audio.AudioEndpoint> { realtek, captainOld, captain, board };
        var whitelist = new List<Audio.DeviceRef>
        {
            new("stale-realtek", "Speakers (Realtek Audio)"),
            new("captain-old", "Headphones (Captain 300)"),
            new("kt", "Speakers (KT-08UP)"),
            new("kt", "Speakers (KT-08UP)")
        };
        var cycleList = Audio.DeviceSelector.CycleList(devices, whitelist);
        Equal(3, cycleList.Count, "cycle length");
        Equal("old-id", cycleList[0].Id, "name fallback for Realtek");
        Equal("captain-new", cycleList[1].Id, "unplugged id falls through to the active name");
        Equal(board, Audio.DeviceSelector.Next(cycleList, "captain-new"), "next after captain");
        Equal(realtek, Audio.DeviceSelector.Next(cycleList, "kt"), "wrap");
        Equal(realtek, Audio.DeviceSelector.Next(cycleList, "missing"), "unknown current starts at the first");
        if (Audio.DeviceSelector.Next([], "x") != null)
            throw new InvalidOperationException("Empty cycle should return null.");

        var settings = new Settings.AppSettings();
        if (Settings.SettingsLogic.InitializeFirstRun(settings, [captainOld], null))
            throw new InvalidOperationException("Unplugged-only lists should not finish first-run setup.");
        if (!Settings.SettingsLogic.InitializeFirstRun(settings, devices, realtek))
            throw new InvalidOperationException("First run should initialize.");
        Equal(3, settings.Whitelist.Count, "first-run whitelist");
        Equal("old-id", settings.HomeDeviceId, "home defaults to the current device");
        if (Settings.SettingsLogic.InitializeFirstRun(settings, devices, board))
            throw new InvalidOperationException("First run should not run twice.");

        settings.Whitelist =
        [
            new Audio.DeviceRef("captain-old", "Headphones (Captain 300)"),
            new Audio.DeviceRef("gone", "Missing Device")
        ];
        settings.HomeDeviceId = "captain-old";
        settings.HomeDeviceName = "Headphones (Captain 300)";
        if (!Settings.SettingsLogic.ReconcileIdentifiers(settings, devices))
            throw new InvalidOperationException("Reconcile should update the replugged id.");
        Equal("captain-new", settings.Whitelist[0].Id, "whitelist id updated");
        Equal("captain-new", settings.HomeDeviceId, "home id updated");
        Equal("gone", settings.Whitelist[1].Id, "disconnected whitelist entry is kept");

        settings.OpenModal = settings.Cycle.Clone();
        var problem = Settings.SettingsLogic.ValidateHotkeys(settings);
        if (problem == null)
            throw new InvalidOperationException("Duplicate shortcuts should be rejected.");

        var root = Path.Combine(Path.GetTempPath(), "audioqp-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var previousRoot = AppPaths.Root;
        AppPaths.Root = root;
        try
        {
            var store = new Settings.SettingsStore(Path.Combine(root, "settings.json"));
            var saved = new Settings.AppSettings
            {
                Initialized = true,
                ModalMode = Settings.ModalDisplayMode.AllScreens,
                HomeDeviceName = "Speakers (KT-08UP)",
                Whitelist = [new Audio.DeviceRef("kt", "Speakers (KT-08UP)")]
            };
            store.Save(saved);
            var loaded = store.Load();
            Equal(Settings.ModalDisplayMode.AllScreens, loaded.ModalMode, "mode roundtrip");
            Equal("Ctrl+Pause", loaded.OpenModal.Display, "default modal hotkey roundtrip");
            Equal("Speakers (KT-08UP)", loaded.Whitelist[0].Name, "whitelist roundtrip");
            File.WriteAllText(store.Path, "{ this is not json");
            var recovered = store.Load();
            Equal("Ctrl+Shift+]", recovered.Cycle.Display, "corrupt file falls back to defaults");
            if (!File.Exists(store.Path + ".bak"))
                throw new InvalidOperationException("Corrupt settings should be backed up.");
        }
        finally
        {
            AppPaths.Root = previousRoot;
            Directory.Delete(root, recursive: true);
        }
    }

    static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected '{expected}', got '{actual}'.");
    }
}
