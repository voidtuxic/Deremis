
vec3 ClipToUV(vec4 clip)
{
    vec3 ret = vec3((clip.x / clip.w) / 2 + 0.5, (clip.y / clip.w) / -2 + 0.5, clip.z / clip.w);

    return ret;
}

float CalculateShadows(vec3 normal, vec3 lightDir, vec4 fragPosLightSpace)
{
    vec3 projCoords = ClipToUV(fragPosLightSpace);
    if(1.0 - projCoords.z > 1.0) return 0.0;
    float shadow = 0.0;

    if((saturate(projCoords.x) == projCoords.x) && (saturate(projCoords.y) == projCoords.y))
    {
        float currentDepth = projCoords.z;
        float bias = 0;// max(0.005 * (1.0 - dot(normal, lightDir)), 0.0005);
        vec2 texelSize = 1.0 / textureSize(sampler2D(shadowMap, shadowMapSampler), 0);
        for(int x = -1; x <= 1; ++x)
        {
            for(int y = -1; y <= 1; ++y)
            {
                float pcfDepth = texture(sampler2D(shadowMap, shadowMapSampler), projCoords.xy + vec2(x, y) * texelSize).r;
                shadow += currentDepth - bias < pcfDepth ? 0.75 : 0.0;
            }
        }
        shadow /= 9.0;
    }

    return shadow;
}