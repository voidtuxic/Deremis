// mostly adapted from https://learnopengl.com
#include "libs/math.glsl"
#include "libs/shadow.glsl"

// based off https://seblagarde.wordpress.com/2011/08/17/hello-world/
vec3 FresnelSchlick(vec3 SpecularColor,vec3 E,vec3 H, float roughness)
{
    return SpecularColor + (max(vec3(1.0 - roughness), SpecularColor) - SpecularColor) * pow(1.0 - saturate(dot(E, H)), 5);
}

float DistributionGGX(vec3 N, vec3 H, float roughness)
{
    float a = roughness*roughness;
    float a2 = a*a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH*NdotH;

    float nom   = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;

    return nom / denom;
}
// ----------------------------------------------------------------------------
float GeometrySchlickGGX(float NdotV, float roughness)
{
    float r = (roughness + 1.0);
    float k = (r*r) / 8.0;

    float nom   = NdotV;
    float denom = NdotV * (1.0 - k) + k;

    return nom / denom;
}
// ----------------------------------------------------------------------------
float GeometrySmith(vec3 N, vec3 V, vec3 L, float roughness)
{
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx2 = GeometrySchlickGGX(NdotV, roughness);
    float ggx1 = GeometrySchlickGGX(NdotL, roughness);

    return ggx1 * ggx2;
}

vec3 CalculatePBR(mat4 fragParams, mat4 viewParams, mat4 fragPosLightSpace, float fragDepth)
{
    vec3 fragPos = fragParams[0].xyz;
    vec3 N = fragParams[1].xyz;
    vec3 albedo = fragParams[2].xyz;
    float metal = fragParams[3].x;
    float rough = fragParams[3].y;
    float ao = fragParams[3].z;

    vec3 viewPos = viewParams[0].xyz;
    vec3 V = normalize(viewPos - fragPos);
    vec3 irradiance = viewParams[1].xyz;

    vec3 prefilteredColor = viewParams[2].xyz;
    vec2 brdf = viewParams[3].xy;

    vec3 Lo = vec3(0.0);
    vec3 F0 = vec3(0.04);
    F0 = mix(F0, albedo, metal);
    vec3 diffuse = irradiance * albedo;

    for(int i = 0; i < MAX_LIGHTS; i++) 
    {
        float lightType = Lights[i].Type;
        vec3 L;
        float attenuation = 1;
        float shadow = 0;
        float intensity = 1.0;
        if (lightType == 0)
        {
            L = normalize(-Lights[i].Direction);
            shadow = CalculateShadows(N, L, fragPosLightSpace, fragDepth);
        }
        else if(lightType == 1)
        {
            L = normalize(Lights[i].Position - fragPos);
            float distance = length(Lights[i].Position - fragPos);
            float range = max(MIN_RANGE, Lights[i].Range);
            float linear = LINEAR_FACTOR/range;
            float quadratic = QUADRATIC_FACTOR/(range*range);
            attenuation = 1.0 / (CONSTANT + linear * distance + quadratic * (distance * distance));
        }
        else if (lightType == 2)
        {
            float lightInnerCutOff = Lights[i].InnerCutoff;
            float lightOuterCutOff = Lights[i].OuterCutoff;
            L = normalize(Lights[i].Position - fragPos);
            float theta = dot(L, normalize(-Lights[i].Direction));
            float epsilon = lightOuterCutOff - lightInnerCutOff;
            intensity = clamp((theta - lightInnerCutOff) / epsilon, 0.0, 1.0);
        }

        float NdotL = max(dot(N, L), 0.0);
    
        vec3 H = normalize(V + L);
        vec3 radiance = Lights[i].Color * attenuation;

        vec3 F =  FresnelSchlick(F0, V, H, rough);
        float NDF = DistributionGGX(N, H, rough);
        float G   = GeometrySmith(N, V, L, rough);
        vec3 specular = NDF * G * F;

        vec3 kS = F;
        vec3 kD = vec3(1.0) - kS;
        kD *= 1.0 - metal;
        
        Lo += ((kD * diffuse + specular) * radiance * NdotL * intensity) * (1.0 - shadow);
    }
    vec3 F =  FresnelSchlick(F0, N, V, rough);
    vec3 kS = F;
    vec3 kD = 1.0 - kS;
    kD *= 1.0 - metal;
    vec3 specular = prefilteredColor * (F * brdf.x + brdf.y);
    vec3 ambient = (kD * diffuse + specular/12.0) * ao;
    vec3 color = ambient + Lo;
    return color;
}