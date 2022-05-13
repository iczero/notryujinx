using Avalonia.Threading;
using Ryujinx.Rsc.Library.Common;
using System;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using Ryujinx.Rsc.Views;
using Ryujinx.Rsc.Common.Configuration;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading;

namespace Ryujinx.Rsc.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private ObservableCollection<ApplicationData> _applications;
        private ReadOnlyObservableCollection<ApplicationData> _appsObservableList;
        private bool _isLoading;
        public MainView Owner { get; set; }

        public MainViewModel()
        {
            Applications = new ObservableCollection<ApplicationData>();

            Applications.ToObservableChangeSet()
                .Bind(out _appsObservableList).AsObservableList();
        }

        public ObservableCollection<ApplicationData> Applications
        {
            get => _applications;
            set
            {
                _applications = value;

                this.RaisePropertyChanged();
            }
        }
        
        public ReadOnlyObservableCollection<ApplicationData> AppsObservableList
        {
            get => _appsObservableList;
            set
            {
                _appsObservableList = value;

                this.RaisePropertyChanged();
            }
        }

        public bool IsGridSmall => ConfigurationState.Instance.Ui.GridSize == 1;
        public bool IsGridMedium => ConfigurationState.Instance.Ui.GridSize == 2;
        public bool IsGridLarge => ConfigurationState.Instance.Ui.GridSize == 3;
        public bool IsGridHuge => ConfigurationState.Instance.Ui.GridSize == 4;
        public bool IsGameRunning { get; set; }
        public string Title { get; set; }
        public bool IsPaused { get; set; }
        public string TitleName { get; set; }

        public void Initialize()
        {
            Owner.ApplicationLibrary.ApplicationCountUpdated += ApplicationLibrary_ApplicationCountUpdated;
            Owner.ApplicationLibrary.ApplicationAdded += ApplicationLibrary_ApplicationAdded;

            ReloadGameList();
        }

        private void ApplicationLibrary_ApplicationAdded(object? sender, ApplicationAddedEventArgs e)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                Applications.Add(e.AppData);
            });
        }

        private void ReloadGameList()
        {
            if (_isLoading)
            {
                return;
            }

            _isLoading = true;

            Thread thread = new(() =>
            {
                Owner.ApplicationLibrary.LoadApplications(ConfigurationState.Instance.Ui.GameDirs.Value,
                    ConfigurationState.Instance.System.Language);

                _isLoading = false;
            }) {Name = "GUI.AppListLoadThread", Priority = ThreadPriority.AboveNormal};

            thread.Start();
        }

        private void ApplicationLibrary_ApplicationCountUpdated(object? sender, ApplicationCountUpdatedEventArgs e)
        {
        }
    }
}
