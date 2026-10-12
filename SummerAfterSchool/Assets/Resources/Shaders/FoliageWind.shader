Shader "Childhood/FoliageWind"
{
    Properties
    {
        _Color ("槐叶颜色", Color) = (.33,.48,.24,1)
        _Glossiness ("叶面光泽", Range(0,1)) = .08
        _WindStrength ("微风摆幅", Range(0,.15)) = .044
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0
        fixed4 _Color;
        half _Glossiness, _WindStrength;
        struct Input { float3 worldPos; float3 worldNormal; };
        void vert(inout appdata_full v)
        {
            float3 world = mul(unity_ObjectToWorld,v.vertex).xyz;
            float t = _Time.y;
            float gust = sin(t * .83 + world.z * .34 + world.x * .42);
            float flutter = sin(t * 1.72 + world.y * 2.4 + world.x * 1.18);
            float weight = saturate((world.y - .45) * .30);
            float3 drift = float3(gust + flutter * .23, flutter * .17, gust * .35) * (_WindStrength * weight);
            v.vertex.xyz += mul((float3x3)unity_WorldToObject,drift);
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // 小范围色斑打破圆球叶簇的光滑感，影子由同一摆动网格产生。
            float leafPattern = sin(IN.worldPos.x * 19 + sin(IN.worldPos.z * 17)) * sin(IN.worldPos.y * 23 - IN.worldPos.z * 13);
            float broader = sin(IN.worldPos.x * 4.7 + IN.worldPos.y * 3.8 + IN.worldPos.z * 5.1);
            o.Albedo = _Color.rgb * (.96 + leafPattern * .075 + broader * .045);
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Occlusion = .94;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
