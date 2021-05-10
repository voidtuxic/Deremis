// this only contains the io for a shader using genericvert.glsl

layout(location = 0) in vec3 f_position;
layout(location = 1) in vec2 f_UV;
layout(location = 2) in float f_fragDepth;
layout(location = 3) in mat3 f_TBN;
layout(location = 6) in mat4 f_FragPosLightSpace;

layout(location = 0) out vec4 out_Color;