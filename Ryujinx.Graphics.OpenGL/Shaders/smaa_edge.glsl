layout(rgba8, binding = 0) uniform image2D imgOutput;

uniform sampler2D input;
layout( location=0 ) uniform vec2 invResolution;

void main() 
{
	vec2 coord = vec2((gl_GlobalInvocationID.x) / invResolution.x, (gl_GlobalInvocationID.y) / invResolution.y);
	vec4 offset[3];
	SMAAEdgeDetectionVS(coord, offset);
	vec2 oColor = SMAAColorEdgeDetectionPS(coord, offset, input);
	if (oColor != float2(-2.0, -2.0))
	{
		imageStore(imgOutput, ivec2(gl_GlobalInvocationID.xy), vec4(oColor, 0.0, 1.0));
	}
}