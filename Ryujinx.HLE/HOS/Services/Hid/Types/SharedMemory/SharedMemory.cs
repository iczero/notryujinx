using Ryujinx.Common.Memory;
using Ryujinx.HLE.HOS.Kernel.Memory;
using Ryujinx.HLE.HOS.Services.Hid.Types.SharedMemory.Common;
using Ryujinx.HLE.HOS.Services.Hid.Types.SharedMemory.DebugPad;
using Ryujinx.HLE.HOS.Services.Hid.Types.SharedMemory.Keyboard;
using Ryujinx.HLE.HOS.Services.Hid.Types.SharedMemory.Mouse;
using Ryujinx.HLE.HOS.Services.Hid.Types.SharedMemory.Npad;
using Ryujinx.HLE.HOS.Services.Hid.Types.SharedMemory.TouchScreen;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Hid.Types.SharedMemory
{
    /// <summary>
    /// Represent the shared memory shared between applications for input.
    /// </summary>
    class SharedMemory
    {
        /// <summary>
        /// Debug controller.
        /// </summary>
        public ref RingLifo<DebugPadState> DebugPad => ref _storage.GetRef<RingLifo<DebugPadState>>(0);

        /// <summary>
        /// Touchscreen.
        /// </summary>
        public ref RingLifo<TouchScreenState> TouchScreen => ref _storage.GetRef<RingLifo<TouchScreenState>>(0x400);

        /// <summary>
        /// Mouse.
        /// </summary>
        public ref RingLifo<MouseState> Mouse => ref _storage.GetRef<RingLifo<MouseState>>(0x3400);

        /// <summary>
        /// Keyboard.
        /// </summary>
        public ref RingLifo<KeyboardState> Keyboard => ref _storage.GetRef<RingLifo<KeyboardState>>(0x3800);

        /// <summary>
        /// Nintendo Pads.
        /// </summary>
        public ref Array10<NpadState> Npads => ref _storage.GetRef<Array10<NpadState>>(0x9A00);

        private SharedMemoryStorage _storage;

        public SharedMemory(SharedMemoryStorage storage)
        {
            _storage = storage;
        }

        public static SharedMemory Create(SharedMemoryStorage storage)
        {
            SharedMemory result = new SharedMemory(storage)
            {
                DebugPad = RingLifo<DebugPadState>.Create(),
                TouchScreen = RingLifo<TouchScreenState>.Create(),
                Mouse = RingLifo<MouseState>.Create(),
                Keyboard = RingLifo<KeyboardState>.Create(),
            };

            for (int i = 0; i < result.Npads.Length; i++)
            {
                result.Npads[i] = NpadState.Create();
            }

            return result;
        }
    }
}
