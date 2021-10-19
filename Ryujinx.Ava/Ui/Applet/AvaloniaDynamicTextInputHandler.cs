using Avalonia.Controls;
using Avalonia.Threading;
using Ryujinx.Ava.Ui.Controls;
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
        private MainWindow _parent;
        private OffscreenTextBox _hiddenTextBox;
        private bool _canProcessInput;
        private long _lastInputTimestamp;
        private long _inputDelay;

        public AvaloniaDynamicTextInputHandler(MainWindow parent)
        {
            _parent = parent;

            (_parent.InputManager.KeyboardDriver as AvaloniaKeyboardDriver).KeyPressed += AvaloniaDynamicTextInputHandler_KeyPressed;
            (_parent.InputManager.KeyboardDriver as AvaloniaKeyboardDriver).KeyRelease += AvaloniaDynamicTextInputHandler_KeyRelease;

            _hiddenTextBox = _parent.HiddenTextBox;
            _inputDelay = TimeSpan.TicksPerMillisecond * 100;
        }

        private void AvaloniaDynamicTextInputHandler_KeyRelease(object sender, Avalonia.Input.KeyEventArgs e)
        {
            var key = (Ryujinx.Common.Configuration.Hid.Key)AvaloniaMappingHelper.ToInputKey(e.Key);

            if (!(KeyReleasedEvent?.Invoke(key)).GetValueOrDefault(true))
            {
                return;
            }

            e.RoutedEvent = _hiddenTextBox.GetKeyUpRoutedEvent();

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_canProcessInput)
                {
                    _hiddenTextBox.SendKeyUpEvent(e);
                }
                
                Task.Run(() => TextChangedEvent?.Invoke(_hiddenTextBox.Text ?? string.Empty,
                    _hiddenTextBox.SelectionStart,
                    _hiddenTextBox.SelectionEnd, true));
            });
        }

        private void AvaloniaDynamicTextInputHandler_KeyPressed(object sender, Avalonia.Input.KeyEventArgs e)
        {
            var key = (Ryujinx.Common.Configuration.Hid.Key)AvaloniaMappingHelper.ToInputKey(e.Key);

            if (!(KeyPressedEvent?.Invoke(key)).GetValueOrDefault(true))
            {
                return;
            }

            e.RoutedEvent = _hiddenTextBox.GetKeyUpRoutedEvent();

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_canProcessInput &&
                    (_lastInputTimestamp == 0 || DateTime.Now.Ticks > _lastInputTimestamp + _inputDelay))
                {
                    _hiddenTextBox.Focus();
                    _hiddenTextBox.SendKeyDownEvent(e);
                    _lastInputTimestamp = DateTime.Now.Ticks;
                }

                Task.Run(() => TextChangedEvent?.Invoke(_hiddenTextBox.Text ?? string.Empty,
                    _hiddenTextBox.SelectionStart,
                    _hiddenTextBox.SelectionEnd, true));
            });
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
            (_parent.InputManager.KeyboardDriver as AvaloniaKeyboardDriver).KeyPressed -= AvaloniaDynamicTextInputHandler_KeyPressed;
            (_parent.InputManager.KeyboardDriver as AvaloniaKeyboardDriver).KeyRelease -= AvaloniaDynamicTextInputHandler_KeyRelease;

            Dispatcher.UIThread.Post(() =>
            {
                _hiddenTextBox.Clear();
                _parent.GlRenderer.Focus();

                _parent = null;
            });
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
