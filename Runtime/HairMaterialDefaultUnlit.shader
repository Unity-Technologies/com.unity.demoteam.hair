Shader "Hair/Default/HairMaterialDefaultUnlit"
{
	HLSLINCLUDE

	#pragma target 5.0

	#include "Packages/com.unity.shadergraph/ShaderGraphLibrary/ShaderConfig.cs.hlsl"
	#ifdef SHADEROPTIONS_CAMERA_RELATIVE_RENDERING
	#undef SHADEROPTIONS_CAMERA_RELATIVE_RENDERING
	#define SHADEROPTIONS_CAMERA_RELATIVE_RENDERING (0)
	#endif

	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	#include "Packages/com.unity.shadergraph/ShaderGraphLibrary/ShaderVariables.hlsl"
	#include "Packages/com.unity.shadergraph/ShaderGraphLibrary/ShaderVariablesFunctions.hlsl"

	#pragma multi_compile __ HAIR_VERTEX_ID_LINES HAIR_VERTEX_ID_STRIPS
	#pragma multi_compile __ HAIR_VERTEX_SRC_SOLVER HAIR_VERTEX_SRC_STAGING

	#pragma multi_compile __ STAGING_COMPRESSION
	// 0 == staging data full precision
	// 1 == staging data compressed

	#include "HairVertex.hlsl"

	ENDHLSL

	SubShader
	{
		Tags { "RenderType" = "Opaque" "DisableBatching" = "True" "Queue" = "Transparent" }
		ZTest LEqual
		ZWrite On

		Pass
		{
			HLSLPROGRAM

			#pragma vertex StrandVert
			#pragma fragment StrandFrag

			struct StrandAttribs
			{
				float vertexID : TEXCOORD0;
				float2 vertexUV : TEXCOORD1;
				float3 staticPositionOS : POSITION;
				float3 staticNormalOS : NORMAL;
				float3 staticTangentOS : TANGENT;
			};

			struct StrandVaryings
			{
				float4 positionCS : SV_POSITION;
				float3 strandColor : COLOR;
				float2 strandUV : TEXCOORD0;
			};

			StrandVaryings StrandVert(StrandAttribs IN)
			{
				HairVertex hair = GetHairVertex((uint)IN.vertexID, IN.vertexUV, IN.staticPositionOS, IN.staticNormalOS, IN.staticTangentOS);
				{
					StrandVaryings OUT;
					OUT.positionCS = TransformObjectToHClip(hair.positionOS);
					OUT.strandColor = hair.strandDebugColor;
					OUT.strandUV = hair.strandUV;
					return OUT;
				}
			}

			float4 StrandFrag(StrandVaryings IN) : SV_Target
			{
				return float4(IN.strandColor, 1.0);
			}

			ENDHLSL
		}
	}
}
