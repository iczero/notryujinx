using ARMeilleure.IntermediateRepresentation;
using System;

namespace ARMeilleure.CodeGen.Arm64
{
    static class CodeGeneratorIntrinsic
    {
        public static void GenerateOperation(CodeGenContext context, Operation operation)
        {
            Intrinsic intrin = operation.Intrinsic;

            IntrinsicInfo info = IntrinsicTable.GetInfo(intrin & ~(Intrinsic.Arm64VTypeMask | Intrinsic.Arm64VSizeMask));

            switch (info.Type)
            {
                case IntrinsicType.ftypeRmRnRd:
                    GenerateScalarBinaryFP(
                        context,
                        (uint)(intrin & Intrinsic.Arm64VSizeMask) >> (int)Intrinsic.Arm64VSizeShift,
                        info.Inst,
                        operation.Destination,
                        operation.GetSource(0),
                        operation.GetSource(1));
                    break;
                case IntrinsicType.QszRmRnRd:
                    GenerateVectorBinaryFP(
                        context,
                        (uint)(intrin & Intrinsic.Arm64VTypeMask) >> (int)Intrinsic.Arm64VTypeShift,
                        (uint)(intrin & Intrinsic.Arm64VSizeMask) >> (int)Intrinsic.Arm64VSizeShift,
                        info.Inst,
                        operation.Destination,
                        operation.GetSource(0),
                        operation.GetSource(1));
                    break;
                default:
                    throw new NotImplementedException(info.Type.ToString());
            }
        }

        private static void GenerateScalarBinaryFP(
            CodeGenContext context,
            uint sz,
            uint instruction,
            Operand rd,
            Operand rn,
            Operand rm)
        {
            instruction |= (sz << 22);

            context.Assembler.WriteInstructionRm16(instruction, rd, rn, rm);
        }

        private static void GenerateVectorBinaryFP(
            CodeGenContext context,
            uint q,
            uint sz,
            uint instruction,
            Operand rd,
            Operand rn,
            Operand rm)
        {
            instruction |= (q << 30) | (sz << 22);

            context.Assembler.WriteInstructionRm16(instruction, rd, rn, rm);
        }
    }
}