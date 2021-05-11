using Ryujinx.Ava.Ui.ViewModels;
using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Configuration.Hid.Controller;
using Ryujinx.Common.Configuration.Hid.Controller.Motion;
using Ryujinx.Common.Configuration.Hid.Keyboard;
using System;

namespace Ryujinx.Ava.Ui.Models
{
    public class InputConfiguration<Key, Stick> : BaseModel where Key : unmanaged where Stick : unmanaged
    {
        private float _deadzoneRight;
        private float _triggerThreshold;
        private float _deadzoneLeft;
        private double _gyroDeadzone;
        
        public InputBackendType Backend { get; set; }

        /// <summary>
        /// Controller id
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        ///  Controller's Type
        /// </summary>
        public ControllerType ControllerType { get; set; }

        /// <summary>
        ///  Player's Index for the controller
        /// </summary>
        public PlayerIndex PlayerIndex { get; set; }
        
        
        public Stick LeftJoystick { get; set; }
        public bool LeftInvertStickX { get; set; }
        public bool LeftInvertStickY { get; set; }
        public Key LeftControllerStickButton { get; set; }
        
        
        public Stick RightJoystick { get; set; }
        public bool RightInvertStickX { get; set; }
        public bool RightInvertStickY { get; set; }
        public Key RightControllerStickButton { get; set; }
        
        public float DeadzoneLeft
        {
            get => _deadzoneLeft; set
            {
                _deadzoneLeft = MathF.Round(value, 3);
                OnPropertyChanged();
            }
        }

        public float DeadzoneRight
        {
            get => _deadzoneRight; set
            {
                _deadzoneRight = MathF.Round(value, 3);
                OnPropertyChanged();
            }
        }

        public float TriggerThreshold
        {
            get => _triggerThreshold; set
            {
                _triggerThreshold = MathF.Round(value, 3);
                OnPropertyChanged();
            }
        }
        
        public MotionInputBackendType MotionBackend { get; set; }

        public Key ButtonMinus { get; set; }
        public Key ButtonL { get; set; }
        public Key ButtonZl { get; set; }
        public Key LeftButtonSl { get; set; }
        public Key LeftButtonSr { get; set; }
        public Key DpadUp { get; set; }
        public Key DpadDown { get; set; }
        public Key DpadLeft { get; set; }
        public Key DpadRight { get; set; }
        
        public Key ButtonPlus { get; set; }
        public Key ButtonR { get; set; }
        public Key ButtonZr { get; set; }
        public Key RightButtonSl { get; set; }
        public Key RightButtonSr { get; set; }
        public Key ButtonX { get; set; }
        public Key ButtonB { get; set; }
        public Key ButtonY { get; set; }
        public Key ButtonA { get; set; }

        
        public Key LeftStickUp { get; set; }
        public Key LeftStickDown { get; set; }
        public Key LeftStickLeft { get; set; }
        public Key LeftStickRight { get; set; }
        public Key LeftKeyboardStickButton { get; set; }

        public Key RightStickUp { get; set; }
        public Key RightStickDown { get; set; }
        public Key RightStickLeft { get; set; }
        public Key RightStickRight { get; set; }
        public Key RightKeyboardStickButton { get; set; }
        
        public int Sensitivity { get; set; }

        public double GyroDeadzone
        {
            get => _gyroDeadzone; set
            {
                _gyroDeadzone = Math.Round(value, 3);
                OnPropertyChanged();
            }
        }
        
        public bool EnableMotion { get; set; }

        public int Slot { get; set; }

        public int AltSlot { get; set; }

        public bool MirrorInput { get; set; }

        public string DsuServerHost { get; set; }

        public int DsuServerPort { get; set; }

        public InputConfiguration(InputConfig config)
        {
            if (config != null)
            {
                Backend = config.Backend;
                Id = config.Id;
                ControllerType = config.ControllerType;
                PlayerIndex = config.PlayerIndex;

                if (config is StandardKeyboardInputConfig keyboardConfig)
                {
                    LeftStickUp = (Key)(Object)keyboardConfig.LeftJoyconStick.StickUp;
                    LeftStickDown = (Key)(Object)keyboardConfig.LeftJoyconStick.StickDown;
                    LeftStickLeft = (Key)(Object)keyboardConfig.LeftJoyconStick.StickLeft;
                    LeftStickRight = (Key)(Object)keyboardConfig.LeftJoyconStick.StickRight;
                    LeftKeyboardStickButton = (Key)(Object)keyboardConfig.LeftJoyconStick.StickButton;
                    
                    RightStickUp = (Key)(Object)keyboardConfig.RightJoyconStick.StickUp;
                    RightStickDown = (Key)(Object)keyboardConfig.RightJoyconStick.StickDown;
                    RightStickLeft = (Key)(Object)keyboardConfig.RightJoyconStick.StickLeft;
                    RightStickRight = (Key)(Object)keyboardConfig.RightJoyconStick.StickRight;
                    RightKeyboardStickButton = (Key)(Object)keyboardConfig.RightJoyconStick.StickButton;

                    ButtonA = (Key)(Object)keyboardConfig.RightJoycon.ButtonA;
                    ButtonB = (Key)(Object)keyboardConfig.RightJoycon.ButtonB;
                    ButtonX = (Key)(Object)keyboardConfig.RightJoycon.ButtonX;
                    ButtonY = (Key)(Object)keyboardConfig.RightJoycon.ButtonY;
                    ButtonR = (Key)(Object)keyboardConfig.RightJoycon.ButtonR;
                    RightButtonSl = (Key)(Object)keyboardConfig.RightJoycon.ButtonSl;
                    RightButtonSr = (Key)(Object)keyboardConfig.RightJoycon.ButtonSr;
                    ButtonZr = (Key)(Object)keyboardConfig.RightJoycon.ButtonZr;
                    ButtonPlus = (Key)(Object)keyboardConfig.RightJoycon.ButtonPlus;

                    DpadUp = (Key)(Object)keyboardConfig.LeftJoycon.DpadUp;
                    DpadDown = (Key)(Object)keyboardConfig.LeftJoycon.DpadDown;
                    DpadLeft = (Key)(Object)keyboardConfig.LeftJoycon.DpadLeft;
                    DpadRight = (Key)(Object)keyboardConfig.LeftJoycon.DpadRight;
                    ButtonMinus = (Key)(Object)keyboardConfig.LeftJoycon.ButtonMinus;
                    LeftButtonSl = (Key)(Object)keyboardConfig.LeftJoycon.ButtonSl;
                    LeftButtonSr = (Key)(Object)keyboardConfig.LeftJoycon.ButtonSr;
                    ButtonZl = (Key)(Object)keyboardConfig.LeftJoycon.ButtonZl;
                    ButtonL = (Key)(Object)keyboardConfig.LeftJoycon.ButtonL;
                }
                else if (config is StandardControllerInputConfig controllerConfig)
                {
                    LeftJoystick = (Stick)(Object)controllerConfig.LeftJoyconStick.Joystick;
                    LeftInvertStickX = controllerConfig.LeftJoyconStick.InvertStickX;
                    LeftInvertStickY = controllerConfig.LeftJoyconStick.InvertStickY;
                    LeftControllerStickButton = (Key)(Object)controllerConfig.LeftJoyconStick.StickButton;
                    
                    RightJoystick = (Stick)(Object)controllerConfig.RightJoyconStick.Joystick;
                    RightInvertStickX = controllerConfig.RightJoyconStick.InvertStickX;
                    RightInvertStickY = controllerConfig.RightJoyconStick.InvertStickY;
                    RightControllerStickButton = (Key)(Object)controllerConfig.RightJoyconStick.StickButton;

                    ButtonA = (Key)(Object)controllerConfig.RightJoycon.ButtonA;
                    ButtonB = (Key)(Object)controllerConfig.RightJoycon.ButtonB;
                    ButtonX = (Key)(Object)controllerConfig.RightJoycon.ButtonX;
                    ButtonY = (Key)(Object)controllerConfig.RightJoycon.ButtonY;
                    ButtonR = (Key)(Object)controllerConfig.RightJoycon.ButtonR;
                    RightButtonSl = (Key)(Object)controllerConfig.RightJoycon.ButtonSl;
                    RightButtonSr = (Key)(Object)controllerConfig.RightJoycon.ButtonSr;
                    ButtonZr = (Key)(Object)controllerConfig.RightJoycon.ButtonZr;
                    ButtonPlus = (Key)(Object)controllerConfig.RightJoycon.ButtonPlus;

                    DpadUp = (Key)(Object)controllerConfig.LeftJoycon.DpadUp;
                    DpadDown = (Key)(Object)controllerConfig.LeftJoycon.DpadDown;
                    DpadLeft = (Key)(Object)controllerConfig.LeftJoycon.DpadLeft;
                    DpadRight = (Key)(Object)controllerConfig.LeftJoycon.DpadRight;
                    ButtonMinus = (Key)(Object)controllerConfig.LeftJoycon.ButtonMinus;
                    LeftButtonSl = (Key)(Object)controllerConfig.LeftJoycon.ButtonSl;
                    LeftButtonSr = (Key)(Object)controllerConfig.LeftJoycon.ButtonSr;
                    ButtonZl = (Key)(Object)controllerConfig.LeftJoycon.ButtonZl;
                    ButtonL = (Key)(Object)controllerConfig.LeftJoycon.ButtonL;

                    DeadzoneLeft = controllerConfig.DeadzoneLeft;
                    DeadzoneRight = controllerConfig.DeadzoneRight;
                    TriggerThreshold = controllerConfig.TriggerThreshold;

                    EnableMotion = controllerConfig.Motion.EnableMotion;
                    MotionBackend = controllerConfig.Motion.MotionBackend;
                    GyroDeadzone = controllerConfig.Motion.GyroDeadzone;
                    Sensitivity = controllerConfig.Motion.Sensitivity;

                    if (controllerConfig.Motion is CemuHookMotionConfigController cemuHook)
                    {
                        DsuServerHost = cemuHook.DsuServerHost;
                        DsuServerPort = cemuHook.DsuServerPort;
                        Slot = cemuHook.Slot;
                        AltSlot = cemuHook.AltSlot;
                        MirrorInput = cemuHook.MirrorInput;
                    }
                }
            }
        }

        public InputConfig GetConfig()
        {
            if (Backend == InputBackendType.WindowKeyboard)
            {
                return new StandardKeyboardInputConfig()
                {
                    Id =  Id,
                    Backend =  Backend,
                    PlayerIndex = PlayerIndex,
                    ControllerType =  ControllerType,
                    LeftJoycon =  new LeftJoyconCommonConfig<Ryujinx.Common.Configuration.Hid.Key>()
                    {
                        DpadUp = (Ryujinx.Common.Configuration.Hid.Key)(object)DpadUp,
                        DpadDown = (Ryujinx.Common.Configuration.Hid.Key)(object)DpadDown,
                        DpadLeft = (Ryujinx.Common.Configuration.Hid.Key)(object)DpadLeft,
                        DpadRight = (Ryujinx.Common.Configuration.Hid.Key)(object)DpadRight,
                        ButtonL = (Ryujinx.Common.Configuration.Hid.Key)(object)ButtonL,
                        ButtonZl  = (Ryujinx.Common.Configuration.Hid.Key)(object)ButtonZl,
                        ButtonSl =  (Ryujinx.Common.Configuration.Hid.Key)(object)LeftButtonSl,
                        ButtonSr =  (Ryujinx.Common.Configuration.Hid.Key)(object)LeftButtonSr,
                        ButtonMinus =  (Ryujinx.Common.Configuration.Hid.Key)(object)ButtonMinus,
                    },
                    RightJoycon = new RightJoyconCommonConfig<Ryujinx.Common.Configuration.Hid.Key>()
                    {
                        ButtonA = (Ryujinx.Common.Configuration.Hid.Key)(object)ButtonA,
                        ButtonB = (Ryujinx.Common.Configuration.Hid.Key)(object)ButtonB,
                        ButtonX = (Ryujinx.Common.Configuration.Hid.Key)(object)ButtonX,
                        ButtonY = (Ryujinx.Common.Configuration.Hid.Key)(object)ButtonY,
                        ButtonPlus = (Ryujinx.Common.Configuration.Hid.Key)(object)ButtonPlus,
                        ButtonSl = (Ryujinx.Common.Configuration.Hid.Key)(object)RightButtonSl,
                        ButtonSr = (Ryujinx.Common.Configuration.Hid.Key)(object)RightButtonSr,
                        ButtonR = (Ryujinx.Common.Configuration.Hid.Key)(object)ButtonR,
                        ButtonZr = (Ryujinx.Common.Configuration.Hid.Key)(object)ButtonZr,
                    },
                    LeftJoyconStick = new JoyconConfigKeyboardStick<Ryujinx.Common.Configuration.Hid.Key>()
                    {
                        StickUp = (Ryujinx.Common.Configuration.Hid.Key)(object)LeftStickUp,
                        StickDown = (Ryujinx.Common.Configuration.Hid.Key)(object)LeftStickDown,
                        StickRight = (Ryujinx.Common.Configuration.Hid.Key)(object)LeftStickRight,
                        StickLeft = (Ryujinx.Common.Configuration.Hid.Key)(object)LeftStickLeft,
                        StickButton = (Ryujinx.Common.Configuration.Hid.Key)(object)LeftKeyboardStickButton,
                    },
                    RightJoyconStick = new JoyconConfigKeyboardStick<Ryujinx.Common.Configuration.Hid.Key>()
                    {
                        StickUp = (Ryujinx.Common.Configuration.Hid.Key)(object)RightStickUp,
                        StickDown = (Ryujinx.Common.Configuration.Hid.Key)(object)RightStickDown,
                        StickLeft = (Ryujinx.Common.Configuration.Hid.Key)(object)RightStickLeft,
                        StickRight = (Ryujinx.Common.Configuration.Hid.Key)(object)RightStickRight,
                        StickButton = (Ryujinx.Common.Configuration.Hid.Key)(object)RightKeyboardStickButton,
                    },
                    Version = InputConfig.CurrentVersion,
                };

            }
            else if (Backend == InputBackendType.GamepadSDL2)
            {
                var config = new StandardControllerInputConfig()
                {
                    Id =  Id,
                    Backend =  Backend,
                    PlayerIndex = PlayerIndex,
                    ControllerType =  ControllerType,
                    LeftJoycon =  new LeftJoyconCommonConfig<GamepadInputId>()
                    {
                        DpadUp = (GamepadInputId)(object)DpadUp,
                        DpadDown = (GamepadInputId)(object)DpadDown,
                        DpadLeft = (GamepadInputId)(object)DpadLeft,
                        DpadRight = (GamepadInputId)(object)DpadRight,
                        ButtonL = (GamepadInputId)(object)ButtonL,
                        ButtonZl  = (GamepadInputId)(object)ButtonZl,
                        ButtonSl =  (GamepadInputId)(object)LeftButtonSl,
                        ButtonSr =  (GamepadInputId)(object)LeftButtonSr,
                        ButtonMinus =  (GamepadInputId)(object)ButtonMinus,
                    },
                    RightJoycon = new RightJoyconCommonConfig<GamepadInputId>()
                    {
                        ButtonA = (GamepadInputId)(object)ButtonA,
                        ButtonB = (GamepadInputId)(object)ButtonB,
                        ButtonX = (GamepadInputId)(object)ButtonX,
                        ButtonY = (GamepadInputId)(object)ButtonY,
                        ButtonPlus = (GamepadInputId)(object)ButtonPlus,
                        ButtonSl = (GamepadInputId)(object)RightButtonSl,
                        ButtonSr = (GamepadInputId)(object)RightButtonSr,
                        ButtonR = (GamepadInputId)(object)ButtonR,
                        ButtonZr = (GamepadInputId)(object)ButtonZr,
                    },
                    LeftJoyconStick = new JoyconConfigControllerStick<GamepadInputId, StickInputId>()
                    {
                        Joystick = (StickInputId)(object)LeftJoystick,
                        InvertStickX = LeftInvertStickX,
                        InvertStickY = LeftInvertStickY,
                        StickButton = (GamepadInputId)(object)LeftControllerStickButton,
                    },
                    RightJoyconStick = new JoyconConfigControllerStick<GamepadInputId, StickInputId>()
                    {
                        Joystick = (StickInputId)(object)RightJoystick,
                        InvertStickX = RightInvertStickX,
                        InvertStickY = RightInvertStickY,
                        StickButton = (GamepadInputId)(object)RightControllerStickButton,
                    },
                    Version = InputConfig.CurrentVersion,
                    DeadzoneLeft = DeadzoneLeft,
                    DeadzoneRight = DeadzoneRight,
                    TriggerThreshold = TriggerThreshold,
                    Motion = MotionBackend == MotionInputBackendType.CemuHook? new CemuHookMotionConfigController()
                        {
                            DsuServerHost = DsuServerHost,
                            DsuServerPort = DsuServerPort,
                            Slot = Slot,
                            AltSlot = AltSlot,
                            MirrorInput = MirrorInput,
                        }
                    :
                    new StandardMotionConfigController()
                };

                config.Motion.MotionBackend = MotionBackend;
                config.Motion.Sensitivity = Sensitivity;
                config.Motion.EnableMotion = EnableMotion;
                config.Motion.GyroDeadzone = GyroDeadzone;
            }

            return null;
        }
    }
}