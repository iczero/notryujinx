using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using LibHac;
using Ryujinx.Ava.Common;
using Ryujinx.Ava.Ui.ViewModels;
using System;
using System.Collections.Generic;

namespace Ryujinx.Ava.Ui.Controls
{
    public partial class GameGridView : UserControl
    {
        private ApplicationData _selectedApplication;

        public event EventHandler<ApplicationData> ApplicationOpened;

        public void GameList_DoubleTapped(object sender, RoutedEventArgs args)
        {
            if (sender is ListBox listBox)
            {
                var selected = listBox.SelectedItem as ApplicationData;

                if (selected != null)
                {
                    ApplicationOpened?.Invoke(this, selected);
                }
            }
        }

        public void GameList_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            if (sender is ListBox listBox)
            {
                var selected = listBox.SelectedItem as ApplicationData;

                _selectedApplication = selected;
            }
        }

        public ApplicationData SelectedApplication => _selectedApplication;

        public GameGridView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void SearchBox_OnKeyUp(object sender, KeyEventArgs e)
        {
            (DataContext as MainWindowViewModel).SearchText = (sender as TextBox).Text;
        }

        private void MenuBase_OnMenuOpened(object sender, RoutedEventArgs e)
        {
            var selection = SelectedApplication;

            if (selection != null)
            {
                if (sender is ContextMenu menu)
                {
                    bool canHaveUserSave = !Utilities.IsZeros(selection.ControlHolder.ByteSpan) && selection.ControlHolder.Value.UserAccountSaveDataSize > 0;
                    bool canHaveDeviceSave = !Utilities.IsZeros(selection.ControlHolder.ByteSpan) && selection.ControlHolder.Value.DeviceSaveDataSize > 0;
                    bool canHaveBcatSave = !Utilities.IsZeros(selection.ControlHolder.ByteSpan) && selection.ControlHolder.Value.BcatDeliveryCacheStorageSize > 0;

                    ((menu.Items as AvaloniaList<object>)[2] as MenuItem).IsEnabled = canHaveUserSave;
                    ((menu.Items as AvaloniaList<object>)[3] as MenuItem).IsEnabled = canHaveDeviceSave;
                    ((menu.Items as AvaloniaList<object>)[4] as MenuItem).IsEnabled = canHaveBcatSave;
                }
            }
        }
    }
}
