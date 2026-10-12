Shader "Childhood/PaperDisc"
{
    Properties
    {
        _MainTex ("印刷图案", 2D) = "white" {}
        _Color ("纸张底色", Color) = (1,1,1,1)
        _Glossiness ("上光", Range(0,1)) = .16
        _Metallic ("金属", Range(0,1)) = 0
        _Wear ("磨损", Range(0,1)) = 0
        _Treasured ("珍藏包边", Range(0,1)) = 0
        _Edge ("纸层侧面", Range(0,1)) = 0
        _Seed ("独特纸纹", Float) = .37
        _RimStrength ("纸边高光", Range(0,1)) = .16
        _RimColor ("纸边暖色", Color) = (.95,.75,.38,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness, _Metallic, _Wear, _Treasured, _Edge, _RimStrength;
        fixed4 _RimColor;
        float _Seed;
        struct Input { float2 uv_MainTex; float3 viewDir; };
        float hash(float2 p)
        {
            return frac(sin(dot(p, float2(127.1,311.7)) + _Seed * 12.13) * 43758.5453);
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.uv_MainTex;
            fixed3 printed = tex2D(_MainTex, uv).rgb * _Color.rgb;
            float fibre = hash(floor(uv * float2(750,610)));
            float grain = hash(floor(uv * float2(58,73)));
            if (_Edge > .5)
            {
                // UV 的纵向线纹模拟层压纸层，侧边与上下纸面保持同一凸体轮廓。
                float layer = .5 + .5 * sin(uv.y * 82 + sin(uv.x * 45 + _Seed) * .56);
                float seam = smoothstep(.70,.97,layer);
                printed *= .83 + fibre * .12 - seam * .18;
                float paperLip = smoothstep(.92,1,uv.y) + (1 - smoothstep(0,.08,uv.y));
                printed = lerp(printed, fixed3(.88,.80,.61), saturate(paperLip) * .40);
                printed = lerp(printed, fixed3(.64,.55,.40), _Wear * grain * .18);
                o.Smoothness = .06;
                o.Metallic = 0;
            }
            else
            {
                float radial = length((uv - .5) * 2);
                float edgeNoise = hash(floor(uv * 126));
                float edgeWear = smoothstep(.975 - _Wear * .14 + edgeNoise * .026,1.0,radial);
                float crease = pow(saturate(1 - abs(sin((uv.x + uv.y * .42) * 137 + _Seed)) * 19),3);
                float scuff = crease * smoothstep(.52,.97,radial) * _Wear * (.18 + grain * .38);
                float faded = dot(printed, fixed3(.299,.587,.114));
                printed = lerp(printed, faded.xxx, _Wear * .09);
                printed *= .989 + fibre * .022;
                printed = lerp(printed, fixed3(.86,.79,.60), saturate(edgeWear * .84 + scuff));
                // 极细的暖金收藏包边只留在最外缘，主体画面仍清晰。
                float trim = smoothstep(.93,.943,radial) * (1-smoothstep(.968,.985,radial)) * _Treasured;
                printed = lerp(printed, fixed3(.76,.54,.23), trim * .82);
                o.Smoothness = lerp(_Glossiness * (1 - _Wear * .6),.36,trim);
                o.Metallic = trim * .46;
            }
            o.Albedo = printed;
            o.Occlusion = 1;
            // 纸片边缘只使用视角高光，不改变物理厚度；俯视镜头下仍能读到圆片轮廓。
            float fresnel = pow(1 - saturate(dot(normalize(o.Normal), normalize(IN.viewDir))), 2.2);
            float edgeMask = smoothstep(.72, .98, length((uv - .5) * 2));
            o.Emission = _RimColor.rgb * fresnel * edgeMask * _RimStrength * (1 - _Wear * .35);
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
