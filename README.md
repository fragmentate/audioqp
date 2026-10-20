# AudIOQP

AudIOQP is a Windows tray application that switches the default audio playback device. It stays running so you can change the output with a global shortcut, from a device list, or from the tray menu.

It switches playback devices only. It does not route audio for individual applications, and it does not change recording devices.

## Requirements

- Windows 10 or Windows 11
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)

No administrator rights are required.

## Run

From this directory:

```powershell
dotnet run --project AudIOQP\AudIOQP.csproj -c Release
```

A Release build is also available at `AudIOQP\bin\Release\net10.0-windows\AudIOQP.exe` after you build:

```powershell
dotnet build AudIOQP\AudIOQP.csproj -c Release
```

The application has no main window. Look for the speaker icon in the notification area. Hovering over the icon shows the current playback device. Only one instance runs at a time. Starting it again shows a notice that it is already running.

On the first launch, every active playback device is added to the device list, and the current default becomes the home device. If no playback devices are connected yet, that setup waits until one appears.

## Switch devices

Right-click the tray icon for the menu. It shows the current device and these actions:

| Menu item | What it does |
| --- | --- |
| Switch device… | Opens the device list |
| Next device | Selects the next checked device |
| Home device | Selects the preferred device |
| Preferences… | Opens settings |
| Exit | Quits AudIOQP |

Left-click the tray icon to open the device list.

### Shortcuts

These shortcuts work while AudIOQP is running, including when another application is in front. Each one needs Ctrl, Alt, Shift, or Win, plus another key. The three shortcuts must be different. Ctrl+Pause and Ctrl+Break count as the same shortcut.

| Action | Default shortcut |
| --- | --- |
| Open the device list | Ctrl+Pause |
| Next device | Ctrl+Shift+] |
| Home device | Ctrl+Shift+Home |

Change them in Preferences. Click a shortcut box and press the combination you want. Backspace clears that box.

If Windows reports that a shortcut is already in use, AudIOQP leaves the previous shortcuts in place and shows a notification.

### Device list

The list contains the playback devices you checked in Preferences, in that order. The current default is marked **Current**.

- Click a device to switch to it.
- Use the arrow keys and Enter.
- Press 1 through 9 to select one of the first nine devices.
- Press Escape, or the close button, to dismiss the list without switching.
- Press the open shortcut again to close the list.

By default the list opens on the monitor where the pointer is. You can instead show one list centered on every monitor. Choosing a device, or dismissing the list, closes all of them together.

### Next and Home

**Next device** moves to the following checked device that is currently connected, then wraps around to the first. Devices that are disconnected, disabled, or missing are skipped.

**Home device** switches straight to the preferred device chosen in Preferences.

Both actions show the new device name in a small overlay for about a second and a half. The overlay does not take keyboard focus. You can turn it off in Preferences.

## Preferences

Open **Preferences…** from the tray menu.

**Shortcuts** sets the three global shortcuts described above.

**Playback devices** lists devices Windows currently knows about, including ones that are disconnected or disabled. Check the devices that should appear in the list and in the cycle. The check order is the cycle order. **Home device** is the target of the home shortcut and menu item. It does not have to be checked.

The device list updates while Preferences is open if you plug in or unplug hardware.

**Behavior**

- **Active screen** opens the list on the monitor under the pointer.
- **All screens** opens one list centered on each monitor.
- **Show a short confirmation when cycling or jumping home** controls the overlay.
- **Also switch the default communications device** updates the device Windows uses for calls as well as the normal playback default. Turn this off to leave the communications device unchanged.
- **Start AudIOQP when Windows starts** adds or removes an entry in your personal Windows startup list. It points at the executable you are running.

Use **Apply** to save and keep Preferences open, **OK** to save and close, or **Cancel** to discard changes that have not been applied. Escape closes Preferences when a shortcut box is not focused.

## When devices change

AudIOQP watches for playback devices being added, removed, enabled, disabled, or unplugged. The tray tooltip and Preferences stay in step with that list.

A checked device that is unplugged stays in your settings and returns to the cycle when it is connected again. If Windows assigns that device a new id, AudIOQP matches it by its name and updates the saved id.

## Settings and logs

Settings are stored in `%AppData%\AudIOQP\settings.json`. A log is written to `%AppData%\AudIOQP\audioqp.log`.

If the settings file cannot be read, AudIOQP keeps a copy beside it with a `.bak` extension and continues with default shortcuts until the next successful save.
