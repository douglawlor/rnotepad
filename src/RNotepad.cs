// Copyright (c) 2026 Doug Lawlor. Developed with assistance from Codex.
// SPDX-License-Identifier: MIT
// See LICENSE for the full license text.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace RNotepad
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            IWin32Window owner = null;
            try
            {
                if (args.Length >= 2 && args[0] == "--owner")
                {
                    long handle;
                    if (!long.TryParse(args[1], out handle)) throw new ArgumentException("Invalid settings window handle.");
                    owner = new WindowOwner(new IntPtr(handle));
                    args = args.Skip(2).ToArray();
                }
                if (args.Length > 0 && args[0] == "--redirect")
                {
                    if (args.Length < 2 || !Path.GetFileName(args[1]).Equals("notepad.exe", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("The Notepad launch command is invalid.");
                    string editor = Settings.ReadEditor();
                    Launch(editor, args.Skip(2).ToArray());
                    return 0;
                }
                if (args.Length > 0 && args[0] == "--enable" && args.Length == 2)
                {
                    Settings.Enable(args[1]);
                    if (owner == null) Settings.EnableRun();
                    return 0;
                }
                if (args.Length == 1 && args[0] == "--restore")
                {
                    Settings.Restore();
                    if (owner == null) Settings.RestoreRun();
                    return 0;
                }
                if (args.Length != 0) throw new ArgumentException("Run rnotepad without arguments to choose an editor.");
                Application.Run(new EditorForm());
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, ex.Message, "RNotepad — unable to complete", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        internal static string ValidateEditor(string input)
        {
            string path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(input.Trim().Trim('"')));
            if (!File.Exists(path) || !Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Choose an existing editor executable (.exe).");
            string name = Path.GetFileName(path);
            if (name.Equals("notepad.exe", StringComparison.OrdinalIgnoreCase) || name.Equals("rnotepad.exe", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Choose a different editor. Notepad and RNotepad would create a launch loop.");
            return path;
        }

        internal static void Launch(string editor, string[] arguments)
        {
            editor = ValidateEditor(editor);
            Process.Start(new ProcessStartInfo(editor, JoinArguments(arguments)) { UseShellExecute = false });
        }

        // Windows command line quoting: double backslashes before quotes and closing quotes.
        internal static string Quote(string value)
        {
            var result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') result.Append('\\', slashes * 2 + 1);
                else result.Append('\\', slashes);
                result.Append(c);
                slashes = 0;
            }
            result.Append('\\', slashes * 2);
            return result.Append('"').ToString();
        }

        internal static string JoinArguments(string[] args)
        {
            return string.Join(" ", args.Select(Quote));
        }
    }

    internal sealed class WindowOwner : IWin32Window
    {
        public IntPtr Handle { get; private set; }
        internal WindowOwner(IntPtr handle) { Handle = handle; }
    }

    internal static class Settings
    {
        internal const string IfeoKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\notepad.exe";
        private const string ConfigKey = @"SOFTWARE\RNotepad";
        private const string AppKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\rnotepad.exe";
        private const string NotepadAppKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\notepad.exe";
        internal static string ClassicNotepad { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "notepad.exe"); } }
        internal static string InstalledExe
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "RNotepad", "rnotepad.exe"); }
        }
        internal static string DebuggerCommand { get { return Program.Quote(InstalledExe) + " --redirect"; } }
        private static RegistryKey Machine()
        {
            return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32);
        }
        internal static string ReadEditor()
        {
            using (var root = Machine())
            using (var config = root.OpenSubKey(ConfigKey))
                return config == null ? "" : config.GetValue("Editor", "") as string ?? "";
        }
        internal static string[] NotepadFileTypes()
        {
            using (var fileExts = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts"))
                return FindNotepadFileTypes(Registry.ClassesRoot, fileExts);
        }

        internal static string[] FindNotepadFileTypes(RegistryKey classes, RegistryKey fileExts)
        {
            var extensions = classes.GetSubKeyNames().Where(n => n.StartsWith(".", StringComparison.Ordinal));
            if (fileExts != null) extensions = extensions.Concat(fileExts.GetSubKeyNames());
            var matches = new System.Collections.Generic.List<string>();
            foreach (string extension in extensions.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!extension.StartsWith(".", StringComparison.Ordinal)) continue;
                string progId = null;
                using (var choice = fileExts == null ? null : fileExts.OpenSubKey(extension + @"\UserChoice"))
                    if (choice != null) progId = choice.GetValue("ProgId") as string;
                if (string.IsNullOrEmpty(progId))
                    using (var key = classes.OpenSubKey(extension))
                        if (key != null) progId = key.GetValue("") as string;
                if (string.IsNullOrEmpty(progId)) continue;
                using (var application = classes.OpenSubKey(progId + @"\Application"))
                {
                    string id = application == null ? "" : application.GetValue("AppUserModelID", "") as string ?? "";
                    if (id.StartsWith("Microsoft.WindowsNotepad_", StringComparison.OrdinalIgnoreCase))
                    {
                        matches.Add(extension); continue;
                    }
                }
                using (var command = classes.OpenSubKey(progId + @"\shell\open\command"))
                {
                    string value = command == null ? "" : command.GetValue("", "") as string ?? "";
                    var executable = System.Text.RegularExpressions.Regex.Match(value, "^\\s*(?:\"([^\"]+)\"|(\\S+))");
                    if (executable.Success)
                    {
                        string path = Environment.ExpandEnvironmentVariables(executable.Groups[1].Success ? executable.Groups[1].Value : executable.Groups[2].Value);
                        if (string.Equals(Path.GetFileName(path), "notepad.exe", StringComparison.OrdinalIgnoreCase)) matches.Add(extension);
                    }
                }
            }
            return matches.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        internal static string ReadDebugger()
        {
            using (var root = Machine())
            using (var key = root.OpenSubKey(IfeoKey))
            {
                if (key == null) return "";
                foreach (string target in RedirectTargets(key))
                {
                    using (var entry = OpenEntry(key, target, false))
                    {
                        string debugger = entry.GetValue("Debugger", "") as string ?? "";
                        if (debugger.Length != 0) return debugger;
                    }
                }
                return "";
            }
        }
        internal static bool IsOwned(string value)
        {
            return string.Equals(value, DebuggerCommand, StringComparison.OrdinalIgnoreCase);
        }
        internal static bool IsRedirectComplete(RegistryKey ifeo)
        {
            return ifeo != null && IsOwned(ifeo.GetValue("Debugger", "") as string ?? "") &&
                Convert.ToInt32(ifeo.GetValue("UseFilter", 0)) == 0 &&
                Convert.ToInt32(ifeo.GetValue("AppExecutionAliasRedirect", 0)) == 0;
        }
        internal static bool RedirectComplete()
        {
            using (var root = Machine())
            using (var ifeo = root.OpenSubKey(IfeoKey))
            using (var app = Registry.CurrentUser.OpenSubKey(NotepadAppKey))
                return IsRedirectComplete(ifeo) && app != null &&
                    string.Equals(app.GetValue("") as string, ClassicNotepad, StringComparison.OrdinalIgnoreCase);
        }
        internal static void ApplyRunRedirect(RegistryKey app, RegistryKey config)
        {
            using (var backup = config.CreateSubKey("RunBackup"))
            {
                if (backup.GetValue("HadDefault") == null)
                {
                    object previous = app.GetValue("", null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                    backup.SetValue("HadDefault", previous == null ? 0 : 1);
                    if (previous != null) backup.SetValue("Default", previous, app.GetValueKind(""));
                }
                // Route the shell through the classic executable, which honors IFEO.
                // Packaged activation can bypass the Debugger even without path filters.
                app.SetValue("", ClassicNotepad, RegistryValueKind.String);
            }
        }
        internal static void UndoRunRedirect(RegistryKey app, RegistryKey config)
        {
            using (var backup = config.OpenSubKey("RunBackup"))
            {
                if (backup == null) return;
                if (app != null && string.Equals(app.GetValue("") as string, ClassicNotepad, StringComparison.OrdinalIgnoreCase))
                {
                    if (Convert.ToInt32(backup.GetValue("HadDefault", 0)) != 0)
                        app.SetValue("", backup.GetValue("Default", null, RegistryValueOptions.DoNotExpandEnvironmentNames), backup.GetValueKind("Default"));
                    else app.DeleteValue("", false);
                }
            }
            config.DeleteSubKey("RunBackup");
        }
        internal static void EnableRun()
        {
            using (var app = Registry.CurrentUser.CreateSubKey(NotepadAppKey))
            using (var config = Registry.CurrentUser.CreateSubKey(ConfigKey)) ApplyRunRedirect(app, config);
        }
        internal static void RestoreRun()
        {
            using (var app = Registry.CurrentUser.OpenSubKey(NotepadAppKey, true))
            using (var config = Registry.CurrentUser.OpenSubKey(ConfigKey, true))
                if (config != null) UndoRunRedirect(app, config);
            using (var config = Registry.CurrentUser.OpenSubKey(ConfigKey))
                if (config != null && config.ValueCount == 0 && config.SubKeyCount == 0)
                {
                    config.Close();
                    Registry.CurrentUser.DeleteSubKey(ConfigKey, false);
                }
        }
        private static void RequireAdmin()
        {
            using (var identity = WindowsIdentity.GetCurrent())
                if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
                    throw new InvalidOperationException("Administrator permission is required to change the Notepad redirection.");
        }
        internal static void Enable(string input)
        {
            RequireAdmin();
            string editor = Program.ValidateEditor(input);
            using (var root = Machine())
            using (var ifeo = root.CreateSubKey(IfeoKey))
            {
                CheckConflicts(ifeo);
                Install(root);
                using (var config = root.CreateSubKey(ConfigKey))
                {
                    object previous = config.GetValue("Editor");
                    try
                    {
                        config.SetValue("Editor", editor, RegistryValueKind.String);
                        ApplyRedirect(ifeo, config);
                    }
                    catch
                    {
                        if (previous == null) config.DeleteValue("Editor", false);
                        else config.SetValue("Editor", previous, RegistryValueKind.String);
                        throw;
                    }
                }
            }
        }

        private static string[] RedirectTargets(RegistryKey ifeo)
        {
            var targets = new System.Collections.Generic.List<string> { "" };
            if (Convert.ToInt32(ifeo.GetValue("UseFilter", 0)) != 0)
            {
                foreach (string name in ifeo.GetSubKeyNames())
                {
                    using (var filter = ifeo.OpenSubKey(name))
                    {
                        string path = filter.GetValue("FilterFullPath", "") as string ?? "";
                        if (Path.GetFileName(path).Equals("notepad.exe", StringComparison.OrdinalIgnoreCase)) targets.Add(name);
                    }
                }
            }
            return targets.ToArray();
        }

        private static RegistryKey OpenEntry(RegistryKey ifeo, string target, bool writable)
        {
            // The empty target refers to the image key itself; opening "" returns a separate handle.
            return ifeo.OpenSubKey(target, writable);
        }

        private static void CheckConflicts(RegistryKey ifeo)
        {
            foreach (string target in RedirectTargets(ifeo))
            {
                using (var entry = OpenEntry(ifeo, target, false))
                {
                    string current = entry.GetValue("Debugger", "") as string ?? "";
                    if (current.Length != 0 && !IsOwned(current))
                        throw new InvalidOperationException("Notepad already has a debugger owned by another tool" + (target.Length == 0 ? "." : " in filter " + target + ".") + " RNotepad will not overwrite it.");
                }
            }
        }

        internal static void ApplyRedirect(RegistryKey ifeo, RegistryKey config)
        {
            CheckConflicts(ifeo);
            string[] targets = RedirectTargets(ifeo);
            using (var backup = config.CreateSubKey("RedirectionBackup"))
            {
                // With UseFilter enabled the root Debugger does not cover unmatched paths,
                // including versioned WindowsApps paths used by the Run dialog.
                if (backup.GetValue("HadUseFilter") == null)
                {
                    object useFilter = ifeo.GetValue("UseFilter");
                    backup.SetValue("HadUseFilter", useFilter == null ? 0 : 1);
                    if (useFilter != null) backup.SetValue("UseFilter", useFilter, ifeo.GetValueKind("UseFilter"));
                }
                foreach (string target in targets)
                {
                    bool saved = false;
                    foreach (string name in backup.GetSubKeyNames())
                        using (var record = backup.OpenSubKey(name))
                            if (string.Equals(record.GetValue("Target") as string, target, StringComparison.OrdinalIgnoreCase)) saved = true;
                    if (saved) continue; // Save the original values once, including across editor changes.
                    using (var entry = OpenEntry(ifeo, target, false))
                    using (var record = backup.CreateSubKey(Guid.NewGuid().ToString("N")))
                    {
                        record.SetValue("Target", target);
                        object debugger = entry.GetValue("Debugger");
                        bool hadDebugger = debugger != null && !IsOwned(debugger as string ?? "");
                        record.SetValue("HadDebugger", hadDebugger ? 1 : 0);
                        if (hadDebugger) record.SetValue("Debugger", debugger, entry.GetValueKind("Debugger"));
                        object alias = entry.GetValue("AppExecutionAliasRedirect");
                        record.SetValue("HadAlias", alias == null ? 0 : 1);
                        if (alias != null) record.SetValue("Alias", alias, entry.GetValueKind("AppExecutionAliasRedirect"));
                    }
                }
            }
            try
            {
                foreach (string target in targets)
                {
                    using (var entry = OpenEntry(ifeo, target, true))
                    {
                        entry.SetValue("Debugger", DebuggerCommand, RegistryValueKind.String);
                        // Windows 11 redirects these same paths to the packaged Notepad before launch.
                        entry.SetValue("AppExecutionAliasRedirect", 0, RegistryValueKind.DWord);
                    }
                }
                // Apply the root redirect to unfiltered executable paths.
                // Keep the filter keys intact for exact restoration.
                ifeo.SetValue("UseFilter", 0, RegistryValueKind.DWord);
            }
            catch
            {
                UndoRedirect(ifeo, config);
                throw;
            }
        }

        internal static void UndoRedirect(RegistryKey ifeo, RegistryKey config)
        {
            using (var backup = config == null ? null : config.OpenSubKey("RedirectionBackup", true))
            {
                if (backup != null)
                {
                    string[] names = backup.GetSubKeyNames();
                    // Preflight all entries so another tool's change cannot cause a partial restore.
                    foreach (string name in names)
                    {
                        using (var record = backup.OpenSubKey(name))
                        using (var entry = OpenEntry(ifeo, record.GetValue("Target", "") as string, false))
                        {
                            if (entry == null) continue;
                            string current = entry.GetValue("Debugger", "") as string ?? "";
                            if (current.Length != 0 && !IsOwned(current))
                                throw new InvalidOperationException("A saved Notepad entry now has a debugger owned by another tool. RNotepad will not overwrite it.");
                        }
                    }
                    foreach (string name in names)
                    {
                        using (var record = backup.OpenSubKey(name))
                        using (var entry = OpenEntry(ifeo, record.GetValue("Target", "") as string, true))
                        {
                            if (entry != null)
                            {
                                if (IsOwned(entry.GetValue("Debugger", "") as string ?? ""))
                                {
                                    if (Convert.ToInt32(record.GetValue("HadDebugger", 0)) != 0)
                                        entry.SetValue("Debugger", record.GetValue("Debugger"), record.GetValueKind("Debugger"));
                                    else entry.DeleteValue("Debugger", false);
                                }
                                // Only undo our disabled-alias value; preserve any subsequent external change.
                                object alias = entry.GetValue("AppExecutionAliasRedirect");
                                if (alias is int && (int)alias == 0)
                                {
                                    if (Convert.ToInt32(record.GetValue("HadAlias", 0)) != 0)
                                        entry.SetValue("AppExecutionAliasRedirect", record.GetValue("Alias"), record.GetValueKind("Alias"));
                                    else entry.DeleteValue("AppExecutionAliasRedirect", false);
                                }
                            }
                        }
                        backup.DeleteSubKey(name);
                    }
                    object useFilter = ifeo.GetValue("UseFilter");
                    if (backup.GetValue("HadUseFilter") != null && useFilter is int && (int)useFilter == 0)
                    {
                        if (Convert.ToInt32(backup.GetValue("HadUseFilter")) != 0)
                            ifeo.SetValue("UseFilter", backup.GetValue("UseFilter"), backup.GetValueKind("UseFilter"));
                        else ifeo.DeleteValue("UseFilter", false);
                    }
                    backup.DeleteValue("HadUseFilter", false);
                    backup.DeleteValue("UseFilter", false);
                }
            }
            // Also support restoring redirection created by the first release (without a backup).
            using (var entry = OpenEntry(ifeo, "", true))
            {
                string current = entry.GetValue("Debugger", "") as string ?? "";
                if (current.Length != 0 && !IsOwned(current))
                    throw new InvalidOperationException("The current Notepad redirection belongs to another tool. RNotepad will not change it.");
                if (IsOwned(current)) entry.DeleteValue("Debugger", false);
            }
        }
        private static void Install(RegistryKey root)
        {
            string source = Application.ExecutablePath;
            Directory.CreateDirectory(Path.GetDirectoryName(InstalledExe));
            if (!string.Equals(source, InstalledExe, StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(InstalledExe))
                {
                    if (!File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(InstalledExe)))
                    {
                        try { File.Copy(source, InstalledExe, true); }
                        catch (IOException ex) { throw new IOException("Close the installed RNotepad window, then run your new copy and save again.", ex); }
                    }
                }
                else File.Copy(source, InstalledExe);
            }
            using (var app = root.CreateSubKey(AppKey))
            {
                string existing = app.GetValue("", "") as string ?? "";
                if (existing.Length > 0 && !string.Equals(existing, InstalledExe, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The rnotepad Run command is registered to another program.");
                app.SetValue("", InstalledExe, RegistryValueKind.String);
            }
            // Read the unexpanded PATH and preserve its existing value kind and entries.
            using (var environment = root.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", true))
            {
                string path = environment.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";
                string directory = Path.GetDirectoryName(InstalledExe);
                if (!path.Split(';').Any(p => string.Equals(Environment.ExpandEnvironmentVariables(p.Trim().Trim('"')).TrimEnd('\\'), directory, StringComparison.OrdinalIgnoreCase)))
                {
                    RegistryValueKind kind = environment.GetValue("Path") == null ? RegistryValueKind.ExpandString : environment.GetValueKind("Path");
                    environment.SetValue("Path", path.Length == 0 ? directory : path.TrimEnd(';') + ";" + directory, kind);
                    IntPtr result;
                    SendMessageTimeout(new IntPtr(0xffff), 0x001a, IntPtr.Zero, "Environment", 2, 2000, out result);
                }
            }
        }
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wparam, string lparam, uint flags, uint timeout, out IntPtr result);

        internal static void Restore()
        {
            RequireAdmin();
            using (var root = Machine())
            using (var key = root.OpenSubKey(IfeoKey, true))
            using (var config = root.OpenSubKey(ConfigKey, true))
            {
                if (key != null) UndoRedirect(key, config);
            }
            using (var root = Machine()) RemoveInstallation(root, InstalledExe);
        }

        internal static void RemoveInstallation(RegistryKey root, string installedExe)
        {
            using (var app = root.OpenSubKey(AppKey))
            {
                if (app != null && !string.Equals(app.GetValue("") as string, installedExe, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The RNotepad Run registration belongs to another program. It was preserved.");
            }
            // Windows permits renaming a running executable, but cannot delete it
            // until its processes exit. Move it out of Program Files first.
            if (File.Exists(installedExe))
            {
                string retired = Path.Combine(Path.GetTempPath(), "RNotepad-removed-" + Guid.NewGuid().ToString("N") + ".exe");
                File.Move(installedExe, retired);
                try { File.Delete(retired); }
                catch (IOException)
                {
                    string script = "$p='" + retired.Replace("'", "''") + "'; while(Test-Path -LiteralPath $p) { try { Remove-Item -LiteralPath $p -Force -ErrorAction Stop } catch { Start-Sleep -Seconds 2 } }";
                    Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script))) {
                        UseShellExecute = false, CreateNoWindow = true
                    });
                }
            }
            string directory = Path.GetDirectoryName(installedExe);
            if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
            root.DeleteSubKeyTree(AppKey, false);
            using (var environment = root.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", true))
            {
                if (environment != null && environment.GetValue("Path") != null)
                {
                    string path = environment.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";
                    string updated = string.Join(";", path.Split(';').Where(p => !string.Equals(Environment.ExpandEnvironmentVariables(p.Trim().Trim('"')).TrimEnd('\\'), directory, StringComparison.OrdinalIgnoreCase)));
                    if (updated != path)
                    {
                        environment.SetValue("Path", updated, environment.GetValueKind("Path"));
                        IntPtr result;
                        SendMessageTimeout(new IntPtr(0xffff), 0x001a, IntPtr.Zero, "Environment", 2, 2000, out result);
                    }
                }
            }
            root.DeleteSubKeyTree(ConfigKey, false);
        }
    }

    internal sealed class HelpForm : Form
    {
        internal HelpForm()
        {
            Text = "RNotepad Help";
            AccessibleName = "RNotepad Help";
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Font;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(620, 460);
            MinimumSize = new Size(400, 300);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 2 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var instructions = new TextBox {
                ReadOnly = true, Multiline = true, WordWrap = true, ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill, TabIndex = 0, AccessibleName = "How RNotepad works",
                BackColor = SystemColors.Window, ForeColor = SystemColors.WindowText,
                Text = "RNotepad lets you use your preferred text editor when you launch notepad.exe. It passes file names and other arguments to the editor you choose.\r\n\r\n" +
                    "Choose an editor: use Browse to select your editor's .exe file, such as Notepad++ or Visual Studio Code. Choose Enable / save and approve the Windows administrator prompt. RNotepad installs itself and redirects Notepad launches to that editor. These changes affect all Windows users.\r\n\r\n" +
                    "Use your editor: press Windows+R, type notepad, and press Enter. You can also run notepad followed by a file name from a command prompt. Run rnotepad to change your editor later. Open a new command window after enabling; an existing Windows Terminal may need a restart.\r\n\r\n" +
                    "Files opened from File Explorer may still use Windows Notepad. Check Set Notepad file associations before Enable / save to open Windows Default apps. Confirm your preferred editor for each listed file type there. Some packaged Windows Notepad launches can bypass redirection.\r\n\r\n" +
                    "Restore Notepad returns the saved Notepad launch settings and uninstalls RNotepad after administrator approval. Default apps you chose in Windows remain until you change them there. Keep your downloaded copy of RNotepad if you want to enable it again.\r\n\r\n" +
                    "Press Tab to reach Close, or Escape to return to RNotepad."
            };
            var close = new Button { Text = "&Close", AccessibleName = "Close help", AutoSize = true, Anchor = AnchorStyles.Right, TabIndex = 1, DialogResult = DialogResult.Cancel, Margin = new Padding(0, 12, 0, 0) };
            layout.Controls.Add(instructions, 0, 0);
            layout.Controls.Add(close, 0, 1);
            Controls.Add(layout);
            CancelButton = close;
            Shown += delegate { instructions.Focus(); instructions.Select(0, 0); };
        }
    }

    internal sealed class EditorForm : Form
    {
        private readonly TextBox editor = new TextBox();
        private readonly TextBox status = new TextBox();
        private readonly Button browse = new Button();
        private readonly Button enable = new Button();
        private readonly Button restore = new Button();
        private readonly Button help = new Button();
        private readonly Button close = new Button();
        private readonly CheckBox associations = new CheckBox();
        private bool busy;

        internal EditorForm()
        {
            Text = "RNotepad — choose your editor";
            AccessibleName = "RNotepad editor settings";
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Font;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(660, 390);
            MinimumSize = new Size(560, 420);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 7 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var description = new Label { AutoSize = true, Dock = DockStyle.Fill, MaximumSize = new Size(610, 0), Text = "Choose the editor to open whenever you launch notepad.exe. File names and other arguments are passed to that editor.", Margin = new Padding(0, 0, 0, 12) };
            layout.Controls.Add(description, 0, 0); layout.SetColumnSpan(description, 2);
            layout.Controls.Add(new Label { Text = "&Editor executable (.exe):", AutoSize = true, Margin = new Padding(0, 0, 0, 4) }, 0, 1);
            editor.Dock = DockStyle.Fill; editor.AccessibleName = "Editor executable"; editor.TabIndex = 0;
            editor.Text = Settings.ReadEditor();
            layout.Controls.Add(editor, 0, 2);
            browse.Text = "&Browse…"; browse.AutoSize = true; browse.TabIndex = 1; browse.Click += Browse;
            layout.Controls.Add(browse, 1, 2);
            var note = new Label { Text = "Enable and Restore require administrator approval and affect all Windows users. The Windows Run registration is repaired for your account. Enable installs RNotepad so you can run it by name later.", AutoSize = true, Dock = DockStyle.Fill, MaximumSize = new Size(610, 0), Margin = new Padding(0, 12, 0, 12) };
            layout.Controls.Add(note, 0, 3); layout.SetColumnSpan(note, 2);
            status.ReadOnly = true; status.Multiline = true; status.WordWrap = true; status.ScrollBars = ScrollBars.Vertical;
            status.Dock = DockStyle.Fill; status.AccessibleName = "Redirection status"; status.TabIndex = 2;
            status.BackColor = SystemColors.Window; status.ForeColor = SystemColors.WindowText;
            layout.Controls.Add(status, 0, 4); layout.SetColumnSpan(status, 2);
            associations.Text = "Set Notepad &file associations (opens Windows Default apps)";
            associations.AccessibleName = "Set Notepad file associations (opens Windows Default apps)";
            associations.AccessibleDescription = "After Enable / save, opens Windows Default apps to choose your editor for file types currently assigned to Notepad for your Windows account. Windows requires you to confirm each default.";
            associations.AutoSize = true; associations.Dock = DockStyle.Fill; associations.TabIndex = 3;
            associations.Margin = new Padding(0, 12, 0, 0);
            layout.Controls.Add(associations, 0, 5); layout.SetColumnSpan(associations, 2);
            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, TabIndex = 4, Margin = new Padding(0, 12, 0, 0) };
            enable.Text = "Enable / &save"; enable.AutoSize = true; enable.TabIndex = 0;
            enable.Click += async delegate { await Change(true); };
            restore.Text = "&Restore Notepad"; restore.AutoSize = true; restore.TabIndex = 1;
            restore.Click += async delegate { await Change(false); };
            help.Text = "&Help"; help.AccessibleName = "Help"; help.AutoSize = true; help.TabIndex = 2;
            help.Click += delegate {
                using (var dialog = new HelpForm()) dialog.ShowDialog(this);
                help.Focus();
            };
            close.Text = "&Close"; close.AutoSize = true; close.TabIndex = 3; close.Click += delegate { Close(); };
            buttons.Controls.AddRange(new Control[] { enable, restore, help, close });
            layout.Controls.Add(buttons, 0, 6); layout.SetColumnSpan(buttons, 2);
            Controls.Add(layout);
            CancelButton = close;
            RefreshStatus();
            Shown += delegate { editor.Focus(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (busy) e.Cancel = true; };
        }

        private void Browse(object sender, EventArgs e)
        {
            using (var picker = new OpenFileDialog { Title = "Choose your editor executable", Filter = "Programs (*.exe)|*.exe", CheckFileExists = true, Multiselect = false, RestoreDirectory = true })
            {
                if (File.Exists(editor.Text)) picker.InitialDirectory = Path.GetDirectoryName(editor.Text);
                if (picker.ShowDialog(this) == DialogResult.OK) editor.Text = picker.FileName;
            }
            browse.Focus();
        }

        private void RefreshStatus()
        {
            string debugger = Settings.ReadDebugger();
            status.Text = debugger.Length == 0 ? "Windows Notepad is enabled." :
                Settings.IsOwned(debugger) ? (Settings.RedirectComplete() ? "Notepad launches are redirected to:\r\n" + Settings.ReadEditor() :
                    "Notepad redirection is incomplete. Windows Run may still open Windows Notepad. Click Enable / save to repair it.\r\nSelected editor: " + Settings.ReadEditor()) :
                "Notepad has a debugger or redirection owned by another tool. RNotepad will not overwrite it.";
        }

        private async Task Change(bool enabling)
        {
            string path = "";
            if (enabling)
            {
                try { path = Program.ValidateEditor(editor.Text); editor.Text = path; }
                catch (Exception ex)
                {
                    status.Text = ex.Message;
                    MessageBox.Show(this, ex.Message, "Choose an editor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    editor.Focus(); return;
                }
            }
            // Query the settings user's associations before launching an elevated
            // helper, which may run under a different administrator account.
            string[] fileTypes;
            try { fileTypes = enabling && associations.Checked ? Settings.NotepadFileTypes() : null; }
            catch (Exception ex)
            {
                status.Text = "Unable to read file associations: " + ex.Message;
                MessageBox.Show(this, status.Text, "File associations", MessageBoxButtons.OK, MessageBoxIcon.Error);
                associations.Focus(); return;
            }
            Control previous = ActiveControl;
            busy = true;
            enable.Enabled = restore.Enabled = help.Enabled = browse.Enabled = close.Enabled = editor.Enabled = associations.Enabled = false;
            status.Text = "Waiting for administrator approval…";
            status.Focus();
            try
            {
                string operation = enabling ? "--enable " + Program.Quote(path) : "--restore";
                var start = new ProcessStartInfo(Application.ExecutablePath, "--owner " + Handle.ToInt64() + " " + operation) { UseShellExecute = true, Verb = "runas" };
                using (var helper = Process.Start(start))
                {
                    await Task.Run(delegate { helper.WaitForExit(); });
                    if (helper.ExitCode == 0)
                    {
                        // Run registration belongs to the settings user, even when UAC
                        // uses credentials belonging to a different administrator.
                        if (enabling) Settings.EnableRun();
                        else Settings.RestoreRun();
                    }
                    RefreshStatus();
                    if (helper.ExitCode == 0)
                        MessageBox.Show(this, enabling ? "Redirection enabled. Run notepad to open your editor. Run rnotepad to change it. Open a new command window to use rnotepad there; existing terminals may need a restart." : "Windows Notepad has been restored and RNotepad has been uninstalled. Run your downloaded copy to enable it again.", "RNotepad", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    else status.AppendText("\r\nThe change did not complete. See the error shown by the administrator process.");
                    if (helper.ExitCode == 0 && fileTypes != null)
                    {
                        if (fileTypes.Length == 0)
                        {
                            status.AppendText("\r\nNo file types currently assigned to Notepad were found. File associations were not changed.");
                            MessageBox.Show(this, "No file types currently assigned to Notepad were found. File associations were not changed.", "File associations", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            string instructions = "Windows requires you to confirm default app changes. File associations have not yet been changed.\r\n\r\nFile types currently assigned to Notepad: " + string.Join(", ", fileTypes) +
                                "\r\n\r\nChosen editor: " + path + "\r\n\r\nIn Windows Default apps, use Tab to reach the file type search field. Enter each extension listed above, select its current default, and choose your editor. If needed, use the option to choose an app on your PC and browse to the editor shown above. Use Tab and Shift+Tab to move between controls, and Enter or Space to activate a focused button. Confirm each change, then return here with Alt+Tab to review this list.\r\n\r\nThese choices remain in Windows until you change them there, including after Restore Notepad.";
                            status.AppendText("\r\n\r\n" + instructions);
                            MessageBox.Show(this, instructions + "\r\n\r\nChoose OK to open Windows Default apps.", "Set file associations", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true });
                        }
                    }
                }
            }
            catch (Win32Exception ex)
            {
                RefreshStatus();
                string message = ex.NativeErrorCode == 1223 ? "Administrator approval was cancelled. No settings were changed." : ex.Message;
                status.AppendText("\r\n" + message);
                MessageBox.Show(this, message, "RNotepad", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
                MessageBox.Show(this, ex.Message, "RNotepad", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                busy = false;
                enable.Enabled = restore.Enabled = help.Enabled = browse.Enabled = close.Enabled = editor.Enabled = associations.Enabled = true;
                if (previous != null) previous.Focus();
            }
        }
    }
}
