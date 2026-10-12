Shader "Childhood/MarbleGlass"
{
    Properties
    {
        _Color ("Glass tint", Color) = (0.2,0.7,0.85,0.12)
        _EdgeStrength ("Edge reflection", Range(0,2)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f
            {
                float4 position : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 worldPosition : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };
            fixed4 _Color;
            float _EdgeStrength;
            v2f vert(appdata vertex)
            {
                v2f output;
                output.position = UnityObjectToClipPos(vertex.vertex);
                output.normal = UnityObjectToWorldNormal(vertex.normal);
                output.worldPosition = mul(unity_ObjectToWorld,vertex.vertex).xyz;
                UNITY_TRANSFER_FOG(output,output.position);
                return output;
            }
            fixed4 frag(v2f input) : SV_Target
            {
                float3 normal = normalize(input.normal);
                float3 view = normalize(_WorldSpaceCameraPos.xyz-input.worldPosition);
                float rim = pow(1.0-saturate(dot(normal,view)),2.8);
                float3 light = normalize(_WorldSpaceLightPos0.xyz);
                float glint = pow(saturate(dot(reflect(-light,normal),view)),75.0);
                float softGlint = pow(saturate(dot(reflect(-light,normal),view)),13.0)*0.075;
                // 中心透视内芯，只有边缘和很小的太阳反光稍微增加不透明度。
                float3 tint = _Color.rgb * (0.74 + rim*0.23);
                tint += (glint*0.48+softGlint) * float3(1.0,0.97,0.86);
                float alpha = saturate(_Color.a*(0.24+rim*2.7)*_EdgeStrength+glint*0.18+softGlint*0.16);
                fixed4 color = fixed4(tint,alpha);
                UNITY_APPLY_FOG(input.fogCoord,color);
                return color;
            }
            ENDCG
        }
    }
    FallBack "Transparent/Diffuse"
}
