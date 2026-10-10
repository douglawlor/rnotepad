// Copyright (c) 2026 Doug Lawlor.
// SPDX-License-Identifier: MIT
// See LICENSE for the full license text.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class Tests
{
    private static int checks;
    private static void Check(bool valid, string name)
    {
        if (!valid) throw new Exception("FAIL: " + name);
        checks++;
        Console.WriteLine("PASS: " + name);
    }
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Assembly app = Assembly.LoadFrom(args[0]);
            Type program = app.GetType("RNotepad.Program");
            Type settings = app.GetType("RNotepad.Settings");
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            var quote = program.GetMethod("Quote", flags);
            Check((string)quote.Invoke(null, new object[] { "" }) == "\"\"", "empty argument quoting");
            string output = Path.Combine(Path.GetDirectoryName(args[1]), "arguments.txt");
            Environment.SetEnvironmentVariable("RNOTEPAD_TEST_OUTPUT", output);
            if (File.Exists(output)) File.Delete(output);
            string[] expected = { "", "ordinary", "a file with spaces.txt", "C:\\folder\\", "quote\"inside", "slash\\\"quote", "日本語 café.txt", "& %PATH% ; $(test)", "--editor-option" };
            program.GetMethod("Launch", flags).Invoke(null, new object[] { args[1], expected });
            for (int i = 0; i < 100 && !File.Exists(output); i++) Thread.Sleep(50);
            Thread.Sleep(100);
            var actual = File.ReadAllLines(output).Select(x => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(x))).ToArray();
            Check(expected.SequenceEqual(actual), "real child process preserves spaces, quotes, trailing slashes, Unicode, empty args and shell metacharacters");
            var validate = program.GetMethod("ValidateEditor", flags);
            foreach (string input in new[] { "missing.exe", args[0], Path.Combine(Path.GetDirectoryName(args[0]), "notepad.exe") })
            {
                bool rejected = false;
                try { validate.Invoke(null, new object[] { input }); }
                catch (TargetInvocationException e) { rejected = e.InnerException is ArgumentException; }
                Check(rejected, "reject invalid or recursive editor: " + Path.GetFileName(input));
            }
            string own = (string)settings.GetProperty("DebuggerCommand", flags).GetValue(null, null);
            var owned = settings.GetMethod("IsOwned", flags);
            Check((bool)owned.Invoke(null, new object[] { own }), "recognize own debugger");
            Check(!(bool)owned.Invoke(null, new object[] { "other-tool.exe --redirect" }), "do not claim another debugger");
            RegistryChecks(settings, flags, own);
            using (var form = (Form)Activator.CreateInstance(app.GetType("RNotepad.EditorForm"), true))
            {
                var controls = AllControls(form).ToArray();
                Check(controls.OfType<TextBox>().Any(c => c.AccessibleName == "Editor executable" && c.TabIndex == 0), "editor accessible name and tab order");
                Check(controls.OfType<TextBox>().Any(c => c.AccessibleName == "Redirection status" && c.ReadOnly && c.TabStop), "status accessible for persistent keyboard review");
                Check(controls.OfType<Button>().Count(c => c.Text.Contains("&")) == 5, "all actions have keyboard mnemonics");
                var help = controls.OfType<Button>().Single(c => c.AccessibleName == "Help");
                var restore = controls.OfType<Button>().Single(c => c.Text == "&Restore Notepad");
                Check(help.Parent == restore.Parent && help.Parent.Controls.GetChildIndex(help) == help.Parent.Controls.GetChildIndex(restore) + 1 && help.TabIndex == restore.TabIndex + 1,
                    "Help immediately follows Restore Notepad in control and tab order");
                var mnemonicKeys = controls.Where(c => c.Text.Contains("&")).Select(c => char.ToUpperInvariant(c.Text[c.Text.IndexOf('&') + 1])).ToArray();
                Check(mnemonicKeys.Distinct().Count() == mnemonicKeys.Length, "editor label and action mnemonics are unique");
                Check(form.CancelButton != null, "Escape closes idle settings form");
                var associations = controls.OfType<CheckBox>().Single();
                var enable = controls.OfType<Button>().Single(c => c.Text == "Enable / &save");
                Check(associations.AccessibleName == "Set Notepad file associations (opens Windows Default apps)" && associations.AccessibleDescription.Contains("confirm each default") && !associations.Checked && associations.TabStop,
                    "file associations checkbox exposes purpose and defaults to unchecked with keyboard access");
                Check(associations.TabIndex == 3 && associations.Parent == enable.Parent.Parent && enable.Parent.TabIndex == 4 && enable.TabIndex == 0,
                    "file associations checkbox immediately precedes Enable/save in tab order");
                associations.Checked = true;
                Check(associations.CheckState == CheckState.Checked, "native checkbox exposes toggled checked state");
                var editor = controls.OfType<TextBox>().First(c => c.AccessibleName == "Editor executable");
                editor.Text = "";
                form.Show();
                Application.DoEvents();
                Check(editor.Focused && form.Visible && Application.OpenForms.Count == 1, "blank editor startup keeps focus in the main window without opening Browse");
                editor.Text = args[1];
                form.PerformLayout();
                HelpChecks(app, form, help);
                using (var bitmap = new System.Drawing.Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height));
                    bitmap.Save(Path.Combine(Path.GetDirectoryName(args[1]), "dialog.png"));
                }
            }
            Console.WriteLine(checks + " checks passed. No machine settings changed.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    private static void HelpChecks(Assembly app, Form owner, Button help)
    {
        bool opened = false;
        Exception failure = null;
        using (var timer = new System.Windows.Forms.Timer { Interval = 100 })
        {
            timer.Tick += delegate {
                timer.Stop();
                var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.GetType() == app.GetType("RNotepad.HelpForm"));
                try
                {
                    Check(dialog != null && dialog.Modal && dialog.Owner == owner, "Help opens an owned modal dialog");
                    opened = true;
                    var text = AllControls(dialog).OfType<TextBox>().Single();
                    Check(text.ReadOnly && text.Multiline && text.TabStop && text.ScrollBars == ScrollBars.Vertical && text.AccessibleName == "How RNotepad works" && text.Focused && text.SelectionStart == 0,
                        "help text is named, scrollable, read-only and initially focused at the beginning");
                    Check(text.Text.Contains("preferred text editor") && text.Text.Contains("Restore Notepad") && text.Text.Contains("Default apps"), "help explains purpose, restoration and file associations");
                    var close = (Button)dialog.CancelButton;
                    Check(close.AccessibleName == "Close help" && close.TabIndex > text.TabIndex && close.DialogResult == DialogResult.Cancel, "help provides a named Close action and Escape dismissal");
                    close.PerformClick();
                }
                catch (Exception ex) { failure = ex; if (dialog != null) dialog.Close(); else owner.Close(); }
            };
            help.Focus();
            timer.Start();
            help.PerformClick();
        }
        if (failure != null) throw failure;
        Check(opened && owner.Enabled && help.Focused, "closing help restores focus to Help and enables the settings window");
    }
    private static void RegistryChecks(Type settings, BindingFlags flags, string own)
    {
        string testPath = @"Software\RNotepad.Tests-" + Guid.NewGuid().ToString("N");
        using (var root = Registry.CurrentUser.CreateSubKey(testPath))
        {
            try
            {
                var apply = settings.GetMethod("ApplyRedirect", flags);
                using (var classes = root.CreateSubKey("AssociationClasses"))
                using (var fileExts = root.CreateSubKey("AssociationChoices"))
                {
                    using (var key = classes.CreateSubKey(".txt")) key.SetValue("", "TextFile");
                    using (var key = classes.CreateSubKey(@"TextFile\shell\open\command")) key.SetValue("", "\"%SystemRoot%\\system32\\notepad.exe\" \"%1\"", RegistryValueKind.ExpandString);
                    using (var key = classes.CreateSubKey(".log")) key.SetValue("", "TextFile");
                    using (var key = fileExts.CreateSubKey(@".log\UserChoice")) key.SetValue("ProgId", "OtherEditor");
                    using (var key = classes.CreateSubKey(@"OtherEditor\shell\open\command")) key.SetValue("", "\"C:\\Editor\\editor.exe\" \"%1\"");
                    using (var key = fileExts.CreateSubKey(@".md\UserChoice")) key.SetValue("ProgId", "PackagedNotepad");
                    using (var key = classes.CreateSubKey(@"PackagedNotepad\Application")) key.SetValue("AppUserModelID", "Microsoft.WindowsNotepad_8wekyb3d8bbwe!App");
                    using (var key = classes.CreateSubKey(".fake")) key.SetValue("", "FakeNotepad");
                    using (var key = classes.CreateSubKey(@"FakeNotepad\shell\open\command")) key.SetValue("", "\"C:\\Editor\\notepad++.exe\" \"%1\"");
                    var found = (string[])settings.GetMethod("FindNotepadFileTypes", flags).Invoke(null, new object[] { classes, fileExts });
                    Check(found.SequenceEqual(new[] { ".md", ".txt" }), "association scan detects classic and packaged Notepad while respecting other user defaults and editors");
                    using (var key = fileExts.OpenSubKey(@".log\UserChoice")) Check((string)key.GetValue("ProgId") == "OtherEditor", "association discovery leaves UserChoice unchanged");
                }
                string installDirectory = Path.Combine(Path.GetTempPath(), "RNotepad.Tests-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(installDirectory);
                string installed = Path.Combine(installDirectory, "rnotepad.exe");
                File.WriteAllText(installed, "test installation");
                using (var registration = root.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\rnotepad.exe")) registration.SetValue("", installed);
                using (var environment = root.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment"))
                    environment.SetValue("Path", @"%SystemRoot%\System32;" + installDirectory + @";C:\Other", RegistryValueKind.ExpandString);
                using (var config = root.CreateSubKey(@"SOFTWARE\RNotepad")) config.SetValue("Editor", "test");
                settings.GetMethod("RemoveInstallation", flags).Invoke(null, new object[] { root, installed });
                Check(!File.Exists(installed) && !Directory.Exists(installDirectory), "uninstall removes installed executable and empty directory");
                Check(root.OpenSubKey(@"SOFTWARE\RNotepad") == null && root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\rnotepad.exe") == null, "uninstall removes configuration and Run registration");
                using (var environment = root.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment"))
                    Check((string)environment.GetValue("Path", null, RegistryValueOptions.DoNotExpandEnvironmentNames) == @"%SystemRoot%\System32;C:\Other" && environment.GetValueKind("Path") == RegistryValueKind.ExpandString, "uninstall preserves other PATH entries and value kind");
                var undo = settings.GetMethod("UndoRedirect", flags);
                var complete = settings.GetMethod("IsRedirectComplete", flags);
                var applyRun = settings.GetMethod("ApplyRunRedirect", flags);
                var undoRun = settings.GetMethod("UndoRunRedirect", flags);
                string classic = (string)settings.GetProperty("ClassicNotepad", flags).GetValue(null, null);
                using (var app = root.CreateSubKey("RunApp"))
                using (var config = root.CreateSubKey("RunConfig"))
                {
                    app.SetValue("", @"%ProgramFiles%\WindowsApps\Notepad\Notepad.exe", RegistryValueKind.ExpandString);
                    app.SetValue("Path", @"C:\Package\Notepad");
                    app.SetValue("Unrelated", "keep");
                    applyRun.Invoke(null, new object[] { app, config });
                    Check((string)app.GetValue("") == classic, "Run registration uses classic Notepad to reach IFEO instead of packaged activation");
                    applyRun.Invoke(null, new object[] { app, config });
                    undoRun.Invoke(null, new object[] { app, config });
                    Check((string)app.GetValue("", null, RegistryValueOptions.DoNotExpandEnvironmentNames) == @"%ProgramFiles%\WindowsApps\Notepad\Notepad.exe" && app.GetValueKind("") == RegistryValueKind.ExpandString && (string)app.GetValue("Path") == @"C:\Package\Notepad" && (string)app.GetValue("Unrelated") == "keep", "Run restore preserves original default, value kind and other registration values after repeated saves");
                    applyRun.Invoke(null, new object[] { app, config });
                    app.SetValue("", @"C:\AnotherTool.exe");
                    undoRun.Invoke(null, new object[] { app, config });
                    Check((string)app.GetValue("") == @"C:\AnotherTool.exe", "Run restore preserves another tool's later registration");
                    app.DeleteValue("");
                    applyRun.Invoke(null, new object[] { app, config });
                    undoRun.Invoke(null, new object[] { app, config });
                    Check(app.GetValue("") == null, "Run restore removes a default that originally did not exist");
                }
                using (var ifeo = root.CreateSubKey("Filtered"))
                using (var config = root.CreateSubKey("Config"))
                {
                    ifeo.SetValue("UseFilter", 1, RegistryValueKind.DWord);
                    for (int i = 0; i < 3; i++)
                    {
                        using (var filter = ifeo.CreateSubKey(i.ToString()))
                        {
                            filter.SetValue("FilterFullPath", new[] { @"C:\Windows\System32\notepad.exe", @"C:\Windows\SysWOW64\notepad.exe", @"C:\Windows\notepad.exe" }[i]);
                            filter.SetValue("AppExecutionAliasRedirect", 1, RegistryValueKind.DWord);
                            filter.SetValue("AppExecutionAliasRedirectPackages", "*");
                        }
                    }
                    using (var other = ifeo.CreateSubKey("Unrelated"))
                    {
                        other.SetValue("FilterFullPath", @"C:\Other\other.exe");
                        other.SetValue("Debugger", "other-debugger.exe");
                    }
                    string before = Snapshot(ifeo);
                    ifeo.SetValue("Debugger", own);
                    Check(!(bool)complete.Invoke(null, new object[] { ifeo }), "root debugger with enabled path filters is incomplete for packaged Run paths");
                    ifeo.DeleteValue("Debugger");
                    apply.Invoke(null, new object[] { ifeo, config });
                    Check((string)ifeo.GetValue("Debugger") == own && (int)ifeo.GetValue("UseFilter") == 0 && (bool)complete.Invoke(null, new object[] { ifeo }), "root redirect covers unmatched packaged and alias paths by disabling UseFilter");
                    for (int i = 0; i < 3; i++)
                        using (var filter = ifeo.OpenSubKey(i.ToString()))
                            Check((string)filter.GetValue("Debugger") == own && (int)filter.GetValue("AppExecutionAliasRedirect") == 0 && (string)filter.GetValue("AppExecutionAliasRedirectPackages") == "*", "redirect Windows Notepad filter " + i + " and preserve package metadata");
                    apply.Invoke(null, new object[] { ifeo, config });
                    undo.Invoke(null, new object[] { ifeo, config });
                    Check(Snapshot(ifeo) == before, "restore exact Windows11 values after repeated saves, preserving unrelated filter");
                    apply.Invoke(null, new object[] { ifeo, config });
                    using (var filter = ifeo.OpenSubKey("1", true)) filter.SetValue("Debugger", "new-owner.exe");
                    string changed = Snapshot(ifeo);
                    bool refused = false;
                    try { undo.Invoke(null, new object[] { ifeo, config }); }
                    catch (TargetInvocationException e) { refused = e.InnerException is InvalidOperationException; }
                    Check(refused && Snapshot(ifeo) == changed, "restore refuses a changed debugger before modifying any entry");
                    using (var filter = ifeo.OpenSubKey("1", true)) filter.SetValue("Debugger", own);
                    undo.Invoke(null, new object[] { ifeo, config });
                    apply.Invoke(null, new object[] { ifeo, config });
                    ifeo.SetValue("UseFilter", 1, RegistryValueKind.DWord);
                    undo.Invoke(null, new object[] { ifeo, config });
                    Check(Snapshot(ifeo) == before, "restore preserves a subsequent external change to UseFilter");
                    // Simulate a saved installation from the previous release, whose backup
                    // records did not include the root UseFilter value.
                    apply.Invoke(null, new object[] { ifeo, config });
                    using (var backup = config.OpenSubKey("RedirectionBackup", true))
                    {
                        backup.DeleteValue("HadUseFilter");
                        backup.DeleteValue("UseFilter");
                    }
                    ifeo.SetValue("UseFilter", 1, RegistryValueKind.DWord);
                    apply.Invoke(null, new object[] { ifeo, config });
                    Check((bool)complete.Invoke(null, new object[] { ifeo }), "Enable repairs the previous release's filtered redirect");
                    undo.Invoke(null, new object[] { ifeo, config });
                    Check(Snapshot(ifeo) == before, "upgraded installation restores original filters and aliases");
                    using (var filter = ifeo.OpenSubKey("2", true)) filter.SetValue("Debugger", "foreign.exe");
                    changed = Snapshot(ifeo);
                    refused = false;
                    try { apply.Invoke(null, new object[] { ifeo, config }); }
                    catch (TargetInvocationException e) { refused = e.InnerException is InvalidOperationException; }
                    Check(refused && Snapshot(ifeo) == changed, "enable refuses foreign filtered debugger without partial changes");
                }
                using (var ifeo = root.CreateSubKey("Classic"))
                using (var config = root.CreateSubKey("ClassicConfig"))
                {
                    ifeo.SetValue("UnrelatedValue", "keep");
                    string before = Snapshot(ifeo);
                    apply.Invoke(null, new object[] { ifeo, config });
                    undo.Invoke(null, new object[] { ifeo, config });
                    Check(Snapshot(ifeo) == before, "classic Notepad enable/restore leaves no added values");
                    ifeo.SetValue("Debugger", own);
                    undo.Invoke(null, new object[] { ifeo, config });
                    Check(Snapshot(ifeo) == before, "restore first-release debugger with no backup");
                }
            }
            finally { Registry.CurrentUser.DeleteSubKeyTree(testPath, false); }
        }
    }
    private static string Snapshot(RegistryKey key)
    {
        var lines = new System.Collections.Generic.List<string>();
        foreach (string name in key.GetValueNames().OrderBy(x => x))
            lines.Add(name + "=" + key.GetValueKind(name) + ":" + key.GetValue(name));
        foreach (string name in key.GetSubKeyNames().OrderBy(x => x))
            using (var child = key.OpenSubKey(name)) lines.Add("[" + name + "]" + Snapshot(child));
        return string.Join("\n", lines);
    }
    private static System.Collections.Generic.IEnumerable<Control> AllControls(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control nested in AllControls(child)) yield return nested;
        }
    }
}
