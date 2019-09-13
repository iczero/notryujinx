﻿using System;

namespace Ryujinx.HLE.HOS.Services.Android.Types
{
    [Flags]
    enum HalTransform
    {
        FlipX     = 1,
        FlipY     = 2,
        Rotate90  = 4,
        Rotate180 = FlipX    | FlipY,
        Rotate270 = Rotate90 | Rotate180
    }
}