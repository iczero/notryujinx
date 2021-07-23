using Avalonia.Controls;
using Avalonia.Threading;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json.Linq;
using Ryujinx.Ava;
using Ryujinx.Ava.Ui.Controls;
using Ryujinx.Ava.Ui.Windows;
using Ryujinx.Common.Logging;
using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Ryujinx.Modules
{
    public static class Updater
    {
        private const string AppveyorApiUrl = "https://ci.appveyor.com/api";
        internal static bool Running;

        private static readonly string HomeDir = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string UpdateDir = Path.Combine(Path.GetTempPath(), "Ryujinx", "update");
        private static readonly string UpdatePublishDir = Path.Combine(UpdateDir, "publish");

        private static string _jobId;
        private static string _buildVer;
        private static string _platformExt;
        private static string _buildUrl;

        public static async Task BeginParse(MainWindow mainWindow, bool showVersionUpToDate)
        {
            if (Running)
            {
                return;
            }

            Running = true;
            mainWindow.CanUpdate = false;

            // Detect current platform
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                _platformExt = "osx_x64.zip";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _platformExt = "win_x64.zip";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _platformExt = "linux_x64.tar.gz";
            }

            Version newVersion;
            Version currentVersion;

            try
            {
                currentVersion = Version.Parse(Program.Version);
            }
            catch
            {
                ContentDialogHelper.CreateWarningDialog(mainWindow, "Failed to convert the current Ryujinx version.", "Cancelling Update!");
                Logger.Error?.Print(LogClass.Application, "Failed to convert the current Ryujinx version!");

                return;
            }

            // Get latest version number from Appveyor
            try
            {
                using (WebClient jsonClient = new())
                {
                    string fetchedJson =
                        await jsonClient.DownloadStringTaskAsync(
                            $"{AppveyorApiUrl}/projects/gdkchan/ryujinx/branch/master");
                    JObject jsonRoot = JObject.Parse(fetchedJson);
                    JToken buildToken = jsonRoot["build"];

                    _jobId = (string)buildToken["jobs"][0]["jobId"];
                    _buildVer = (string)buildToken["version"];
                    _buildUrl = $"{AppveyorApiUrl}/buildjobs/{_jobId}/artifacts/ryujinx-{_buildVer}-{_platformExt}";

                    // If build not done, assume no new update are availaible.
                    if ((string)buildToken["jobs"][0]["status"] != "success")
                    {
                        if (showVersionUpToDate)
                        {
                            ContentDialogHelper.CreateUpdaterInfoDialog(mainWindow,
                                "You are already using the most updated version of Ryujinx!", "");
                        }

                        return;
                    }
                }
            }
            catch (Exception exception)
            {
                Logger.Error?.Print(LogClass.Application, exception.Message);
                ContentDialogHelper.CreateErrorDialog(mainWindow,
                    "An error has occurred when trying to get release information from AppVeyor.");

                return;
            }

            try
            {
                newVersion = Version.Parse(_buildVer);
            }
            catch
            {
                ContentDialogHelper.CreateWarningDialog(mainWindow, "Failed to convert the received Ryujinx version from AppVeyor.",
                    "Cancelling Update!");
                Logger.Error?.Print(LogClass.Application,
                    "Failed to convert the received Ryujinx version from AppVeyor!");

                return;
            }

            if (newVersion <= currentVersion)
            {
                if (showVersionUpToDate)
                {
                    ContentDialogHelper.CreateUpdaterInfoDialog(mainWindow, "You are already using the most updated version of Ryujinx!", "");
                }

                Running = false;
                mainWindow.CanUpdate = true;

                return;
            }

            // Show a message asking the user if they want to update
            UpdaterWindow updateDialog = new(mainWindow, newVersion, _buildUrl);
            await updateDialog.ShowDialog(mainWindow);
        }

        public static async Task UpdateRyujinx(UpdaterWindow updateDialog, string downloadUrl)
        {
            // Empty update dir, although it shouldn't ever have anything inside it
            if (Directory.Exists(UpdateDir))
            {
                Directory.Delete(UpdateDir, true);
            }

            Directory.CreateDirectory(UpdateDir);

            string updateFile = Path.Combine(UpdateDir, "update.bin");

            // Download the update .zip
            updateDialog.MainText.Text = "Downloading Update...";
            updateDialog.ProgressBar.Value = 0;
            updateDialog.ProgressBar.Maximum = 100;

            using (WebClient client = new())
            {
                client.DownloadProgressChanged += (_, args) =>
                {
                    updateDialog.ProgressBar.Value = args.ProgressPercentage;
                };

                await client.DownloadFileTaskAsync(downloadUrl, updateFile);
            }

            // Extract Update
            updateDialog.MainText.Text = "Extracting Update...";
            updateDialog.ProgressBar.Value = 0;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                using (Stream inStream = File.OpenRead(updateFile))
                using (Stream gzipStream = new GZipInputStream(inStream))
                using (TarInputStream tarStream = new(gzipStream, Encoding.ASCII))
                {
                    updateDialog.ProgressBar.Maximum = inStream.Length;

                    await Task.Run(() =>
                    {
                        TarEntry tarEntry;
                        while ((tarEntry = tarStream.GetNextEntry()) != null)
                        {
                            if (tarEntry.IsDirectory)
                            {
                                continue;
                            }

                            string outPath = Path.Combine(UpdateDir, tarEntry.Name);

                            Directory.CreateDirectory(Path.GetDirectoryName(outPath));

                            using (FileStream outStream = File.OpenWrite(outPath))
                            {
                                tarStream.CopyEntryContents(outStream);
                            }

                            File.SetLastWriteTime(outPath, DateTime.SpecifyKind(tarEntry.ModTime, DateTimeKind.Utc));

                            TarEntry entry = tarEntry;

                            Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                updateDialog.ProgressBar.Value += entry.Size;
                            });
                        }
                    });

                    updateDialog.ProgressBar.Value = inStream.Length;
                }
            }
            else
            {
                using (Stream inStream = File.OpenRead(updateFile))
                using (ZipFile zipFile = new(inStream))
                {
                    updateDialog.ProgressBar.Maximum = zipFile.Count;

                    await Task.Run(() =>
                    {
                        foreach (ZipEntry zipEntry in zipFile)
                        {
                            if (zipEntry.IsDirectory)
                            {
                                continue;
                            }

                            string outPath = Path.Combine(UpdateDir, zipEntry.Name);

                            Directory.CreateDirectory(Path.GetDirectoryName(outPath));

                            using (Stream zipStream = zipFile.GetInputStream(zipEntry))
                            using (FileStream outStream = File.OpenWrite(outPath))
                            {
                                zipStream.CopyTo(outStream);
                            }

                            File.SetLastWriteTime(outPath, DateTime.SpecifyKind(zipEntry.DateTime, DateTimeKind.Utc));

                            Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                updateDialog.ProgressBar.Value++;
                            });
                        }
                    });
                }
            }

            // Delete downloaded zip
            File.Delete(updateFile);

            string[] allFiles = Directory.GetFiles(HomeDir, "*", SearchOption.AllDirectories);

            updateDialog.MainText.Text = "Renaming Old Files...";
            updateDialog.ProgressBar.Value = 0;
            updateDialog.ProgressBar.Maximum = allFiles.Length;

            // Replace old files
            await Task.Run(() =>
            {
                foreach (string file in allFiles)
                {
                    if (!Path.GetExtension(file).Equals(".log"))
                    {
                        try
                        {
                            File.Move(file, file + ".ryuold");

                            Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                updateDialog.ProgressBar.Value++;
                            });
                        }
                        catch
                        {
                            Logger.Warning?.Print(LogClass.Application, "Updater wasn't able to rename file: " + file);
                        }
                    }
                }

                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    updateDialog.MainText.Text = "Adding New Files...";
                    updateDialog.ProgressBar.Value = 0;
                    updateDialog.ProgressBar.Maximum =
                        Directory.GetFiles(UpdatePublishDir, "*", SearchOption.AllDirectories).Length;
                });

                MoveAllFilesOver(UpdatePublishDir, HomeDir, updateDialog);
            });

            Directory.Delete(UpdateDir, true);

            updateDialog.MainText.Text = "Update Complete!";
            updateDialog.SecondaryText.Text = "Do you want to restart Ryujinx now?";

            updateDialog.ProgressBar.IsVisible = false;
            updateDialog.ButtonBox.IsVisible = true;
        }

        public static bool CanUpdate(bool showWarnings, StyleableWindow parent)
        {
            if (RuntimeInformation.OSArchitecture != Architecture.X64)
            {
                if (showWarnings)
                {
                    ContentDialogHelper.CreateWarningDialog( parent,"You are not running a supported system architecture!",
                        "(Only x64 systems are supported!)");
                }

                return false;
            }

            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                if (showWarnings)
                {
                    ContentDialogHelper.CreateWarningDialog(parent, "You are not connected to the Internet!",
                        "Please verify that you have a working Internet connection!");
                }

                return false;
            }

            if (Program.Version.Contains("dirty"))
            {
                if (showWarnings)
                {
                    ContentDialogHelper.CreateWarningDialog(parent, "You Cannot update a Dirty build of Ryujinx!",
                        "Please download Ryujinx at https://ryujinx.org/ if you are looking for a supported version.");
                }

                return false;
            }

            return true;
        }

        private static void MoveAllFilesOver(string root, string dest, UpdaterWindow dialog)
        {
            foreach (string directory in Directory.GetDirectories(root))
            {
                string dirName = Path.GetFileName(directory);

                if (!Directory.Exists(Path.Combine(dest, dirName)))
                {
                    Directory.CreateDirectory(Path.Combine(dest, dirName));
                }

                MoveAllFilesOver(directory, Path.Combine(dest, dirName), dialog);
            }

            foreach (string file in Directory.GetFiles(root))
            {
                File.Move(file, Path.Combine(dest, Path.GetFileName(file)), true);

                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    dialog.ProgressBar.Value++;
                });
            }
        }

        public static void CleanupUpdate()
        {
            foreach (string file in Directory.GetFiles(HomeDir, "*", SearchOption.AllDirectories))
            {
                if (Path.GetExtension(file).EndsWith(".ryuold"))
                {
                    File.Delete(file);
                }
            }
        }
    }
}