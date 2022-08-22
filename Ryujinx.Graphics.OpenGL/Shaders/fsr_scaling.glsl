#version 430 core
layout (local_size_x = 10, local_size_y = 10, local_size_z = 10) in;
layout(rgba8, binding = 0) uniform image2D imgOutput;
uniform sampler2D Source;
layout( location=0 ) uniform vec2 invResolution; 
layout( location=1 ) uniform vec2 outvResolution; 
layout( location=2 ) uniform float frameCount; 
layout( location=3 ) uniform float filmGrain; 
layout( location=4 ) uniform float grainColor; 
layout( location=5 ) uniform float grainPDF; 

#define A_GPU 1
#define A_GLSL 1
#include "ffx_a.h"

#define FSR_EASU_F 1
AU4 con0, con1, con2, con3;

AF4 FsrEasuRF(AF2 p) { return textureGather(Source, p, 0); }
AF4 FsrEasuGF(AF2 p) { return textureGather(Source, p, 1); }
AF4 FsrEasuBF(AF2 p) { return textureGather(Source, p, 2); }

#include "ffx_fsr1.h"

// prng: A simple but effective pseudo-random number generator [0;1[
float prng(vec2 uv, float time) {
    return fract(sin(dot(uv + fract(time), vec2(12.9898, 78.233))) * 43758.5453);
}

// pdf: [-0.5;0.5[
// Removes noise modulation effect by reshaping the uniform/rectangular noise
// distribution (RPDF) into a Triangular (TPDF) or Gaussian Probability Density
// Function (GPDF).
// shape = 1.0: Rectangular
// shape = 0.5: Triangular
// shape < 0.5: Gaussian (0.2~0.4)
float pdf(float noise, float shape) {
    float orig = noise * 2.0 - 1.0;
    noise = pow(abs(orig), shape);
    noise *= sign(orig);
    noise -= sign(orig);
    return noise * 0.5;
}


void main() {
    // Upscaling
    FsrEasuCon(con0, con1, con2, con3,
        invResolution.x, invResolution.y,  // Viewport size (top left aligned) in the input image which is to be scaled.
        invResolution.x, invResolution.y,  // The size of the input image.
        outvResolution.x, outvResolution.y); // The output resolution.

    vec2 coord = vec2((gl_GlobalInvocationID.x) / invResolution.x, (gl_GlobalInvocationID.y) / invResolution.y);
    AU2 gxy = AU2(coord * outvResolution.xy); // Integer pixel position in output.
    AF3 Gamma2Color = AF3(0, 0, 0);
    FsrEasuF(Gamma2Color, gxy, con0, con1, con2, con3);

    vec4 outColor = vec4(Gamma2Color, 1.0);

    // Sharpening
    FsrRcasF(Gamma2Color.r, Gamma2Color.g, Gamma2Color.b, gxy, con0);

    // FSR - [LFGA] LINEAR FILM GRAIN APPLICATOR
    if (filmGrain > 0.0) {
        if (grainColor == 0.0) {
            float noise = pdf(prng(coord, frameCount * 0.11), grainPDF);
            FsrLfgaF(Gamma2Color, vec3(noise), filmGrain);
        } else {
            vec3 rgbNoise = vec3(
                pdf(prng(coord, frameCount * 0.11), grainPDF),
                pdf(prng(coord, frameCount * 0.13), grainPDF),
                pdf(prng(coord, frameCount * 0.17), grainPDF)
            );
            FsrLfgaF(Gamma2Color, rgbNoise, filmGrain);
        }
    }

    outColor = vec4(Gamma2Color, 1.0);

    imageStore(imgOutput, ivec2(gxy.xy), outColor);
}