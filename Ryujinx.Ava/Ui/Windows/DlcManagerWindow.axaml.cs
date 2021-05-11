using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.FsSystem.NcaUtils;
using Ryujinx.Ava.Ui.Controls;
using Ryujinx.Ava.Ui.Models;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.FileSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Ryujinx.Ava.Ui.Windows
{
    public class DlcManagerWindow : StyleableWindow
    {
        private readonly List<DlcContainer> _dlcContainerList;
        private readonly string _dlcJsonPath;

        public DlcManagerWindow()
        {
            DataContext = this;

            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        public DlcManagerWindow(VirtualFileSystem virtualFileSystem, string titleId, string titleName)
        {
            VirtualFileSystem = virtualFileSystem;
            TitleId = titleId;
            TitleName = titleName;

            DataContext = this;

            _dlcJsonPath = Path.Combine(AppDataManager.GamesDirPath, titleId, "dlc.json");

            try
            {
                _dlcContainerList = JsonHelper.DeserializeFromFile<List<DlcContainer>>(_dlcJsonPath);
            }
            catch
            {
                _dlcContainerList = new List<DlcContainer>();
            }

            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            LoadDlcs();
        }

        public AvaloniaList<DlcModel> Dlcs { get; set; }
        public Grid DlcGrid { get; private set; }
        public VirtualFileSystem VirtualFileSystem { get; }
        public string TitleId { get; }
        public string TitleName { get; }

        public string Heading => $"DLC Available for {TitleName} [{TitleId.ToUpper()}]";

        private void InitializeComponent()
        {
            Dlcs = new AvaloniaList<DlcModel>();

            AvaloniaXamlLoader.Load(this);

            DlcGrid = this.FindControl<Grid>("DlcGrid");

            IObservable<Size> resizeObserverable = this.GetObservable(ClientSizeProperty);

            resizeObserverable.Subscribe(Resized);

            IObservable<Rect> stateObserverable = this.GetObservable(BoundsProperty);

            stateObserverable.Subscribe(StateChanged);
        }

        public void UpdateSizes(Size size)
        {
            //Workaround for dlc list not fitting parent

            if (DlcGrid != null)
            {
                DlcGrid.Width = Bounds.Width - DlcGrid.Margin.Left - DlcGrid.Margin.Right;
            }
        }

        private void Resized(Size size)
        {
            UpdateSizes(size);
        }


        private void StateChanged(Rect rect)
        {
            UpdateSizes(ClientSize);
        }

        public void LoadDlcs()
        {
            foreach (DlcContainer dlcContainer in _dlcContainerList)
            {
                using FileStream containerFile = File.OpenRead(dlcContainer.Path);
                PartitionFileSystem pfs = new(containerFile.AsStorage());
                VirtualFileSystem.ImportTickets(pfs);

                foreach (DlcNca dlcNca in dlcContainer.DlcNcaList)
                {
                    pfs.OpenFile(out IFile ncaFile, dlcNca.Path.ToU8Span(), OpenMode.Read).ThrowIfFailure();
                    Nca nca = TryCreateNca(ncaFile.AsStorage(), dlcContainer.Path);

                    if (nca != null)
                    {
                        Dlcs.Add(new DlcModel(nca.Header.TitleId.ToString("X16"), dlcContainer.Path, dlcNca.Path,
                            dlcNca.Enabled));
                    }
                }
            }
        }

        private Nca TryCreateNca(IStorage ncaStorage, string containerPath)
        {
            try
            {
                return new Nca(VirtualFileSystem.KeySet, ncaStorage);
            }
            catch (Exception exception)
            {
                AvaDialog.CreateErrorDialog($"{exception.Message}. Errored File: {containerPath}", this);
            }

            return null;
        }

        private void AddDlc(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

            using (FileStream containerFile = File.OpenRead(path))
            {
                PartitionFileSystem pfs = new(containerFile.AsStorage());
                bool containsDlc = false;

                VirtualFileSystem.ImportTickets(pfs);

                foreach (DirectoryEntryEx fileEntry in pfs.EnumerateEntries("/", "*.nca"))
                {
                    pfs.OpenFile(out IFile ncaFile, fileEntry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

                    Nca nca = TryCreateNca(ncaFile.AsStorage(), path);

                    if (nca == null)
                    {
                        continue;
                    }

                    if (nca.Header.ContentType == NcaContentType.PublicData)
                    {
                        if ((nca.Header.TitleId & 0xFFFFFFFFFFFFE000).ToString("x16") != TitleId)
                        {
                            break;
                        }

                        Dlcs.Add(new DlcModel(nca.Header.TitleId.ToString("X16"), path, fileEntry.FullPath, true));

                        containsDlc = true;
                    }
                }

                if (!containsDlc)
                {
                    AvaDialog.CreateErrorDialog("The specified file does not contain a DLC for the selected title!",
                        this);
                }
            }
        }

        private void RemoveDlcs(bool removeSelectedOnly = false)
        {
            if (removeSelectedOnly)
            {
                List<DlcModel> enabled = Dlcs.ToList().FindAll(x => x.IsEnabled);

                foreach (DlcModel dlc in enabled)
                {
                    Dlcs.Remove(dlc);
                }
            }
            else
            {
                Dlcs.Clear();
            }
        }

        public void RemoveSelected()
        {
            RemoveDlcs(true);
        }

        public void RemoveAll()
        {
            RemoveDlcs();
        }

        public async void Add()
        {
            OpenFileDialog dialog = new() {Title = "Select dlc files", AllowMultiple = true};
            dialog.Filters.Add(new FileDialogFilter {Name = "NSP", Extensions = {"nsp"}});

            string[] files = await dialog.ShowAsync(this);

            if (files != null)
            {
                foreach (string file in files)
                {
                    AddDlc(file);
                }
            }
        }

        public void Save()
        {
            _dlcContainerList.Clear();

            DlcContainer container = default;

            foreach (DlcModel dlc in Dlcs)
            {
                if (container.Path != dlc.ContainerPath)
                {
                    if (!string.IsNullOrWhiteSpace(container.Path))
                    {
                        _dlcContainerList.Add(container);
                    }

                    container = new DlcContainer {Path = dlc.ContainerPath, DlcNcaList = new List<DlcNca>()};
                }

                container.DlcNcaList.Add(new DlcNca
                {
                    Enabled = dlc.IsEnabled, TitleId = Convert.ToUInt64(dlc.TitleId, 16), Path = dlc.FullPath
                });
            }

            if (!string.IsNullOrWhiteSpace(container.Path))
            {
                _dlcContainerList.Add(container);
            }

            using (FileStream dlcJsonStream = File.Create(_dlcJsonPath, 4096, FileOptions.WriteThrough))
            {
                dlcJsonStream.Write(Encoding.UTF8.GetBytes(JsonHelper.Serialize(_dlcContainerList, true)));
            }

            Close();
        }
    }
}