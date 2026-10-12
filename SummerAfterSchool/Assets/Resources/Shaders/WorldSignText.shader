Shader "Childhood/WorldSignText"
{
    Properties { _MainTex ("字体图集", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="AlphaTest+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Lighting Off
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; UNITY_FOG_COORDS(1) };
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv;
                o.color = v.color; o.color.rgb *= fixed3(.94,.93,.88);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 ink = i.color;
                ink.a *= tex2D(_MainTex,i.uv).a;
                clip(ink.a - .015);
                UNITY_APPLY_FOG(i.fogCoord,ink);
                return ink;
            }
            ENDCG
        }
    }
    FallBack "GUI/Text Shader"
}
