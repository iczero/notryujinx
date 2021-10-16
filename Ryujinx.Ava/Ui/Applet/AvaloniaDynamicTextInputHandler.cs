using Avalonia.Controls;
using Avalonia.Threading;
using Ryujinx.Ava.Ui.Windows;
using Ryujinx.HLE.Ui;
using Ryujinx.Input.Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ryujinx.Ava.Ui.Applet
{
    class AvaloniaDynamicTextInputHandler : IDynamicTextInputHandler
    {
        private readonly MainWindow _parent;
        private TextBox _hiddenTextBox;
        private bool _canProcessInput;

        public AvaloniaDynamicTextInputHandler(MainWindow parent)
        {
            _parent = parent;

            (_parent.InputManager.KeyboardDriver as AvaloniaKeyboardDriver).KeyPressed += AvaloniaDynamicTextInputHandler_KeyPressed;
            (_parent.InputManager.KeyboardDriver as AvaloniaKeyboardDriver).KeyRelease += AvaloniaDynamicTextInputHandler_KeyRelease;

            Dispatcher.UIThread.Post(() =>
            {
                _hiddenTextBox = new TextBox();
            });
        }

        private void AvaloniaDynamicTextInputHandler_KeyRelease(object sender, Avalonia.Input.KeyEventArgs e)
        {
            var key = (Ryujinx.Common.Configuration.Hid.Key)AvaloniaMappingHelper.ToInputKey(e.Key);

            if (!(KeyReleasedEvent?.Invoke(key)).GetValueOrDefault(true))
            {
                return;
            }

            RaiseKeyEvent(e);
        }

        private void AvaloniaDynamicTextInputHandler_KeyPressed(object sender, Avalonia.Input.KeyEventArgs e)
        {
            var key = (Ryujinx.Common.Configuration.Hid.Key)AvaloniaMappingHelper.ToInputKey(e.Key);

            if (!(KeyPressedEvent?.Invoke(key)).GetValueOrDefault(true))
            {
                return;
            }

            RaiseKeyEvent(e);
        }

        private void RaiseKeyEvent(Avalonia.Input.KeyEventArgs e)
        {
            if (_canProcessInput)
            {
                _hiddenTextBox.RaiseEvent(e);
                TextChangedEvent?.Invoke(_hiddenTextBox.Text, _hiddenTextBox.SelectionStart, _hiddenTextBox.SelectionEnd, true);
            }
        }

        public bool TextProcessingEnabled
        {
            get
            {
                return Volatile.Read(ref _canProcessInput);
            }

            set
            {
                Volatile.Write(ref _canProcessInput, value);
            }
        }

        public event DynamicTextChangedHandler TextChangedEvent;
        public event KeyPressedHandler KeyPressedEvent;
        public event KeyReleasedHandler KeyReleasedEvent;

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void SetText(string text, int cursorBegin)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _hiddenTextBox.Text = text;
                _hiddenTextBox.CaretIndex = cursorBegin;
            });
        }

        public void SetText(string text, int cursorBegin, int cursorEnd)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _hiddenTextBox.Text = text;
                _hiddenTextBox.SelectionStart = cursorBegin;
                _hiddenTextBox.SelectionEnd = cursorEnd;
            });
        }
    }
}
