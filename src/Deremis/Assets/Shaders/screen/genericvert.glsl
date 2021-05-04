#version 450

#include "uniforms/transform.glsl"
#include "uniforms/in_vert.glsl"

layout(location = 0) out vec2 f_UV;

void main()
{
    gl_Position = vec4(Position.x, Position.y, 0.0, 1.0); 
    f_UV = UV;
}