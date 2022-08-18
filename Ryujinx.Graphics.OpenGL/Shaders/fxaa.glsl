// https://gist.github.com/doxas/db88570fbc01b364fa3aa01e39227fe8
#version 420
precision mediump float;

layout(location = 0) out vec4 outputColor;

uniform sampler2D textureUnit;
uniform vec2      invResolution; // 1.0 / resolution

const float REDUCE_MIN = 1.0 / 128.0;
const float REDUCE_MUL = 1.0 / 8.0;
const float SPAN_MAX   = 8.0;
const vec3  LUMA = vec3(0.299, 0.587, 0.114);

vec4 fxaa(sampler2D tex, vec2 fragCoord){
    vec4 color = vec4(1.0);
    vec3 rgbNW = texture(tex, (fragCoord + vec2(-1.0, -1.0)) * invResolution).xyz;
    vec3 rgbNE = texture(tex, (fragCoord + vec2( 1.0, -1.0)) * invResolution).xyz;
    vec3 rgbSW = texture(tex, (fragCoord + vec2(-1.0,  1.0)) * invResolution).xyz;
    vec3 rgbSE = texture(tex, (fragCoord + vec2( 1.0,  1.0)) * invResolution).xyz;
    vec3 rgbM  = texture(tex,  fragCoord * invResolution).xyz;
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
        texture(tex, fragCoord * invResolution + d * (1.0 / 3.0 - 0.5)).xyz +
        texture(tex, fragCoord * invResolution + d * (2.0 / 3.0 - 0.5)).xyz
    );
    vec3 rgbB = rgbA * 0.5 + 0.25 * (
        texture(tex, fragCoord * invResolution + d * -0.5).xyz +
        texture(tex, fragCoord * invResolution + d *  0.5).xyz
    );

    float lumaB = dot(rgbB, LUMA);
    if((lumaB < lumaMin) || (lumaB > lumaMax)){
        color = vec4(rgbA, 1.0);
    }else{
        color = vec4(rgbB, 1.0);
    }
    return color;
}

void main(void)
{
    outputColor = fxaa(textureUnit, gl_FragCoord.xy);
}