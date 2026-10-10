# RNotepad

A small Windows utility that opens your chosen editor when you launch `notepad.exe`. Run RNotepad to browse for a different editor or restore Windows Notepad.

Written by **Doug Lawlor**, with assistance from **Codex**. The code and documentation are licensed under the [MIT License](LICENSE).

## Use

1. Run `rnotepad.exe`. If Windows displays a SmartScreen warning, follow the [SmartScreen keyboard and screen reader instructions](#smartscreen-keyboard-and-screen-reader-instructions) below. The main window opens with focus on the editor executable field.
2. Choose **Browse** or press **Alt+B** to select your editor's `.exe`, such as `notepad++.exe` or `Code.exe`. You can also type its path in the editor executable field.
3. If files opened from File Explorer still launch Windows Notepad, check **Set Notepad file associations (opens Windows Default apps)** immediately before **Enable / save**. This option is unchecked by default; use Tab to reach it, Space to toggle it, or Alt+F. Click **Enable / save**, then approve the Windows administrator prompt.
4. Press Windows+R and enter `notepad`, or enter `notepad "C:\path\my file.txt"` at a command prompt.
5. Run `rnotepad` from Windows+R to change the editor. Open a new command window to use `rnotepad` there. An existing Windows Terminal process may need to be fully restarted.
6. To return to Windows Notepad and uninstall RNotepad, click **Restore Notepad**, approve the administrator prompt, and close RNotepad.

For an explanation of RNotepad and how to use it, choose **Help** immediately after **Restore Notepad**, or press **Alt+H**. The help dialog opens with focus on read-only text you can review using the arrow keys. Press **Escape** or choose **Close** to return to the Help button.

You can distribute just `rnotepad.exe`; no separate installer package is required. RNotepad is a single-file, self-installing utility, rather than a fully portable application. Opening its settings does not install it; clicking **Enable / save** performs the installation with administrator approval.

The build embeds the complete MIT license in `rnotepad.exe` and places a readable `LICENSE` file beside it. Include `LICENSE` when distributing the executable or source, and retain the copyright and permission notices in copies or substantial portions of the software.

Enable installs the utility into `%ProgramFiles%\RNotepad` (usually `C:\Program Files\RNotepad`), registers the Run command, and adds that directory to the machine PATH. Windows uses this installed executable to launch the selected editor, so keep it in place while redirection is enabled. Deleting or moving the installed copy breaks redirected launches. Keep the downloaded/build copy if you want to reinstall after restoring Notepad.

**Restore Notepad** restores the saved Notepad registry settings and uninstalls RNotepad: it removes the installed executable, its Run command registration, its machine PATH entry, and its configuration. Keep your downloaded copy if you want to enable it again later. Restoring and enabling affect all users and require administrator approval; ordinary editor launches do not request elevation.

Enable also repairs the Windows Run registration for your account so `notepad` launches through the classic Windows executable and reaches the redirect. Restore puts back your original registration, preserving later changes by other programs. Other accounts can run RNotepad and enable/save to repair their own Run registration.

The file associations checkbox finds file extensions currently assigned to classic or packaged Windows Notepad for your account. After a successful save, RNotepad lists those extensions and opens Windows **Default apps**. For each listed extension, use the file type search field, select the current default, choose your editor, and confirm the change. If your editor is not listed, choose the option to browse for an app on your PC. Use Tab and Shift+Tab to move between controls and Enter or Space to activate a focused button. Return to RNotepad with Alt+Tab to review the extension list and chosen editor path in the **Redirection status** field.

Windows requires you to confirm default-app changes in its own interface; checking the box alone does not change the defaults. This also handles packaged Notepad associations that bypass the executable redirect. Types assigned to other programs are left alone. If no Notepad file types are found, RNotepad reports that and does not open Settings. Defaults you choose in Windows remain in place after **Restore Notepad**; change them back through Default apps if desired. See Microsoft's [Windows default-app requirements](https://learn.microsoft.com/en-us/windows/apps/develop/windows-integration/default-apps-platform).

### SmartScreen keyboard and screen reader instructions

RNotepad is unsigned, so Windows may display a Microsoft Defender SmartScreen dialog with the text **Windows protected your PC** when you launch a downloaded copy. Identify the dialog by its text; you do not need to see its color or use a mouse. A warning can also appear for a new version. Microsoft's [SmartScreen reputation guidance](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation) explains these warnings.

If you trust the source of your downloaded copy and want to run it:

1. Press **Tab** or **Shift+Tab** until your screen reader announces **More info**, then press **Enter**.
2. Read the expanded details using your screen reader. Confirm that the app is `rnotepad.exe`. An unsigned copy may show **Unknown publisher**; this is not confirmation that the file is safe.
3. Press **Tab** or **Shift+Tab** until **Run anyway** is announced, then press **Enter**. Do not press Enter before checking which control has focus. The dialog's focus order and announcements can vary by Windows version and screen reader.
4. Once RNotepad opens, continue with choosing your editor in step 2 of the Use instructions.

To cancel instead, press **Tab** or **Shift+Tab** until your screen reader announces **Don't run**, then press **Enter**. If the warning is not being read, use **Alt+Tab** to select the SmartScreen dialog, listening to the window names as you switch. If your usual screen reader still cannot read it, Windows Narrator is another option: **Windows+Ctrl+Enter** starts or stops Narrator, as described in Microsoft's [Narrator introduction](https://support.microsoft.com/en-us/accessibility/windows/narrator/chapter-1-introducing-narrator). Avoid running two screen readers at once if their speech or keyboard commands conflict.

If **Run anyway** is unavailable after expanding **More info**, your device's security policy may prevent proceeding. On a managed device, contact your administrator. These instructions apply to the SmartScreen unrecognized-app warning; other security blocks may have different options.

The SmartScreen warning is separate from the administrator approval prompt shown later when you choose **Enable / save** or **Restore Notepad**. Allowing the app through SmartScreen does not approve those changes.

## Behavior and limits

- Uses Windows Image File Execution Options (IFEO) for `notepad.exe`; it does not replace the Windows executable. The redirect itself leaves file associations unchanged; the optional checkbox opens Windows Default apps so you can confirm association changes for your account.
- Preserves file names, argument boundaries, and the caller's working directory. Notepad-specific switches are passed through; the selected editor must support the arguments you use.
- Rejects `notepad.exe` and `rnotepad.exe` as replacement editors to prevent recursion. Do not select a renamed copy of either executable or a launcher that calls Notepad.
- Supports Windows 11's filtered Notepad entries. Enable saves the original alias-routing and `UseFilter` values, disables path filtering so the root redirect covers unmatched executable paths, and adds redirection to matching Notepad filters. Restore puts back the original values and preserves the filters and package metadata.
- Refuses to overwrite another tool's debugger, including one inside a matching filter. Restore also checks for subsequent changes by another tool before modifying any saved entry.
- Applies to classic launches of `notepad.exe` and the repaired Windows Run command. Packaged Notepad launched directly through its executable, package, or URI can bypass this mechanism. Check the Run and command-prompt paths on your Windows installation after enabling.
- A missing/uninstalled editor produces an error explaining the failure. Run `rnotepad` to choose another editor or restore Notepad.
- To update, close any installed RNotepad window, run the new executable from your download/beta folder, then enable/save. The administrator helper updates the installed copy. If that copy is still running, close it and retry.
- If an older version says redirection is enabled but Windows Run still opens Notepad, run the updated build and click **Enable / save** to repair the configuration. The updated status detects enabled path filtering instead of treating a saved debugger entry alone as success.

## Build and checks

Requires Windows with .NET Framework 4.8. No SDK, external packages, or downloads are needed; the build uses the framework compiler included with Windows.

```powershell
.\build.ps1
.\test.ps1
```

Tests launch a harmless argument-recorder process to check command-line forwarding, validate editor rejection and debugger ownership, and inspect accessible control names and keyboard properties. Registry regression tests recreate Windows 11's filtered entries and Run registration under a temporary current-user test key, verify upgrades, repeated saves and exact restoration, and check foreign-debugger conflicts and preservation of subsequent registration changes; the test key is removed afterward. Tests do not alter actual Notepad IFEO settings, PATH, or machine configuration. The Enable/UAC/Restore flow requires a manual check on the target Windows machine. Screen-reader behavior and enlarged display settings require manual testing as well.

## Recovery and removal

If the installed utility was deleted while enabled, first put a copy of `rnotepad.exe` back in `%ProgramFiles%\RNotepad`, run it, and use **Restore Notepad**. That restores saved Windows 11 alias values as well as debugger entries.

For a first-release installation without filtered-entry backups, an administrator Command Prompt can remove the root Debugger value **only if it points to RNotepad**:

```bat
reg query "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\notepad.exe" /v Debugger
reg delete "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\notepad.exe" /v Debugger /f
```

To remove the utility completely, use **Restore Notepad**, approve the administrator prompt, then close RNotepad. Restore performs these steps automatically:

- Restores the saved Notepad debugger, Windows 11 alias-routing, and filtering values.
- Restores your account's original Windows Run registration for Notepad.
- Removes `%ProgramFiles%\RNotepad\rnotepad.exe` and the installation directory if it is empty.
- Removes RNotepad's Run command registration, machine PATH entry, and saved configuration.

If the installed executable is running, Restore moves it to a temporary file outside Program Files and deletes that file after the running copies exit. Unrelated files and PATH entries are preserved. Restoration uses the saved original registry values and preserves subsequent changes by other programs.

After restoring, `rnotepad` is no longer installed as a Run command. To enable redirection again, run your downloaded/build copy and click **Enable / save**. Restart existing command windows or Windows Terminal to refresh their PATH.

The Run registration follows [Microsoft's App Paths documentation](https://learn.microsoft.com/en-us/windows/win32/shell/app-registration).

## Author and license

Author: **Doug Lawlor**, with assistance from **Codex**.

RNotepad's code and documentation are MIT licensed. The full license is included below and in [LICENSE](LICENSE).

MIT License

Copyright (c) 2026 Doug Lawlor

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
