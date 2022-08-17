#version 430 core

layout (local_size_x = 10, local_size_y = 10, local_size_z = 10) in;
layout(rgba8, binding = 0) uniform image2D imgOutput;

const float REDUCE_MIN = 1.0 / 128.0;
const float REDUCE_MUL = 1.0 / 8.0;
const float SPAN_MAX   = 8.0;
const float invResolution   = 1.0;
const vec3  LUMA = vec3(0.299, 0.587, 0.114);

void main() {
    
}

vec4 fxaa(ivec2 coord){
    vec4 color = vec4(1.0);    
    ivec2 texelCoord = ivec2(gl_GlobalInvocationID.xy);
    vec3 rgbNW = imageLoad(imgOutput, (texelCoord + ivec2(-1, -1))).xyz;
    vec3 rgbNE = imageLoad(imgOutput, (texelCoord + ivec2( 1, -1))).xyz;
    vec3 rgbSW = imageLoad(imgOutput, (texelCoord + ivec2(-1,  1))).xyz;
    vec3 rgbSE = imageLoad(imgOutput, (texelCoord + ivec2( 1,  1))).xyz;
    vec3 rgbM  = imageLoad(imgOutput,  texelCoord).xyz;
    float lumaNW  = dot(rgbNW, LUMA);
    float lumaNE  = dot(rgbNE, LUMA);
    float lumaSW  = dot(rgbSW, LUMA);
    float lumaSE  = dot(rgbSE, LUMA);
    float lumaM   = dot(rgbM,  LUMA);
    float lumaMin = min(lumaM, min(min(lumaNW, lumaNE), min(lumaSW, lumaSE)));
    float lumaMax = max(lumaM, max(max(lumaNW, lumaNE), max(lumaSW, lumaSE)));

    vec2 dir = vec2(-((lumaNW + lumaNE) - (lumaSW + lumaSE)), (lumaNW + lumaSW) - (lumaNE + lumaSE));
    float dirReduce = max((lumaNW + lumaNE + lumaSW + lumaSE) * (0.25 * REDUCE_MUL), REDUCE_MIN);
    float rcpDirMin = 1.0 / (min(abs(dir.x), abs(dir.y)) + dirReduce);
    vec2 d = min(vec2(SPAN_MAX, SPAN_MAX), max(vec2(-SPAN_MAX, -SPAN_MAX), dir * rcpDirMin)) * invResolution;

    vec3 rgbA = 0.5 * (
        imageLoad(imgOutput, coord * invResolution + d * (1.0 / 3.0 - 0.5)).xyz +
        imageLoad(imgOutput, coord * invResolution + d * (2.0 / 3.0 - 0.5)).xyz
    );
    vec3 rgbB = rgbA * 0.5 + 0.25 * (
        imageLoad(tex, coord * invResolution + d * -0.5).xyz +
        imageLoad(tex, coord * invResolution + d *  0.5).xyz
    );

    float lumaB = dot(rgbB, LUMA);
    if((lumaB < lumaMin) || (lumaB > lumaMax)){
        color = vec4(rgbA, 1.0);
    }else{
        color = vec4(rgbB, 1.0);
    }
    return color;
}
