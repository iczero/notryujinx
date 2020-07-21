using ARMeilleure.IntermediateRepresentation;
using ARMeilleure.Memory;
using System;
using System.Runtime.CompilerServices;

namespace ARMeilleure.State
{
    class NativeContext : IDisposable
    {
        private unsafe struct NativeCtxStorage
        {
            public fixed ulong X[RegisterConsts.IntRegsCount];
            public fixed ulong V[RegisterConsts.VecRegsCount * 2];
            public fixed uint Flags[RegisterConsts.FlagsCount];
            public fixed uint FpFlags[RegisterConsts.FpFlagsCount];
            public int Counter;
            public ulong CallAddress;
        }

        private static NativeCtxStorage _dummyStorage = new NativeCtxStorage();

        private readonly IJitMemoryBlock _block;

        public IntPtr BasePtr => _block.Pointer;

        public NativeContext(IJitMemoryAllocator allocator)
        {
            _block = allocator.Allocate((ulong)Unsafe.SizeOf<NativeCtxStorage>());
        }

        public unsafe ulong GetX(int index)
        {
            if ((uint)index >= RegisterConsts.IntRegsCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return GetStorage().X[index];
        }

        public unsafe void SetX(int index, ulong value)
        {
            if ((uint)index >= RegisterConsts.IntRegsCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            GetStorage().X[index] = value;
        }

        public unsafe V128 GetV(int index)
        {
            if ((uint)index >= RegisterConsts.IntRegsCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return new V128(GetStorage().V[index * 2 + 0], GetStorage().V[index * 2 + 1]);
        }

        public unsafe void SetV(int index, V128 value)
        {
            if ((uint)index >= RegisterConsts.IntRegsCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            GetStorage().V[index * 2 + 0] = value.Extract<ulong>(0);
            GetStorage().V[index * 2 + 1] = value.Extract<ulong>(1);
        }

        public unsafe bool GetPstateFlag(PState flag)
        {
            if ((uint)flag >= RegisterConsts.FlagsCount)
            {
                throw new ArgumentException($"Invalid flag \"{flag}\" specified.");
            }

            return GetStorage().Flags[(int)flag] != 0;
        }

        public unsafe void SetPstateFlag(PState flag, bool value)
        {
            if ((uint)flag >= RegisterConsts.FlagsCount)
            {
                throw new ArgumentException($"Invalid flag \"{flag}\" specified.");
            }

            GetStorage().Flags[(int)flag] = value ? 1u : 0u;
        }

        public unsafe bool GetFPStateFlag(FPState flag)
        {
            if ((uint)flag >= RegisterConsts.FlagsCount)
            {
                throw new ArgumentException($"Invalid flag \"{flag}\" specified.");
            }

            return GetStorage().FpFlags[(int)flag] != 0;
        }

        public unsafe void SetFPStateFlag(FPState flag, bool value)
        {
            if ((uint)flag >= RegisterConsts.FlagsCount)
            {
                throw new ArgumentException($"Invalid flag \"{flag}\" specified.");
            }

            GetStorage().FpFlags[(int)flag] = value ? 1u : 0u;
        }

        public int GetCounter() => GetStorage().Counter;
        public void SetCounter(int value) => GetStorage().Counter = value;

        public unsafe static int GetRegisterOffset(Register reg)
        {
            if ((uint)reg.Index >= 32)
            {
                throw new ArgumentException("Invalid register.");
            }

            if (reg.Type == RegisterType.Integer)
            {
                return StorageOffset(ref _dummyStorage, ref _dummyStorage.X[reg.Index]);
            }
            else if (reg.Type == RegisterType.Vector)
            {
                return StorageOffset(ref _dummyStorage, ref _dummyStorage.V[reg.Index * 2]);
            }
            else if (reg.Type == RegisterType.Flag)
            {
                return StorageOffset(ref _dummyStorage, ref _dummyStorage.Flags[reg.Index]);
            }
            else /* if (reg.Type == RegisterType.FpFlag) */
            {
                return StorageOffset(ref _dummyStorage, ref _dummyStorage.FpFlags[reg.Index]);
            }
        }

        public static int GetCounterOffset()
        {
            return StorageOffset(ref _dummyStorage, ref _dummyStorage.Counter);
        }

        public static int GetCallAddressOffset()
        {
            return StorageOffset(ref _dummyStorage, ref _dummyStorage.CallAddress);
        }

        private static int StorageOffset<T>(ref NativeCtxStorage storage, ref T target)
        {
            return (int)Unsafe.ByteOffset(ref Unsafe.As<NativeCtxStorage, T>(ref storage), ref target);
        }

        private unsafe ref NativeCtxStorage GetStorage() => ref Unsafe.AsRef<NativeCtxStorage>((void*)_block.Pointer);

        public void Dispose() => _block.Dispose();
    }
}