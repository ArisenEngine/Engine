struct Attribute
{
    float3 positionOS : POSITION0;
    float3 normalOS : NORMAL0;
    float2 uv : TEXCOORD0;
};

struct Varying
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
};

cbuffer UboView : register(b0, space0)
{
    float4x4 model;
    float4x4 view;
    float4x4 projection;
};

Texture2D tex : register(t1, space0);
SamplerState sam : register(s2, space0);

Varying Vert(Attribute input)
{
    Varying output = (Varying)0;
    output.positionCS = mul(projection, mul(view, mul(model, float4(input.positionOS, 1.0))));
    output.uv = input.uv;
    output.normalWS = mul((float3x3)model, input.normalOS);
    return output;
}

float4 Frag(Varying input) : SV_Target
{
    float4 color = tex.Sample(sam, input.uv);
    // Simple NdotL for better visualization
    float3 lightDir = normalize(float3(1.0, 1.0, 1.0));
    float ndotl = max(dot(normalize(input.normalWS), lightDir), 0.2);
    return float4(color.rgb * ndotl, color.a);
}
