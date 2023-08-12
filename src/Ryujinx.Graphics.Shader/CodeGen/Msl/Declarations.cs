using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using Ryujinx.Graphics.Shader.StructuredIr;
using Ryujinx.Graphics.Shader.Translation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Ryujinx.Graphics.Shader.CodeGen.Msl
{
    static class Declarations
    {
        public static void Declare(CodeGenContext context, StructuredProgramInfo info)
        {
            context.AppendLine("#include <metal_stdlib>");
            context.AppendLine("#include <simd/simd.h>");
            context.AppendLine();
            context.AppendLine("using namespace metal;");
            context.AppendLine();

            if ((info.HelperFunctionsMask & HelperFunctionsMask.SwizzleAdd) != 0)
            {

            }

            // DeclareInputAttributes(context, info.IoDefinitions.Where(x => IsUserDefined(x, StorageKind.Input)));
        }

        static bool IsUserDefined(IoDefinition ioDefinition, StorageKind storageKind)
        {
            return ioDefinition.StorageKind == storageKind && ioDefinition.IoVariable == IoVariable.UserDefined;
        }

        public static void DeclareLocals(CodeGenContext context, StructuredFunction function)
        {
            foreach (AstOperand decl in function.Locals)
            {
                string name = context.OperandManager.DeclareLocal(decl);

                context.AppendLine(GetVarTypeName(context, decl.VarType) + " " + name + ";");
            }
        }

        public static string GetVarTypeName(CodeGenContext context, AggregateType type)
        {
            return type switch
            {
                AggregateType.Void => "void",
                AggregateType.Bool => "bool",
                AggregateType.FP32 => "float",
                AggregateType.S32 => "int",
                AggregateType.U32 => "uint",
                AggregateType.Vector2 | AggregateType.Bool => "bool2",
                AggregateType.Vector2 | AggregateType.FP32 => "float2",
                AggregateType.Vector2 | AggregateType.S32 => "int2",
                AggregateType.Vector2 | AggregateType.U32 => "uint2",
                AggregateType.Vector3 | AggregateType.Bool => "bool3",
                AggregateType.Vector3 | AggregateType.FP32 => "float3",
                AggregateType.Vector3 | AggregateType.S32 => "int3",
                AggregateType.Vector3 | AggregateType.U32 => "uint3",
                AggregateType.Vector4 | AggregateType.Bool => "bool4",
                AggregateType.Vector4 | AggregateType.FP32 => "float4",
                AggregateType.Vector4 | AggregateType.S32 => "int4",
                AggregateType.Vector4 | AggregateType.U32 => "uint4",
                _ => throw new ArgumentException($"Invalid variable type \"{type}\"."),
            };
        }

        // TODO: Redo for new Shader IR rep
        // private static void DeclareInputAttributes(CodeGenContext context, IEnumerable<IoDefinition> inputs)
        // {
        //     if (context.AttributeUsage.UsedInputAttributes != 0)
        //     {
        //         context.AppendLine("struct VertexIn");
        //         context.EnterScope();
        //
        //         int usedAttributes = context.AttributeUsage.UsedInputAttributes | context.AttributeUsage.PassthroughAttributes;
        //         while (usedAttributes != 0)
        //         {
        //             int index = BitOperations.TrailingZeroCount(usedAttributes);
        //
        //             string name = $"{DefaultNames.IAttributePrefix}{index}";
        //             var type = context.AttributeUsage.get .QueryAttributeType(index).ToVec4Type(TargetLanguage.Msl);
        //             context.AppendLine($"{type} {name} [[attribute({index})]];");
        //
        //             usedAttributes &= ~(1 << index);
        //         }
        //
        //         context.LeaveScope(";");
        //     }
        // }
    }
}
