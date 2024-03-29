#define PI 3.14159265359
#define INV_PI 0.31830988618

float saturate(float value) {
    return clamp(value, 0.0, 1.0);
}
vec2 saturate(vec2 value) {
    return vec2(clamp(value.x, 0.0, 1.0),clamp(value.y, 0.0, 1.0));
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

const vec2 invAtan = vec2(0.1591, 0.3183);
vec2 SampleSphericalMap(vec3 v)
{
    vec2 uv = vec2(atan(v.z, v.x), asin(-v.y));
    uv *= invAtan;
    uv += 0.5;
    return uv;
}