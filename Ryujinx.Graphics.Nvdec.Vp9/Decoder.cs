﻿using Ryujinx.Common.Memory;
using Ryujinx.Graphics.Nvdec.Vp9.Types;
using Ryujinx.Graphics.Video;
using System;
using MvRef = Ryujinx.Graphics.Video.MvRef;

namespace Ryujinx.Graphics.Nvdec.Vp9
{
    public class Decoder : IVp9Decoder
    {
        public bool IsHardwareAccelerated => false;

        public ISurface CreateSurface(int width, int height) => new Surface(width, height);

        private static readonly byte[] LiteralToFilter = new byte[]
        {
            Constants.EightTapSmooth,
            Constants.EightTap,
            Constants.EightTapSharp,
            Constants.Bilinear
        };

        public unsafe void Decode(
            ref Vp9PictureInfo pictureInfo,
            ISurface output,
            ReadOnlySpan<byte> bitstream,
            ReadOnlySpan<MvRef> mvsIn,
            Span<MvRef> mvsOut)
        {
            Vp9Common cm = new Vp9Common();

            cm.FrameType = pictureInfo.IsKeyFrame ? FrameType.KeyFrame : FrameType.InterFrame;
            cm.IntraOnly = pictureInfo.IntraOnly;

            cm.Width = pictureInfo.Width;
            cm.Height = pictureInfo.Height;

            cm.UsePrevFrameMvs = pictureInfo.UsePrevInFindMvRefs;

            cm.RefFrameSignBias = pictureInfo.RefFrameSignBias;

            cm.BaseQindex = pictureInfo.BaseQIndex;
            cm.YDcDeltaQ = pictureInfo.YDcDeltaQ;
            cm.UvAcDeltaQ = pictureInfo.UvAcDeltaQ;
            cm.UvDcDeltaQ = pictureInfo.UvDcDeltaQ;

            cm.Mb.Lossless = pictureInfo.Lossless;

            cm.TxMode = (TxMode)pictureInfo.TransformMode;

            cm.AllowHighPrecisionMv = pictureInfo.AllowHighPrecisionMv;

            cm.InterpFilter = (byte)pictureInfo.InterpFilter;

            if (cm.InterpFilter != Constants.Switchable)
            {
                cm.InterpFilter = LiteralToFilter[cm.InterpFilter];
            }

            cm.ReferenceMode = (ReferenceMode)pictureInfo.ReferenceMode;

            cm.CompFixedRef = pictureInfo.CompFixedRef;
            cm.CompVarRef = pictureInfo.CompVarRef;

            cm.Log2TileCols = pictureInfo.Log2TileCols;
            cm.Log2TileRows = pictureInfo.Log2TileRows;

            cm.Seg.Enabled = pictureInfo.SegmentEnabled;
            cm.Seg.UpdateMap = pictureInfo.SegmentMapUpdate;
            cm.Seg.TemporalUpdate = pictureInfo.SegmentMapTemporalUpdate;
            cm.Seg.AbsDelta = (byte)pictureInfo.SegmentAbsDelta;
            cm.Seg.FeatureMask = pictureInfo.SegmentFeatureEnable;
            cm.Seg.FeatureData = pictureInfo.SegmentFeatureData;

            cm.Lf.ModeRefDeltaEnabled = pictureInfo.ModeRefDeltaEnabled;
            cm.Lf.RefDeltas = pictureInfo.RefDeltas;
            cm.Lf.ModeDeltas = pictureInfo.ModeDeltas;

            cm.Fc = new Ptr<Vp9EntropyProbs>(ref pictureInfo.Entropy);
            cm.Counts = new Ptr<Vp9BackwardUpdates>(ref pictureInfo.BackwardUpdateCounts);

            cm.FrameRefs[0].Buf = (Surface)pictureInfo.LastReference;
            cm.FrameRefs[1].Buf = (Surface)pictureInfo.GoldenReference;
            cm.FrameRefs[2].Buf = (Surface)pictureInfo.AltReference;
            cm.Mb.CurBuf = (Surface)output;

            cm.Mb.SetupBlockPlanes(1, 1);

            cm.InitializeTileWorkerData(1 << pictureInfo.Log2TileCols, 1 << pictureInfo.Log2TileRows);

            cm.AllocContextBuffers(pictureInfo.Width, pictureInfo.Height);
            cm.InitContextBuffers();
            cm.SetupSegmentationDequant();
            cm.SetupScaleFactors();

            SetMvs(ref cm, mvsIn);

            fixed (byte* dataPtr = bitstream)
            {
                DecodeFrame.DecodeTiles(ref cm, new ArrayPtr<byte>(dataPtr, bitstream.Length));
            }

            GetMvs(ref cm, mvsOut);

            cm.FreeContextBuffers();
        }

        public bool ReceiveFrame(ISurface surface)
        {
            throw new NotImplementedException();
        }

        private static void SetMvs(ref Vp9Common cm, ReadOnlySpan<MvRef> mvs)
        {
            if (mvs.Length > cm.PrevFrameMvs.Length)
            {
                throw new ArgumentException($"Size mismatch, expected: {cm.PrevFrameMvs.Length}, but got: {mvs.Length}.");
            }

            for (int i = 0; i < mvs.Length; i++)
            {
                ref var mv = ref cm.PrevFrameMvs[i];

                mv.Mv[0].Row = mvs[i].Mvs[0].Row;
                mv.Mv[0].Col = mvs[i].Mvs[0].Col;
                mv.Mv[1].Row = mvs[i].Mvs[1].Row;
                mv.Mv[1].Col = mvs[i].Mvs[1].Col;

                mv.RefFrame[0] = (sbyte)mvs[i].RefFrames[0];
                mv.RefFrame[1] = (sbyte)mvs[i].RefFrames[1];
            }
        }

        private static void GetMvs(ref Vp9Common cm, Span<MvRef> mvs)
        {
            if (mvs.Length > cm.CurFrameMvs.Length)
            {
                throw new ArgumentException($"Size mismatch, expected: {cm.CurFrameMvs.Length}, but got: {mvs.Length}.");
            }

            for (int i = 0; i < mvs.Length; i++)
            {
                ref var mv = ref cm.CurFrameMvs[i];

                mvs[i].Mvs[0].Row = mv.Mv[0].Row;
                mvs[i].Mvs[0].Col = mv.Mv[0].Col;
                mvs[i].Mvs[1].Row = mv.Mv[1].Row;
                mvs[i].Mvs[1].Col = mv.Mv[1].Col;

                mvs[i].RefFrames[0] = mv.RefFrame[0];
                mvs[i].RefFrames[1] = mv.RefFrame[1];
            }
        }        
    }
}
