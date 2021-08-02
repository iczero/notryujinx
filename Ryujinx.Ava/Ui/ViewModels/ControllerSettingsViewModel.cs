using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Svg.Skia;
using Avalonia.VisualTree;
using FluentAvalonia.Core;
using IX.Observable;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.Ui.Controls;
using Ryujinx.Ava.Ui.Models;
using Ryujinx.Ava.Ui.Windows;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Configuration.Hid.Controller;
using Ryujinx.Common.Configuration.Hid.Controller.Motion;
using Ryujinx.Common.Configuration.Hid.Keyboard;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using Ryujinx.Configuration;
using Ryujinx.Input;
using Ryujinx.Input.Avalonia;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using ConfigGamepadInputId = Ryujinx.Common.Configuration.Hid.Controller.GamepadInputId;
using ConfigStickInputId = Ryujinx.Common.Configuration.Hid.Controller.StickInputId;
using Key = Ryujinx.Common.Configuration.Hid.Key;

namespace Ryujinx.Ava.Ui.ViewModels
{
    public class ControllerSettingsViewModel : BaseModel, IDisposable
    {
        private readonly MainWindow _mainWindow;

        private PlayerIndex _playerId;
        private int         _controller;
        private string      _controllerImage;
        private int         _device;
        private int         _profile;
        private bool        _isMotionController;
        private bool        _isCemuHookMotionController;

        private InputConfig _inputConfig;
        private object      _inputConfiguration;
        private bool _isLoaded;
        private readonly UserControl _owner;

        public IGamepadDriver AvaloniaKeyboardDriver { get; }
        public IGamepad       SelectedGamepad        { get; private set; }

        public ObservableDictionary<PlayerIndex, string>    PlayerIndexes { get; set; }
        public ObservableDictionary<string, string>         Devices       { get; set; }
        public ObservableDictionary<ControllerType, string> Controllers   { get; set; }
        public ObservableDictionary<string, string>         Profiles      { get; set; }

        // XAML Flags
        public bool ShowSettings => _device > 0;
        public bool IsController => _device > 1;
        public bool IsKeyboard   => !IsController;
        public bool IsRight { get; set; }
        public bool IsLeft  { get; set; }
        
        public bool IsModified { get; set; }

        public object InputConfig
        {
            get => _inputConfiguration;
            set
            {
                _inputConfiguration = value;

                OnPropertyChanged();
            }
        }

        public PlayerIndex PlayerId
        {
            get => _playerId;
            set
            {
                if (IsModified)
                {
                    return;
                }

                IsModified = false;
                _playerId = value;

                if (!Enum.IsDefined(typeof(PlayerIndex), _playerId))
                {
                    _playerId = PlayerIndex.Player1;
                }

                LoadConfiguration();
                LoadDevice();
                LoadProfiles();

                _isLoaded = true;

                OnPropertyChanged();
            }
        }

        public int Controller
        {
            get => _controller;
            set
            {
                _controller = value;

                if (_controller == -1)
                {
                    _controller = 0;
                }

                if (Controllers.Count > 0 && value < Controllers.Count && _controller > -1)
                {
                    ControllerType controller = Controllers.Keys.ToArray()[_controller];

                    IsLeft  = true;
                    IsRight = true;

                    switch (controller)
                    {
                        case ControllerType.Handheld:
                            ControllerImage = "Ryujinx.Ava.Assets.Images.Controller_JoyConPair.svg";
                            break;
                        case ControllerType.ProController:
                            ControllerImage = "Ryujinx.Ava.Assets.Images.Controller_ProCon.svg";
                            break;
                        case ControllerType.JoyconPair:
                            ControllerImage = "Ryujinx.Ava.Assets.Images.Controller_JoyConPair.svg";
                            break;
                        case ControllerType.JoyconLeft:
                            ControllerImage = "Ryujinx.Ava.Assets.Images.Controller_JoyConLeft.svg";
                            IsRight = false;
                            break;
                        case ControllerType.JoyconRight:
                            ControllerImage = "Ryujinx.Ava.Assets.Images.Controller_JoyConRight.svg";
                            IsLeft = false;
                            break;
                    }

                    LoadInputDriver();
                    LoadProfiles();
                }

                OnPropertyChanged();
                NotifyChanges();
            }
        }

        public string ControllerImage
        {
            get => _controllerImage;
            set
            {
                _controllerImage = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(Image));
            }
        }

        public SvgImage Image
        {
            get
            {
                SvgImage image = new SvgImage();

                if (!string.IsNullOrWhiteSpace(_controllerImage))
                {
                    SvgSource source = new SvgSource();

                    source.Load(Assembly.GetAssembly(typeof(ControllerSettingsViewModel)).GetManifestResourceStream(_controllerImage));

                    image.Source = source;
                }

                return image;
            }
        }

        public bool IsMotionController
        {
            get
            {
                if (!IsController)
                {
                    return false;
                }

                return _isMotionController;
                //return (_inputConfig as StandardControllerInputConfig).Motion.MotionBackend == MotionInputBackendType.CemuHook;
            }
            set
            {
                //(_inputConfig as StandardControllerInputConfig).Motion.MotionBackend = value ? MotionInputBackendType.CemuHook : MotionInputBackendType.GamepadDriver;
                _isMotionController = value;
                OnPropertyChanged();
            }
        }

        public int Device
        {
            get => _device;
            set
            {
                _device = value < 0 ? 0 : value;

                string selected = Devices.Keys.ToArray()[_device];

                if (selected == "disable")
                {
                    return;
                }

                LoadControllers();

                if (_isLoaded)
                {
                    LoadConfiguration(LoadDefaultConfiguration());
                }

                OnPropertyChanged();
                NotifyChanges();
            }
        }

        public int Profile
        {
            get => _profile;
            set
            {
                _profile = value;

                OnPropertyChanged();
            }
        }

        public bool IsCemuHookMotionController
        {
            get
            {
                if (!IsController)
                {
                    return false;
                }

                return _isCemuHookMotionController;
            }
            set
            {
                _isCemuHookMotionController = value;
                OnPropertyChanged();
            }
        }

        public ControllerSettingsViewModel(UserControl owner) : this()
        {
            _owner = owner;
            
            if (Program.PreviewerDetached)
            {
                _mainWindow =
                    (MainWindow)((IClassicDesktopStyleApplicationLifetime)Avalonia.Application.Current
                        .ApplicationLifetime).MainWindow;

                AvaloniaKeyboardDriver = new AvaloniaKeyboardDriver(owner);

                _mainWindow.InputManager.GamepadDriver.OnGamepadConnected += HandleOnGamepadConnected;
                _mainWindow.InputManager.GamepadDriver.OnGamepadDisconnected += HandleOnGamepadDisconnected;
                if (_mainWindow.AppHost != null)
                {
                    _mainWindow.AppHost.NpadManager.BlockInputUpdates();
                }

                LoadDevices();

                PlayerId = PlayerIndex.Player1;
            }
        }

        public ControllerSettingsViewModel()
        {
            PlayerIndexes = new ObservableDictionary<PlayerIndex, string>();
            Controllers   = new ObservableDictionary<ControllerType, string>();
            Devices       = new ObservableDictionary<string, string>();
            Profiles      = new ObservableDictionary<string, string>();

            ControllerImage = "Ryujinx.Ava.Assets.Images.Controller_ProCon.svg";

            PlayerIndexes.Add(PlayerIndex.Player1,  LocaleManager.Instance["ControllerSettingsPlayer1"]);
            PlayerIndexes.Add(PlayerIndex.Player2,  LocaleManager.Instance["ControllerSettingsPlayer2"]);
            PlayerIndexes.Add(PlayerIndex.Player3,  LocaleManager.Instance["ControllerSettingsPlayer3"]);
            PlayerIndexes.Add(PlayerIndex.Player4,  LocaleManager.Instance["ControllerSettingsPlayer4"]);
            PlayerIndexes.Add(PlayerIndex.Player5,  LocaleManager.Instance["ControllerSettingsPlayer5"]);
            PlayerIndexes.Add(PlayerIndex.Player6,  LocaleManager.Instance["ControllerSettingsPlayer6"]);
            PlayerIndexes.Add(PlayerIndex.Player7,  LocaleManager.Instance["ControllerSettingsPlayer7"]);
            PlayerIndexes.Add(PlayerIndex.Player8,  LocaleManager.Instance["ControllerSettingsPlayer8"]);
            PlayerIndexes.Add(PlayerIndex.Handheld, LocaleManager.Instance["ControllerSettingsHandheld"]);
        }

        private void LoadConfiguration(InputConfig inputConfig = null)
        {
            _inputConfig = inputConfig ?? ConfigurationState.Instance.Hid.InputConfig.Value.Find(inputConfig => inputConfig.PlayerIndex == _playerId);

            if (_inputConfig is StandardKeyboardInputConfig)
            {
                _inputConfiguration = new InputConfiguration<Key, ConfigStickInputId>(_inputConfig as StandardKeyboardInputConfig);
            }

            if (_inputConfig is StandardControllerInputConfig)
            {
                _inputConfiguration = new InputConfiguration<ConfigGamepadInputId, ConfigStickInputId>(_inputConfig as StandardControllerInputConfig);
            }
        }

        public void LoadDevice()
        {
            if (_inputConfig == null || _inputConfig.Backend == InputBackendType.Invalid)
            {
                Device = 0;
            }
            else
            {
                string ident = "";

                if (_inputConfig is StandardKeyboardInputConfig)
                {
                    ident = "keyboard";
                }

                if (_inputConfig is StandardControllerInputConfig)
                {
                    ident = "controller";
                }

                var item = Devices.FirstOrDefault(x => x.Key == $"{ident}/{_inputConfig.Id}");
                if (item.Key != null)
                {
                    Device = Devices.Keys.ToList().IndexOf(item.Key);
                }
                else
                {
                    Device = 0;
                }
            }
        }

        private void LoadInputDriver()
        {
            if (_device < 0)
            {
                return;
            }

            string id       = GetCurrentGamepadId();
            string selected = Devices.Keys.ToArray()[Device];

            if (selected == "disabled")
            {
                return;
            }

            if (selected.StartsWith("keyboard"))
            {
                if (_mainWindow.InputManager.KeyboardDriver is AvaloniaKeyboardDriver)
                {
                    // NOTE: To get input in this window, we need to bind a custom keyboard driver instead of using the InputManager one as the main window isn't focused...
                    SelectedGamepad = AvaloniaKeyboardDriver.GetGamepad(id);
                }
                else
                {
                    SelectedGamepad = _mainWindow.InputManager.KeyboardDriver.GetGamepad(id);
                }
            }
            else
            {
                SelectedGamepad = _mainWindow.InputManager.GamepadDriver.GetGamepad(id);
            }
        }

        private void HandleOnGamepadDisconnected(string id)
        {
            LoadDevices();
        }

        private void HandleOnGamepadConnected(string id)
        {
            LoadDevices();
        }

        private string GetCurrentGamepadId()
        {
            if (_device < 0)
            {
                return string.Empty;
            }

            string selected = Devices.Keys.ToArray()[Device];

            if (selected == null || selected == "disabled")
            {
                return null;
            }

            return selected.Split("/")[1].Split(" ")[0];
        }

        public void LoadControllers()
        {
            Controllers.Clear();

            if (_playerId == PlayerIndex.Handheld)
            {
                Controllers.Add(ControllerType.Handheld, LocaleManager.Instance["ControllerSettingsControllerTypeHandheld"]);

                Controller = 0;
            }
            else
            {
                Controllers.Add(ControllerType.ProController, LocaleManager.Instance["ControllerSettingsControllerTypeProController"]);
                Controllers.Add(ControllerType.JoyconPair,    LocaleManager.Instance["ControllerSettingsControllerTypeJoyConPair"]);
                Controllers.Add(ControllerType.JoyconLeft,    LocaleManager.Instance["ControllerSettingsControllerTypeJoyConLeft"]);
                Controllers.Add(ControllerType.JoyconRight,   LocaleManager.Instance["ControllerSettingsControllerTypeJoyConRight"]);

                if (_inputConfig != null && Controllers.ContainsKey(_inputConfig.ControllerType))
                {
                    Controller = Controllers.Keys.ToList().IndexOf(_inputConfig.ControllerType);
                }
                else
                {
                    Controller = 0;
                }
            }
        }
        
        private static string GetShrinkedGamepadName(string str)
        {
            const string ShrinkChars = "...";
            const int MaxSize = 50;

            if (str.Length > MaxSize)
            {
                return str.Substring(0, MaxSize - ShrinkChars.Length) + ShrinkChars;
            }

            return str;
        }
        
        public void LoadDevices()
        {
            Devices.Clear();
            Devices.Add("disabled", LocaleManager.Instance["ControllerSettingsDeviceDisabled"]);

            foreach (string id in _mainWindow.InputManager.KeyboardDriver.GamepadsIds)
            {
                IGamepad gamepad = _mainWindow.InputManager.KeyboardDriver.GetGamepad(id);

                if (gamepad != null)
                {
                    Devices.Add($"keyboard/{id}", $"{gamepad.Name} ({id})");

                    gamepad.Dispose();
                }
            }

            foreach (string id in _mainWindow.InputManager.GamepadDriver.GamepadsIds)
            {
                IGamepad gamepad = _mainWindow.InputManager.GamepadDriver.GetGamepad(id);

                if (gamepad != null)
                {
                    Devices.Add($"controller/{id}", $"{gamepad.Name} ({id})");

                    gamepad.Dispose();
                }
            }
        }

        private string GetProfileBasePath()
        {
            string path     = AppDataManager.ProfilesDirPath;
            string selected = Devices.Keys.ToArray()[Device == -1 ? 0 : Device];

            if (selected.StartsWith("keyboard"))
            {
                path = Path.Combine(path, "keyboard");
            }
            else if (selected.StartsWith("controller"))
            {
                path = Path.Combine(path, "controller");
            }

            return path;
        }

        private void LoadProfiles()
        {
            Profiles.Clear();

            string basePath = GetProfileBasePath();

            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            Profiles.Add("default", LocaleManager.Instance["ControllerSettingsProfileDefault"]);

            foreach (string profile in Directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories))
            {
                Profiles.Add(Path.GetFileName(profile), Path.GetFileNameWithoutExtension(profile));
            }

            Profile = 0;

            OnPropertyChanged(nameof(Profiles));
        }

        public InputConfig LoadDefaultConfiguration()
        {
            string activeDevice = "disabled";

            if (Devices.Keys.Count > 0 && Device < Devices.Keys.Count && Device >= 0)
            {
                activeDevice = Devices.Keys.ToArray()[Device];
            }

            InputConfig config;
            if (activeDevice.StartsWith("keyboard"))
            {
                string id = activeDevice.Split("/")[1];
                
                config = new StandardKeyboardInputConfig
                {
                    Version = Ryujinx.Common.Configuration.Hid.InputConfig.CurrentVersion,
                    Backend = InputBackendType.WindowKeyboard,
                    Id = id,
                    ControllerType = ControllerType.ProController,
                    LeftJoycon =
                        new LeftJoyconCommonConfig<Key>
                        {
                            DpadUp = Key.Up,
                            DpadDown = Key.Down,
                            DpadLeft = Key.Left,
                            DpadRight = Key.Right,
                            ButtonMinus = Key.Minus,
                            ButtonL = Key.E,
                            ButtonZl = Key.Q,
                            ButtonSl = Key.Unbound,
                            ButtonSr = Key.Unbound
                        },
                    LeftJoyconStick =
                        new JoyconConfigKeyboardStick<Key>
                        {
                            StickUp = Key.W,
                            StickDown = Key.S,
                            StickLeft = Key.A,
                            StickRight = Key.D,
                            StickButton = Key.F
                        },
                    RightJoycon = new RightJoyconCommonConfig<Key>
                    {
                        ButtonA = Key.Z,
                        ButtonB = Key.X,
                        ButtonX = Key.C,
                        ButtonY = Key.V,
                        ButtonPlus = Key.Plus,
                        ButtonR = Key.U,
                        ButtonZr = Key.O,
                        ButtonSl = Key.Unbound,
                        ButtonSr = Key.Unbound
                    },
                    RightJoyconStick = new JoyconConfigKeyboardStick<Key>
                    {
                        StickUp = Key.I,
                        StickDown = Key.K,
                        StickLeft = Key.J,
                        StickRight = Key.L,
                        StickButton = Key.H
                    }
                };
            }
            else if (activeDevice.StartsWith("controller"))
            {
                bool isNintendoStyle = Devices[activeDevice].Contains("Nintendo");
                
                string id = activeDevice.Split("/")[1].Split(" ")[0];

                config = new StandardControllerInputConfig
                {
                    Version = Ryujinx.Common.Configuration.Hid.InputConfig.CurrentVersion,
                    Backend = InputBackendType.GamepadSDL2,
                    Id = id,
                    ControllerType = ControllerType.ProController,
                    DeadzoneLeft = 0.1f,
                    DeadzoneRight = 0.1f,
                    TriggerThreshold = 0.5f,
                    LeftJoycon =
                        new LeftJoyconCommonConfig<ConfigGamepadInputId>
                        {
                            DpadUp = ConfigGamepadInputId.DpadUp,
                            DpadDown = ConfigGamepadInputId.DpadDown,
                            DpadLeft = ConfigGamepadInputId.DpadLeft,
                            DpadRight = ConfigGamepadInputId.DpadRight,
                            ButtonMinus = ConfigGamepadInputId.Minus,
                            ButtonL = ConfigGamepadInputId.LeftShoulder,
                            ButtonZl = ConfigGamepadInputId.LeftTrigger,
                            ButtonSl = ConfigGamepadInputId.Unbound,
                            ButtonSr = ConfigGamepadInputId.Unbound
                        },
                    LeftJoyconStick =
                        new JoyconConfigControllerStick<ConfigGamepadInputId, ConfigStickInputId>
                        {
                            Joystick = ConfigStickInputId.Left,
                            StickButton = ConfigGamepadInputId.LeftStick,
                            InvertStickX = false,
                            InvertStickY = false
                        },
                    RightJoycon =
                        new RightJoyconCommonConfig<ConfigGamepadInputId>
                        {
                            ButtonA = isNintendoStyle ? ConfigGamepadInputId.A : ConfigGamepadInputId.B,
                            ButtonB = isNintendoStyle ? ConfigGamepadInputId.B : ConfigGamepadInputId.A,
                            ButtonX = isNintendoStyle ? ConfigGamepadInputId.X : ConfigGamepadInputId.Y,
                            ButtonY = isNintendoStyle ? ConfigGamepadInputId.Y : ConfigGamepadInputId.X,
                            ButtonPlus = ConfigGamepadInputId.Plus,
                            ButtonR = ConfigGamepadInputId.RightShoulder,
                            ButtonZr = ConfigGamepadInputId.RightTrigger,
                            ButtonSl = ConfigGamepadInputId.Unbound,
                            ButtonSr = ConfigGamepadInputId.Unbound
                        },
                    RightJoyconStick = new JoyconConfigControllerStick<ConfigGamepadInputId, ConfigStickInputId>
                    {
                        Joystick = ConfigStickInputId.Right,
                        StickButton = ConfigGamepadInputId.RightStick,
                        InvertStickX = false,
                        InvertStickY = false
                    },
                    Motion = new StandardMotionConfigController
                    {
                        MotionBackend = MotionInputBackendType.GamepadDriver,
                        EnableMotion = true,
                        Sensitivity = 100,
                        GyroDeadzone = 1
                    }
                };
            }
            else
            {
                config = new InputConfig();
            }

            config.PlayerIndex = _playerId;

            return config;
        }

        public void LoadProfile()
        {
            if (Device == 0)
            {
                return;
            }

            InputConfig config = null;

            string activeProfile = Profiles.Keys.ToArray()[Profile];

            if (activeProfile == "default")
            {
                config = LoadDefaultConfiguration();
            }
            else
            {
                string path = Path.Combine(GetProfileBasePath(), activeProfile);

                if (!File.Exists(path))
                {
                    Profiles.Remove(activeProfile);

                    return;
                }

                try
                {
                    using (Stream stream = File.OpenRead(path))
                    {
                        config = JsonHelper.Deserialize<InputConfig>(stream);
                    }
                }
                catch (JsonException) { }
                catch (InvalidOperationException)
                {
                    ContentDialogHelper.CreateErrorDialog(_owner.GetVisualRoot() as StyleableWindow,
                        $"Profile {activeProfile} is incompatible with the current input configuration system.");
                    Logger.Error?.Print(LogClass.Configuration, $"Profile {activeProfile} is incompatible with the current input configuration system.");
                    
                    return;
                }
            }

            if (config != null)
            {
                LoadConfiguration(config);

                LoadDevice();

                NotifyChanges();
            }
        }

        public async void AddProfile()
        {
            if (Device == 0)
            {
                return;
            }

            if (InputConfig == null)
            {
                return;
            }

            string name = await ProfileDialog.ShowProfileDialog(_owner.GetVisualRoot() as StyleableWindow);

            if (!string.IsNullOrWhiteSpace(name))
            {
                string path = Path.Combine(GetProfileBasePath(), name);

                InputConfig config = null;

                if (IsKeyboard)
                {
                    config = (InputConfig as InputConfiguration<Key, ConfigStickInputId>).GetConfig();
                }
                else if (IsController)
                {
                    config = (InputConfig as InputConfiguration<GamepadInputId, ConfigStickInputId>).GetConfig();
                }

                string jsonString = JsonHelper.Serialize(config, true);

                await File.WriteAllTextAsync(path, jsonString);
            }

            LoadProfiles();

        }

        public async void SaveProfile()
        {
            if (Device == 0)
            {
                return;
            }

            if (InputConfig == null)
            {
                return;
            }

            string activeProfile = Profiles.Keys.ToArray()[Profile];

            if (activeProfile == "default")
            {
                ContentDialogHelper.CreateErrorDialog(_owner.GetVisualRoot() as StyleableWindow, "Default Profile can not be overwritten");

                return;
            }
            else
            {
                string path = Path.Combine(GetProfileBasePath(), activeProfile);

                InputConfig config = null;

                if (IsKeyboard)
                {
                    config = (InputConfig as InputConfiguration<Key, ConfigStickInputId>).GetConfig();
                }
                else if (IsController)
                {
                    config = (InputConfig as InputConfiguration<GamepadInputId, ConfigStickInputId>).GetConfig();
                }

                config.ControllerType = Controllers.Keys.ToArray()[_controller];

                string jsonString = JsonHelper.Serialize(config, true);

                await File.WriteAllTextAsync(path, jsonString);
            }
        }

        public async void RemoveProfile()
        {
            if (Device == 0 || Profile == 0)
            {
                return;
            }

            UserResult result = await ContentDialogHelper.CreateConfirmationDialog(_owner.GetVisualRoot() as StyleableWindow, "Deleting Profile", "This action is irreversible, are your sure you want to continue?");

            if (result == UserResult.Yes)
            {
                string activeProfile = Profiles.Keys.ToArray()[Profile];
                string path          = Path.Combine(GetProfileBasePath(), activeProfile);

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                LoadProfiles();
            }
        }

        public void Save()
        {
            IsModified = false;
            
            List<InputConfig> newConfig = new();

            newConfig.AddRange(ConfigurationState.Instance.Hid.InputConfig.Value);

            newConfig.Remove(newConfig.Find(x => x == null));

            if (Device == 0)
            {
                newConfig.Remove(newConfig.Find(x => x.PlayerIndex == this.PlayerId));
            }
            else
            {
                string selected = Devices.Keys.ToArray()[Device];

                if (selected.StartsWith("keyboard"))
                {
                    var inputConfig = InputConfig as InputConfiguration<Key, ConfigStickInputId>;
                    inputConfig.Id = selected.Split("/")[1];
                }
                else
                {
                    var inputConfig = InputConfig as InputConfiguration<GamepadInputId, ConfigStickInputId>;
                    inputConfig.Id = selected.Split("/")[1].Split(" ")[0];
                }

                var config = !IsController
                    ? (InputConfig as InputConfiguration<Key, ConfigStickInputId>).GetConfig()
                    : (InputConfig as InputConfiguration<GamepadInputId, ConfigStickInputId>).GetConfig();
                config.ControllerType = Controllers.Keys.ToArray()[_controller];
                config.PlayerIndex = _playerId;

                int i = newConfig.FindIndex(x => x.PlayerIndex == this.PlayerId);
                if (i == -1)
                {
                    newConfig.Add(config);
                }
                else
                {
                    newConfig[i] = config;
                }
            }

            if (_mainWindow.AppHost != null)
            {
                _mainWindow.AppHost.NpadManager.ReloadConfiguration(newConfig, ConfigurationState.Instance.Hid.EnableKeyboard, ConfigurationState.Instance.Hid.EnableMouse);
            }
            
            // Atomically replace and signal input change.
            // NOTE: Do not modify InputConfig.Value directly as other code depends on the on-change event.
            ConfigurationState.Instance.Hid.InputConfig.Value = newConfig;

            ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);
        }

        public void NotifyChange(string property)
        {
            OnPropertyChanged(property);
        }

        public void NotifyChanges()
        {
            OnPropertyChanged(nameof(InputConfig));
            OnPropertyChanged(nameof(IsController));
            OnPropertyChanged(nameof(ShowSettings));
            OnPropertyChanged(nameof(IsKeyboard));
            OnPropertyChanged(nameof(IsRight));
            OnPropertyChanged(nameof(IsLeft));
        }

        public void Dispose()
        {
            _mainWindow.InputManager.GamepadDriver.OnGamepadConnected    -= HandleOnGamepadConnected;
            _mainWindow.InputManager.GamepadDriver.OnGamepadDisconnected -= HandleOnGamepadDisconnected;

            _mainWindow.AppHost?.NpadManager.UnblockInputUpdates();

            SelectedGamepad?.Dispose();

            AvaloniaKeyboardDriver.Dispose();
        }
    }
}