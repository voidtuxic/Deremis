#define MIN_RANGE 0.001
#define GAMMA 2.2
#define MAX_LIGHTS 8
// based off http://wiki.ogre3d.org/Light+Attenuation+Shortcut
#define CONSTANT 1.0
#define LINEAR_FACTOR 4.5
#define QUADRATIC_FACTOR 75.0

struct LightStruct
{
    vec3 Position;
    vec3 Direction;
    vec3 Color;

    float Type;

    float Range;
    float InnerCutoff;
    float OuterCutoff;
};

vec3 CorrectGamma(vec3 color) {
    return pow(color, vec3(GAMMA));
}

vec3 GetBloom(vec3 color) 
{
    vec3 tonemapped = vec3(1.0) - exp(-color * 1.0);
    const vec3 luminanceVector = vec3(0.2126, 0.7152, 0.0722);
    float luminance = dot(luminanceVector, tonemapped);
    luminance = max(0.15, max(0.0, luminance - 1.0));
    vec3 bloom = tonemapped * luminance;

    return bloom;
}