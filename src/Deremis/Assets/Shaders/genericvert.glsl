#version 450

layout(set = 0, binding = 0) uniform Transform
{
    mat4 ViewProj;
    mat4 World;
    mat4 NormalWorld;
};

layout(location = 0) in vec3 Position;
layout(location = 1) in vec3 Normal;
layout(location = 2) in vec2 UV;
layout(location = 3) in vec3 Tangent;
layout(location = 4) in vec3 Bitangent;

layout(location = 0) out vec3 f_position;
layout(location = 1) out vec2 f_UV;
layout(location = 2) out mat3 f_TBN;

void main()
{
    vec4 worldPos = World * vec4(Position, 1);
    gl_Position = ViewProj * worldPos;
    f_position = worldPos.xyz;
    //f_normal = mat3(NormalWorld) * Normal;
    f_UV = UV;

    vec3 T = normalize(mat3(NormalWorld) * Tangent);
    vec3 B = normalize(mat3(NormalWorld) * Bitangent);
    vec3 N = normalize(mat3(NormalWorld) * Normal);
    f_TBN = mat3(T, B, N);
}