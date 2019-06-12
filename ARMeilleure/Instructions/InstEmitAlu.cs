using ARMeilleure.Decoders;
using ARMeilleure.IntermediateRepresentation;
using ARMeilleure.State;
using ARMeilleure.Translation;

using static ARMeilleure.Instructions.InstEmitAluHelper;
using static ARMeilleure.Instructions.InstEmitHelper;
using static ARMeilleure.IntermediateRepresentation.OperandHelper;

namespace ARMeilleure.Instructions
{
    static partial class InstEmit
    {
        public static void Adc(EmitterContext context)  => EmitAdc(context, setFlags: false);
        public static void Adcs(EmitterContext context) => EmitAdc(context, setFlags: true);

        private static void EmitAdc(EmitterContext context, bool setFlags)
        {
            Operand n = GetAluN(context);
            Operand m = GetAluM(context);

            Operand d = context.Add(n, m);

            Operand carry = GetFlag(PState.CFlag);

            if (context.CurrOp.RegisterSize == RegisterSize.Int64)
            {
                carry = context.Copy(Local(OperandType.I64), carry);
            }

            d = context.Add(d, carry);

            if (setFlags)
            {
                EmitNZFlagsCheck(context, d);

                EmitAdcsCCheck(context, n, d);
                EmitAddsVCheck(context, n, m, d);
            }

            SetAluDOrZR(context, d);
        }

        public static void Add(EmitterContext context)
        {
            SetAluD(context, context.Add(GetAluN(context), GetAluM(context)));
        }

        public static void Adds(EmitterContext context)
        {
            Operand n = GetAluN(context);
            Operand m = GetAluM(context);

            Operand d = context.Add(n, m);

            EmitNZFlagsCheck(context, d);

            EmitAddsCCheck(context, n, d);
            EmitAddsVCheck(context, n, m, d);

            SetAluDOrZR(context, d);
        }

        public static void And(EmitterContext context)
        {
            SetAluD(context, context.BitwiseAnd(GetAluN(context), GetAluM(context)));
        }

        public static void Ands(EmitterContext context)
        {
            Operand n = GetAluN(context);
            Operand m = GetAluM(context);

            Operand d = context.BitwiseAnd(n, m);

            EmitNZFlagsCheck(context, d);
            EmitCVFlagsClear(context);

            SetAluDOrZR(context, d);
        }

        public static void Asrv(EmitterContext context)
        {
            SetAluDOrZR(context, context.ShiftRightSI(GetAluN(context), GetAluMShift(context)));
        }

        public static void Bic(EmitterContext context)  => EmitBic(context, setFlags: false);
        public static void Bics(EmitterContext context) => EmitBic(context, setFlags: true);

        private static void EmitBic(EmitterContext context, bool setFlags)
        {
            Operand n = GetAluN(context);
            Operand m = GetAluM(context);

            Operand d = context.BitwiseAnd(n, context.BitwiseNot(m));

            if (setFlags)
            {
                EmitNZFlagsCheck(context, d);
                EmitCVFlagsClear(context);
            }

            SetAluD(context, d, setFlags);
        }

        public static void Cls(EmitterContext context)
        {
            OpCodeAlu op = (OpCodeAlu)context.CurrOp;

            Operand n = GetIntOrZR(op, op.Rn);

            Operand nHigh = context.ShiftRightUI(n, Const(1));

            bool is32Bits = op.RegisterSize == RegisterSize.Int32;

            Operand mask = is32Bits ? Const(int.MaxValue) : Const(long.MaxValue);

            Operand nLow = context.BitwiseAnd(n, mask);

            Operand res = context.CountLeadingZeros(context.BitwiseExclusiveOr(nHigh, nLow));

            res = context.Subtract(res, Const(res.Type, 1));

            SetAluDOrZR(context, res);
        }

        public static void Clz(EmitterContext context)
        {
            OpCodeAlu op = (OpCodeAlu)context.CurrOp;

            Operand n = GetIntOrZR(op, op.Rn);

            Operand d = context.CountLeadingZeros(n);

            SetAluDOrZR(context, d);
        }

        public static void Eon(EmitterContext context)
        {
            Operand n = GetAluN(context);
            Operand m = GetAluM(context);

            Operand d = context.BitwiseExclusiveOr(n, context.BitwiseNot(m));

            SetAluD(context, d);
        }

        public static void Eor(EmitterContext context)
        {
            SetAluD(context, context.BitwiseExclusiveOr(GetAluN(context), GetAluM(context)));
        }

        public static void Extr(EmitterContext context)
        {
            OpCodeAluRs op = (OpCodeAluRs)context.CurrOp;

            Operand res = GetIntOrZR(op, op.Rm);

            if (op.Shift != 0)
            {
                if (op.Rn == op.Rm)
                {
                    res = context.RotateRight(res, Const(op.Shift));
                }
                else
                {
                    res = context.ShiftRightUI(res, Const(op.Shift));

                    Operand n = GetIntOrZR(op, op.Rn);

                    int invShift = op.GetBitsCount() - op.Shift;

                    res = context.BitwiseOr(res, context.ShiftLeft(n, Const(invShift)));
                }
            }

            SetAluDOrZR(context, res);
        }

        public static void Lslv(EmitterContext context)
        {
            SetAluDOrZR(context, context.ShiftLeft(GetAluN(context), GetAluMShift(context)));
        }

        public static void Lsrv(EmitterContext context)
        {
            SetAluDOrZR(context, context.ShiftRightUI(GetAluN(context), GetAluMShift(context)));
        }

        public static void Sbc(EmitterContext context)  => EmitSbc(context, setFlags: false);
        public static void Sbcs(EmitterContext context) => EmitSbc(context, setFlags: true);

        private static void EmitSbc(EmitterContext context, bool setFlags)
        {
            Operand n = GetAluN(context);
            Operand m = GetAluM(context);

            Operand d = context.Subtract(n, m);

            Operand borrow = context.BitwiseExclusiveOr(GetFlag(PState.CFlag), Const(1));

            if (context.CurrOp.RegisterSize == RegisterSize.Int64)
            {
                borrow = context.Copy(Local(OperandType.I64), borrow);
            }

            d = context.Subtract(d, borrow);

            if (setFlags)
            {
                EmitNZFlagsCheck(context, d);

                EmitSbcsCCheck(context, n, m);
                EmitSubsVCheck(context, n, m, d);
            }

            SetAluDOrZR(context, d);
        }

        public static void Sub(EmitterContext context)
        {
            SetAluD(context, context.Subtract(GetAluN(context), GetAluM(context)));
        }

        public static void Subs(EmitterContext context)
        {
            Operand n = GetAluN(context);
            Operand m = GetAluM(context);

            Operand d = context.Subtract(n, m);

            EmitNZFlagsCheck(context, d);

            EmitSubsCCheck(context, n, m);
            EmitSubsVCheck(context, n, m, d);

            SetAluDOrZR(context, d);
        }

        public static void Orn(EmitterContext context)
        {
            Operand n = GetAluN(context);
            Operand m = GetAluM(context);

            Operand d = context.BitwiseOr(n, context.BitwiseNot(m));

            SetAluD(context, d);
        }

        public static void Orr(EmitterContext context)
        {
            SetAluD(context, context.BitwiseOr(GetAluN(context), GetAluM(context)));
        }

        public static void Rbit(EmitterContext context) => EmitCall32_64(context,
            nameof(SoftFallback.ReverseBits32),
            nameof(SoftFallback.ReverseBits64));

        public static void Rev16(EmitterContext context) => EmitCall32_64(context,
            nameof(SoftFallback.ReverseBytes16_32),
            nameof(SoftFallback.ReverseBytes16_64));

        public static void Rev32(EmitterContext context)
        {
            OpCodeAlu op = (OpCodeAlu)context.CurrOp;

            Operand n = GetIntOrZR(op, op.Rn);

            if (op.RegisterSize == RegisterSize.Int32)
            {
                SetAluDOrZR(context, context.ByteSwap(n));
            }
            else
            {
                EmitCall32_64(context, null, nameof(SoftFallback.ReverseBytes32_64));
            }
        }

        private static void EmitCall32_64(EmitterContext context, string name32, string name64)
        {
            OpCodeAlu op = (OpCodeAlu)context.CurrOp;

            Operand n = GetIntOrZR(op, op.Rn);
            Operand d;

            if (op.RegisterSize == RegisterSize.Int32)
            {
                d = context.Call(typeof(SoftFallback).GetMethod(name32), n);
            }
            else
            {
                d = context.Call(typeof(SoftFallback).GetMethod(name64), n);
            }

            SetAluDOrZR(context, d);
        }

        public static void Rev64(EmitterContext context)
        {
            OpCodeAlu op = (OpCodeAlu)context.CurrOp;

            SetAluDOrZR(context, context.ByteSwap(GetIntOrZR(op, op.Rn)));
        }

        public static void Rorv(EmitterContext context)
        {
            SetAluDOrZR(context, context.RotateRight(GetAluN(context), GetAluMShift(context)));
        }

        private static Operand GetAluMShift(EmitterContext context)
        {
            IOpCodeAluRs op = (IOpCodeAluRs)context.CurrOp;

            Operand m = GetIntOrZR(op, op.Rm);

            if (op.RegisterSize == RegisterSize.Int64)
            {
                m = context.Copy(Local(OperandType.I32), m);
            }

            return context.BitwiseAnd(m, Const(context.CurrOp.GetBitsCount() - 1));
        }

        private static void EmitNZFlagsCheck(EmitterContext context, Operand d)
        {
            context.Copy(GetFlag(PState.NFlag), context.ICompareLess (d, Const(d.Type, 0)));
            context.Copy(GetFlag(PState.ZFlag), context.ICompareEqual(d, Const(d.Type, 0)));
        }

        private static void EmitCVFlagsClear(EmitterContext context)
        {
            context.Copy(GetFlag(PState.CFlag), Const(0));
            context.Copy(GetFlag(PState.VFlag), Const(0));
        }

        public static void SetAluD(EmitterContext context, Operand d)
        {
            SetAluD(context, d, x31IsZR: false);
        }

        public static void SetAluDOrZR(EmitterContext context, Operand d)
        {
            SetAluD(context, d, x31IsZR: true);
        }

        public static void SetAluD(EmitterContext context, Operand d, bool x31IsZR)
        {
            IOpCodeAlu op = (IOpCodeAlu)context.CurrOp;

            if ((x31IsZR || op is IOpCodeAluRs) && op.Rd == RegisterConsts.ZeroIndex)
            {
                return;
            }

            context.Copy(GetIntOrSP(op, op.Rd), d);
        }
    }
}
