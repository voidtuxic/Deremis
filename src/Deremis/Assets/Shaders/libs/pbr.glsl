// mostly adapted from https://learnopengl.com
#include "libs/math.glsl"
#include "libs/shadow.glsl"

float DistributionGGX(vec3 N, vec3 H, float roughness)
{
    float a      = roughness*roughness;
    float a2     = a*a;
    float NdotH  = max(dot(N, H), 0.0);
    float NdotH2 = NdotH*NdotH;

    float num   = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;

    return num / denom;
}

float GeometrySchlickGGX(float NdotV, float roughness)
{
    float r = ((roughness*roughness) + 1.0);
    float k = (r*r) / 8.0;

    float num   = NdotV;
    float denom = NdotV * (1.0 - k) + k;

    return num / denom;
}
float GeometrySmith(vec3 N, vec3 V, vec3 L, float roughness)
{
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx2  = GeometrySchlickGGX(NdotV, roughness);
    float ggx1  = GeometrySchlickGGX(NdotL, roughness);

    return ggx1 * ggx2;
}

// based off https://seblagarde.wordpress.com/2011/08/17/hello-world/
vec3 FresnelSchlick(vec3 SpecularColor,vec3 E,vec3 H, float roughness)
{
    return SpecularColor + (max(vec3(1.0 - roughness), SpecularColor) - SpecularColor) * pow(1.0 - saturate(dot(E, H)), 5);
}

vec3 Calculate(vec3 fragPos, vec3 normal, vec3 viewPos, vec3 albedo, float metal, float rough, float ao, vec3 irradiance, vec4 fragPosLightSpace) {
    vec3 N = normal; 
    vec3 V = normalize(viewPos - fragPos);
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
        if (lightType == 0)
        {
            L = normalize(-Lights[i].Direction);
            shadow = CalculateShadows(N, L, fragPosLightSpace);
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
        // TODO support spotlights

        float NdotL = max(dot(N, L), 0.0);
    
        vec3 H = normalize(V + L);
        vec3 radiance = Lights[i].Color * attenuation;

        vec3 F = FresnelSchlick(F0, N, H, rough) * ((F0 + 2.0) / 8.0 ) * pow(saturate(dot(N, H)), length(F0)) * NdotL;//fresnelSchlick(max(dot(H, V), 0.0), F0);
        float NDF = DistributionGGX(N, H, rough);
        float G = GeometrySmith(N, V, L, rough);

        vec3 numerator = NDF * G * F;
        vec3 specular = numerator;

        vec3 kS = F;
        vec3 kD = vec3(1.0) - kS;
        kD *= 1.0 - metal;
        vec3 ambient = (kD * diffuse) * ao * F0; 
        
        Lo += (ambient + (kD * albedo + specular) * radiance * NdotL) * (1.0 - shadow);
    }
    
    return Lo;
}