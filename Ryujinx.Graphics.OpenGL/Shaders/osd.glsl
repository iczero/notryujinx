#version 430 core
precision mediump float;
layout (local_size_x = 16, local_size_y = 16) in;
layout(rgba8, binding = 0) uniform image2D img;
uniform sampler2D fontAtlas;
layout( location=0 ) uniform vec4 color;
layout( location=1 ) uniform vec2 padding;
layout( location=2 ) uniform int lineHeight;
layout( location=3 ) uniform uint edge;
layout( location=4 ) uniform uint size;
layout( location=5 ) uniform uint draw;
layout( location=6 ) uniform int scale;

layout(std430, binding = 1) buffer mapData
{
    int mapData_data[];
};

void drawGlyph(uint unit)
{
    uint unitOffset = unit * 8;
    uint xOffset = mapData_data[unitOffset + 0];
    uint line = mapData_data[unitOffset + 1];
    uint mapOffsetX = mapData_data[unitOffset + 2];
    uint mapOffsetY = mapData_data[unitOffset + 3];
    uint offsetX = mapData_data[unitOffset + 4];
    uint offsetY = mapData_data[unitOffset + 5];
    uint width = mapData_data[unitOffset + 6];
    uint height = mapData_data[unitOffset + 7];

    for (int i = 0; i < width; i++) 
    {
        for(int j = 0; j < height; j++)
        {
            uint x = int(padding.x) + xOffset + i + offsetX;
            uint y = (line * lineHeight) + int(padding.y) + j + offsetY;
            y = edge - (y * scale);
            x = x * scale;

            vec2 pos = vec2(mapOffsetX + i, mapOffsetY + j);
            vec2 coor = vec2(pos.x / size, pos.y / size);

            vec4 pixel = texture(fontAtlas, coor);
            vec4 col = vec4(color.rgb, pixel.r);
            if(pixel != vec4(0,0,0,1)) 
            {
                for(int sx = 0; sx < scale; sx++)
                {  
                    for(int sy = 0; sy < scale; sy++)
                    {
                        pos = vec2(x + sx, y + sy);
                        vec4 base = imageLoad(img, ivec2(pos));
                        vec4 final = col * col.a + base * (1.0-col.a);
                        imageStore(img, ivec2(pos) , final);
                    }
                }
            }
        }
    }
}

void main()
{
    if(draw == 1)
    {   
        uint unit = gl_GlobalInvocationID.x;
    
        drawGlyph(unit);
    }
    else 
    {
        vec4 bckground = vec4(0.1,0.1,0.1,0.5);
        vec2 pos = vec2(padding.x + gl_GlobalInvocationID.x, padding.y + gl_GlobalInvocationID.y);
        pos.y = edge - pos.y;
        vec2 coor = vec2(pos.x / size, pos.y / size);
        vec4 base = imageLoad(img, ivec2(pos));
        vec4 final = bckground * bckground.a + base * (1.0-bckground.a);
        imageStore(img, ivec2(pos) , final);
    }
}