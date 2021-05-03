#define PI 3.1415926538

float saturate(float value) {
    return clamp(value, 0.0, 1.0);
}
vec3 saturate(vec3 value) {
    return vec3(clamp(value.x, 0.0, 1.0),clamp(value.y, 0.0, 1.0),clamp(value.z, 0.0, 1.0));
}