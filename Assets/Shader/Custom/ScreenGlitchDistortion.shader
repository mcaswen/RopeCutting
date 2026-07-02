Shader "Hidden/Custom/ScreenGlitchDistortion"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Intensity;
            float _RGBSplit;
            float _HorizontalJitter;
            float _LeftRightShake;
            float _WaveDistortion;
            float _BlockChance;
            float _BlockFrequency;
            float _ScanlineStrength;
            float _NoiseAmount;
            float _Darken;
            float _JitterSpeed;
            float _TimeSeed;

            float Hash(float n)
            {
                return frac(sin(n) * 43758.5453123);
            }

            float Hash2(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            fixed4 SampleGlitched(float2 uv, float split)
            {
                float2 redUv = uv + float2(split, 0);
                float2 blueUv = uv - float2(split, 0);

                fixed4 center = tex2D(_MainTex, uv);
                fixed4 color;
                color.r = tex2D(_MainTex, redUv).r;
                color.g = center.g;
                color.b = tex2D(_MainTex, blueUv).b;
                color.a = center.a;
                return color;
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float amount = saturate(_Intensity);
                float2 baseUv = i.uv;
                float2 uv = baseUv;

                float tick = floor(_TimeSeed * _JitterSpeed);
                float band = floor(baseUv.y * _BlockFrequency + tick * 0.17);
                float bandRandom = Hash(band * 19.19 + tick * 7.31);
                float bandActive = step(1.0 - saturate(_BlockChance) * amount, bandRandom);

                float randomOffset = Hash(band * 43.17 + tick * 11.13) - 0.5;
                float bandOffset = randomOffset * _HorizontalJitter * bandActive * amount;

                float shakeRandom = Hash(tick * 5.37) - 0.5;
                float shakeWave = sin(_TimeSeed * _JitterSpeed * 8.0) * 0.5;
                float globalOffset = (shakeRandom + shakeWave) * _LeftRightShake * amount;

                float wave = sin(baseUv.y * 95.0 + _TimeSeed * 42.0) * _WaveDistortion * amount;
                uv.x += bandOffset + globalOffset + wave;

                float splitPulse = 1.0 + bandActive * 2.5 + abs(sin(_TimeSeed * _JitterSpeed)) * 0.7;
                fixed4 color = SampleGlitched(uv, _RGBSplit * amount * splitPulse);

                float scanline = 0.5 + 0.5 * sin(baseUv.y * _ScreenParams.y * 3.14159265);
                color.rgb *= 1.0 - ((1.0 - scanline) * _ScanlineStrength * amount);

                float2 noiseCell = floor(baseUv * _ScreenParams.xy * 0.55);
                float noise = Hash2(noiseCell + tick);
                color.rgb += (noise - 0.5) * _NoiseAmount * amount;

                float whiteTear = step(0.985, Hash(band * 3.91 + tick * 23.43)) * bandActive;
                color.rgb += whiteTear * amount * 0.18;

                color.rgb *= 1.0 - _Darken * amount;
                color.rgb = saturate(color.rgb);
                return color;
            }
            ENDCG
        }
    }

    Fallback Off
}
