﻿using System.Diagnostics.CodeAnalysis;

namespace Gee.External.Capstone.Arm {
    /// <summary>
    ///     ARM Register Unique Identifier.
    /// </summary>
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public enum ArmRegisterId {
        /// <summary>
        ///     Indicates an invalid, or an uninitialized, register.
        /// </summary>
        Invalid = 0,
        ARM_REG_APSR,
        ARM_REG_APSR_NZCV,
        ARM_REG_CPSR,
        ARM_REG_FPEXC,
        ARM_REG_FPINST,
        ARM_REG_FPSCR,
        ARM_REG_FPSCR_NZCV,
        ARM_REG_FPSID,
        ARM_REG_ITSTATE,
        ARM_REG_LR,
        ARM_REG_PC,
        ARM_REG_SP,
        ARM_REG_SPSR,
        ARM_REG_D0,
        ARM_REG_D1,
        ARM_REG_D2,
        ARM_REG_D3,
        ARM_REG_D4,
        ARM_REG_D5,
        ARM_REG_D6,
        ARM_REG_D7,
        ARM_REG_D8,
        ARM_REG_D9,
        ARM_REG_D10,
        ARM_REG_D11,
        ARM_REG_D12,
        ARM_REG_D13,
        ARM_REG_D14,
        ARM_REG_D15,
        ARM_REG_D16,
        ARM_REG_D17,
        ARM_REG_D18,
        ARM_REG_D19,
        ARM_REG_D20,
        ARM_REG_D21,
        ARM_REG_D22,
        ARM_REG_D23,
        ARM_REG_D24,
        ARM_REG_D25,
        ARM_REG_D26,
        ARM_REG_D27,
        ARM_REG_D28,
        ARM_REG_D29,
        ARM_REG_D30,
        ARM_REG_D31,
        ARM_REG_FPINST2,
        ARM_REG_MVFR0,
        ARM_REG_MVFR1,
        ARM_REG_MVFR2,
        ARM_REG_Q0,
        ARM_REG_Q1,
        ARM_REG_Q2,
        ARM_REG_Q3,
        ARM_REG_Q4,
        ARM_REG_Q5,
        ARM_REG_Q6,
        ARM_REG_Q7,
        ARM_REG_Q8,
        ARM_REG_Q9,
        ARM_REG_Q10,
        ARM_REG_Q11,
        ARM_REG_Q12,
        ARM_REG_Q13,
        ARM_REG_Q14,
        ARM_REG_Q15,
        ARM_REG_R0,
        ARM_REG_R1,
        ARM_REG_R2,
        ARM_REG_R3,
        ARM_REG_R4,
        ARM_REG_R5,
        ARM_REG_R6,
        ARM_REG_R7,
        ARM_REG_R8,
        ARM_REG_R9,
        ARM_REG_R10,
        ARM_REG_R11,
        ARM_REG_R12,
        ARM_REG_S0,
        ARM_REG_S1,
        ARM_REG_S2,
        ARM_REG_S3,
        ARM_REG_S4,
        ARM_REG_S5,
        ARM_REG_S6,
        ARM_REG_S7,
        ARM_REG_S8,
        ARM_REG_S9,
        ARM_REG_S10,
        ARM_REG_S11,
        ARM_REG_S12,
        ARM_REG_S13,
        ARM_REG_S14,
        ARM_REG_S15,
        ARM_REG_S16,
        ARM_REG_S17,
        ARM_REG_S18,
        ARM_REG_S19,
        ARM_REG_S20,
        ARM_REG_S21,
        ARM_REG_S22,
        ARM_REG_S23,
        ARM_REG_S24,
        ARM_REG_S25,
        ARM_REG_S26,
        ARM_REG_S27,
        ARM_REG_S28,
        ARM_REG_S29,
        ARM_REG_S30,
        ARM_REG_S31,
        ARM_REG_R13 = ArmRegisterId.ARM_REG_SP,
        ARM_REG_R14 = ArmRegisterId.ARM_REG_LR,
        ARM_REG_R15 = ArmRegisterId.ARM_REG_PC,
        ARM_REG_SB = ArmRegisterId.ARM_REG_R9,
        ARM_REG_SL = ArmRegisterId.ARM_REG_R10,
        ARM_REG_FP = ArmRegisterId.ARM_REG_R11,
        ARM_REG_IP = ArmRegisterId.ARM_REG_R12
    }
}