using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System;

namespace Ryujinx.Ava.Ui.Controls
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

        public void SendKeyDownEvent(KeyEventArgs keyEvent)
        {
            OnKeyDown(keyEvent);
            
            SendKey(keyEvent);
        }

        public void SendKeyUpEvent(KeyEventArgs keyEvent)
        {
            OnKeyUp(keyEvent);
        }

        public void SendKey(KeyEventArgs keyEvent)
        {
            string keyText = keyEvent.Key switch
            {
                Key.Space => " ",
                Key.Tab => "\t",
                _ => keyEvent.Key.ToString()
            };
            if (keyText.Length == 1)
            {
                InputManager.Instance.ProcessInput(new RawTextInputEventArgs(
                        KeyboardDevice.Instance, 
                        (ulong)DateTime.Now.Ticks,
                        (Window)this.GetVisualRoot(),
                        keyText.ToLower()
                    ));
            }
        }
    }
}