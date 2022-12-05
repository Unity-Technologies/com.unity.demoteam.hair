Shader "Hair/Default/HairMaterialDefaultLitBuiltin"
{
	CGINCLUDE

	#pragma target 5.0

	#pragma multi_compile __ STAGING_COMPRESSION
	// 0 == staging data full precision
	// 1 == staging data compressed

	#pragma multi_compile HAIR_VERTEX_ID_LINES HAIR_VERTEX_ID_STRIPS
	// *_LINES == render as line segments
	// *_STRIPS == render as view facing strips

	#pragma multi_compile HAIR_VERTEX_SRC_SOLVER HAIR_VERTEX_SRC_STAGING
	// *_SOLVER == source vertex from solver data
	// *_STAGING == source vertex data from staging data

	#define UNITY_COMMON_INCLUDED

	#ifndef real
	#define real float
	#endif
	#ifndef real3
	#define real3 float3
	#endif
	#ifndef real3x3
	#define real3x3 float3x3
	#endif
	#ifndef SafeNormalize
	#define SafeNormalize(v) (normalize(v))
	#endif

	#include "Packages/com.unity.shadergraph/ShaderGraphLibrary/ShaderConfig.cs.hlsl"
	#ifdef SHADEROPTIONS_CAMERA_RELATIVE_RENDERING
	#undef SHADEROPTIONS_CAMERA_RELATIVE_RENDERING
	#define SHADEROPTIONS_CAMERA_RELATIVE_RENDERING (0)
	#endif

	#ifndef UNITY_MATRIX_M
	#define UNITY_MATRIX_M unity_ObjectToWorld
	#endif
	#ifndef UNITY_MATRIX_I_M
	#define UNITY_MATRIX_I_M unity_WorldToObject
	#endif
	#ifndef UNITY_PREV_MATRIX_M
	#define UNITY_PREV_MATRIX_M UNITY_MATRIX_M
	#endif
	#ifndef UNITY_PREV_MATRIX_I_M
	#define UNITY_PREV_MATRIX_I_M UNITY_MATRIX_I_M
	#endif

	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	#include "Packages/com.unity.shadergraph/ShaderGraphLibrary/ShaderVariables.hlsl"
	#include "Packages/com.unity.shadergraph/ShaderGraphLibrary/ShaderVariablesFunctions.hlsl"

#ifdef SHADER_API_D3D11
	#include "Packages/com.unity.demoteam.hair/Runtime/HairVertex.hlsl"
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
