using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Windows.Forms;

namespace TimCookGuardInstaller
{
    internal static class Program
    {
        private const string ProductName = "Tim Cook Guard";
        private const string RunValueName = "TimCookGuard";
        private const string PayloadResource = "TimCookGuardInstaller.payload.exe";

        [STAThread]
        private static void Main(string[] args)
        {
            if (HasArgument(args, "--self-test"))
            {
                Environment.ExitCode = PayloadLooksValid() ? 0 : 1;
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (HasArgument(args, "/uninstall"))
                Uninstall(HasArgument(args, "/silent"));
            else
                Install(HasArgument(args, "/silent"));
        }

        private static void Install(bool silent)
        {
            string installDirectory = GetInstallDirectory();
            string applicationPath = Path.Combine(installDirectory, "TimCookGuard.exe");
            string uninstallerPath = Path.Combine(installDirectory, "Uninstall.exe");
            try
            {
                Directory.CreateDirectory(installDirectory);
                StopRunningGuard();
                WriteEmbeddedPayload(applicationPath);
                File.Copy(Assembly.GetExecutingAssembly().Location, uninstallerPath, true);
                CreateShortcuts(applicationPath, uninstallerPath);
                RegisterStartup(applicationPath);
                RegisterUninstaller(applicationPath, uninstallerPath);

                if (!silent)
                {
                    MessageBox.Show(
                        "Tim Cook Guard is installed for your Windows account.\n\nIt will start automatically when you sign in. Automatic arming and forced shutdown are both optional dashboard settings.",
                        ProductName,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    Process.Start(new ProcessStartInfo(applicationPath, "--settings") { UseShellExecute = true });
                }
            }
            catch (Exception error)
            {
                if (!silent)
                    MessageBox.Show("Installation failed:\n\n" + error.Message, ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.ExitCode = 1;
            }
        }

        private static void Uninstall(bool silent)
        {
            if (!silent)
            {
                DialogResult answer = MessageBox.Show(
                    "Remove Tim Cook Guard, its startup entry, and its Start Menu shortcuts?\n\nIncident photos and videos in Downloads will not be deleted.",
                    "Uninstall " + ProductName,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (answer != DialogResult.Yes)
                    return;
            }

            string installDirectory = GetInstallDirectory();
            string applicationPath = Path.Combine(installDirectory, "TimCookGuard.exe");
            string uninstallerPath = Path.Combine(installDirectory, "Uninstall.exe");
            try
            {
                StopRunningGuard();
                RemoveStartupIfOwned(applicationPath);
                RemoveShortcuts();
                using (RegistryKey uninstall = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall", true))
                {
                    if (uninstall != null)
                        uninstall.DeleteSubKeyTree("TimCookGuard", false);
                }
                if (File.Exists(applicationPath))
                    File.Delete(applicationPath);
                MoveFileEx(uninstallerPath, null, MoveFileDelayUntilReboot);
                MoveFileEx(installDirectory, null, MoveFileDelayUntilReboot);

                if (!silent)
                    MessageBox.Show("Tim Cook Guard was removed. Captured evidence in Downloads was left untouched.", ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception error)
            {
                if (!silent)
                    MessageBox.Show("Uninstall failed:\n\n" + error.Message, ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.ExitCode = 1;
            }
        }

        private static void WriteEmbeddedPayload(string destination)
        {
            using (Stream input = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResource))
            {
                if (input == null)
                    throw new InvalidOperationException("The embedded application payload is missing.");
                using (FileStream output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
                    input.CopyTo(output);
            }
        }

        private static bool PayloadLooksValid()
        {
            try
            {
                using (Stream input = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResource))
                    return input != null && input.Length > 1024 && input.ReadByte() == 0x4D && input.ReadByte() == 0x5A;
            }
            catch
            {
                return false;
            }
        }

        private static void RegisterStartup(string applicationPath)
        {
            using (RegistryKey run = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run"))
                run.SetValue(RunValueName, "\"" + applicationPath + "\"", RegistryValueKind.String);
        }

        private static void RemoveStartupIfOwned(string applicationPath)
        {
            using (RegistryKey run = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                if (run == null)
                    return;
                string current = Convert.ToString(run.GetValue(RunValueName));
                if (current.IndexOf(applicationPath, StringComparison.OrdinalIgnoreCase) >= 0)
                    run.DeleteValue(RunValueName, false);
            }
        }

        private static void RegisterUninstaller(string applicationPath, string uninstallerPath)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\TimCookGuard"))
            {
                key.SetValue("DisplayName", ProductName);
                key.SetValue("DisplayVersion", "1.1.0");
                key.SetValue("Publisher", "DaBeaklyBird");
                key.SetValue("InstallLocation", GetInstallDirectory());
                key.SetValue("DisplayIcon", applicationPath);
                key.SetValue("UninstallString", "\"" + uninstallerPath + "\" /uninstall");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }

        private static void CreateShortcuts(string applicationPath, string uninstallerPath)
        {
            string shortcutDirectory = GetShortcutDirectory();
            Directory.CreateDirectory(shortcutDirectory);
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            dynamic shell = Activator.CreateInstance(shellType);
            dynamic appShortcut = shell.CreateShortcut(Path.Combine(shortcutDirectory, "Tim Cook Guard.lnk"));
            appShortcut.TargetPath = applicationPath;
            appShortcut.Arguments = "--settings";
            appShortcut.WorkingDirectory = Path.GetDirectoryName(applicationPath);
            appShortcut.IconLocation = applicationPath + ",0";
            appShortcut.Save();
            dynamic uninstallShortcut = shell.CreateShortcut(Path.Combine(shortcutDirectory, "Uninstall Tim Cook Guard.lnk"));
            uninstallShortcut.TargetPath = uninstallerPath;
            uninstallShortcut.Arguments = "/uninstall";
            uninstallShortcut.WorkingDirectory = Path.GetDirectoryName(uninstallerPath);
            uninstallShortcut.IconLocation = applicationPath + ",0";
            uninstallShortcut.Save();
        }

        private static void RemoveShortcuts()
        {
            string shortcutDirectory = GetShortcutDirectory();
            if (Directory.Exists(shortcutDirectory))
                Directory.Delete(shortcutDirectory, true);
        }

        private static void StopRunningGuard()
        {
            foreach (Process process in Process.GetProcessesByName("TimCookGuard"))
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
                catch
                {
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        private static string GetInstallDirectory()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "TimCookGuard");
        }

        private static string GetShortcutDirectory()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), ProductName);
        }

        private static bool HasArgument(string[] args, string expected)
        {
            return Array.Exists(args, delegate(string value) { return String.Equals(value, expected, StringComparison.OrdinalIgnoreCase); });
        }

        private const int MoveFileDelayUntilReboot = 0x4;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileEx(string existingFileName, string newFileName, int flags);
    }
}
