Shader "Hidden/Hair/HairMaterialDefaultLit"
{
	SubShader
	{
		Tags { "RenderType" = "Opaque" }

		CGPROGRAM

		#pragma target 5.0
		#pragma exclude_renderers xboxone

		#pragma surface StrandSurf Lambert addshadow fullforwardshadows vertex:StrandVert

#if SHADER_API_D3D11
		#ifdef SHADER_API_XBOXONE
		#undef SHADER_API_XBOXONE
		#endif
		#ifndef UNITY_COMMON_INCLUDED
		#define UNITY_COMMON_INCLUDED
		#endif
		#ifndef UNITY_MATRIX_I_M
		#define UNITY_MATRIX_I_M unity_WorldToObject
		#endif
		#define real float
		#define real3 float3
		#define real3x3 float3x3
		#define SafeNormalize(v) (normalize(v))
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

		#pragma multi_compile __ LAYOUT_INTERLEAVED
		// 0 == particles grouped by strand, i.e. root, root+1, root, root+1
		// 1 == particles grouped by index, i.e. root, root, root+1, root+1

		#pragma multi_compile __ STAGING_COMPRESSION
		// 0 == staging data full precision
		// 1 == staging data compressed

		#include "HairVertex.hlsl"
#endif

		struct Input
		{
			float3 strandColor;
			float2 strandUV;
		};

		void StrandVert(inout appdata_full v, out Input o)
		{
#if SHADER_API_D3D11
			HairVertex vhair = GetHairVertex((uint)v.texcoord.x, (float2)v.texcoord1.xy, v.vertex.xyz, v.normal.xyz, v.tangent.xyz);
			{
				v.vertex = float4(vhair.positionOS, 1.0);
				v.normal = float4(vhair.normalOS, 0.0);
				v.tangent = float4(vhair.tangentOS, 0.0);

				UNITY_INITIALIZE_OUTPUT(Input, o);
				o.strandColor = vhair.debugColor;
				o.strandUV = vhair.strandUV;
			}
#endif
		}

		void StrandSurf(Input IN, inout SurfaceOutput o)
		{
#if SHADER_API_D3D11
			o.Albedo = IN.strandColor;
			o.Normal = GetHairNormalTS(IN.strandUV) * float3(-1, 1, 1);
#endif
		}

		ENDCG
	}
	Fallback "Hidden/Hair/HairMaterialDefaultUnlit"
}
