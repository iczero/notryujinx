using System;

namespace ARMeilleure.State
{
    public struct V128 : IEquatable<V128>
    {
        private ulong _e0;
        private ulong _e1;

        public V128(float value) : this(value, value, value, value) { }

        public V128(double value) : this(value, value) { }

        public V128(float e0, float e1, float e2, float e3)
        {
            _e0  = (ulong)(uint)BitConverter.SingleToInt32Bits(e0) << 0;
            _e0 |= (ulong)(uint)BitConverter.SingleToInt32Bits(e1) << 32;
            _e1  = (ulong)(uint)BitConverter.SingleToInt32Bits(e2) << 0;
            _e1 |= (ulong)(uint)BitConverter.SingleToInt32Bits(e3) << 32;
        }

        public V128(double e0, double e1)
        {
            _e0 = (ulong)BitConverter.DoubleToInt64Bits(e0);
            _e1 = (ulong)BitConverter.DoubleToInt64Bits(e1);
        }

        public V128(int e0, int e1, int e2, int e3)
        {
            _e0  = (ulong)(uint)e0 << 0;
            _e0 |= (ulong)(uint)e1 << 32;
            _e1  = (ulong)(uint)e2 << 0;
            _e1 |= (ulong)(uint)e3 << 32;
        }

        public V128(uint e0, uint e1, uint e2, uint e3)
        {
            _e0  = (ulong)e0 << 0;
            _e0 |= (ulong)e1 << 32;
            _e1  = (ulong)e2 << 0;
            _e1 |= (ulong)e3 << 32;
        }

        public V128(long e0, long e1)
        {
            _e0 = (ulong)e0;
            _e1 = (ulong)e1;
        }

        public V128(ulong e0, ulong e1)
        {
            _e0 = e0;
            _e1 = e1;
        }

        public V128(byte[] data)
        {
            _e0 = (ulong)BitConverter.ToInt64(data, 0);
            _e1 = (ulong)BitConverter.ToInt64(data, 8);
        }

        public float AsFloat()
        {
            return GetFloat(0);
        }

        public double AsDouble()
        {
            return GetDouble(0);
        }

        public float GetFloat(int index)
        {
            return BitConverter.Int32BitsToSingle(GetInt32(index));
        }

        public double GetDouble(int index)
        {
            return BitConverter.Int64BitsToDouble(GetInt64(index));
        }

        public int  GetInt32(int index) => (int)GetUInt32(index);
        public long GetInt64(int index) => (long)GetUInt64(index);

        public uint GetUInt32(int index)
        {
            if ((uint)index > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return (uint)(GetUInt64(index >> 1) >> (index & 1));
        }

        public ulong GetUInt64(int index)
        {
            switch (index)
            {
                case 0: return _e0;
                case 1: return _e1;
            }

            throw new ArgumentOutOfRangeException(nameof(index));
        }

        public byte[] ToArray()
        {
            byte[] e0Data = BitConverter.GetBytes(_e0);
            byte[] e1Data = BitConverter.GetBytes(_e1);

            byte[] data = new byte[16];

            Buffer.BlockCopy(e0Data, 0, data, 0, 8);
            Buffer.BlockCopy(e1Data, 0, data, 8, 8);

            return data;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_e0, _e1);
        }

        public static V128 operator ~(V128 x)
        {
            return new V128(~x._e0, ~x._e1);
        }

        public static V128 operator &(V128 x, V128 y)
        {
            return new V128(x._e0 & y._e0, x._e1 & y._e1);
        }

        public static V128 operator |(V128 x, V128 y)
        {
            return new V128(x._e0 | y._e0, x._e1 | y._e1);
        }

        public static V128 operator ^(V128 x, V128 y)
        {
            return new V128(x._e0 ^ y._e0, x._e1 ^ y._e1);
        }

        public static bool operator ==(V128 x, V128 y)
        {
            return x.Equals(y);
        }

        public static bool operator !=(V128 x, V128 y)
        {
            return !x.Equals(y);
        }

        public override bool Equals(object obj)
        {
            return obj is V128 vector && Equals(vector);
        }

        public bool Equals(V128 other)
        {
            return other._e0 == _e0 && other._e1 == _e1;
        }

        public override string ToString()
        {
            return $"0x{_e1:X16}{_e0:X16}";
        }
    }
}