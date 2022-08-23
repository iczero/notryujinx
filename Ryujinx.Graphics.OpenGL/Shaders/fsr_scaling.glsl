#version 430 core
precision mediump float;
layout (local_size_x = 64) in;
layout(rgba8, binding = 0, location=0) uniform image2D imgOutput;
layout( location=1 ) uniform vec2 invResolution; 
layout( location=2 ) uniform vec2 outvResolution; 
layout( location=3 ) uniform sampler2D Source;

#define A_GPU 1
#define A_GLSL 1
#include "ffx_a.h"

#define FSR_EASU_F 1
AU4 con0, con1, con2, con3;

AF4 FsrEasuRF(AF2 p) { AF4 res = textureGather(Source, p, 0); return res; }
AF4 FsrEasuGF(AF2 p) { AF4 res = textureGather(Source, p, 1); return res; }
AF4 FsrEasuBF(AF2 p) { AF4 res = textureGather(Source, p, 2); return res; }

#include "ffx_fsr1.h"

void CurrFilter(AU2 pos)
{
    AF3 c;
    FsrEasuF(c, pos, con0, con1, con2, con3);
    imageStore(imgOutput, ASU2(pos), AF4(c, 1));
}

void main() {
    // Upscaling
    FsrEasuCon(con0, con1, con2, con3,
        invResolution.x, invResolution.y,  // Viewport size (top left aligned) in the input image which is to be scaled.
        invResolution.x, invResolution.y,  // The size of the input image.
        outvResolution.x, outvResolution.y); // The output resolution.

	AU2 gxy = ARmp8x8(gl_LocalInvocationID.x) + AU2(gl_WorkGroupID.x << 4u, gl_WorkGroupID.y << 4u);
    CurrFilter(gxy);
	gxy.x += 8u;
	CurrFilter(gxy);
	gxy.y += 8u;
	CurrFilter(gxy);
	gxy.x -= 8u;
	CurrFilter(gxy);
}