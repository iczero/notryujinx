using Ryujinx.Common.Configuration.Hid.Controller.Motion;

namespace Ryujinx.Common.Configuration.Hid.Controller
{
    public class StandardControllerInputConfig : GenericControllerInputConfig<GamepadInputId, StickInputId>
    {
        public override object CreateCopy()
        {
            var config = new StandardControllerInputConfig()
            {
                Version = Version,
                Backend = Backend,
                Id = Id,
                ControllerType = ControllerType,
                PlayerIndex = PlayerIndex,
                LeftJoycon =
                    new LeftJoyconCommonConfig<GamepadInputId>()
                    {
                        ButtonL = LeftJoycon.ButtonL,
                        ButtonSl = LeftJoycon.ButtonSl,
                        ButtonMinus = LeftJoycon.ButtonMinus,
                        ButtonSr = LeftJoycon.ButtonSr,
                        ButtonZl = LeftJoycon.ButtonZl,
                        DpadUp = LeftJoycon.DpadUp,
                        DpadDown = LeftJoycon.DpadDown,
                        DpadLeft = LeftJoycon.DpadLeft,
                        DpadRight = LeftJoycon.DpadRight
                    },
                RightJoycon =
                    new RightJoyconCommonConfig<GamepadInputId>()
                    {
                        ButtonSl = RightJoycon.ButtonSl,
                        ButtonSr = RightJoycon.ButtonSr,
                        ButtonA = RightJoycon.ButtonA,
                        ButtonB = RightJoycon.ButtonB,
                        ButtonPlus = RightJoycon.ButtonPlus,
                        ButtonR = RightJoycon.ButtonR,
                        ButtonX = RightJoycon.ButtonX,
                        ButtonY = RightJoycon.ButtonY,
                        ButtonZr = RightJoycon.ButtonZr
                    },
                LeftJoyconStick =
                    new JoyconConfigControllerStick<GamepadInputId, StickInputId>()
                    {
                        InvertStickX = LeftJoyconStick.InvertStickX,
                        InvertStickY = LeftJoyconStick.InvertStickY,
                        Joystick = LeftJoyconStick.Joystick,
                        StickButton = LeftJoyconStick.StickButton
                    },
                RightJoyconStick = new JoyconConfigControllerStick<GamepadInputId, StickInputId>()
                {
                    InvertStickX = RightJoyconStick.InvertStickX,
                    InvertStickY = RightJoyconStick.InvertStickY,
                    Joystick = RightJoyconStick.Joystick,
                    StickButton = RightJoyconStick.StickButton
                },
                DeadzoneLeft = DeadzoneLeft,
                DeadzoneRight = DeadzoneRight,
                TriggerThreshold = TriggerThreshold
            };

            switch (Motion.MotionBackend)
            {
                case MotionInputBackendType.CemuHook:
                    var motion = Motion as CemuHookMotionConfigController;
                    config.Motion = new CemuHookMotionConfigController()
                    {
                        EnableMotion = Motion.EnableMotion,
                        GyroDeadzone = Motion.GyroDeadzone,
                        Sensitivity = Motion.Sensitivity,
                        MotionBackend = MotionInputBackendType.CemuHook,
                        MirrorInput = motion.MirrorInput,
                        AltSlot = motion.AltSlot,
                        DsuServerHost = motion.DsuServerHost,
                        DsuServerPort = motion.DsuServerPort,
                        Slot = motion.Slot
                    };
                    break;
                default:
                    config.Motion = new StandardMotionConfigController()
                    {
                        EnableMotion = Motion.EnableMotion,
                        GyroDeadzone = Motion.GyroDeadzone,
                        Sensitivity = Motion.Sensitivity,
                        MotionBackend = MotionInputBackendType.GamepadDriver,
                    };
                    break;
            }

            return config;
        }
    }
}
