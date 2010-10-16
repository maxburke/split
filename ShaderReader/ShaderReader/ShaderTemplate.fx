// Copyright (c) Max Burke

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


struct PS_INPUT {
    float2 surfaceUV  : TEXCOORD0;
    float2 lightmapUV : TEXCOORD1;
    float3 normal   : NORMAL;
    float4 color    : COLOR;
};

float4x4 View;
float4x4 Projection;
float Time;

#define PI 3.1415926535

float SinWave(float t, float base, float amp, float phase, float freq) {
    const float t1 = t * 2 * PI;
    return sin(freq * t1 + phase) * amp + base;
}

float SquareWave(float t, float base, float amp, float phase, float freq) {
    const float t1 = t * 2 * PI;
    const float sign = sign(sin(freq * t1 + phase));
    return sign * amp + base;
}

float TriangleWave(float t, float base, float amp, float phase, float freq) {
    // this is kind of broken for now.
    const float t1 = t * PI;
    return abs((2.0 / PI) * asin(sin(t1)));
}

float SawtoothWave(float t, float base, float amp, float phase, float freq) {
    const float t1 = t * freq + phase;
    return (t1 - floor(t1)) * amp + base;
}

float InverseSawtooth(float t, float base, float amp, float phase, float freq) {
    return amp - sawtooth(t, base, amp, phase, freq);
}


