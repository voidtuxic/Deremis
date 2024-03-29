#version 450

#include "uniforms/transform.glsl"
#include "uniforms/lights.glsl"
#include "genericfrag.glsl"

layout(set = 0, binding = 1) uniform Material
{
    vec3 albedo;
    float metallic;
    float roughness;
    float ao;
    float emissiveStrength;
};

layout(set = 1, binding = 0) uniform texture2D albedoTexture;
layout(set = 1, binding = 1) uniform texture2D mraTexture;
layout(set = 1, binding = 2) uniform texture2D normalTexture;
layout(set = 1, binding = 3) uniform texture2D emissiveTexture;
layout(set = 1, binding = 4) uniform textureCube environmentTexture;
layout(set = 1, binding = 5) uniform texture2D brdfLutTex;
layout(set = 1, binding = 6) uniform textureCube prefilteredEnvTexture;
layout(set = 1, binding = 7) uniform texture2D shadowMap1;
layout(set = 1, binding = 8) uniform texture2D shadowMap2;
layout(set = 1, binding = 9) uniform texture2D shadowMap3;
layout(set = 1, binding = 10) uniform sampler texSampler;
layout(set = 1, binding = 11) uniform sampler shadowMapSampler;

// later include to have layout definition
#include "libs/pbr.glsl"
#include "libs/normals.glsl"

void main()
{
    vec3 albedoColor = CorrectGamma(texture(sampler2D(albedoTexture, texSampler), f_UV).rgb) * albedo;
    vec3 normalDir = getNormalFromMap(f_UV, f_position, f_TBN);
    vec3 mra = texture(sampler2D(mraTexture, texSampler), f_UV).rgb;
    float metal = min(0.9, mra.r * metallic);
    float rough =  max(0.01, mra.g * roughness);
    float aoVal = mra.b * ao;
    vec3 irradiance = CorrectGamma(texture(samplerCube(environmentTexture, texSampler), normalDir).rgb);

    vec3 V = normalize(GetViewPos() - f_position);
    const float MAX_REFLECTION_LOD = 5.0;
    vec3 prefilteredColor = CorrectGamma(textureLod(samplerCube(prefilteredEnvTexture, texSampler), normalDir, rough * MAX_REFLECTION_LOD).rgb);
    vec2 envBRDF = texture(sampler2D(brdfLutTex, texSampler), vec2(max(dot(normalDir, V), 0.0001), rough)).rg;

    mat4 fragParams = mat4(
            vec4(f_position, 0.0), 
            vec4(normalDir, 0.0), 
            vec4(albedoColor, 0.0), 
            vec4(metal, rough, aoVal, 0.0));
    mat4 viewParams = mat4(
            vec4(GetViewPos(), 0.0), 
            vec4(irradiance, 0.0), 
            vec4(prefilteredColor, 0.0), 
            vec4(envBRDF, 0.0, 0.0));

    vec3 color = CalculatePBR(fragParams, viewParams, f_FragPosLightSpace, f_fragDepth);

    color += CorrectGamma(texture(sampler2D(emissiveTexture, texSampler), f_UV).rgb) * emissiveStrength;
    out_Color = vec4(color, 1);
    
    vec3 bloom = GetBloom(color);
    out_Bloom = vec4(bloom, 1.0);
}