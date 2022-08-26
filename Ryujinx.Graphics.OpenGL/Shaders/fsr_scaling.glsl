#version 430 core
precision mediump float;
layout (local_size_x = 64) in;
layout(rgba8, binding = 0, location=0) uniform image2D imgOutput;
layout( location=1 ) uniform sampler2D Source;
layout( location=2 ) uniform float srcX0;
layout( location=3 ) uniform float srcX1;
layout( location=4 ) uniform float srcY0;
layout( location=5 ) uniform float srcY1;
layout( location=6 ) uniform float dstX0;
layout( location=7 ) uniform float dstX1;
layout( location=8 ) uniform float dstY0;
layout( location=9 ) uniform float dstY1;

#define A_GPU 1
#define A_GLSL 1
#include "ffx_a.h"

#define FSR_EASU_F 1
AU4 con0, con1, con2, con3;
float srcW, srcH, dstW, dstH;

AF2 translate(AF2 pos) {
    return AF2(pos.x + srcX0, pos.y + srcY0);
}

AF4 FsrEasuRF(AF2 p) { AF4 res = textureGather(Source, translate(p), 0); return res; }
AF4 FsrEasuGF(AF2 p) { AF4 res = textureGather(Source, translate(p), 1); return res; }
AF4 FsrEasuBF(AF2 p) { AF4 res = textureGather(Source, translate(p), 2); return res; }

#include "ffx_fsr1.h"

void CurrFilter(AU2 pos)
{
    AF3 c;
    FsrEasuF(c, pos, con0, con1, con2, con3);
    imageStore(imgOutput, ASU2(pos), AF4(c, 1));
}

void main() {
    srcW = srcX1 - srcX0;
    srcH = srcY1 - srcY0;
    dstW = dstX1 - dstX0;
    dstH = dstY1 - dstY0;
    // Upscaling
    FsrEasuCon(con0, con1, con2, con3,
        srcW, srcH,  // Viewport size (top left aligned) in the input image which is to be scaled.
        srcW, srcH,  // The size of the input image.
        dstW, dstH); // The output resolution.

	AU2 gxy = ARmp8x8(gl_LocalInvocationID.x) + AU2(gl_WorkGroupID.x << 4u, gl_WorkGroupID.y << 4u);

    if(gxy.x < dstX0 || gxy.x > dstX1 || gxy.y < dstY0 || gxy.y > dstY1) {
        imageStore(imgOutput, ASU2(gxy), AF4(0,0,0,1));
        return;
    }

    CurrFilter(gxy);
	gxy.x += 8u;
	CurrFilter(gxy);
	gxy.y += 8u;
	CurrFilter(gxy);
	gxy.x -= 8u;
	CurrFilter(gxy);
}