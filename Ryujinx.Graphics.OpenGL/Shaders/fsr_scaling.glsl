#version 430 core
layout (local_size_x = 10, local_size_y = 10, local_size_z = 10) in;
layout(rgba8, binding = 0, location=0) uniform image2D imgOutput;
layout( location=1 ) uniform vec2 invResolution; 
layout( location=2 ) uniform vec2 outvResolution; 
layout( location=3 ) uniform sampler2D Source;

#define A_GPU 1
#define A_GLSL 1
#include "ffx_a.h"

#define FSR_EASU_F 1
AU4 con0, con1, con2, con3;

AF4 FsrEasuRF(AF2 p) { return textureGather(Source, p, 0); }
AF4 FsrEasuGF(AF2 p) { return textureGather(Source, p, 1); }
AF4 FsrEasuBF(AF2 p) { return textureGather(Source, p, 2); }

#include "ffx_fsr1.h"


void main() {
    // Upscaling
    FsrEasuCon(con0, con1, con2, con3,
        invResolution.x, invResolution.y,  // Viewport size (top left aligned) in the input image which is to be scaled.
        invResolution.x, invResolution.y,  // The size of the input image.
        outvResolution.x, outvResolution.y); // The output resolution.

    vec2 coord = vec2((gl_GlobalInvocationID.x +0.5) / invResolution.x, (gl_GlobalInvocationID.y +0.5) / invResolution.y);
    AU2 gxy = AU2(coord * outvResolution.xy); // Integer pixel position in output.
    AF3 Gamma2Color = AF3(0, 0, 0);
    FsrEasuF(Gamma2Color, gxy, con0, con1, con2, con3);

    vec4 outColor = vec4(Gamma2Color, 1.0);

    imageStore(imgOutput, ivec2(gxy.xy), outColor);
}