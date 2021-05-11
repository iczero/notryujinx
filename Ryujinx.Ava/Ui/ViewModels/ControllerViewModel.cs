using Avalonia.Controls;
using Avalonia.Svg.Skia;
using DynamicData;
using IX.Observable;
using MessageBoxSlim.Avalonia;
using Ryujinx.Ava.Ui.Controls;
using Ryujinx.Ava.Ui.Models;
using Ryujinx.Ava.Ui.Windows;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Configuration.Hid.Controller;
using Ryujinx.Common.Configuration.Hid.Controller.Motion;
using Ryujinx.Common.Configuration.Hid.Keyboard;
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
using StickInputId = Ryujinx.Input.StickInputId;

namespace Ryujinx.Ava.Ui.ViewModels
{
    public class ControllerSettingsViewModel : BaseModel, IDisposable
    {
        private readonly MainWindow _mainWindow;
        private readonly Window _owner;
        private readonly PlayerIndex _player;
        private int _controller;
        private string _controllerImage;
        private int _device;
        private bool _loaded;
        private int _profile;

        public ControllerSettingsViewModel(PlayerIndex player, Window owner, MainWindow mainWindow) : this()
        {
            _player = player;
            _owner = owner;
            _mainWindow = mainWindow;

            AvaloniaKeyboardDriver = new AvaloniaKeyboardDriver(owner);

            mainWindow.Manager.GamepadDriver.OnGamepadConnected += HandleOnGamepadConnected;
            mainWindow.Manager.GamepadDriver.OnGamepadDisconnected += HandleOnGamepadDisconnected;

            if (_mainWindow.AppHost != null)
            {
                _mainWindow.AppHost.NpadManager.BlockInputUpdates();
            }

            Initialize();
        }

        public ControllerSettingsViewModel()
        {
            ControllerTypes = new ObservableDictionary<ControllerType, string>();
            Devices = new ObservableDictionary<string, string>();
            Profiles = new ObservableDictionary<string, string>();

            ControllerImage = "Ryujinx.Ava.Assets.Images.Controller_ProCon.svg";
        }

        public string Title => $"Ryujinx - Controller Settings - {_player}";

        public IGamepadDriver AvaloniaKeyboardDriver { get; }
        public IGamepad SelectedGamepad { get; private set; }

        // Flags
        public bool ShowSettings => _device > 0;
        public bool IsController => _device > 1;
        public bool IsKeyboard => !IsController;
        public bool IsRight { get; set; }
        public bool IsLeft { get; set; }

        private object _inputConfiguration;
        
        public ObservableDictionary<ControllerType, string> ControllerTypes { get; set; }
        public ObservableDictionary<string, string> Devices { get; set; }
        public ObservableDictionary<string, string> Profiles { get; set; }

        public bool IsCemuHookMotionController
        {
            get
            {
                if (!IsController)
                {
                    return false;
                }

                return (InputConfig as InputConfiguration<ConfigGamepadInputId, StickInputId>).MotionBackend == MotionInputBackendType.CemuHook;
            }

            set
            {
                var config = InputConfig as InputConfiguration<ConfigGamepadInputId, StickInputId>;
                config.MotionBackend = value ? MotionInputBackendType.CemuHook : MotionInputBackendType.GamepadDriver;
                
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

                if (ControllerTypes.Count > 0 && value < ControllerTypes.Count && value > -1)
                {
                    string controller = ControllerTypes.Values.ToArray()[value];

                    IsLeft = false;
                    IsRight = false;

                    switch (controller)
                    {
                        case "Handheld":
                            ControllerImage = "Ryujinx.Ava.Assets.Images.Controller_JoyConPair.svg";
                            break;
                        case "Pro Controller":
                            ControllerImage = "Ryujinx.Ava.Assets.Images.Controller_ProCon.svg";
                            break;
                        case "Joycon Pair":
                            ControllerImage = "Ryujinx.Ava.Assets.Images.Controller_JoyConPair.svg";
                            break;
                        case "Joycon Left":
                            ControllerImage = "Ryujinx.Ava.Assets.Images.Controller_JoyConLeft.svg";
                            IsLeft = true;
                            break;
                        case "Joycon Right":
                            ControllerImage = "Ryujinx.Ava.Assets.Images.Controller_JoyConRight.svg";
                            IsRight = true;
                            break;
                    }
                }

                OnPropertyChanged();
                NotifyChanges();
            }
        }

        public int Device
        {
            get => _device;
            set
            {
                _device = value;

                if (_device < 0)
                {
                    return;
                }

                string selected = Devices.Keys.ToArray()[Device];

                if (selected == "disable")
                {
                    return;
                }

                if (_loaded)
                {
                    InputConfig config = null;
                    try
                    {
                        config =
                            (InputConfig)ConfigurationState.Instance.Hid.InputConfig.Value.Find(inputConfig =>
                                inputConfig.PlayerIndex == _player);
                        
                        if (config is StandardControllerInputConfig)
                        {
                            InputConfig = new InputConfiguration<GamepadButtonInputId, StickInputId>(config);
                        }
                        else
                        {
                            InputConfig = new InputConfiguration<Key, StickInputId>(config);
                        }
                    }
                    catch { }

                    if (config == null
                        || (selected.StartsWith("keyboard") && config is StandardControllerInputConfig)
                        || (selected.StartsWith("controller") && config is StandardKeyboardInputConfig))
                    {
                        config = LoadDefault();

                        if (IsController)
                        {
                            InputConfig = new InputConfiguration<GamepadInputId, StickInputId>(config);
                        }
                        else
                        {
                            InputConfig = new InputConfiguration<Key, StickInputId>(config);
                        }
                    }

                    LoadInputDriver();

                    SetControllerType();
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
                SvgImage image = new();

                if (!string.IsNullOrWhiteSpace(_controllerImage))
                {
                    SvgSource source = new();

                    source.Load(Assembly.GetAssembly(typeof(ControllerSettingsViewModel))
                        .GetManifestResourceStream(_controllerImage));

                    image.Source = source;
                }

                return image;
            }
        }

        public object InputConfig
        {
            get => _inputConfiguration;
            set
            {
                _inputConfiguration = value;
                
                OnPropertyChanged();
            }
        }

        public void Dispose()
        {
            _mainWindow.Manager.GamepadDriver.OnGamepadConnected -= HandleOnGamepadConnected;
            _mainWindow.Manager.GamepadDriver.OnGamepadDisconnected -= HandleOnGamepadDisconnected;

            _mainWindow.AppHost?.NpadManager.UnblockInputUpdates();

            SelectedGamepad?.Dispose();

            AvaloniaKeyboardDriver.Dispose();
        }

        public event EventHandler OnClose;

        private void LoadInputDriver()
        {
            if (_device < 0)
            {
                return;
            }

            string id = GetCurrentGamepadId();
            string selected = Devices.Keys.ToArray()[Device];

            if (selected == "disabled")
            {
                return;
            }

            if (selected.StartsWith("keyboard"))
            {
                if (_mainWindow.Manager.KeyboardDriver is AvaloniaKeyboardDriver)
                {
                    // NOTE: To get input in this window, we need to bind a custom keyboard driver instead of using the InputManager one as the main window isn't focused...
                    SelectedGamepad = AvaloniaKeyboardDriver.GetGamepad(id);
                }
                else
                {
                    SelectedGamepad = _mainWindow.Manager.KeyboardDriver.GetGamepad(id);
                }
            }
            else
            {
                SelectedGamepad = _mainWindow.Manager.GamepadDriver.GetGamepad(id);
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

        public void Initialize()
        {
            SelectedGamepad = null;

            InputConfig config = null;
            
            try
            {
                config = (InputConfig)ConfigurationState.Instance.Hid.InputConfig.Value
                    .Find(inputConfig => inputConfig.PlayerIndex == _player);
            }
            catch { }

            if (config == null)
            {
                config = LoadDefault();
            }
            
            if (config is StandardControllerInputConfig)
            {
                InputConfig = new InputConfiguration<GamepadButtonInputId, StickInputId>(config);
            }
            else
            {
                InputConfig = new InputConfiguration<Key, StickInputId>(config);
            }

            LoadDevices();
            SetProfiles();
            SetControllerType();

            _loaded = true;

            LoadInputDriver();
        }

        private string GetCurrentGamepadId()
        {
            if (_device < 0)
            {
                return String.Empty;
            }

            string selected = Devices.Keys.ToArray()[Device];
            if (selected == null || selected == "disabled")
            {
                return null;
            }

            return selected.Split("/")[1].Split(" ")[0];
        }

        public void SetControllerType()
        {
            ControllerTypes.Clear();
            if (_player == PlayerIndex.Handheld)
            {
                ControllerTypes.Add(ControllerType.Handheld, "Handheld");
                Controller = 0;
            }
            else
            {
                ControllerTypes.Add(ControllerType.ProController, "Pro Controller");
                ControllerTypes.Add(ControllerType.JoyconPair, "Joycon Pair");
                ControllerTypes.Add(ControllerType.JoyconLeft, "Joycon Left");
                ControllerTypes.Add(ControllerType.JoyconRight, "Joycon Right");
                
                var key = IsController ? (InputConfig as InputConfiguration<GamepadInputId, StickInputId>).ControllerType : (InputConfig as InputConfiguration<Key, StickInputId>).ControllerType;

                if (ControllerTypes.ContainsKey(key))
                {
                    Controller = ControllerTypes.Keys.IndexOf(key);
                }
            }
        }

        private static string GetShrinkedGamepadName(string str)
        {
            const string shrinkChars = "..";
            const int maxSize = 52;

            if (str.Length > maxSize - shrinkChars.Length)
            {
                return str.Substring(0, maxSize) + shrinkChars;
            }

            return str;
        }

        public void LoadDevices()
        {
            Devices.Clear();
            Devices.Add("disabled", "Disabled");

            foreach (string id in _mainWindow.Manager.KeyboardDriver.GamepadsIds)
            {
                IGamepad gamepad = _mainWindow.Manager.KeyboardDriver.GetGamepad(id);

                if (gamepad != null)
                {
                    Devices.Add($"keyboard/{id}", GetShrinkedGamepadName($"{gamepad.Name} ({id})"));

                    gamepad.Dispose();
                }
            }

            foreach (string id in _mainWindow.Manager.GamepadDriver.GamepadsIds)
            {
                IGamepad gamepad = _mainWindow.Manager.GamepadDriver.GetGamepad(id);

                if (gamepad != null)
                {
                    Devices.Add($"controller/{id}", GetShrinkedGamepadName($"{gamepad.Name} ({id})"));

                    gamepad.Dispose();
                }
            }

            KeyValuePair<string, string> item;
            switch (InputConfig)
            {
                case InputConfiguration<Key, StickInputId> keyboard:
                    item = Devices.FirstOrDefault(x => x.Key == $"keyboard/{keyboard.Id}");
                    if (item.Key != null)
                    {
                        Device = Devices.IndexOf(item);
                    }
                    else
                    {
                        Device = -1;
                    }

                    break;
                case InputConfiguration<GamepadInputId, StickInputId> controller:
                    item = Devices.FirstOrDefault(x => x.Key == $"controller/{controller.Id}");
                    if (item.Key != null)
                    {
                        Device = Devices.IndexOf(item);
                    }
                    else
                    {
                        Device = -1;
                    }

                    break;
            }
        }

        private string GetProfileBasePath()
        {
            string path = AppDataManager.ProfilesDirPath;

            string selected = Devices.Keys.ToArray()[Device];

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


        private void SetProfiles()
        {
            Profiles.Clear();

            string basePath = GetProfileBasePath();

            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            Profiles.Add("default", "Default");

            foreach (string profile in Directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories))
            {
                Profiles.Add(Path.GetFileName(profile), Path.GetFileNameWithoutExtension(profile));
            }

            Profile = 0;

            OnPropertyChanged(nameof(Profiles));
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

        public InputConfig LoadDefault()
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
                    ControllerType = ControllerType.JoyconPair,
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
                bool isNintendoStyle = activeDevice.Contains("Nintendo");
                
                string id = activeDevice.Split("/")[1].Split(" ")[0];

                config = new StandardControllerInputConfig
                {
                    Version = Ryujinx.Common.Configuration.Hid.InputConfig.CurrentVersion,
                    Backend = InputBackendType.GamepadSDL2,
                    Id = id,
                    ControllerType = ControllerType.JoyconPair,
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

            config.PlayerIndex = _player;

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
                config = LoadDefault();
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
            }

            if (config != null)
            {
                List<InputConfig> newConfig = new();

                newConfig.AddRange(ConfigurationState.Instance.Hid.InputConfig.Value);

                if (newConfig.FindIndex(x => x.PlayerIndex == _player) > -1)
                {
                    newConfig.Remove(newConfig.Find(x => x.PlayerIndex == _player));
                }

                newConfig.Add(config);

                ConfigurationState.Instance.Hid.InputConfig.Value = newConfig;

                Initialize();

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

            ProfileWindow profileDialog = new();

            await profileDialog.ShowDialog(_owner);

            if (profileDialog.IsOkPressed)
            {
                string path = Path.Combine(GetProfileBasePath(), profileDialog.FileName);

                Ryujinx.Common.Configuration.Hid.InputConfig config = null;

                if (IsKeyboard)
                {
                    config = (InputConfig as InputConfiguration<Key, StickInputId>).GetConfig();
                }
                else if (IsController)
                {
                    config = (InputConfig as InputConfiguration<GamepadInputId, StickInputId>).GetConfig();
                }

                string jsonString = JsonHelper.Serialize(config, true);

                File.WriteAllText(path, jsonString);
            }

            SetProfiles();
        }

        public async void RemoveProfile()
        {
            if (Device == 0 || Profile == 0)
            {
                return;
            }

            AvaDialog messageDialog = AvaDialog.CreateConfirmationDialog("Deleting Profile",
                "This action is irreversible, are your sure you want to continue?", _owner);

            UserResult result = await messageDialog.Run();

            if (result == UserResult.Yes)
            {
                string activeProfile = Profiles.Keys.ToArray()[Profile];

                string path = Path.Combine(GetProfileBasePath(), activeProfile);

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                SetProfiles();
            }
        }

        public void Save()
        {
            List<InputConfig> newConfig = new();

            newConfig.AddRange(ConfigurationState.Instance.Hid.InputConfig.Value);

            if (Device == 0)
            {
                newConfig.Remove(newConfig.Find(x => x.PlayerIndex == _player));
            }
            else
            {
                string selected = Devices.Keys.ToArray()[Device];

                if (selected.StartsWith("keyboard"))
                {
                    var inputConfig = InputConfig as InputConfiguration<Key, StickInputId>;
                    inputConfig.Id = selected.Split("/")[1];
                }
                else
                {
                    var inputConfig = InputConfig as InputConfiguration<GamepadInputId, StickInputId>;
                    inputConfig.Id = selected.Split("/")[1].Split(" ")[0];
                }
            }
            
           var config = !IsController ? (InputConfig as InputConfiguration<Key, StickInputId>).GetConfig() : (InputConfig as InputConfiguration<GamepadInputId, StickInputId>).GetConfig();

            int i = newConfig.FindIndex(x => x.PlayerIndex == _player);
            if (i == -1)
            {
                newConfig.Add(config);
            }
            else
            {
                newConfig[i] = config;
            }

            if (_mainWindow.AppHost != null)
            {
                _mainWindow.AppHost.NpadManager.ReloadConfiguration(newConfig);
            }

            // Atomically replace and signal input change.
            // NOTE: Do not modify InputConfig.Value directly as other code depends on the on-change event.
            ConfigurationState.Instance.Hid.InputConfig.Value = newConfig;

            ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);

            Close();
        }

        public void Close()
        {
            OnClose?.Invoke(this, EventArgs.Empty);
        }

        public void NotifyChange(string property)
        {
            OnPropertyChanged(property);
        }
    }
}