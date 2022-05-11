using ARMeilleure.Decoders;
using ARMeilleure.IntermediateRepresentation;
using ARMeilleure.Translation;

using static ARMeilleure.Instructions.InstEmitHelper;

namespace ARMeilleure.Instructions
{
    static class InstEmitSimdNativeArmHelper
    {
        public static void EmitScalarBinaryOpF(ArmEmitterContext context, Intrinsic inst)
        {
            OpCodeSimdReg op = (OpCodeSimdReg)context.CurrOp;

            Operand n = GetVec(op.Rn);
            Operand m = GetVec(op.Rm);

            if ((op.Size & 1) != 0)
            {
                inst |= Intrinsic.Arm64VDouble;
            }

            context.Copy(GetVec(op.Rd), context.AddIntrinsic(inst, n, m));
        }

        public static void EmitVectorBinaryOpF(ArmEmitterContext context, Intrinsic inst)
        {
            OpCodeSimdReg op = (OpCodeSimdReg)context.CurrOp;

            Operand n = GetVec(op.Rn);
            Operand m = GetVec(op.Rm);

            if ((op.Size & 1) != 0)
            {
                inst |= Intrinsic.Arm64VDouble;
            }

            if (op.RegisterSize == RegisterSize.Simd128)
            {
                inst |= Intrinsic.Arm64V128;
            }

            context.Copy(GetVec(op.Rd), context.AddIntrinsic(inst, n, m));
        }
    }
}