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

struct SPIRV_Cross_Input
{
    float2 f_UV : TEXCOORD0;
};

struct SPIRV_Cross_Output
{
    float4 out_Color : SV_Target0;
};

void frag_main()
{
    float3 screen = screenTex.Sample(texSampler, f_UV).xyz;
    
    float2 texelSize = 1.0 / 512.0;
    float result = 0.0;
    for (int x = -2; x < 2; ++x) 
    {
        for (int y = -2; y < 2; ++y) 
        {
            float2 offset = float2(float(x), float(y)) * texelSize;
            result += ssaoPassTex.Sample(texSampler, f_UV + offset).r;
        }
    }
    result /= (4.0 * 4.0);
    out_Color = float4(screen * max(0.1, result),1.0);
}

SPIRV_Cross_Output main(SPIRV_Cross_Input stage_input)
{
    f_UV = stage_input.f_UV;
    frag_main();
    SPIRV_Cross_Output stage_output;
    stage_output.out_Color = out_Color;
    return stage_output;
}
