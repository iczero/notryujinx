layout(rgba8, binding = 0) uniform image2D imgOutput;

uniform sampler2D input;
layout( location=0 ) uniform vec2 invResolution;
uniform sampler2D samplerArea;
uniform sampler2D samplerSearch;

void main() {
    vec2 coord = vec2(gl_GlobalInvocationID.x / invResolution.x, gl_GlobalInvocationID.y / invResolution.y);
	vec2 pixCoord;
	vec4 offset[3];

	SMAABlendingWeightCalculationVS( coord, pixCoord, offset);

	vec4 oColor = SMAABlendingWeightCalculationPS(coord, pixCoord, offset, input, samplerArea, samplerSearch, ivec4(0));	

	imageStore(imgOutput,  ivec2(gl_GlobalInvocationID.xy), oColor);
}
