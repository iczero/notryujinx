using ARMeilleure.CodeGen.RegisterAllocators;
using ARMeilleure.IntermediateRepresentation;
using System;
using System.Collections.Generic;
using System.IO;

namespace ARMeilleure.CodeGen.Arm64
{
    class CodeGenContext
    {
        private const int BccInstLength = 4;
        private const int CbnzInstLength = 4;

        private Stream _stream;

        public int StreamOffset => (int)_stream.Length;

        public AllocationResult AllocResult { get; }

        public Assembler Assembler { get; }

        public BasicBlock CurrBlock { get; private set; }

        public bool HasCall { get; }

        public int CallArgsRegionSize { get; }
        public int FpLrSaveRegionSize { get; }

        private readonly Dictionary<BasicBlock, long> _visitedBlocks;
        private readonly Dictionary<BasicBlock, List<(ArmCondition Condition, long BranchPos)>> _pendingBranches;

        private ArmCondition _jNearCondition;
        private Operand _jNearValue;

        private long _jNearPosition;

        public CodeGenContext(AllocationResult allocResult, int maxCallArgs, int blocksCount, bool relocatable)
        {
            _stream = new MemoryStream();

            AllocResult = allocResult;

            Assembler = new Assembler(_stream, relocatable);

            bool hasCall = maxCallArgs >= 0;

            HasCall = hasCall;

            if (maxCallArgs < 0)
            {
                maxCallArgs = 0;
            }

            CallArgsRegionSize = maxCallArgs * 16;
            FpLrSaveRegionSize = hasCall ? 16 : 0;

            _visitedBlocks = new Dictionary<BasicBlock, long>();
            _pendingBranches = new Dictionary<BasicBlock, List<(ArmCondition, long)>>();
        }

        public void EnterBlock(BasicBlock block)
        {
            CurrBlock = block;

            long target = _stream.Position;

            if (_pendingBranches.TryGetValue(block, out var list))
            {
                foreach (var tuple in list)
                {
                    _stream.Seek(tuple.BranchPos, SeekOrigin.Begin);
                    WriteBranch(tuple.Condition, target);
                }

                _stream.Seek(target, SeekOrigin.Begin);
                _pendingBranches.Remove(block);
            }

            _visitedBlocks.Add(block, target);
        }

        public void JumpTo(BasicBlock target)
        {
            JumpTo(ArmCondition.Al, target);
        }

        public void JumpTo(ArmCondition condition, BasicBlock target)
        {
            if (_visitedBlocks.TryGetValue(target, out long offset))
            {
                WriteBranch(condition, offset);
            }
            else
            {
                if (!_pendingBranches.TryGetValue(target, out var list))
                {
                    list = new List<(ArmCondition, long)>();
                    _pendingBranches.Add(target, list);
                }

                list.Add((condition, _stream.Position));

                _stream.Seek(BccInstLength, SeekOrigin.Current);
            }
        }

        private void WriteBranch(ArmCondition condition, long to)
        {
            int imm = checked((int)(to - _stream.Position));

            if (condition != ArmCondition.Al)
            {
                Assembler.B(condition, imm);
            }
            else
            {
                Assembler.B(imm);
            }
        }

        public void JumpToNear(ArmCondition condition)
        {
            _jNearCondition = condition;
            _jNearPosition = _stream.Position;

            _stream.Seek(BccInstLength, SeekOrigin.Current);
        }

        public void JumpToNearIfNotZero(Operand value)
        {
            _jNearValue = value;
            _jNearPosition = _stream.Position;

            _stream.Seek(CbnzInstLength, SeekOrigin.Current);
        }

        public void JumpHere()
        {
            long currentPosition = _stream.Position;
            long offset = currentPosition - _jNearPosition;

            _stream.Seek(_jNearPosition, SeekOrigin.Begin);

            if (_jNearValue != default)
            {
                Assembler.Cbnz(_jNearValue, checked((int)offset));
                _jNearValue = default;
            }
            else
            {
                Assembler.B(_jNearCondition, checked((int)offset));
            }

            _stream.Seek(currentPosition, SeekOrigin.Begin);
        }
    }
}