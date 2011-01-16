struct vs_input {
    float4 position : POSITION;
    float2 surfaceST  : TEXCOORD0;
    float2 lightmapST : TEXCOORD1;
    float3 normal   : NORMAL;
    float4 color    : COLOR;
};

float4x4 gWorld;
float4x4 gViewProjection;
float gTime;

sampler gSampler0 : register(s0);
sampler gSampler1 : register(s1);

vs_input vs_main(in vs_input input) {
    vs_input output;
    output.position = mul(input.position, mul(gWorld, gViewProjection));
    output.surfaceST = input.surfaceST;
    output.lightmapST = input.lightmapST;
    output.normal = input.normal;
    output.color = input.color;

    return output;
}

float4 ps_main(vs_input input) : COLOR {
	float4 albedo = tex2D(gSampler0, input.surfaceST);
	float4 light = tex2D(gSampler1, input.lightmapST);
	float4 color = albedo * light;
			
	return color;
}

technique defaultTechnique {
    pass P0 {
        VertexShader = compile vs_3_0 vs_main();
        PixelShader = compile ps_3_0 ps_main();
    }
}

