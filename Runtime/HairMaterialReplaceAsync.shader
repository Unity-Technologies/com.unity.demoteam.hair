Shader "Hair/Hidden/HairMaterialReplaceAsync"
{
	HLSLINCLUDE

	#pragma target 5.0
	#pragma editor_sync_compilation

	#pragma multi_compile_local_vertex HAIR_VERTEX_STATIC HAIR_VERTEX_LIVE

	#include "HairMaterialCommonUnlit.hlsl"

	ENDHLSL

	SubShader
	{
		Tags { "RenderType" = "Opaque" "DisableBatching" = "True" "Queue" = "Transparent-1" }
		ZTest LEqual
		ZWrite On

		Pass
		{
			HLSLPROGRAM

			#pragma vertex UnlitVert
			#pragma fragment UnlitFragAsync

			float4 UnlitFragAsync(UnlitVaryings IN) : SV_Target
			{
				float3 asyncColor = float3(0.0, 1.0, 1.0);
				return float4(lerp(IN.strandColor, asyncColor, 0.9), 1.0);
			}

			ENDHLSL
		}
	}

	Fallback Off
}
