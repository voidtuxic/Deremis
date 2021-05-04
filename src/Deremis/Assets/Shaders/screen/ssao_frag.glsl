#version 450

#include "uniforms/transform.glsl"
#include "libs/math.glsl"

layout(location = 0) in vec2 f_UV;
layout(location = 0) out vec4 out_Color;

layout(set = 1, binding = 0) uniform texture2D screenTex;
layout(set = 1, binding = 1) uniform texture2D positionTex;
layout(set = 1, binding = 2) uniform texture2D normalTex;
layout(set = 1, binding = 3) uniform sampler texSampler;

void main()
{
    mat4 projection = ViewProj;
    // hack to force correct spirv-cross texture slots
    vec3 screen = texture(sampler2D(screenTex, texSampler), f_UV).rgb;
    vec3 position = texture(sampler2D(screenTex, texSampler), f_UV).rgb;
    vec3 posTmp = texture(sampler2D(screenTex, texSampler), f_UV).rgb;
    vec3 normal = texture(sampler2D(screenTex, texSampler), f_UV).rgb;

    if(f_UV.x <= 1)
    {
        position = posTmp = texture(sampler2D(positionTex, texSampler), f_UV).rgb;
        projection = Proj;
    }
    if(f_UV.x <= 1)
    {
        position = texture(sampler2D(normalTex, texSampler), f_UV).rgb;
    }
    normal = position;
    position = posTmp;
    // end hack FFS
    
    const float radius = 0.5;
    const float bias = 0.025;
    const float kernelSize = 32;
    vec3 randomVec = normalize(vec3(random(f_UV) * 2.0 - 1, random((f_UV + vec2(1)) * 2.0 - 1), 0));
    vec3 randomNormal = normalize(vec3(random(f_UV) * 2.0 - 1, random((f_UV + vec2(1)) * 2.0 - 1), random(f_UV - vec2(1))));
    vec3 tangent = normalize(randomVec - normal * dot(randomVec, normal));
    vec3 bitangent = cross(normal, tangent);
    mat3 TBN = mat3(tangent, bitangent, normal);

    float occlusion = 0.0;
    vec3 tmp = texture(sampler2D(screenTex, texSampler), f_UV).rgb;
    for(int i = 0; i < kernelSize; ++i)
    {
        vec3 samplePos = TBN * randomHemispherePoint(randomVec, randomNormal);
        samplePos = position + samplePos * radius;
        vec4 offset = vec4(samplePos, 1.0);
        offset = projection * offset;
        offset.xyz /= offset.w;
        offset.xyz  = offset.xyz * 0.5 + 0.5;
        if(f_UV.x <= 1)
        {
            tmp = texture(sampler2D(positionTex, texSampler), f_UV).rgb;
        }
        float rangeCheck = smoothstep(0.0, 1.0, radius / abs(position.z - tmp.z));
        occlusion += (tmp.z >= samplePos.z + bias ? 1.0 : 0.0) * rangeCheck;
    } 
    occlusion = 1.0 - (occlusion / kernelSize);
    out_Color = vec4(screen * occlusion, 1);
}