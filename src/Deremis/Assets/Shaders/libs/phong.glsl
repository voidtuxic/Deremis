vec3 CalculateLight(vec3 lightDir, vec3 lightColor, float attenuation, float intensity, mat3 dsn, vec3 fragPos)
{    
    vec3 viewPos = GetViewPos();
    vec3 viewDir = normalize(viewPos - fragPos);
    vec3 halfwayDir = normalize(lightDir + viewDir);

    vec3 diffTex = dsn[0].xyz;
    vec3 specTex = dsn[1].xyz;
    vec3 norm = dsn[2].xyz;

    diffTex = CorrectGamma(diffTex); // should probably not be here

    vec3 ambient = ambientStrength * lightColor;

    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = (diff * diffTex) * lightColor;

    float spec = pow(max(dot(norm, halfwayDir), 0.0), 32);
    vec3 specular = (specularStrength * spec * specTex) * lightColor;

    ambient  *= attenuation;
    diffuse  *= attenuation;
    specular *= attenuation;

    diffuse  *= intensity;
    specular *= intensity;

    return ambient + diffuse + specular;
}

vec3 Calculate(mat3 dsn, vec3 fragPos)
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
            color += CalculateLight(lightDir, lightColor, CONSTANT, 1.0, dsn, fragPos);
        }
        else if (lightType == 1)
        {
            float lightRange = Lights[i].Range;
            vec3 lightDir = normalize(lightPosition - fragPos);
            float range = max(MIN_RANGE, lightRange);
            float linear = LINEAR_FACTOR/range;
            float quadratic = QUADRATIC_FACTOR/(range*range);
            float distance = length(lightPosition - fragPos);
            color += CalculateLight(lightDir, lightColor, 1.0 / (CONSTANT + linear * distance + quadratic * (distance * distance)), 1.0, dsn, fragPos);
        }
        else if (lightType == 2)
        {
            float lightInnerCutOff = Lights[i].InnerCutoff;
            float lightOuterCutOff = Lights[i].OuterCutoff;
            vec3 lightDir = normalize(lightPosition - fragPos);
            float theta = dot(lightDir, normalize(-lightDirection));
            float epsilon = lightInnerCutOff - lightOuterCutOff;
            float intensity = clamp((theta - lightOuterCutOff) / epsilon, 0.0, 1.0);
            color += CalculateLight(lightDir, lightColor, CONSTANT, intensity, dsn, fragPos);
        }
    }

    return color;
}