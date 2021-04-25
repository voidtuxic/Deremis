#include "lighting.glsl"

layout(set = 0, binding = 2) uniform Light {
    LightStruct Lights[MAX_LIGHTS];
};