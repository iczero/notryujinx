using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Ryujinx.Ava.Ui.Models;
using Ryujinx.Ava.Ui.ViewModels;
using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Configuration.Hid.Controller;
using Ryujinx.Input;
using Ryujinx.Input.Assigner;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Key = Ryujinx.Input.Key;
using StickInputId = Ryujinx.Input.StickInputId;

namespace Ryujinx.Ava.Ui.Windows
{
    public class ControllerSettingsWindow : StyleableWindow
    {
        private bool _isWaitingForInput;
        private bool _mousePressed;

        public ControllerSettingsWindow()
        {
            Initialize();
        }

        public ControllerSettingsWindow(PlayerIndex playerIndex, MainWindow parent)
        {
            ViewModel = new ControllerSettingsViewModel(playerIndex, this, parent);

            ViewModel.OnClose += ViewModelOnOnClose;

            DataContext = ViewModel;

            Initialize();

            Activated += OnActivated;
        }

        public ScrollViewer View { get; set; }
        public Grid ViewGrid { get; set; }
        public ToggleButton CurrentToggledButton { get; set; }

        public ControllerSettingsViewModel ViewModel { get; set; }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            ViewModel?.Dispose();
        }

        private void ViewModelOnOnClose(object sender, EventArgs e)
        {
            Close();
        }

        private void OnActivated(object sender, EventArgs e)
        {
            IEnumerable<ILogical> children = View.GetLogicalDescendants();
            foreach (ILogical visual in children)
            {
                if (visual is Control control)
                {
                    if (visual is ToggleButton button && !(visual is CheckBox))
                    {
                        button.Checked += ButtonOnCheck;
                        button.Unchecked += ButtonOnUnchecked;
                    }
                }
            }
        }

        private void ButtonOnUnchecked(object sender, RoutedEventArgs e)
        {
            if (CurrentToggledButton != null)
            {
                ToggleButton button = CurrentToggledButton;
                CurrentToggledButton = null;
                button.IsChecked = false;
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (CurrentToggledButton != null && !CurrentToggledButton.IsPointerOver)
            {
                ToggleButton button = CurrentToggledButton;
                CurrentToggledButton = null;
                button.IsChecked = false;
            }
        }

        private void ButtonOnCheck(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton button)
            {
                if (button == CurrentToggledButton)
                {
                    return;
                }

                if (CurrentToggledButton == null && (bool)button.IsChecked)
                {
                    CurrentToggledButton = button;

                    _isWaitingForInput = false;

                    bool isStick = button.Tag != null && button.Tag.ToString() == "stick";
                    
                    FocusManager.Instance.Focus(this, NavigationMethod.Pointer);

                    Task.Run(() => HandleButtonPressed(button, isStick));
                }
                else
                {
                    if (CurrentToggledButton != null)
                    {
                        ToggleButton oldButton = CurrentToggledButton;
                        CurrentToggledButton = null;
                        oldButton.IsChecked = false;
                        button.IsChecked = false;
                    }
                }
            }
        }

        public void HandleButtonPressed(ToggleButton button, bool forStick)
        {
            if (_isWaitingForInput)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    button.IsChecked = false;
                });
                return;
            }

            _mousePressed = false;

            PointerPressed += MouseClick;

            IButtonAssigner assigner = CreateButtonAssigner(forStick);

            _isWaitingForInput = true;

            // Open Avalonia keyboard for cancel operations
            IKeyboard keyboard = (IKeyboard)ViewModel.AvaloniaKeyboardDriver.GetGamepad("0");

            Thread inputThread = new(() =>
            {
                assigner.Initialize();

                while (true)
                {
                    if (!_isWaitingForInput)
                    {
                        return;
                    }
                    Thread.Sleep(10);
                    assigner.ReadInput();

                    if (_mousePressed || keyboard.IsPressed(Key.Escape) || assigner.HasAnyButtonPressed() ||
                        assigner.ShouldCancel())
                    {
                        break;
                    }
                }

                string pressedButton = assigner.GetPressedButton();

                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        SetButtonText(button, pressedButton);
                    }
                    catch { }

                    keyboard.Dispose();
                    _isWaitingForInput = false;
                    button = CurrentToggledButton;
                    CurrentToggledButton = null;
                    if (button != null)
                    {
                        button.IsChecked = false;
                    }

                    PointerPressed -= MouseClick;

                    void SetButtonText(ToggleButton button, string text)
                    {
                        ILogical textBlock = button.GetLogicalDescendants().First(x => x is TextBlock);

                        if (textBlock != null && textBlock is TextBlock block)
                        {
                            block.Text = text;
                        }
                    }
                });
            });

            inputThread.Name = "GUI.InputThread";
            inputThread.IsBackground = true;
            inputThread.Start();
        }


        private IButtonAssigner CreateButtonAssigner(bool forStick)
        {
            IButtonAssigner assigner;

            string selected = ViewModel.Devices.Keys.ToArray()[ViewModel.Device];

            if (selected.StartsWith("keyboard"))
            {
                assigner = new KeyboardKeyAssigner((IKeyboard)ViewModel.SelectedGamepad);
            }
            else if (selected.StartsWith("controller"))
            {
                assigner = new GamepadButtonAssigner(ViewModel.SelectedGamepad,
                    (ViewModel.InputConfig as InputConfiguration<GamepadInputId, StickInputId>).TriggerThreshold, forStick);
            }
            else
            {
                throw new Exception("Controller not supported");
            }

            return assigner;
        }

        private void MouseClick(object sender, PointerPressedEventArgs e)
        {
            _mousePressed = true;
        }

        public void Initialize()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            View = this.FindControl<ScrollViewer>("View");
            ViewGrid = this.FindControl<Grid>("ViewGrid");

            IObservable<Size> resizeObserverable = this.GetObservable(ClientSizeProperty);

            resizeObserverable.Subscribe(Resized);

            IObservable<Rect> stateObserverable = this.GetObservable(BoundsProperty);

            stateObserverable.Subscribe(StateChanged);
        }

        public void UpdateSizes(Size size)
        {
            //Workaround for gamelist not fitting parent

            if (ViewGrid != null)
            {
                ViewGrid.Height = ClientSize.Height;
                ViewGrid.Width = ClientSize.Width;
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

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}