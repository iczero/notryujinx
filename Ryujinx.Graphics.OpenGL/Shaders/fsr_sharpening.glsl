#version 430 core
layout (local_size_x = 10, local_size_y = 10, local_size_z = 10) in;
layout(rgba8, binding = 0, location=0) uniform image2D imgOutput;
layout( location=1 ) uniform vec2 invResolution; 
layout( location=2 ) uniform vec2 outvResolution; 
layout( location=3 ) uniform sampler2D Source;
layout( location=4 ) uniform float frameCount; 

#define FSR_SHARPENING  0.3
#define FSR_FILMGRAIN  0.3
#define FSR_GRAINCOLOR  1.0
#define FSR_GRAINPDF  0.3

#define A_GPU 1
#define A_GLSL 1
#include "ffx_a.h"

#define FSR_RCAS_F 1
AU4 con0;

AF4 FsrRcasLoadF(ASU2 p) { return AF4(texelFetch(Source, p, 0)); }
void FsrRcasInputF(inout AF1 r, inout AF1 g, inout AF1 b) {}

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
    FsrRcasCon(con0, FSR_SHARPENING);
    
    vec2 coord = vec2((gl_GlobalInvocationID.x) / invResolution.x, (gl_GlobalInvocationID.y) / invResolution.y);
    AU2 gxy = AU2(coord.xy * outvResolution.xy); // Integer pixel position in output.
    AF3 Gamma2Color = AF3(0, 0, 0);
    FsrRcasF(Gamma2Color.r, Gamma2Color.g, Gamma2Color.b, gxy, con0);

    // FSR - [LFGA] LINEAR FILM GRAIN APPLICATOR
    if (FSR_FILMGRAIN > 0.0) {
        if (FSR_GRAINCOLOR == 0.0) {
            float noise = pdf(prng(coord, frameCount * 0.11), FSR_GRAINPDF);
            FsrLfgaF(Gamma2Color, vec3(noise), FSR_FILMGRAIN);
        } else {
            vec3 rgbNoise = vec3(
                pdf(prng(coord, frameCount * 0.11), FSR_GRAINPDF),
                pdf(prng(coord, frameCount * 0.13), FSR_GRAINPDF),
                pdf(prng(coord, frameCount * 0.17), FSR_GRAINPDF)
            );
            FsrLfgaF(Gamma2Color, rgbNoise, FSR_FILMGRAIN);
        }
    }
    
    vec4 outColor = vec4(Gamma2Color, 1.0);

    imageStore(imgOutput, ivec2(gxy.xy), outColor);
}