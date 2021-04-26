#version 450

#include "uniforms/transform.glsl"
#include "uniforms/in_vert.glsl"

layout(location = 0) out vec3 f_position;
layout(location = 1) out vec2 f_UV;
layout(location = 2) out mat3 f_TBN;

void main()
{
    vec4 worldPos = World * vec4(Position, 1);
    gl_Position = ViewProj * worldPos;
    f_position = worldPos.xyz;
    f_UV = UV;

    vec3 T = normalize(mat3(NormalWorld) * Tangent);
    vec3 B = normalize(mat3(NormalWorld) * Bitangent);
    vec3 N = normalize(mat3(NormalWorld) * Normal);
    f_TBN = mat3(T, B, N);
}