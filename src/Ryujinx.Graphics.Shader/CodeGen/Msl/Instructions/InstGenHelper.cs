using Ryujinx.Graphics.Shader.IntermediateRepresentation;

namespace Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions
{
    static class InstGenHelper
    {
        private static readonly InstInfo[] _infoTable;

        static InstGenHelper()
        {
            _infoTable = new InstInfo[(int)Instruction.Count];

#pragma warning disable IDE0055 // Disable formatting
            Add(Instruction.AtomicAdd,                InstType.AtomicBinary,   "add");
            Add(Instruction.AtomicAnd,                InstType.AtomicBinary,   "and");
            Add(Instruction.AtomicCompareAndSwap,     0);
            Add(Instruction.AtomicMaxU32,             InstType.AtomicBinary,   "max");
            Add(Instruction.AtomicMinU32,             InstType.AtomicBinary,   "min");
            Add(Instruction.AtomicOr,                 InstType.AtomicBinary,   "or");
            Add(Instruction.AtomicSwap,               0);
            Add(Instruction.AtomicXor,                InstType.AtomicBinary,   "xor");
            Add(Instruction.Absolute,                 InstType.AtomicBinary,   "abs");
            Add(Instruction.Add,                      InstType.OpBinaryCom,    "+");
            Add(Instruction.Ballot,                   InstType.Special);
            Add(Instruction.Barrier,                  InstType.CallUnary,      "threadgroup_barrier");
            Add(Instruction.BitCount,                 InstType.CallUnary,      "popcount");
            Add(Instruction.BitfieldExtractS32,       InstType.CallTernary,    "extract_bits");
            Add(Instruction.BitfieldExtractU32,       InstType.CallTernary,    "extract_bits");
            Add(Instruction.BitfieldInsert,           InstType.CallQuaternary, "insert_bits");
            Add(Instruction.BitfieldReverse,          InstType.CallUnary,      "reverse_bits");
            Add(Instruction.BitwiseAnd,               InstType.OpBinaryCom,    "&");
            Add(Instruction.BitwiseExclusiveOr,       InstType.OpBinaryCom,    "^");
            Add(Instruction.BitwiseNot,               InstType.OpUnary,        "~");
            Add(Instruction.BitwiseOr,                InstType.OpBinaryCom,    "|");
            Add(Instruction.Call,                     InstType.Special);
            Add(Instruction.Ceiling,                  InstType.CallUnary,      "ceil");
            Add(Instruction.Clamp,                    InstType.CallTernary,    "clamp");
            Add(Instruction.ClampU32,                 InstType.CallTernary,    "clamp");
            Add(Instruction.CompareEqual,             InstType.OpBinaryCom,    "==");
            Add(Instruction.CompareGreater,           InstType.OpBinary,       ">");
            Add(Instruction.CompareGreaterOrEqual,    InstType.OpBinary,       ">=");
            Add(Instruction.CompareGreaterOrEqualU32, InstType.OpBinary,       ">=");
            Add(Instruction.CompareGreaterU32,        InstType.OpBinary,       ">");
            Add(Instruction.CompareLess,              InstType.OpBinary,       "<");
            Add(Instruction.CompareLessOrEqual,       InstType.OpBinary,       "<=");
            Add(Instruction.CompareLessOrEqualU32,    InstType.OpBinary,       "<=");
            Add(Instruction.CompareLessU32,           InstType.OpBinary,       "<");
            Add(Instruction.CompareNotEqual,          InstType.OpBinaryCom,    "!=");
            Add(Instruction.ConditionalSelect,        InstType.OpTernary,      "?:");
            Add(Instruction.ConvertFP32ToFP64,        0); // MSL does not have a 64-bit FP
            Add(Instruction.ConvertFP64ToFP32,        0); // MSL does not have a 64-bit FP
            Add(Instruction.ConvertFP32ToS32,         InstType.Cast,           "int");
            Add(Instruction.ConvertFP32ToU32,         InstType.Cast,           "uint");
            Add(Instruction.ConvertFP64ToS32,         0); // MSL does not have a 64-bit FP
            Add(Instruction.ConvertFP64ToU32,         0); // MSL does not have a 64-bit FP
            Add(Instruction.ConvertS32ToFP32,         InstType.Cast,           "float");
            Add(Instruction.ConvertS32ToFP64,         0); // MSL does not have a 64-bit FP
            Add(Instruction.ConvertU32ToFP32,         InstType.Cast,           "float");
            Add(Instruction.ConvertU32ToFP64,         0); // MSL does not have a 64-bit FP
            Add(Instruction.Cosine,                   InstType.CallUnary,      "cos");
            Add(Instruction.Ddx,                      InstType.CallUnary,      "dfdx");
            Add(Instruction.Ddy,                      InstType.CallUnary,      "dfdy");
            Add(Instruction.Discard,                  InstType.CallNullary,    "discard_fragment");
            Add(Instruction.Divide,                   InstType.OpBinary,       "/");
            Add(Instruction.EmitVertex,               0); // MSL does not have geometry shaders
            Add(Instruction.EndPrimitive,             0); // MSL does not have geometry shaders
            Add(Instruction.ExponentB2,               InstType.CallUnary,      "exp2");
            Add(Instruction.FSIBegin,                 InstType.Special);
            Add(Instruction.FSIEnd,                   InstType.Special);
            // TODO: LSB and MSB Implementations https://github.com/KhronosGroup/SPIRV-Cross/blob/bccaa94db814af33d8ef05c153e7c34d8bd4d685/reference/shaders-msl-no-opt/asm/comp/bitscan.asm.comp#L8
            Add(Instruction.FindLSB,                  InstType.Special);
            Add(Instruction.FindMSBS32,               InstType.Special);
            Add(Instruction.FindMSBU32,               InstType.Special);
            Add(Instruction.Floor,                    InstType.CallUnary,      "floor");
            Add(Instruction.FusedMultiplyAdd,         InstType.CallTernary,    "fma");
            Add(Instruction.GroupMemoryBarrier,       InstType.CallUnary,      "threadgroup_barrier");
            Add(Instruction.ImageLoad,                InstType.Special);
            Add(Instruction.ImageStore,               InstType.Special);
            Add(Instruction.ImageAtomic,              InstType.Special); // Metal 3.1+
            Add(Instruction.IsNan,                    InstType.CallUnary,      "isnan");
            Add(Instruction.Load,                     InstType.Special);
            Add(Instruction.Lod,                      InstType.Special);
            Add(Instruction.LogarithmB2,              InstType.CallUnary,      "log2");
            Add(Instruction.LogicalAnd,               InstType.OpBinaryCom,    "&&");
            Add(Instruction.LogicalExclusiveOr,       InstType.OpBinaryCom,    "^");
            Add(Instruction.LogicalNot,               InstType.OpUnary,        "!");
            Add(Instruction.LogicalOr,                InstType.OpBinaryCom,    "||");
            Add(Instruction.LoopBreak,                InstType.OpNullary,      "break");
            Add(Instruction.LoopContinue,             InstType.OpNullary,      "continue");

#pragma warning restore IDE0055
        }

        private static void Add(Instruction inst, InstType flags, string opName = null)
        {
            _infoTable[(int)inst] = new InstInfo(flags, opName);
        }
    }
}
