Shader "Hidden/Hair/HairMaterialDefaultUnlit"
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

	#pragma multi_compile __ LAYOUT_INTERLEAVED
	// 0 == particles grouped by strand, i.e. root, root+1, root, root+1
	// 1 == particles grouped by index, i.e. root, root, root+1, root+1

	#pragma multi_compile __ STAGING_COMPRESSION
	// 0 == staging data full precision
	// 1 == staging data compressed

	#include "HairVertex.hlsl"

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
		float4 positionCS : SV_Position;
		float3 color : COLOR;
	};

	StrandVaryings StrandVert(StrandAttribs IN, uint in_vertexID : SV_VertexID)
	{
		HairVertex v = GetHairVertex((uint)IN.vertexID, IN.vertexUV, IN.staticPositionOS, IN.staticNormalOS, IN.staticTangentOS);
		{
			StrandVaryings OUT;
			OUT.positionCS = TransformObjectToHClip(v.positionOS);
			OUT.color = ColorCycle(v.strandIndex, _StrandCount);
			return OUT;
		}
	}

	float4 StrandFrag(StrandVaryings IN) : SV_Target
	{
		return float4(IN.color, 1.0);
	}

	ENDHLSL

	SubShader
	{
		Tags { "RenderType" = "Opaque" }

		Pass
		{
			HLSLPROGRAM

			#pragma vertex StrandVert_Color
			#pragma fragment StrandFrag_Color

			ENDHLSL
		}
	}
}
