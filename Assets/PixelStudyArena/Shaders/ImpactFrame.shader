// Anime-style impact frame: the whole picture becomes two flat colors (ink and paper) split by
// brightness, with speed lines bursting out from the point of impact. Driven by ImpactFrame.cs.
Shader "PixelStudy/ImpactFrame"
{
    Properties
    {
        _MainTex ("Screen", 2D) = "white" {}
        _Ink ("Ink", Color) = (0,0,0,1)
        _Paper ("Paper", Color) = (1,1,1,1)
        _Threshold ("Brightness split", Range(0,1)) = 0.3
        _Invert ("Invert", Float) = 0
        _Focus ("Focus (uv)", Vector) = (0.5,0.5,0,0)
        _Lines ("Speed lines", Float) = 1
        _Seed ("Seed", Float) = 0
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Ink;
            fixed4 _Paper;
            float _Threshold;
            float _Invert;
            float4 _Focus;
            float _Lines;
            float _Seed;

            float hash(float n) { return frac(sin(n) * 43758.5453); }

            fixed4 frag (v2f_img i) : SV_Target
            {
                fixed3 c = tex2D(_MainTex, i.uv).rgb;
                float lum = dot(c, float3(0.299, 0.587, 0.114));
                float v = step(_Threshold, lum);

                // speed lines: thin wedges radiating from the focus, starting a little way out
                float2 d = i.uv - _Focus.xy;
                d.x *= _MainTex_TexelSize.z / max(1.0, _MainTex_TexelSize.w);
                float ang = atan2(d.y, d.x) / 6.2831853 + 0.5;
                float slices = 150.0;
                float slice = floor(ang * slices);
                float h = hash(slice * 1.7 + _Seed * 13.13);
                float r = length(d);
                float start = 0.16 + hash(slice + 31.0 + _Seed) * 0.3;
                float width = (h - 0.72) * 1.2 * saturate((r - start) * 2.5);
                float across = abs(frac(ang * slices) - 0.5);
                float lineMask = step(0.72, h) * step(across, width) * step(start, r);
                v = lerp(v, 1.0, lineMask * _Lines); // paper lines on the ink background (ink on paper when inverted)

                v = lerp(v, 1.0 - v, _Invert);
                return fixed4(lerp(_Ink.rgb, _Paper.rgb, v), 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
