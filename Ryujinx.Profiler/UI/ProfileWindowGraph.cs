﻿using System;
using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace Ryujinx.Profiler.UI
{
    public partial class ProfileWindow
    {
        private const float GraphMoveSpeed = 20000;
        private const float GraphZoomSpeed = 10;

        private float _graphZoom      = 1;
        private float  _graphPosition = 0;

        private void DrawGraph(float xOffset, float yOffset, float width)
        {
            if (_sortedProfileData.Count != 0)
            {
                int   left, right;
                float top, bottom;

                int   verticalIndex      = 0;
                float barHeight          = (LineHeight - LinePadding);
                long  history            = Profile.HistoryLength;
                long  timeWidthTicks     = (long)(history / (double)_graphZoom);
                long  graphPositionTicks = Profile.ConvertMSToTicks(_graphPosition);

                // Reset start point if out of bounds
                if (timeWidthTicks + graphPositionTicks > history)
                {
                    graphPositionTicks = history - timeWidthTicks;
                    _graphPosition = (float)Profile.ConvertTicksToMS(graphPositionTicks);
                }

                GL.Enable(EnableCap.ScissorTest);
                GL.Begin(PrimitiveType.Triangles);
                foreach (var entry in _sortedProfileData)
                {
                    GL.Color3(Color.Green);
                    foreach (Timestamp timestamp in entry.Value.GetAllTimestamps())
                    {
                        left   = (int)(xOffset + width - ((float)(_captureTime - (timestamp.BeginTime + graphPositionTicks)) / timeWidthTicks) * width);
                        right  = (int)(xOffset + width - ((float)(_captureTime - (timestamp.EndTime + graphPositionTicks)) / timeWidthTicks) * width);
                        bottom = GetLineY(yOffset, LineHeight, LinePadding, true, verticalIndex);
                        top    = bottom + barHeight;

                        // Make sure width is at least 1px
                        right = Math.Max(left + 1, right);

                        // Skip rendering out of bounds bars
                        if (top < 0 || bottom > Height)
                            continue;

                        GL.Vertex2(left,  bottom);
                        GL.Vertex2(left,  top);
                        GL.Vertex2(right, top);

                        GL.Vertex2(right, top);
                        GL.Vertex2(right, bottom);
                        GL.Vertex2(left,  bottom);
                    }

                    GL.Color3(Color.Red);
                    // Currently capturing timestamp
                    long entryBegin = entry.Value.BeginTime;
                    if (entryBegin != -1)
                    {
                        left   = (int)(xOffset + width + _graphPosition - (((float)_captureTime - entryBegin) / timeWidthTicks) * width);
                        bottom = GetLineY(yOffset, LineHeight, LinePadding, true, verticalIndex);
                        top    = bottom + barHeight;
                        right  = (int)(xOffset + width);

                        // Make sure width is at least 1px
                        left = Math.Min(left - 1, right);

                        // Skip rendering out of bounds bars
                        if (top < 0 || bottom > Height)
                            continue;

                        GL.Vertex2(left,  bottom);
                        GL.Vertex2(left,  top);
                        GL.Vertex2(right, top);

                        GL.Vertex2(right, top);
                        GL.Vertex2(right, bottom);
                        GL.Vertex2(left,  bottom);
                    }

                    verticalIndex++;
                }

                GL.End();
                GL.Disable(EnableCap.ScissorTest);

                string label = $"-{MathF.Round(_graphPosition, 2)} ms";

                // Dummy draw for measure
                float labelWidth = _fontService.DrawText(label, 0, 0, LineHeight, false);
                _fontService.DrawText(label, xOffset + width - labelWidth - LinePadding, FilterHeight + LinePadding, LineHeight);
                
                _fontService.DrawText($"-{MathF.Round((float)(Profile.ConvertTicksToMS(timeWidthTicks) + _graphPosition), 2)} ms", xOffset + LinePadding, FilterHeight + LinePadding, LineHeight);
            }
        }
    }
}
