Shader "Hair/Hidden/HairMaterialReplaceError"
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
			#pragma fragment UnlitFragError

			float4 UnlitFragError(UnlitVaryings IN) : SV_Target
			{
				float3 errorColor = float3(1.0, 0.0, 1.0);
				return float4(lerp(IN.strandColor, errorColor, 0.9), 1.0);
			}

			ENDHLSL
		}
	}

	Fallback Off
}
