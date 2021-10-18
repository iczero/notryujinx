using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Ryujinx.Ava.Ui.Applet
{
    public class OffscreenTextBox : TextBox
    {
        public RoutedEvent<KeyEventArgs> GetKeyDownRoutedEvent()
        {
            return KeyDownEvent;
        }
        
        public RoutedEvent<KeyEventArgs> GetKeyUpRoutedEvent()
        {
            return KeyUpEvent;
        }
    }
}