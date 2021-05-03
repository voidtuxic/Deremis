// mostly adapted from https://learnopengl.com
#include "libs/math.glsl"

vec3 ClipToUV(vec4 clip)
{
    vec3 ret = vec3((clip.x / clip.w) / 2 + 0.5, (clip.y / clip.w) / -2 + 0.5, clip.z / clip.w);

    return ret;
}

float CalculateShadows(vec3 normal, vec3 lightDir, vec4 fragPosLightSpace)
{
    vec3 projCoords = ClipToUV(fragPosLightSpace);
    float shadow = 0.0;

    if((saturate(projCoords.x) == projCoords.x) && (saturate(projCoords.y) == projCoords.y))
    {
        float currentDepth = projCoords.z;
        float bias = max(0.05 * (1.0 - dot(normal, lightDir)), 0.005);
        vec2 texelSize = 1.0 / textureSize(sampler2D(shadowMap, shadowMapSampler), 0);
        for(int x = -1; x <= 1; ++x)
        {
            for(int y = -1; y <= 1; ++y)
            {
                float pcfDepth = texture(sampler2D(shadowMap, shadowMapSampler), projCoords.xy + vec2(x, y) * texelSize).r;
                shadow += currentDepth - bias < pcfDepth ? 0.5 : 0.0;
            }
        }
        shadow /= 9.0;
    }

    return shadow;
}

vec3 CalculateLight(vec3 lightDir, vec3 lightColor, float attenuation, float intensity, mat3 dsn, vec3 fragPos, vec4 fragPosLightSpace)
{    
    vec3 viewPos = GetViewPos();
    vec3 viewDir = normalize(viewPos - fragPos);
    vec3 halfwayDir = normalize(lightDir + viewDir);

    vec3 diffTex = dsn[0].xyz;
    vec3 specTex = dsn[1].xyz;
    vec3 norm = dsn[2].xyz;

    diffTex = CorrectGamma(diffTex); // should probably not be here

    vec3 ambient = ambientStrength * diffTex * lightColor;

    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = (diff * diffTex) * lightColor;

    float spec = pow(max(dot(norm, halfwayDir), 0.0), 32);
    vec3 specular = (specularStrength * spec * specTex) * lightColor;

    ambient  *= attenuation;
    diffuse  *= attenuation;
    specular *= attenuation;

    diffuse  *= intensity;
    specular *= intensity;
    
    float shadow = CalculateShadows(norm, lightDir, fragPosLightSpace);
    vec3 lighting = ambient + (1.0 - shadow) * (diffuse + specular);

    return saturate(lighting);
}

vec3 Calculate(mat3 dsn, vec3 fragPos, vec4 fragPosLightSpace)
{
    vec3 color;
    for(int i = 0; i < MAX_LIGHTS; i++)
    {
        float lightType = Lights[i].Type;
        vec3 lightPosition = Lights[i].Position;
        vec3 lightDirection = Lights[i].Direction;
        vec3 lightColor = Lights[i].Color;
        if (lightType == 0)
        {
            vec3 lightDir = normalize(-lightDirection);
            color += CalculateLight(lightDir, lightColor, CONSTANT, 1.0, dsn, fragPos, fragPosLightSpace);
        }
        else if (lightType == 1)
        {
            float lightRange = Lights[i].Range;
            vec3 lightDir = normalize(lightPosition - fragPos);
            float range = max(MIN_RANGE, lightRange);
            float linear = LINEAR_FACTOR/range;
            float quadratic = QUADRATIC_FACTOR/(range*range);
            float distance = length(lightPosition - fragPos);
            color += CalculateLight(
                lightDir, 
                lightColor, 1.0 / (CONSTANT + linear * distance + quadratic * (distance * distance)), 
                1.0, dsn, fragPos, fragPosLightSpace);
        }
        else if (lightType == 2)
        {
            float lightInnerCutOff = Lights[i].InnerCutoff;
            float lightOuterCutOff = Lights[i].OuterCutoff;
            vec3 lightDir = normalize(lightPosition - fragPos);
            float theta = dot(lightDir, normalize(-lightDirection));
            float epsilon = lightInnerCutOff - lightOuterCutOff;
            float intensity = clamp((theta - lightOuterCutOff) / epsilon, 0.0, 1.0);
            color += CalculateLight(lightDir, lightColor, CONSTANT, intensity, dsn, fragPos, fragPosLightSpace);
        }
    }

    return color;
}