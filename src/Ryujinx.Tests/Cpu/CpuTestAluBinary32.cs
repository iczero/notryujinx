﻿#define AluBinary32

using ARMeilleure.State;
using System;
using Xunit;

namespace Ryujinx.Tests.Cpu
{

    [Collection("AluBinary32")]
    public sealed class CpuTestAluBinary32 : CpuTest32
    {
#if AluBinary32
        public struct CrcTest32
        {
            public uint Crc;
            public uint Value;
            public bool C;

            public uint[] Results; // One result for each CRC variant (8, 16, 32)

            public CrcTest32(uint crc, uint value, bool c, params uint[] results)
            {
                Crc = crc;
                Value = value;
                C = c;
                Results = results;
            }
        }

        #region "ValueSource (CRC32/CRC32C)"
        private static CrcTest32[] _CRC32_Test_Values_()
        {
            // Created with http://www.sunshine2k.de/coding/javascript/crc/crc_js.html, with:
            //  - non-reflected polynomials
            //  - input reflected, result reflected
            //  - bytes in order of increasing significance
            //  - xor 0

            return new[]
            {
                new CrcTest32(0x00000000u, 0x00_00_00_00u, false, 0x00000000, 0x00000000, 0x00000000),
                new CrcTest32(0x00000000u, 0x7f_ff_ff_ffu, false, 0x2d02ef8d, 0xbe2612ff, 0x3303a3c3),
                new CrcTest32(0x00000000u, 0x80_00_00_00u, false, 0x00000000, 0x00000000, 0xedb88320),
                new CrcTest32(0x00000000u, 0xff_ff_ff_ffu, false, 0x2d02ef8d, 0xbe2612ff, 0xdebb20e3),
                new CrcTest32(0x00000000u, 0x9d_cb_12_f0u, false, 0xbdbdf21c, 0xe70590f5, 0x3f7480c5),

                new CrcTest32(0xffffffffu, 0x00_00_00_00u, false, 0x2dfd1072, 0xbe26ed00, 0xdebb20e3),
                new CrcTest32(0xffffffffu, 0x7f_ff_ff_ffu, false, 0x00ffffff, 0x0000ffff, 0xedb88320),
                new CrcTest32(0xffffffffu, 0x80_00_00_00u, false, 0x2dfd1072, 0xbe26ed00, 0x3303a3c3),
                new CrcTest32(0xffffffffu, 0xff_ff_ff_ffu, false, 0x00ffffff, 0x0000ffff, 0x00000000),
                new CrcTest32(0xffffffffu, 0x9d_cb_12_f0u, false, 0x9040e26e, 0x59237df5, 0xe1cfa026),

                new CrcTest32(0x00000000u, 0x00_00_00_00u, true, 0x00000000, 0x00000000, 0x00000000),
                new CrcTest32(0x00000000u, 0x7f_ff_ff_ffu, true, 0xad7d5351, 0x0e9e77d2, 0x356e8f40),
                new CrcTest32(0x00000000u, 0x80_00_00_00u, true, 0x00000000, 0x00000000, 0x82f63b78),
                new CrcTest32(0x00000000u, 0xff_ff_ff_ffu, true, 0xad7d5351, 0x0e9e77d2, 0xb798b438),
                new CrcTest32(0x00000000u, 0x9d_cb_12_f0u, true, 0xf36e6f75, 0xb5ff99e6, 0x782dfbf1),

                new CrcTest32(0xffffffffu, 0x00_00_00_00u, true, 0xad82acae, 0x0e9e882d, 0xb798b438),
                new CrcTest32(0xffffffffu, 0x7f_ff_ff_ffu, true, 0x00ffffff, 0x0000ffff, 0x82f63b78),
                new CrcTest32(0xffffffffu, 0x80_00_00_00u, true, 0xad82acae, 0x0e9e882d, 0x356e8f40),
                new CrcTest32(0xffffffffu, 0xff_ff_ff_ffu, true, 0x00ffffff, 0x0000ffff, 0x00000000),
                new CrcTest32(0xffffffffu, 0x9d_cb_12_f0u, true, 0x5eecc3db, 0xbb6111cb, 0xcfb54fc9),
            };
        }
        #endregion

        private static uint[] _testData_rd =
        {
            0u,
        };
        private static uint[] _testData_rn =
        {
            1u,
        };
        private static uint[] _testData_rm =
        {
            2u,
        };

        public static readonly MatrixTheoryData<uint, uint, uint, uint, CrcTest32> TestData = new(_testData_rd, _testData_rn, _testData_rm, RangeUtils.RangeData(0u, 2u, 1u), _CRC32_Test_Values_());

        [Theory]
        [MemberData(nameof(TestData))]
        public void Crc32_Crc32c_b_h_w(uint rd, uint rn, uint rm, uint size, CrcTest32 test)
        {
            // Unicorn does not yet support 32bit crc instructions, so test against a known table of results/values.

            uint opcode = 0xe1000040; // CRC32B R0, R0, R0
            opcode |= ((rm & 15) << 0) | ((rd & 15) << 12) | ((rn & 15) << 16);
            opcode |= size << 21;
            if (test.C)
            {
                opcode |= 1 << 9;
            }

            uint sp = Random.Shared.NextUInt();

            SingleOpcode(opcode, r1: test.Crc, r2: test.Value, sp: sp, runUnicorn: false);

            ExecutionContext context = GetContext();
            ulong result = context.GetX((int)rd);
            Assert.True(result == test.Results[size]);
        }
#endif
    }
}
