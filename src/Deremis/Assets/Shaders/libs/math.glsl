#include "libs/lwjglrandom.glsl"

float saturate(float value) {
    return clamp(value, 0.0, 1.0);
}
vec3 saturate(vec3 value) {
    return vec3(clamp(value.x, 0.0, 1.0),clamp(value.y, 0.0, 1.0),clamp(value.z, 0.0, 1.0));
}

// from https://github.com/mattdesl/glsl-random/blob/master/index.glsl
float random(vec2 co)
{
    float a = 12.9898;
    float b = 78.233;
    float c = 43758.5453;
    float dt= dot(co.xy ,vec2(a,b));
    float sn= mod(dt,3.14);
    return fract(sin(sn) * c);
}