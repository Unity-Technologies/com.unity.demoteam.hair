Shader "Hair/Default/HairMaterialDefaultLitBuiltin"
{
	CGINCLUDE

	#pragma target 5.0

	#ifndef UNITY_COMMON_INCLUDED
	#define UNITY_COMMON_INCLUDED
	#endif

	#ifndef UNITY_MATRIX_I_M
	#define UNITY_MATRIX_I_M unity_WorldToObject
	#endif
	#ifndef UNITY_PREV_MATRIX_M
	#define UNITY_PREV_MATRIX_M unity_MatrixPreviousM
	#endif
	#ifndef UNITY_PREV_MATRIX_I_M
	#define UNITY_PREV_MATRIX_I_M unity_MatrixPreviousMI
	#endif

	#define real float
	#define real3 float3
	#define real3x3 float3x3
	#define SafeNormalize(v) (normalize(v))
	float4x4 unity_MatrixPreviousM;
	float4x4 unity_MatrixPreviousMI;

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

#ifdef SHADER_API_D3D11
	#include "HairVertex.hlsl"
#endif

	ENDCG

	SubShader
	{
		Tags { "RenderType" = "Opaque" "DisableBatching" = "True" "Queue" = "Geometry" }
		ZTest LEqual
		ZWrite On

		CGPROGRAM

		#pragma surface StrandSurf Lambert vertex:StrandVert addshadow fullforwardshadows

		struct Input
		{
			float3 strandTangentWS;
			float3 strandColor;
			float2 strandUV;
		};

		void StrandVert(inout appdata_full v, out Input o)
		{
#ifdef SHADER_API_D3D11
			HairVertex hair = GetHairVertex((uint)v.texcoord.x, v.texcoord1.xy, v.vertex.xyz, v.normal.xyz, v.tangent.xyz);
			{
				v.vertex = float4(hair.positionOS, 1.0);
				v.normal = float4(hair.normalOS, 1.0);
				v.tangent = float4(hair.tangentOS, 1.0);

				UNITY_INITIALIZE_OUTPUT(Input, o);
				o.strandTangentWS = TransformObjectToWorldNormal(hair.tangentOS);
				o.strandColor = hair.strandDebugColor;
				o.strandUV = hair.strandUV;
			}
#else
			o.strandTangentWS = TransformObjectToWorldNormal(v.tangent.xyz);
			o.strandColor = float3(1.0, 1.0, 1.0);
			o.strandUV = float2(0.5, 0.5);
#endif
		}

		void StrandSurf(Input IN, inout SurfaceOutput o)
		{
#ifdef SHADER_API_D3D11
			float3 normalTS = GetStrandNormalTangentSpace(IN.strandUV);
#else
			float3 normalTS = float3(0.0, 0.0, 1.0);
#endif

			o.Albedo = IN.strandColor;
			o.Normal = TransformTangentToWorld(normalTS,
				float3x3(
					normalize(IN.strandTangentWS),
					normalize(cross(IN.strandTangentWS, o.Normal)),
					o.Normal)
			);
		}

		ENDCG
	}

	Fallback "Hair/Default/HairMaterialDefaultUnlit"
}
