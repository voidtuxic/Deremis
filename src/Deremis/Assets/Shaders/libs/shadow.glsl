#define SHADOW_FAR 25

vec3 ClipToUV(vec4 clip)
{
    vec3 ret = vec3((clip.x / clip.w) / 2 + 0.5, (clip.y / clip.w) / -2 + 0.5, clip.z / clip.w);

    return ret;
}

bool IsDepthNearer(float a, float b)
{
    return a < b;
}

vec4 GetLightSpacePosition(mat4 fragPosLightSpace, float currentDepth) {
    if(IsDepthNearer(currentDepth, SHADOW_FAR)) return fragPosLightSpace[0].xyzw;
    if(IsDepthNearer(currentDepth, SHADOW_FAR*4)) return fragPosLightSpace[1].xyzw;
    if(IsDepthNearer(currentDepth, SHADOW_FAR*16)) return fragPosLightSpace[2].xyzw;
    return fragPosLightSpace[3].xyzw;
}

float SampleShadowDepth(vec2 uv, float currentDepth) {
    if(IsDepthNearer(currentDepth, SHADOW_FAR)) return texture(sampler2D(shadowMap1, shadowMapSampler), uv).r;
    if(IsDepthNearer(currentDepth, SHADOW_FAR*4)) return texture(sampler2D(shadowMap2, shadowMapSampler), uv).r;
    if(IsDepthNearer(currentDepth, SHADOW_FAR*16)) return texture(sampler2D(shadowMap3, shadowMapSampler), uv).r;
    return 0.0;
}

float CalculateShadows(vec3 normal, vec3 lightDir, mat4 fragPosLightSpace, float fragDepth)
{
    vec3 projCoords = ClipToUV(GetLightSpacePosition(fragPosLightSpace, fragDepth));
    float shadow = 0.0;

    // if((saturate(projCoords.x) == projCoords.x) && (saturate(projCoords.y) == projCoords.y))
    // {
        float currentDepth = projCoords.z;
        float bias = 0;// max(0.005 * (1.0 - dot(normal, lightDir)), 0.0005);
        vec2 texelSize = 1.0 / textureSize(sampler2D(shadowMap1, shadowMapSampler), 0);
        for(int x = -1; x <= 1; ++x)
        {
            for(int y = -1; y <= 1; ++y)
            {
                float pcfDepth = SampleShadowDepth(projCoords.xy + vec2(x, y) * texelSize, fragDepth);
                shadow += currentDepth - bias < pcfDepth ? 0.75 : 0.0;
            }
        }
        shadow /= 9.0;
    // }

    return shadow;
}