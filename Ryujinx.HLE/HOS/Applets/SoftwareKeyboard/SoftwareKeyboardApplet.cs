﻿using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Applets.SoftwareKeyboard;
using Ryujinx.HLE.HOS.Services.Am.AppletAE;
using Ryujinx.HLE.HOS.Services.Hid.Types.SharedMemory.Npad;
using Ryujinx.HLE.Ui;
using Ryujinx.HLE.Ui.Input;
using Ryujinx.Memory;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ryujinx.HLE.HOS.Applets
{
    internal class SoftwareKeyboardApplet : IApplet
    {
        private const string DefaultInputText = "Ryujinx";

        private const int StandardBufferSize = 0x7D8;
        private const int MaxUserWords       = 0x1388;
        private const int MaxUiTextSize      = 100;

        private const Key CycleInputModesKey = Key.F5;

        private readonly Switch _device;

        private SoftwareKeyboardState        _foregroundState = SoftwareKeyboardState.Uninitialized;
        private volatile InlineKeyboardState _backgroundState = InlineKeyboardState.Uninitialized;

        private bool _isBackground = false;

        private AppletSession _normalSession;
        private AppletSession _interactiveSession;

        // Configuration for foreground mode.
        private SoftwareKeyboardConfig _keyboardForegroundConfig;

        // Configuration for background (inline) mode.
        private SoftwareKeyboardInitialize   _keyboardBackgroundInitialize;
        private SoftwareKeyboardCustomizeDic _keyboardBackgroundDic;
        private SoftwareKeyboardDictSet      _keyboardBackgroundDictSet;
        private SoftwareKeyboardUserWord[]   _keyboardBackgroundUserWords;

        private byte[] _transferMemory;

        private string         _textValue   = "";
        private int            _cursorBegin = 0;
        private Encoding       _encoding    = Encoding.Unicode;
        private KeyboardResult _lastResult  = KeyboardResult.NotSet;

        private IDynamicTextInputHandler _dynamicTextInputHandler = null;
        private SoftwareKeyboardRenderer _keyboardRenderer        = null;
        private NpadReader               _npads                   = null;
        private bool                     _canAcceptController     = false;
        private KeyboardInputMode        _inputMode               = KeyboardInputMode.ControllerAndKeyboard;

        private object _lock = new object();

        public event EventHandler AppletStateChanged;

        public SoftwareKeyboardApplet(Horizon system)
        {
            _device = system.Device;
        }

        public ResultCode Start(AppletSession normalSession,
                                AppletSession interactiveSession)
        {
            lock (_lock)
            {
                _normalSession      = normalSession;
                _interactiveSession = interactiveSession;

                _interactiveSession.DataAvailable += OnInteractiveData;

                var launchParams   = _normalSession.Pop();
                var keyboardConfig = _normalSession.Pop();

                _isBackground = keyboardConfig.Length == Marshal.SizeOf<SoftwareKeyboardInitialize>();

                if (_isBackground)
                {
                    // Initialize the keyboard applet in background mode.

                    _keyboardBackgroundInitialize = ReadStruct<SoftwareKeyboardInitialize>(keyboardConfig);
                    _backgroundState              = InlineKeyboardState.Uninitialized;

                    if (_device.UiHandler == null)
                    {
                        Logger.Error?.Print(LogClass.ServiceAm, "GUI Handler is not set, software keyboard applet will not work properly");
                    }
                    else
                    {
                        // Create a text handler that converts keyboard strokes to strings.
                        _dynamicTextInputHandler = _device.UiHandler.CreateDynamicTextInputHandler();
                        _dynamicTextInputHandler.TextChangedEvent += HandleTextChangedEvent;
                        _dynamicTextInputHandler.KeyPressedEvent  += HandleKeyPressedEvent;

                        _npads = new NpadReader(_device);
                        _npads.NpadButtonDownEvent += HandleNpadButtonDownEvent;
                        _npads.NpadButtonUpEvent   += HandleNpadButtonUpEvent;

                        _keyboardRenderer = new SoftwareKeyboardRenderer(_device.UiHandler.HostUiTheme);
                    }

                    return ResultCode.Success;
                }
                else
                {
                    // Initialize the keyboard applet in foreground mode.

                    if (keyboardConfig.Length < Marshal.SizeOf<SoftwareKeyboardConfig>())
                    {
                        Logger.Error?.Print(LogClass.ServiceAm, $"SoftwareKeyboardConfig size mismatch. Expected {Marshal.SizeOf<SoftwareKeyboardConfig>():x}. Got {keyboardConfig.Length:x}");
                    }
                    else
                    {
                        _keyboardForegroundConfig = ReadStruct<SoftwareKeyboardConfig>(keyboardConfig);
                    }

                    if (!_normalSession.TryPop(out _transferMemory))
                    {
                        Logger.Error?.Print(LogClass.ServiceAm, "SwKbd Transfer Memory is null");
                    }

                    if (_keyboardForegroundConfig.UseUtf8)
                    {
                        _encoding = Encoding.UTF8;
                    }

                    _foregroundState = SoftwareKeyboardState.Ready;

                    ExecuteForegroundKeyboard();

                    return ResultCode.Success;
                }
            }
        }

        public ResultCode GetResult()
        {
            return ResultCode.Success;
        }

        private bool IsKeyboardActive()
        {
            return _backgroundState >= InlineKeyboardState.Appearing && _backgroundState < InlineKeyboardState.Disappearing;
        }

        private bool InputModeControllerEnabled()
        {
            return _inputMode == KeyboardInputMode.ControllerAndKeyboard ||
                   _inputMode == KeyboardInputMode.ControllerOnly;
        }

        private bool InputModeTypingEnabled()
        {
            return _inputMode == KeyboardInputMode.ControllerAndKeyboard ||
                   _inputMode == KeyboardInputMode.KeyboardOnly;
        }

        private void AdvanceInputMode()
        {
            _inputMode = (KeyboardInputMode)((int)(_inputMode + 1) % (int)KeyboardInputMode.Count);
        }

        public bool DrawTo(RenderingSurfaceInfo surfaceInfo, IVirtualMemoryManager destination, ulong position)
        {
            _npads?.Update();

            return _keyboardRenderer?.DrawTo(surfaceInfo, destination, position) ?? false;
        }

        private void ExecuteForegroundKeyboard()
        {
            string initialText = null;

            // Initial Text is always encoded as a UTF-16 string in the work buffer (passed as transfer memory)
            // InitialStringOffset points to the memory offset and InitialStringLength is the number of UTF-16 characters
            if (_transferMemory != null && _keyboardForegroundConfig.InitialStringLength > 0)
            {
                initialText = Encoding.Unicode.GetString(_transferMemory, _keyboardForegroundConfig.InitialStringOffset,
                    2 * _keyboardForegroundConfig.InitialStringLength);
            }

            // If the max string length is 0, we set it to a large default
            // length.
            if (_keyboardForegroundConfig.StringLengthMax == 0)
            {
                _keyboardForegroundConfig.StringLengthMax = 100;
            }

            if (_device.UiHandler == null)
            {
                Logger.Warning?.Print(LogClass.Application, "GUI Handler is not set. Falling back to default");

                _textValue = DefaultInputText;
                _lastResult = KeyboardResult.Accept;
            }
            else
            {
                // Call the configured GUI handler to get user's input.

                var args = new SoftwareKeyboardUiArgs
                {
                    HeaderText = _keyboardForegroundConfig.HeaderText,
                    SubtitleText = _keyboardForegroundConfig.SubtitleText,
                    GuideText = _keyboardForegroundConfig.GuideText,
                    SubmitText = (!string.IsNullOrWhiteSpace(_keyboardForegroundConfig.SubmitText) ?
                    _keyboardForegroundConfig.SubmitText : "OK"),
                    StringLengthMin = _keyboardForegroundConfig.StringLengthMin,
                    StringLengthMax = _keyboardForegroundConfig.StringLengthMax,
                    InitialText = initialText
                };

                _lastResult = _device.UiHandler.DisplayInputDialog(args, out _textValue) ? KeyboardResult.Accept : KeyboardResult.Cancel;
                _textValue ??= initialText ?? DefaultInputText;
            }

            // If the game requests a string with a minimum length less
            // than our default text, repeat our default text until we meet
            // the minimum length requirement.
            // This should always be done before the text truncation step.
            while (_textValue.Length < _keyboardForegroundConfig.StringLengthMin)
            {
                _textValue = String.Join(" ", _textValue, _textValue);
            }

            // If our default text is longer than the allowed length,
            // we truncate it.
            if (_textValue.Length > _keyboardForegroundConfig.StringLengthMax)
            {
                _textValue = _textValue.Substring(0, _keyboardForegroundConfig.StringLengthMax);
            }

            // Does the application want to validate the text itself?
            if (_keyboardForegroundConfig.CheckText)
            {
                // The application needs to validate the response, so we
                // submit it to the interactive output buffer, and poll it
                // for validation. Once validated, the application will submit
                // back a validation status, which is handled in OnInteractiveDataPushIn.
                _foregroundState = SoftwareKeyboardState.ValidationPending;

                _interactiveSession.Push(BuildForegroundResponse());
            }
            else
            {
                // If the application doesn't need to validate the response,
                // we push the data to the non-interactive output buffer
                // and poll it for completion.
                _foregroundState = SoftwareKeyboardState.Complete;

                _normalSession.Push(BuildForegroundResponse());

                AppletStateChanged?.Invoke(this, null);
            }
        }

        private void OnInteractiveData(object sender, EventArgs e)
        {
            // Obtain the validation status response.
            var data = _interactiveSession.Pop();

            if (_isBackground)
            {
                lock (_lock)
                {
                    OnBackgroundInteractiveData(data);
                }
            }
            else
            {
                OnForegroundInteractiveData(data);
            }
        }

        private void OnForegroundInteractiveData(byte[] data)
        {
            if (_foregroundState == SoftwareKeyboardState.ValidationPending)
            {
                // TODO(jduncantor):
                // If application rejects our "attempt", submit another attempt,
                // and put the applet back in PendingValidation state.

                // For now we assume success, so we push the final result
                // to the standard output buffer and carry on our merry way.
                _normalSession.Push(BuildForegroundResponse());

                AppletStateChanged?.Invoke(this, null);

                _foregroundState = SoftwareKeyboardState.Complete;
            }
            else if(_foregroundState == SoftwareKeyboardState.Complete)
            {
                // If we have already completed, we push the result text
                // back on the output buffer and poll the application.
                _normalSession.Push(BuildForegroundResponse());

                AppletStateChanged?.Invoke(this, null);
            }
            else
            {
                // We shouldn't be able to get here through standard swkbd execution.
                throw new InvalidOperationException("Software Keyboard is in an invalid state.");
            }
        }

        private void OnBackgroundInteractiveData(byte[] data)
        {
            // WARNING: Only invoke applet state changes after an explicit finalization
            // request from the game, this is because the inline keyboard is expected to
            // keep running in the background sending data by itself.

            using (MemoryStream stream = new MemoryStream(data))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                var request = (InlineKeyboardRequest)reader.ReadUInt32();

                long remaining;

                Logger.Debug?.Print(LogClass.ServiceAm, $"Keyboard received command {request} in state {_backgroundState}");

                switch (request)
                {
                    case InlineKeyboardRequest.UseChangedStringV2:
                        Logger.Stub?.Print(LogClass.ServiceAm, "Inline keyboard request UseChangedStringV2");
                        break;
                    case InlineKeyboardRequest.UseMovedCursorV2:
                        Logger.Stub?.Print(LogClass.ServiceAm, "Inline keyboard request UseMovedCursorV2");
                        break;
                    case InlineKeyboardRequest.SetUserWordInfo:
                        // Read the user word info data.
                        remaining = stream.Length - stream.Position;
                        if (remaining < sizeof(int))
                        {
                            Logger.Warning?.Print(LogClass.ServiceAm, $"Received invalid Software Keyboard User Word Info of {remaining} bytes");
                        }
                        else
                        {
                            int wordsCount = reader.ReadInt32();
                            int wordSize = Marshal.SizeOf<SoftwareKeyboardUserWord>();
                            remaining = stream.Length - stream.Position;

                            if (wordsCount > MaxUserWords)
                            {
                                Logger.Warning?.Print(LogClass.ServiceAm, $"Received {wordsCount} User Words but the maximum is {MaxUserWords}");
                            }
                            else if (wordsCount * wordSize != remaining)
                            {
                                Logger.Warning?.Print(LogClass.ServiceAm, $"Received invalid Software Keyboard User Word Info data of {remaining} bytes for {wordsCount} words");
                            }
                            else
                            {
                                _keyboardBackgroundUserWords = new SoftwareKeyboardUserWord[wordsCount];

                                for (int word = 0; word < wordsCount; word++)
                                {
                                    byte[] wordData = reader.ReadBytes(wordSize);
                                    _keyboardBackgroundUserWords[word] = ReadStruct<SoftwareKeyboardUserWord>(wordData);
                                }
                            }
                        }
                        _interactiveSession.Push(InlineResponses.ReleasedUserWordInfo(_backgroundState));
                        break;
                    case InlineKeyboardRequest.SetCustomizeDic:
                        // Read the custom dic data.
                        remaining = stream.Length - stream.Position;
                        if (remaining != Marshal.SizeOf<SoftwareKeyboardCustomizeDic>())
                        {
                            Logger.Warning?.Print(LogClass.ServiceAm, $"Received invalid Software Keyboard Customize Dic of {remaining} bytes");
                        }
                        else
                        {
                            var keyboardDicData = reader.ReadBytes((int)remaining);
                            _keyboardBackgroundDic = ReadStruct<SoftwareKeyboardCustomizeDic>(keyboardDicData);
                        }
                        break;
                    case InlineKeyboardRequest.SetCustomizedDictionaries:
                        // Read the custom dictionaries data.
                        remaining = stream.Length - stream.Position;
                        if (remaining != Marshal.SizeOf<SoftwareKeyboardDictSet>())
                        {
                            Logger.Warning?.Print(LogClass.ServiceAm, $"Received invalid Software Keyboard DictSet of {remaining} bytes");
                        }
                        else
                        {
                            var keyboardDictData = reader.ReadBytes((int)remaining);
                            _keyboardBackgroundDictSet = ReadStruct<SoftwareKeyboardDictSet>(keyboardDictData);
                        }
                        break;
                    case InlineKeyboardRequest.Calc:
                        // The Calc request is used to communicate configuration changes and commands to the keyboard.
                        // Fields in the Calc struct and operations are masked by the Flags field.

                        // Read the Calc data.
                        SoftwareKeyboardCalcEx newCalc;
                        remaining = stream.Length - stream.Position;
                        if (remaining == Marshal.SizeOf<SoftwareKeyboardCalc>())
                        {
                            var keyboardCalcData = reader.ReadBytes((int)remaining);
                            var keyboardCalc     = ReadStruct<SoftwareKeyboardCalc>(keyboardCalcData);

                            newCalc = keyboardCalc.ToExtended();
                        }
                        else if (remaining == Marshal.SizeOf<SoftwareKeyboardCalcEx>())
                        {
                            var keyboardCalcData = reader.ReadBytes((int)remaining);

                            newCalc = ReadStruct<SoftwareKeyboardCalcEx>(keyboardCalcData);
                        }
                        else
                        {
                            Logger.Error?.Print(LogClass.ServiceAm, $"Received invalid Software Keyboard Calc of {remaining} bytes");

                            newCalc = new SoftwareKeyboardCalcEx();
                        }

                        // Process each individual operation specified in the flags.

                        bool updateText = false;

                        if ((newCalc.Flags & KeyboardCalcFlags.Initialize) != 0)
                        {
                            _interactiveSession.Push(InlineResponses.FinishedInitialize(_backgroundState));

                            _backgroundState = InlineKeyboardState.Initialized;
                        }

                        if ((newCalc.Flags & KeyboardCalcFlags.SetCursorPos) != 0)
                        {
                            _cursorBegin = newCalc.CursorPos;
                            updateText = true;

                            Logger.Debug?.Print(LogClass.ServiceAm, $"Cursor position set to {_cursorBegin}");
                        }

                        if ((newCalc.Flags & KeyboardCalcFlags.SetInputText) != 0)
                        {
                            _textValue = newCalc.InputText;
                            updateText = true;

                            Logger.Debug?.Print(LogClass.ServiceAm, $"Input text set to {_textValue}");
                        }

                        if ((newCalc.Flags & KeyboardCalcFlags.SetUtf8Mode) != 0)
                        {
                            _encoding = newCalc.UseUtf8 ? Encoding.UTF8 : Encoding.Default;

                            Logger.Debug?.Print(LogClass.ServiceAm, $"Encoding set to {_encoding}");
                        }

                        if (updateText)
                        {
                            _dynamicTextInputHandler.SetText(_textValue, _cursorBegin);
                            _keyboardRenderer.UpdateTextState(_textValue, _cursorBegin, _cursorBegin, null, null);
                        }

                        if ((newCalc.Flags & KeyboardCalcFlags.MustShow) != 0)
                        {
                            ActivateFrontend();

                            _backgroundState = InlineKeyboardState.Shown;

                            PushChangedString(_textValue, (uint)_cursorBegin, _backgroundState);
                        }

                        // Send the response to the Calc
                        _interactiveSession.Push(InlineResponses.Default(_backgroundState));
                        break;
                    case InlineKeyboardRequest.Finalize:
                        // Destroy the frontend.
                        DestroyFrontend();
                        // The calling application wants to close the keyboard applet and will wait for a state change.
                        _backgroundState = InlineKeyboardState.Uninitialized;
                        AppletStateChanged?.Invoke(this, null);
                        break;
                    default:
                        // We shouldn't be able to get here through standard swkbd execution.
                        Logger.Warning?.Print(LogClass.ServiceAm, $"Invalid Software Keyboard request {request} during state {_backgroundState}");
                        _interactiveSession.Push(InlineResponses.Default(_backgroundState));
                        break;
                }
            }
        }

        private void ActivateFrontend()
        {
            Logger.Debug?.Print(LogClass.ServiceAm, $"Activating software keyboard frontend");

            _inputMode = KeyboardInputMode.ControllerAndKeyboard;

            _npads.Update(true);

            NpadButton buttons = _npads.GetCurrentButtonsOfAllNpads();

            // Block the input if the current accept key is pressed so the applet won't be instantly closed.
            _canAcceptController = (buttons & NpadButton.A) == 0;

            _dynamicTextInputHandler.TextProcessingEnabled = true;

            _keyboardRenderer.UpdateCommandState(null, null, true);
            _keyboardRenderer.UpdateTextState(null, null, null, null, true);
        }

        private void DeactivateFrontend()
        {
            Logger.Debug?.Print(LogClass.ServiceAm, $"Deactivating software keyboard frontend");

            _inputMode           = KeyboardInputMode.ControllerAndKeyboard;
            _canAcceptController = false;

            _dynamicTextInputHandler.TextProcessingEnabled = false;
            _dynamicTextInputHandler.SetText(_textValue, _cursorBegin);
        }

        private void DestroyFrontend()
        {
            Logger.Debug?.Print(LogClass.ServiceAm, $"Destroying software keyboard frontend");

            _keyboardRenderer?.Dispose();
            _keyboardRenderer = null;

            if (_dynamicTextInputHandler != null)
            {
                _dynamicTextInputHandler.TextChangedEvent -= HandleTextChangedEvent;
                _dynamicTextInputHandler.KeyPressedEvent  -= HandleKeyPressedEvent;
                _dynamicTextInputHandler.Dispose();
                _dynamicTextInputHandler = null;
            }

            if (_npads != null)
            {
                _npads.NpadButtonDownEvent -= HandleNpadButtonDownEvent;
                _npads.NpadButtonUpEvent   -= HandleNpadButtonUpEvent;
                _npads = null;
            }
        }

        private bool HandleKeyPressedEvent(Key key)
        {
            if (key == CycleInputModesKey)
            {
                lock (_lock)
                {
                    if (IsKeyboardActive())
                    {
                        AdvanceInputMode();

                        bool typingEnabled     = InputModeTypingEnabled();
                        bool controllerEnabled = InputModeControllerEnabled();

                        _dynamicTextInputHandler.TextProcessingEnabled = typingEnabled;

                        _keyboardRenderer.UpdateTextState(null, null, null, null, typingEnabled);
                        _keyboardRenderer.UpdateCommandState(null, null, controllerEnabled);
                    }
                }
            }

            return true;
        }

        private void HandleTextChangedEvent(string text, int cursorBegin, int cursorEnd, bool overwriteMode)
        {
            lock (_lock)
            {
                // Text processing should not run with typing disabled.
                Debug.Assert(InputModeTypingEnabled());

                if (text.Length > MaxUiTextSize)
                {
                    // Limit the text size and change it back.
                    text        = text.Substring(0, MaxUiTextSize);
                    cursorBegin = Math.Min(cursorBegin, MaxUiTextSize);
                    cursorEnd   = Math.Min(cursorEnd, MaxUiTextSize);

                    _dynamicTextInputHandler.SetText(text, cursorBegin, cursorEnd);
                }

                _textValue   = text;
                _cursorBegin = cursorBegin;
                _keyboardRenderer.UpdateTextState(text, cursorBegin, cursorEnd, overwriteMode, null);

                PushUpdatedState(text, cursorBegin, KeyboardResult.NotSet);
            }
        }

        private void HandleNpadButtonDownEvent(int npadIndex, NpadButton button)
        {
            lock (_lock)
            {
                if (!IsKeyboardActive())
                {
                    return;
                }

                switch (button)
                {
                    case NpadButton.A:
                        _keyboardRenderer.UpdateCommandState(_canAcceptController, null, null);
                        break;
                    case NpadButton.B:
                        _keyboardRenderer.UpdateCommandState(null, _canAcceptController, null);
                        break;
                }
            }
        }

        private void HandleNpadButtonUpEvent(int npadIndex, NpadButton button)
        {
            lock (_lock)
            {
                KeyboardResult result = KeyboardResult.NotSet;

                switch (button)
                {
                    case NpadButton.A:
                        result = KeyboardResult.Accept;
                        _keyboardRenderer.UpdateCommandState(false, null, null);
                        break;
                    case NpadButton.B:
                        result = KeyboardResult.Cancel;
                        _keyboardRenderer.UpdateCommandState(null, false, null);
                        break;
                }

                if (IsKeyboardActive())
                {
                    if (!_canAcceptController)
                    {
                        _canAcceptController = true;
                    }
                    else if (InputModeControllerEnabled())
                    {
                        PushUpdatedState(_textValue, _cursorBegin, result);
                    }
                }
            }
        }

        private void PushUpdatedState(string text, int cursorBegin, KeyboardResult result)
        {
            _lastResult = result;
            _textValue  = text;

            bool cancel = result == KeyboardResult.Cancel;
            bool accept = result == KeyboardResult.Accept;

            if (!IsKeyboardActive())
            {
                // Keyboard is not active.

                return;
            }

            if (accept == false && cancel == false)
            {
                Logger.Debug?.Print(LogClass.ServiceAm, $"Updating keyboard text to {text} and cursor position to {cursorBegin}");

                PushChangedString(text, (uint)cursorBegin, _backgroundState);
            }
            else
            {
                // Disable the frontend.
                DeactivateFrontend();

                // The 'Complete' state indicates the Calc request has been fulfilled by the applet.
                _backgroundState = InlineKeyboardState.Disappearing;

                if (accept)
                {
                    Logger.Debug?.Print(LogClass.ServiceAm, $"Sending keyboard OK with text {text}");

                    DecidedEnter(text, _backgroundState);
                }
                else if (cancel)
                {
                    Logger.Debug?.Print(LogClass.ServiceAm, "Sending keyboard Cancel");

                    DecidedCancel(_backgroundState);
                }

                _interactiveSession.Push(InlineResponses.Default(_backgroundState));

                Logger.Debug?.Print(LogClass.ServiceAm, $"Resetting state of the keyboard to {_backgroundState}");

                // Set the state of the applet to 'Initialized' as it is the only known state so far
                // that does not soft-lock the keyboard after use.

                _backgroundState = InlineKeyboardState.Initialized;

                _interactiveSession.Push(InlineResponses.Default(_backgroundState));
            }
        }

        private void PushChangedString(string text, uint cursor, InlineKeyboardState state)
        {
            // TODO (Caian): The *V2 methods are not supported because the applications that request
            // them do not seem to accept them. The regular methods seem to work just fine in all cases.

            if (_encoding == Encoding.UTF8)
            {
                _interactiveSession.Push(InlineResponses.ChangedStringUtf8(text, cursor, state));
            }
            else
            {
                _interactiveSession.Push(InlineResponses.ChangedString(text, cursor, state));
            }
        }

        private void DecidedEnter(string text, InlineKeyboardState state)
        {
            if (_encoding == Encoding.UTF8)
            {
                _interactiveSession.Push(InlineResponses.DecidedEnterUtf8(text, state));
            }
            else
            {
                _interactiveSession.Push(InlineResponses.DecidedEnter(text, state));
            }
        }

        private void DecidedCancel(InlineKeyboardState state)
        {
            _interactiveSession.Push(InlineResponses.DecidedCancel(state));
        }

        private byte[] BuildForegroundResponse()
        {
            int bufferSize = StandardBufferSize;

            using (MemoryStream stream = new MemoryStream(new byte[bufferSize]))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                byte[] output = _encoding.GetBytes(_textValue);

                // Result Code.
                writer.Write(_lastResult == KeyboardResult.Accept ? 0U : 1U);
                writer.Write(output);

                return stream.ToArray();
            }
        }

        private static T ReadStruct<T>(byte[] data)
            where T : struct
        {
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);

            try
            {
                return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }
        }
    }
}
