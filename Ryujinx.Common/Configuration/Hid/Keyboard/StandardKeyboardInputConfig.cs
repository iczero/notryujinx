namespace Ryujinx.Common.Configuration.Hid.Keyboard
{
    public class StandardKeyboardInputConfig : GenericKeyboardInputConfig<Key>
    {
        public override object CreateCopy()
         {
             var config = new StandardKeyboardInputConfig()
             {
                 Version = Version,
                 Id = Id,
                 Backend = Backend,
                 ControllerType = ControllerType,
                 PlayerIndex = PlayerIndex,
                 LeftJoycon =
                     new LeftJoyconCommonConfig<Key>()
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
                     new RightJoyconCommonConfig<Key>()
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
                 LeftJoyconStick = new JoyconConfigKeyboardStick<Key>()
                 {
                     StickButton = LeftJoyconStick.StickButton,
                     StickUp = LeftJoyconStick.StickUp,
                     StickDown = LeftJoyconStick.StickDown,
                     StickLeft = LeftJoyconStick.StickLeft,
                     StickRight = LeftJoyconStick.StickRight
                 },
                 RightJoyconStick = new JoyconConfigKeyboardStick<Key>()
                 {
                     StickButton = RightJoyconStick.StickButton,
                     StickUp = RightJoyconStick.StickUp,
                     StickDown = RightJoyconStick.StickDown,
                     StickLeft = RightJoyconStick.StickLeft,
                     StickRight = RightJoyconStick.StickRight
                 }
             };

            return config;
        }
    }
}
