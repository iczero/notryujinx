using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.FsSystem.NcaUtils;
using LibHac.Ns;
using Ryujinx.Ava.Ui.Controls;
using Ryujinx.Ava.Ui.Models;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SpanHelpers = LibHac.Common.SpanHelpers;

namespace Ryujinx.Ava.Ui.Windows
{
    public class TitleUpdateWindow : StyleableWindow
    {
        private readonly string _updateJsonPath;
        private TitleUpdateMetadata _titleUpdateWindowData;

        public TitleUpdateWindow()
        {
            DataContext = this;

            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        public TitleUpdateWindow(VirtualFileSystem virtualFileSystem, string titleId, string titleName)
        {
            VirtualFileSystem = virtualFileSystem;
            TitleId = titleId;
            TitleName = titleName;

            DataContext = this;

            _updateJsonPath = Path.Combine(AppDataManager.GamesDirPath, titleId, "updates.json");

            try
            {
                _titleUpdateWindowData = JsonHelper.DeserializeFromFile<TitleUpdateMetadata>(_updateJsonPath);
            }
            catch
            {
                _titleUpdateWindowData = new TitleUpdateMetadata {Selected = "", Paths = new List<string>()};
            }

            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            LoadUpdates();
        }

        public AvaloniaList<TitleUpdateModel> TitleUpdates { get; set; }
        public VirtualFileSystem VirtualFileSystem { get; }
        public string TitleId { get; }
        public string TitleName { get; }

        public string Heading => $"Updates Available for {TitleName} [{TitleId.ToUpper()}]";

        private void InitializeComponent()
        {
            TitleUpdates = new AvaloniaList<TitleUpdateModel>();

            AvaloniaXamlLoader.Load(this);
        }

        public void LoadUpdates()
        {
            TitleUpdates.Add(new TitleUpdateModel(default, string.Empty, true));

            foreach (string path in _titleUpdateWindowData.Paths)
            {
                AddUpdate(path);
            }

            if (_titleUpdateWindowData.Selected == "")
            {
                TitleUpdates[0].IsEnabled = true;
            }
            else
            {
                TitleUpdateModel? selected = TitleUpdates.ToList().Find(x => x.Path == _titleUpdateWindowData.Selected);
                List<TitleUpdateModel> enabled = TitleUpdates.ToList().FindAll(x => x.IsEnabled);

                foreach (TitleUpdateModel update in enabled)
                {
                    update.IsEnabled = false;
                }

                if (selected != null)
                {
                    selected.IsEnabled = true;
                }
            }
        }

        private async void AddUpdate(string path)
        {
            if (File.Exists(path))
            {
                using (FileStream file = new(path, FileMode.Open, FileAccess.Read))
                {
                    PartitionFileSystem nsp = new(file.AsStorage());

                    try
                    {
                        (Nca patchNca, Nca controlNca) =
                            ApplicationLoader.GetGameUpdateDataFromPartition(VirtualFileSystem, nsp, TitleId, 0);

                        if (controlNca != null && patchNca != null)
                        {
                            ApplicationControlProperty controlData = new();

                            controlNca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None)
                                .OpenFile(out IFile nacpFile, "/control.nacp".ToU8Span(), OpenMode.Read)
                                .ThrowIfFailure();
                            nacpFile.Read(out _, 0, SpanHelpers.AsByteSpan(ref controlData), ReadOption.None)
                                .ThrowIfFailure();

                            TitleUpdates.Add(new TitleUpdateModel(controlData, path));
                        }
                        else
                        {
                            AvaDialog.CreateErrorDialog(
                                "The specified file does not contain an update for the selected title!", this);
                        }
                    }
                    catch (Exception exception)
                    {
                        AvaDialog.CreateErrorDialog($"{exception.Message}. Errored File: {path}", this);
                    }
                }
            }
        }

        private void RemoveUpdates(bool removeSelectedOnly = false)
        {
            if (removeSelectedOnly)
            {
                List<TitleUpdateModel> enabled = TitleUpdates.ToList().FindAll(x => x.IsEnabled);

                foreach (TitleUpdateModel update in enabled)
                {
                    TitleUpdates.Remove(update);
                }
            }
            else
            {
                TitleUpdates.Clear();
            }
        }

        public void RemoveSelected()
        {
            RemoveUpdates(true);
        }

        public void RemoveAll()
        {
            RemoveUpdates();
        }

        public async void Add()
        {
            OpenFileDialog dialog = new() {Title = "Select update files", AllowMultiple = true};

            dialog.Filters.Add(new FileDialogFilter {Name = "NSP", Extensions = {"nsp"}});

            string[] files = await dialog.ShowAsync(this);

            if (files != null)
            {
                foreach (string file in files)
                {
                    AddUpdate(file);
                }
            }
        }

        public void Save()
        {
            _titleUpdateWindowData.Paths.Clear();
            _titleUpdateWindowData.Selected = "";

            foreach (TitleUpdateModel update in TitleUpdates)
            {
                _titleUpdateWindowData.Paths.Add(update.Path);

                if (update.IsEnabled)
                {
                    _titleUpdateWindowData.Selected = update.Path;
                }
            }

            using (FileStream dlcJsonStream = File.Create(_updateJsonPath, 4096, FileOptions.WriteThrough))
            {
                dlcJsonStream.Write(Encoding.UTF8.GetBytes(JsonHelper.Serialize(_titleUpdateWindowData, true)));
            }

            if (Owner is MainWindow window)
            {
                window.ViewModel.LoadApplications();
            }

            Close();
        }
    }
}