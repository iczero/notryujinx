using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Ryujinx.Ava.Ui.ViewModels;
using Ryujinx.Common.Configuration.Hid;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.FileSystem.Content;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ryujinx.Ava.Ui.Windows
{
    public class SettingsWindow : StyleableWindow
    {
        private ListBox _gameList;
        // public ConfigurationState Config => ConfigurationState.Instance;

        private TextBox _pathBox;
        private AutoCompleteBox _timeZoneBox;

        public SettingsWindow(VirtualFileSystem virtualFileSystem, ContentManager contentManager)
        {
            ViewModel = new SettingsViewModel(virtualFileSystem, contentManager);
            DataContext = ViewModel;

            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            FuncMultiValueConverter<string, string> converter = new(parts =>
                String.Format("{0}  {1}   {2}", parts.ToArray()));
            MultiBinding tzMultiBinding = new() {Converter = converter};
            tzMultiBinding.Bindings.Add(new Binding("UtcDifference"));
            tzMultiBinding.Bindings.Add(new Binding("Location"));
            tzMultiBinding.Bindings.Add(new Binding("Abbreviation"));

            _timeZoneBox.ValueMemberBinding = tzMultiBinding;
        }

        public SettingsWindow()
        {
            ViewModel = new SettingsViewModel();
            DataContext = ViewModel;

            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        public SettingsViewModel ViewModel { get; set; }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _pathBox = this.FindControl<TextBox>("PathBox");
            _gameList = this.FindControl<ListBox>("GameList");
            _timeZoneBox = this.FindControl<AutoCompleteBox>("TimeZoneBox");
        }

        private async void AddButton_OnClick(object? sender, RoutedEventArgs e)
        {
            string path = _pathBox.Text;

            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path) && !ViewModel.GameDirectories.Contains(path))
            {
                ViewModel.GameDirectories.Add(path);
            }
            else
            {
                OpenFolderDialog dialog = new();

                path = await dialog.ShowAsync(this);

                if (!string.IsNullOrWhiteSpace(path))
                {
                    ViewModel.GameDirectories.Add(path);
                }
            }
        }

        private void RemoveButton_OnClick(object? sender, RoutedEventArgs e)
        {
            List<string> selected = new(_gameList.SelectedItems.Cast<string>());

            foreach (string path in selected)
            {
                ViewModel.GameDirectories.Remove(path);
            }
        }

        private void TimeZoneBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems != null && e.AddedItems.Count > 0)
            {
                TimeZone timeZone = e.AddedItems[0] as TimeZone;

                if (timeZone != null)
                {
                    e.Handled = true;

                    ViewModel.ValidateAndSetTimeZone(timeZone.Location);
                }
            }
        }

        private void TimeZoneBox_OnTextChanged(object? sender, EventArgs e)
        {
            if (sender is AutoCompleteBox box)
            {
                if (box.SelectedItem != null && box.SelectedItem is TimeZone timeZone)
                {
                    ViewModel.ValidateAndSetTimeZone(timeZone.Location);
                }
            }
        }

        private void SaveButton_Clicked(object? sender, RoutedEventArgs e)
        {
            ViewModel.SaveSettings();

            if (Owner is MainWindow window)
            {
                window.ViewModel.LoadApplications();
            }

            Close();
        }

        private void CloseButton_Clicked(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ApplyButton_Clicked(object? sender, RoutedEventArgs e)
        {
            ViewModel.SaveSettings();

            if (Owner is MainWindow window)
            {
                window.ViewModel.LoadApplications();
            }
        }

        private async void Button_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                string? tag = button.Tag.ToString();

                if (!string.IsNullOrWhiteSpace(tag))
                {
                    PlayerIndex player = Enum.Parse<PlayerIndex>(tag);

                    ControllerSettingsWindow settingsWindow = new(player, Owner as MainWindow);

                    await settingsWindow.ShowDialog(this);
                }
            }
        }
    }
}