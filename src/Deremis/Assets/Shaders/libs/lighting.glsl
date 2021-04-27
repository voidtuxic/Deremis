#define MIN_RANGE 0.001
#define GAMMA 2.2
#define MAX_LIGHTS 4
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