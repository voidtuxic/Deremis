cbuffer Transform : register(b0)
{
    row_major float4x4 ViewProj : packoffset(c0);
    row_major float4x4 World : packoffset(c4);
    row_major float4x4 NormalWorld : packoffset(c8);
    row_major float4x4 View : packoffset(c12);
    row_major float4x4 Proj : packoffset(c16);
    row_major float4x4 LightSpace : packoffset(c20);
};

Texture2D<float4> screenTex : register(t0);
Texture2D<float4> positionTex : register(t1);
Texture2D<float4> normalTex : register(t2);
Texture2D<float4> ssaoPassTex : register(t3);
SamplerState texSampler : register(s0);

static float2 f_UV;
static float4 out_Color;
static float scale = 1.0;
static float intensity = 5.0f;
static float radius = 0.95;
static float bias = 0.025f;

struct SPIRV_Cross_Input
{
    float2 f_UV : TEXCOORD0;
};

struct SPIRV_Cross_Output
{
    float4 out_Color : SV_Target0;
};

float2 hash22(float2 p)
{
	float3 p3 = frac(float3(p.xyx) * float3(.1031, .1030, .0973));
    p3 += dot(p3, p3.yzx+33.33);
    return frac((p3.xx+p3.yz)*p3.zy);

}

float3 getPosition(in float2 uv) 
{
    return positionTex.Sample(texSampler, uv).xyz;
}

float3 getNormal(in float2 uv) 
{
    return normalize(normalTex.Sample(texSampler, uv).xyz);
}

float2 getRandom(in float2 uv) 
{
    return normalize(hash22(uv*1000.0f) * 2.0f - 1.0f); 
}

float doAmbientOcclusion(in float2 tcoord,in float2 uv, in float3 p, in float3 cnorm) 
{
    float3 diff = getPosition(tcoord + uv) - p; 
    float3 v = normalize(diff); 
    float d = length(diff)*scale; 
    return max(0.0,dot(cnorm,v)-bias)*(1.0/(1.0+d))*intensity;
}

void frag_main()
{
    const float2 vec[4] = {float2(1,0),float2(-1,0), float2(0,1),float2(0,-1)};
    const int iterations = 4; 
    float ao = 0.0f; 

    float3 p = getPosition(f_UV); 
    float3 n = getNormal(f_UV); 
    float2 rand = getRandom(f_UV); 
    float rad = radius/p.z;
    for (int j = 0; j < iterations; ++j) 
    {
        float2 coord1 = reflect(vec[j%4],rand)*rad; 
        float2 coord2 = float2(coord1.x*0.707 - coord1.y*0.707, coord1.x*0.707 + coord1.y*0.707); 
        
        ao += doAmbientOcclusion(f_UV,coord1*0.25, p, n); 
        ao += doAmbientOcclusion(f_UV,coord2*0.5, p, n); 
        ao += doAmbientOcclusion(f_UV,coord1*0.75, p, n); 
        ao += doAmbientOcclusion(f_UV,coord2, p, n); 
    }
    ao /=(float)iterations*4.0;
    ao = pow(ao, 5);
    ao = 1.0 - ao;
    
    out_Color = float4(ao,ao, ao, 1.0f); // screen* min(0.5, ao)
}

SPIRV_Cross_Output main(SPIRV_Cross_Input stage_input)
{
    f_UV = stage_input.f_UV;
    frag_main();
    SPIRV_Cross_Output stage_output;
    stage_output.out_Color = out_Color;
    return stage_output;
}
