#version 450

#include "uniforms/transform.glsl"
#include "uniforms/in_vert_instanced.glsl"

layout(location = 0) out vec3 f_position;
layout(location = 1) out vec2 f_UV;
layout(location = 2) out float f_fragDepth;
layout(location = 3) out mat3 f_TBN;
layout(location = 6) out mat4 f_FragPosLightSpace;

void main()
{
    mat4 worldMatrix = mat4(
        WR1,
        WR2,
        WR3,
        WR4
    );
    vec4 worldPos = worldMatrix * vec4(Position, 1);
    gl_Position = ViewProj * worldPos;
    f_position = worldPos.xyz;
    f_UV = UV;
    f_fragDepth = gl_Position.z;

    mat3 normalWorld = mat3(transpose(inverse(worldMatrix)));
    vec3 T = normalize(normalWorld * Tangent);
    vec3 B = normalize(normalWorld * Bitangent);
    vec3 N = normalize(normalWorld * Normal);
    f_TBN = mat3(T, B, N);
    f_FragPosLightSpace = mat4(
        LightSpace1 * vec4(f_position, 1), 
        LightSpace2 * vec4(f_position, 1), 
        LightSpace3 * vec4(f_position, 1),
        vec4(0));
}