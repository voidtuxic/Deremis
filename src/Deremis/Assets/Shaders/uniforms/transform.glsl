layout(set = 0, binding = 0) uniform Transform
{
    mat4 ViewProj;
    mat4 World;
    mat4 NormalWorld;
    mat4 View;
    mat4 Proj;
    mat4 LightSpace1;
    mat4 LightSpace2;
    mat4 LightSpace3;
};

vec3 GetViewPos() {
    return vec3(View[0][3], View[1][3], View[2][3]);
}