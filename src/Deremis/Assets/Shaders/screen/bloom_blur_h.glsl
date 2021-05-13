#version 450

#include "uniforms/transform.glsl"

layout(location = 0) in vec2 f_UV;
layout(location = 0) out vec4 out_Color;

layout(set = 1, binding = 0) uniform texture2D bloomTex;
layout(set = 1, binding = 1) uniform sampler texSampler;


const float offset[3] = float[](0.0, 1.3846153846, 3.2307692308);
const float weight[3] = float[](0.2270270270, 0.3162162162, 0.0702702703);

void main()
{
    vec2 resolution = textureSize(sampler2D(bloomTex, texSampler), 0);
    vec4 bloom = texture(sampler2D(bloomTex, texSampler), f_UV) * weight[0];
    for (int i=1; i<3; i++) {
        bloom +=
            texture(sampler2D(bloomTex, texSampler), (f_UV + vec2(offset[i], 0.0) / resolution))
                * weight[i];
        bloom +=
            texture(sampler2D(bloomTex, texSampler), (f_UV - vec2(offset[i], 0.0) / resolution))
                * weight[i];
    }
    out_Color = vec4(bloom.rgb, 1.0);
}