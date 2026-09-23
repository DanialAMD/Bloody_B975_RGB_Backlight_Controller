# Bloody B975 RGB Controller

[فارسی](README.fa.md) | **English**

A lightweight Windows controller for custom RGB lighting on the Bloody B975 keyboard. It combines a configurable Breathing background with per-key default colors and multiple reactive effects, without requiring KeyDominator to remain open.

> This is an independent community project and is not affiliated with or endorsed by Bloody/A4Tech.

## Highlights

- Solid-color or per-key Breathing background
- Import of Bloody `.ckPannel` color profiles
- Visual editor matching the physical B975 keyboard layout
- Individual key selection, `Ctrl` multi-selection, and drag-box selection
- Solid colors and angled gradients with 2 to 8 color stops
- Undo, Redo, Reset, Select All, and Clear Selection
- Multicolor Flash/Fade reactive effect
- Row-based Meteor effect with `.ckButton` profile support
- Multicolor Radial Explosion effect that expands in every physical direction
- Independent overlapping effects: a new key press does not cancel an earlier effect
- Instant Persian/English UI switching, with Persian as the default language
- Start with Windows, automatic effect startup, and system-tray support

The HID protocol has been tested on a real Bloody B975 with:

```text
VID: 09DA
PID: FA10
Interface: MI_02 / COL04
```

The LED row mapping and continuous RGB frame transmission were verified on that device.

## Requirements

- Windows 10 or Windows 11
- Bloody B975 keyboard
- Visual Studio 2022 with the **.NET Desktop Development** workload for source builds
- .NET 8 SDK for building
- .NET Desktop Runtime 8 for the framework-dependent release

Administrator access is normally not required. A restrictive Windows policy or another application holding the HID interface may still prevent access.

## Running from Visual Studio

1. Open `B975RgbApp.sln` in Visual Studio 2022.
2. Install the **.NET Desktop Development** workload and .NET 8 SDK if Visual Studio requests them.
3. Close KeyDominator completely, including its system-tray process.
4. Press `F5`.
5. Choose the background and reactive-effect settings, then press **Start**.

## Language Selection

Use the **Language / زبان** selector at the top of the main window:

- `فارسی` — default, right-to-left interface
- `English` — left-to-right interface

The language changes immediately and is saved for the next launch. The main window, keyboard designer, status messages, system-tray menu, file-dialog titles, and application error messages follow the selected language.

## Background Lighting

The background can be configured in two ways.

### Solid background

Select a single background color. Breathing changes its brightness between the configured minimum brightness and full intensity.

### Per-key background

You can either import a Bloody `.ckPannel` file or create a layout inside the application.

To import a profile:

1. Find **Per-key default colors**.
2. Press **Load**.
3. Select a `.ckPannel` file.

The colors are copied into the application settings. The original file does not need to remain in the same location afterward.

## Visual Keyboard Designer

Press **Design** beside **Per-key default colors**.

Selection controls:

- Click a key to select it.
- Hold `Ctrl` while clicking to select multiple keys.
- Drag over the keyboard to select a rectangular area.
- Press `Ctrl+A` to select all physical keys.
- Press `Esc` to clear the selection.

Color controls:

- Apply one solid color to the selected keys.
- Create a gradient with 2 to 8 independently configurable colors.
- Set any angle from `-180°` to `180°`.
- Use the `0°`, `45°`, `90°`, and `-45°` shortcuts.
- Use **Undo**, **Redo**, or **Reset** when needed.

If no key is selected, the solid color or gradient is applied to all 104 physical keys. The B975 also exposes 12 additional lighting slots without visible key positions; the designer preserves those slots unchanged.

Press **Apply and Close**, then press **Start** in the main window to send the design to the keyboard.

## Reactive Effects

### Flash / Fade

The pressed key moves through the configured 8-color effect palette while fading back into the Breathing background. Each palette color can be edited from the main window.

### Row Meteor

The Meteor starts at the pressed key and travels through the same keyboard row. The default settings can be replaced with a Bloody `.ckButton` file whose `ButtonType` is `1`.

To load another Meteor profile:

1. Select **Row Meteor**.
2. Press **Load ckButton**.
3. Select the `.ckButton` file.

The application imports its `ButtonDownColor` values and speed.

### Radial Explosion

The Radial Explosion uses the physical center of the pressed key as its origin. A multicolor ring expands in every direction across the keyboard and gradually blends back into the background. The forced white core used by early versions has been removed.

All reactive triggers are independent. Rapid typing or pressing the same key repeatedly creates overlapping effects; existing waves continue until their own duration ends.

## Main Settings

| Setting | Description |
| --- | --- |
| Breathing background color | Solid fallback color used when no per-key profile is active |
| Per-key default colors | Load, design, or clear a custom background layout |
| Reactive effect type | Flash/Fade, Row Meteor, or Radial Explosion |
| Reactive effect colors | Eight editable colors used by Flash/Fade and Radial Explosion |
| Breathing cycle duration | Time required for one complete brightness cycle |
| Minimum brightness | Lowest brightness reached by Breathing |
| Reactive effect duration | Lifetime of Flash/Fade or Radial Explosion |
| Start with Windows | Adds the application to the current user's Run registry key |
| Start effect automatically | Starts lighting when the application opens |
| Keep running in system tray | Closing the window hides it instead of terminating it |

## Building a Release

Run PowerShell in the project root:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\build-release.ps1
```

The framework-dependent build is created at:

```text
publish\win-x64\B975RgbApp.exe
```

To create a build that also works on machines without .NET Desktop Runtime 8:

```powershell
.\build-release.ps1 -SelfContained
```

## Application Data and Behavior

- Settings are stored in `%LocalAppData%\B975RgbApp\settings.json`.
- Imported `.ckPannel` colors and `.ckButton` Meteor values are stored in the settings file.
- Closing the window can keep the application running in the system tray.
- Use **Exit completely** from the tray menu to terminate it.
- Stopping the effect leaves the keyboard on the full background colors instead of turning it black.
- The application installs a low-level keyboard hook only while lighting is active. It maps key-down events to LED positions; it does not store typed text or send it over the network.
- The application currently contains no telemetry or online account requirement.

## Troubleshooting

### The B975 lighting interface was not found

- Confirm that the keyboard is connected directly through USB.
- Close KeyDominator completely from the system tray and Task Manager.
- Unplug and reconnect the keyboard, then restart the application.
- If another RGB utility is using the device, close it before pressing **Start**.

### The application is already running

Check the Windows system tray. Only one instance is allowed at a time.

### A key has no reactive effect

Some hardware-controlled keys, especially `Fn`, may not produce a standard Windows keyboard event and therefore may not trigger a software effect.

### Reset all saved settings

Exit the application completely, then delete:

```text
%LocalAppData%\B975RgbApp\settings.json
```

The next launch recreates it with Persian as the default language.

## Current Limitations

- Only the Bloody B975 device/interface listed above has been physically verified.
- `.ckAnimation` playback is not implemented.
- `.ckButton` import currently supports Meteor profiles with `ButtonType=1`.
- Physical key geometry is defined for the 104 visible keys; the remaining 12 LED slots are preserved but are not shown in the editor.
- Firmware revisions exposing a different HID collection may require a device-interface update.

## Project Structure

```text
src/B975RgbApp/
  MainForm.cs                    Main window and settings UI
  KeyboardLightingEditorForm.cs Visual per-key color editor
  KeyboardLayoutControl.cs      Keyboard drawing and selection
  AppLanguage.cs                Persian/English localization
  Models/LightingSettings.cs    Persisted configuration
  Services/LightingEngine.cs    Breathing and reactive-effect renderer
  Services/B975HidDevice.cs     B975 HID communication
  Services/KeyboardHook.cs      Key-down to LED mapping
  Services/CkPannelProfile.cs   .ckPannel importer
  Services/CkButtonProfile.cs   Meteor .ckButton importer
```

## Credits and Protocol Notes

See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for protocol references and third-party attribution.

