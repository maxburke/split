struct VS_INPUT {
    float4 position : POSITION;
    float2 surfaceUV  : TEXCOORD0;
    float2 lightmapUV : TEXCOORD1;
    float3 normal   : NORMAL;
    float4 color    : COLOR;
};


struct VS_OUTPUT {
    float4 position : POSITION;
    float2 surfaceUV  : TEXCOORD0;
    float2 lightmapUV : TEXCOORD1;
    float3 normal   : NORMAL;
    float4 color    : COLOR;
};

float2 LightMapSize;
float2 LightMapStart;
float4x4 WorldViewProjection;

sampler SurfaceTexture : register(s0) = sampler_state
{
    MinFilter = Anisotropic;
    MagFilter = Anisotropic;
};

sampler LightMap : register(s1) = sampler_state
{
    MinFilter = Anisotropic;
    MagFilter = Anisotropic;
};

VS_OUTPUT vs_main(in VS_INPUT input) {
    VS_OUTPUT output;
    output.position = mul(input.position, WorldViewProjection);
    output.surfaceUV = input.surfaceUV;
    output.lightmapUV = input.lightmapUV;
    output.normal = input.normal;
    output.color = input.color;

    return output;
}


struct PS_INPUT {
    float2 surfaceUV  : TEXCOORD0;
    float2 lightmapUV : TEXCOORD1;
    float3 normal   : NORMAL;
    float4 color    : COLOR;
};


float4 ps_main(in PS_INPUT input) : COLOR {
	float4 albedo = tex2D(SurfaceTexture, input.surfaceUV);
	float4 light = tex2D(LightMap, input.lightmapUV) * 4;
	float4 color = albedo * light;
	
	if (albedo.w <= 0.1f)
		clip(-1.0f);
		
	return color;
}


float4 ps_main_nolight(in PS_INPUT input) : COLOR {
	float4 albedo = tex2D(SurfaceTexture, input.surfaceUV);
	
	if (albedo.w <= 0.1f)
		clip(-1.0f);
		
	return albedo;
}


technique defaultTechnique {
    pass P0 {
        VertexShader = compile vs_3_0 vs_main();
        PixelShader = compile ps_3_0 ps_main();
    }
    
    pass P1 {
        VertexShader = compile vs_3_0 vs_main();
        PixelShader = compile ps_3_0 ps_main_nolight();
    }
}

