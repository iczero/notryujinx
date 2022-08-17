using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using shaderc;
using Silk.NET.Vulkan;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Ryujinx.Graphics.Vulkan
{
    class Shader : IDisposable
    {
        // The shaderc.net dependency's Options constructor and dispose are not thread safe.
        // Take this lock when using them.
        private static object _shaderOptionsLock = new object();

        private static readonly IntPtr _ptrMainEntryPointName = Marshal.StringToHGlobalAnsi("main");

        private readonly Vk _api;
        private readonly Device _device;
        private readonly ShaderStageFlags _stage;

        private bool _disposed;
        private ShaderSpecializationInfo _shaderSpecializationInfo;
        private ShaderModule _module;
        private NativeArray<SpecializationMapEntry> _mapEntries;
        private NativeArray<SpecializationInfo> _specializationInfo;

        public ShaderStageFlags StageFlags => _stage;

        public ShaderBindings Bindings { get; }

        public ProgramLinkStatus CompileStatus { private set; get; }

        public readonly Task CompileTask;

        public unsafe Shader(Vk api, Device device, ShaderSource shaderSource, ShaderSpecializationInfo specializationInfo = null)
        {
            _api = api;
            _device = device;
            Bindings = shaderSource.Bindings;

            CompileStatus = ProgramLinkStatus.Incomplete;

            _stage = shaderSource.Stage.Convert();

            _shaderSpecializationInfo = specializationInfo;

            CompileTask = Task.Run(() =>
            {
                byte[] spirv = shaderSource.BinaryCode;

                if (spirv == null)
                {
                    spirv = GlslToSpirv(shaderSource.Code, shaderSource.Stage);

                    if (spirv == null)
                    {
                        CompileStatus = ProgramLinkStatus.Failure;

                        return;
                    }
                }

                fixed (byte* pCode = spirv)
                {
                    var shaderModuleCreateInfo = new ShaderModuleCreateInfo()
                    {
                        SType = StructureType.ShaderModuleCreateInfo,
                        CodeSize = (uint)spirv.Length,
                        PCode = (uint*)pCode
                    };

                    api.CreateShaderModule(device, shaderModuleCreateInfo, null, out _module).ThrowOnError();
                }

                CompileStatus = ProgramLinkStatus.Success;
            });
        }

        private unsafe static byte[] GlslToSpirv(string glsl, ShaderStage stage)
        {
            // TODO: We should generate the correct code on the shader translator instead of doing this compensation.
            glsl = glsl.Replace("gl_VertexID", "(gl_VertexIndex - gl_BaseVertex)");
            glsl = glsl.Replace("gl_InstanceID", "(gl_InstanceIndex - gl_BaseInstance)");

            Options options;

            lock (_shaderOptionsLock)
            {
                options = new Options(false)
                {
                    SourceLanguage = SourceLanguage.Glsl,
                    TargetSpirVVersion = new SpirVVersion(1, 5)
                };
            }

            options.SetTargetEnvironment(TargetEnvironment.Vulkan, EnvironmentVersion.Vulkan_1_2);
            Compiler compiler = new Compiler(options);
            var scr = compiler.Compile(glsl, "Ryu", GetShaderCShaderStage(stage));

            lock (_shaderOptionsLock)
            {
                options.Dispose();
            }

            if (scr.Status != Status.Success)
            {
                Logger.Error?.Print(LogClass.Gpu, $"Shader compilation error: {scr.Status} {scr.ErrorMessage}");

                return null;
            }

            var spirvBytes = new Span<byte>((void*)scr.CodePointer, (int)scr.CodeLength);

            byte[] code = new byte[(scr.CodeLength + 3) & ~3];

            spirvBytes.CopyTo(code.AsSpan().Slice(0, (int)scr.CodeLength));

            return code;
        }

        private static ShaderKind GetShaderCShaderStage(ShaderStage stage)
        {
            switch (stage)
            {
                case ShaderStage.Vertex:
                    return ShaderKind.GlslVertexShader;
                case ShaderStage.Geometry:
                    return ShaderKind.GlslGeometryShader;
                case ShaderStage.TessellationControl:
                    return ShaderKind.GlslTessControlShader;
                case ShaderStage.TessellationEvaluation:
                    return ShaderKind.GlslTessEvaluationShader;
                case ShaderStage.Fragment:
                    return ShaderKind.GlslFragmentShader;
                case ShaderStage.Compute:
                    return ShaderKind.GlslComputeShader;
            }

            Logger.Debug?.Print(LogClass.Gpu, $"Invalid {nameof(ShaderStage)} enum value: {stage}.");

            return ShaderKind.GlslVertexShader;
        }

        public unsafe PipelineShaderStageCreateInfo GetInfo()
        {
            if (_shaderSpecializationInfo != null)
            {
                var mapEntries = this._shaderSpecializationInfo.Entries.Select(x => new SpecializationMapEntry()
                {
                    ConstantID = x.Location,
                    Offset = x.Offset,
                    Size = x.Size
                }).ToArray();

                _mapEntries = new NativeArray<SpecializationMapEntry>(mapEntries.Length);

                for (int i = 0; i < mapEntries.Length; i++)
                {
                    _mapEntries[i] = mapEntries[i];
                }

                _specializationInfo = new NativeArray<SpecializationInfo>(1);

                _specializationInfo[0] = new SpecializationInfo()
                {
                    MapEntryCount = (uint)_mapEntries.Length,
                    PMapEntries = _mapEntries.Pointer,
                    DataSize = (nuint)_shaderSpecializationInfo.Data.Length,
                    PData = _shaderSpecializationInfo.Data.Pointer
                };
            }

            return new PipelineShaderStageCreateInfo()
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = _stage,
                Module = _module,
                PName = (byte*)_ptrMainEntryPointName,
                PSpecializationInfo = _shaderSpecializationInfo != null ? _specializationInfo.Pointer : null
            };
        }

        public void WaitForCompile()
        {
            CompileTask.Wait();
        }

        public unsafe void Dispose()
        {
            if (!_disposed)
            {
                _api.DestroyShaderModule(_device, _module, null);
                _disposed = true;
                _mapEntries?.Dispose();
                _shaderSpecializationInfo?.Dispose();
                _specializationInfo?.Dispose();
            }
        }
    }
}
