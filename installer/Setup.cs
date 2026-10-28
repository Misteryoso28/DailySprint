using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using Microsoft.Win32;
using System.Windows.Forms;

namespace DailySprintInstaller
{
    internal static class SetupProgram
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(args.Length == 1 && args[0] == "--uninstall"
                ? (Form)new UninstallForm()
                : new SetupForm());
        }
    }

    internal sealed class SetupForm : Form
    {
        private const string UninstallRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\DailySprint";
        private readonly TextBox folderInput;
        private readonly CheckBox desktopShortcut;

        public SetupForm()
        {
            Text = "Install DailySprint";
            ClientSize = new Size(520, 285);
            MinimumSize = new Size(520, 285);
            MaximumSize = new Size(520, 285);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(15, 23, 42);
            ForeColor = Color.FromArgb(241, 245, 249);
            Font = new Font("Segoe UI", 9F);

            var title = new Label
            {
                Text = "Install DailySprint",
                Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold),
                ForeColor = Color.FromArgb(241, 245, 249),
                AutoSize = true,
                Location = new Point(26, 22)
            };
            Controls.Add(title);
            var explanation = new Label
            {
                Text = "Your personal daily habits and expense tracker. It works offline and stores your data on this Windows account.",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(28, 62),
                Size = new Size(460, 45)
            };
            Controls.Add(explanation);
            var pathLabel = new Label
            {
                Text = "Install location",
                AutoSize = true,
                ForeColor = Color.FromArgb(203, 213, 225),
                Location = new Point(28, 122)
            };
            Controls.Add(pathLabel);
            folderInput = new TextBox
            {
                Text = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs",
                    "DailySprint"),
                BackColor = Color.FromArgb(30, 42, 66),
                ForeColor = Color.FromArgb(241, 245, 249),
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(28, 148),
                Size = new Size(458, 27)
            };
            Controls.Add(folderInput);
            desktopShortcut = new CheckBox
            {
                Text = "Create a desktop shortcut",
                Checked = true,
                AutoSize = true,
                ForeColor = Color.FromArgb(203, 213, 225),
                Location = new Point(28, 189)
            };
            Controls.Add(desktopShortcut);

            var install = new Button
            {
                Text = "Install",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(79, 70, 229),
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                Location = new Point(353, 224),
                Size = new Size(133, 38),
                Cursor = Cursors.Hand
            };
            install.FlatAppearance.BorderSize = 0;
            install.Click += Install;
            Controls.Add(install);
            AcceptButton = install;
        }

        private void Install(object sender, EventArgs e)
        {
            string target;
            try
            {
                target = Path.GetFullPath(folderInput.Text.Trim());
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "The installation folder is invalid.\r\n\r\n" + ex.Message,
                    "Invalid folder", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!IsPermittedInstallFolder(target))
            {
                MessageBox.Show(this,
                    "For your safety, install DailySprint in a folder inside your Windows account's Programs folder.",
                    "Choose a user install folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string executable = Path.Combine(target, "DailySprint.exe");
                string uninstaller = Path.Combine(target, "Uninstall.exe");
                Directory.CreateDirectory(target);
                using (Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream("DailySprint.exe"))
                {
                    if (source == null)
                        throw new InvalidOperationException("The DailySprint application is missing from this installer.");
                    using (var destination = new FileStream(executable, FileMode.Create, FileAccess.Write, FileShare.None))
                        source.CopyTo(destination);
                }
                File.Copy(Assembly.GetExecutingAssembly().Location, uninstaller, true);

                string startMenu = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                    "Programs",
                    "DailySprint");
                Directory.CreateDirectory(startMenu);
                CreateShortcut(Path.Combine(startMenu, "DailySprint.lnk"), executable);
                CreateShortcut(Path.Combine(startMenu, "Uninstall DailySprint.lnk"), uninstaller, "--uninstall");
                if (desktopShortcut.Checked)
                {
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    CreateShortcut(Path.Combine(desktop, "DailySprint.lnk"), executable);
                }

                using (RegistryKey uninstallKey = Registry.CurrentUser.CreateSubKey(UninstallRegistryPath))
                {
                    if (uninstallKey == null)
                        throw new InvalidOperationException("The uninstall information could not be registered for this Windows account.");
                    uninstallKey.SetValue("DisplayName", "DailySprint");
                    uninstallKey.SetValue("DisplayVersion", "1.0");
                    uninstallKey.SetValue("Publisher", "DailySprint");
                    uninstallKey.SetValue("InstallLocation", target);
                    uninstallKey.SetValue("DisplayIcon", executable);
                    uninstallKey.SetValue("UninstallString", "\"" + uninstaller + "\" --uninstall");
                    uninstallKey.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    uninstallKey.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }

                Process.Start(executable);
                MessageBox.Show(this,
                    "DailySprint is installed. You can open it from the Start menu" +
                    (desktopShortcut.Checked ? " or your desktop." : "."),
                    "Installation complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "DailySprint could not be installed.\r\n\r\n" + ex.Message,
                    "Installation failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void CreateShortcut(string path, string target, string arguments = "")
        {
            object instance = Activator.CreateInstance(Type.GetTypeFromCLSID(ShellLinkClassId, true));
            try
            {
                var link = (IShellLinkW)instance;
                link.SetPath(target);
                link.SetArguments(arguments);
                link.SetWorkingDirectory(Path.GetDirectoryName(target));
                link.SetDescription("Daily habits and expense tracker");
                ((IPersistFile)instance).Save(path, false);
            }
            finally
            {
                Marshal.FinalReleaseComObject(instance);
            }
        }

        private static readonly Guid ShellLinkClassId = new Guid("00021401-0000-0000-C000-000000000046");

        internal static bool IsPermittedInstallFolder(string path)
        {
            string root = Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs")).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed class UninstallForm : Form
    {
        private const string UninstallRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\DailySprint";

        public UninstallForm()
        {
            Text = "Uninstall DailySprint";
            ClientSize = new Size(460, 190);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(15, 23, 42);
            ForeColor = Color.FromArgb(241, 245, 249);
            Font = new Font("Segoe UI", 9F);

            var prompt = new Label
            {
                Text = "Remove DailySprint from this computer?\r\n\r\nYour saved records will be kept in your Windows profile.",
                AutoSize = true,
                Location = new Point(24, 25),
                ForeColor = Color.FromArgb(203, 213, 225)
            };
            Controls.Add(prompt);
            var remove = new Button
            {
                Text = "Uninstall",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(185, 28, 28),
                ForeColor = Color.White,
                Location = new Point(306, 130),
                Size = new Size(126, 36)
            };
            remove.FlatAppearance.BorderSize = 0;
            remove.Click += RemoveInstallation;
            Controls.Add(remove);
            var cancel = new Button
            {
                Text = "Cancel",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(30, 42, 66),
                ForeColor = Color.FromArgb(203, 213, 225),
                Location = new Point(213, 130),
                Size = new Size(82, 36),
                DialogResult = DialogResult.Cancel
            };
            Controls.Add(cancel);
            CancelButton = cancel;
        }

        private void RemoveInstallation(object sender, EventArgs e)
        {
            string uninstallExecutable = Application.ExecutablePath;
            if (!String.Equals(Path.GetFileName(uninstallExecutable), "Uninstall.exe", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "The uninstaller must be run from its installed location.",
                    "Uninstall failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string installFolder = Path.GetDirectoryName(uninstallExecutable);
            if (!SetupForm.IsPermittedInstallFolder(installFolder))
            {
                MessageBox.Show(this, "The installation folder is outside your user Programs folder. No files were removed.",
                    "Uninstall failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(UninstallRegistryPath))
            {
                string registeredFolder = key == null ? null : key.GetValue("InstallLocation") as string;
                if (!String.Equals(registeredFolder, installFolder, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(this, "The installation information does not match this folder. No files were removed.",
                        "Uninstall failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            if (MessageBox.Show(this, "DailySprint will be removed. Your saved records will be kept.\r\n\r\nContinue?",
                "Confirm uninstall", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                string appExecutable = Path.Combine(installFolder, "DailySprint.exe");
                if (File.Exists(appExecutable))
                    File.Delete(appExecutable);

                string startMenu = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                    "Programs",
                    "DailySprint");
                DeleteShortcut(Path.Combine(startMenu, "DailySprint.lnk"));
                DeleteShortcut(Path.Combine(startMenu, "Uninstall DailySprint.lnk"));
                if (Directory.Exists(startMenu) && Directory.GetFileSystemEntries(startMenu).Length == 0)
                    Directory.Delete(startMenu);

                string desktopShortcut = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    "DailySprint.lnk");
                DeleteShortcut(desktopShortcut);

                Registry.CurrentUser.DeleteSubKeyTree(UninstallRegistryPath, false);
                bool scheduled = MoveFileEx(uninstallExecutable, null, 0x4);
                try
                {
                    Directory.Delete(installFolder, false);
                }
                catch (IOException)
                {
                    if (!scheduled)
                        throw;
                }

                MessageBox.Show(this,
                    scheduled
                        ? "DailySprint was removed. Windows will clean up the uninstaller file after you restart."
                        : "DailySprint was removed. A small empty install folder may remain.",
                    "Uninstall complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "DailySprint could not be fully removed.\r\n\r\n" + ex.Message,
                    "Uninstall failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void DeleteShortcut(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileEx(string existingFileName, string newFileName, int flags);
    }

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder file, int maximumLength, IntPtr findData, uint flags);
        void GetIDList(out IntPtr itemIdList);
        void SetIDList(IntPtr itemIdList);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder description, int maximumLength);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder directory, int maximumLength);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder arguments, int maximumLength);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCommand);
        void SetShowCmd(int showCommand);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder iconPath, int maximumLength, out int iconIndex);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr windowHandle, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }

    [ComImport]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPersistFile
    {
        void GetClassID(out Guid classId);
        [PreserveSig]
        int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string fileName, int mode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string fileName, bool remember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string fileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string fileName);
    }
}
