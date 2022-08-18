
#version 420 core
layout(location = 0) in vec4 aPosition;

void main(void)
{
    gl_Position = aPosition;
}