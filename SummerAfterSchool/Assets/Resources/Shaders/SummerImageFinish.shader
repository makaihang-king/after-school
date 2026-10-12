Shader "Hidden/Childhood/SummerImageFinish"
{
    Properties { _MainTex ("镜头画面", 2D) = "white" {} _Amount ("色调强度", Range(0,1)) = .7 }
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
            half _Amount;
            half4 frag(v2f_img i) : SV_Target
            {
                half4 source = tex2D(_MainTex,i.uv);
                half3 color = source.rgb;
                half luma = dot(color,half3(.2126,.7152,.0722));
                color = lerp(luma.xxx,color,1.025);
                color += half3(-.002,.0008,.0045) * (1-smoothstep(.12,.56,luma));
                // 保留彩色玻璃的饱和度，仅轻压最高亮部分的炫光。
                color /= 1 + max(color - .92,0) * .09;
                half2 offset = (i.uv - .5) * half2(1.1,1);
                half vignette = smoothstep(.27,.65,dot(offset,offset)) * .05;
                color *= 1-vignette;
                return half4(lerp(source.rgb,max(color,0),_Amount),source.a);
            }
            ENDCG
        }
    }
    FallBack Off
}
