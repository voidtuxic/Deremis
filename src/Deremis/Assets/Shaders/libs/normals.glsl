vec3 getNormalFromMap(vec2 uv, vec3 worldPos, mat3 TBN)
{
    vec3 tangentNormal = texture(sampler2D(normalTexture, texSampler), uv).xyz * 2.0 - 1.0;

    return normalize(TBN * tangentNormal);
}