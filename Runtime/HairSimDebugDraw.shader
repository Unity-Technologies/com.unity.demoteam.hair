Shader "Hidden/HairSimDebugDraw"
{
	HLSLINCLUDE

	#pragma target 5.0

	#pragma multi_compile __ LAYOUT_INTERLEAVED
	// 0 == particles grouped by strand, i.e. root, root+1, root, root+1
	// 1 == particles grouped by index, i.e. root, root, root+1, root+1
	
	#pragma multi_compile __ VOLUME_TARGET_INITIAL_POSE VOLUME_TARGET_INITIAL_POSE_IN_PARTICLES
	// 0 == uniform target density
	// 1 == non uniform target density

	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

	#include "HairSimData.hlsl"
	#include "HairSimComputeConfig.hlsl"
	#include "HairSimComputeSolverBoundaries.hlsl"
	#include "HairSimComputeVolumeUtility.hlsl"
	#include "HairSimDebugDrawUtility.hlsl"
	
	uint _DebugSliceAxis;
	float _DebugSliceOffset;
	float _DebugSliceDivider;
	float _DebugSliceOpacity;
	float _DebugIsosurfaceDensity;
	uint _DebugIsosurfaceSubsteps;

	struct DebugVaryings
	{
		float4 positionCS : SV_POSITION;
		float4 color : TEXCOORD0;
	};

	DebugVaryings DebugVert_StrandParticle(uint instanceID : SV_InstanceID, uint vertexID : SV_VertexID)
	{
	#if LAYOUT_INTERLEAVED
		const uint strandParticleBegin = instanceID;
		const uint strandParticleStride = _StrandCount;
	#else
		const uint strandParticleBegin = instanceID * _StrandParticleCount;
		const uint strandParticleStride = 1;
	#endif

	#if DEBUG_STRAND_31_32 == 2
		if (vertexID > 1)
			vertexID = 1;
	#endif

		uint i = strandParticleBegin + strandParticleStride * vertexID;
		float3 worldPos = _ParticlePosition[i].xyz;

		DebugVaryings output;
		output.positionCS = TransformWorldToHClip(GetCameraRelativePositionWS(worldPos));
		output.color = float4(ColorCycle(instanceID, _StrandCount), 1.0);
		return output;
	}

	DebugVaryings DebugVert_StrandParticleWorldVelocity(uint instanceID : SV_InstanceID, uint vertexID : SV_VertexID)
	{
	#if LAYOUT_INTERLEAVED
		const uint strandParticleBegin = instanceID;
		const uint strandParticleStride = _StrandCount;
	#else
		const uint strandParticleBegin = instanceID * _StrandParticleCount;
		const uint strandParticleStride = 1;
	#endif

		uint i = strandParticleBegin + strandParticleStride * (vertexID >> 2);
		float3 worldPos = _ParticlePosition[i].xyz;

		if (vertexID & 2)
		{
			if (!(vertexID & 1))
			{
				worldPos -= _DT * _ParticleVelocityPrev[i].xyz;
			}
		}
		else
		{
			if (vertexID & 1)
			{
				worldPos += _DT * _ParticleVelocity[i].xyz;
			}
		}

		DebugVaryings output;
		output.positionCS = TransformWorldToHClip(GetCameraRelativePositionWS(worldPos));
		output.color = float4(0.0, vertexID & 1, vertexID & 2, 1.0);
		return output;

	}

	DebugVaryings DebugVert_StrandParticleClusters(uint instanceID : SV_InstanceID, uint vertexID : SV_VertexID)
	{
#if LAYOUT_INTERLEAVED
		const uint strandParticleBegin = instanceID;
		const uint strandParticleStride = _StrandCount;
#else
		const uint strandParticleBegin = instanceID * _StrandParticleCount;
		const uint strandParticleStride = 1;
#endif

		uint i = strandParticleBegin + strandParticleStride * (vertexID >> 1);

		uint strandIndexLo = _LODGuideIndex[(_LODIndexLo * _StrandCount) + instanceID];
		uint strandIndexHi = _LODGuideIndex[(_LODIndexHi * _StrandCount) + instanceID];

#if LAYOUT_INTERLEAVED
		const uint strandParticleBeginLo = strandIndexLo;
		const uint strandParticleBeginHi = strandIndexHi;
#else
		const uint strandParticleBeginLo = strandIndexLo * _StrandParticleCount;
		const uint strandParticleBeginHi = strandIndexHi * _StrandParticleCount;
#endif

		float3 worldPosLo = _ParticlePosition[strandParticleBeginLo + strandParticleStride * (vertexID >> 1)].xyz;
		float3 worldPosHi = _ParticlePosition[strandParticleBeginHi + strandParticleStride * (vertexID >> 1)].xyz;

		float3 colorLo = ColorCycle(strandIndexLo, _LODGuideCount[_LODIndexLo]);
		float3 colorHi = ColorCycle(strandIndexHi, _LODGuideCount[_LODIndexHi]);

		float3 worldPos = lerp(worldPosLo, worldPosHi, _LODBlendFraction);
		float3 color = lerp(colorLo, colorHi, _LODBlendFraction);

		if (vertexID & 1)
		{
			worldPos = _ParticlePosition[i].xyz;
			//color = ColorCycle(instanceID, _StrandCount);
		}

		DebugVaryings output;
		output.positionCS = TransformWorldToHClip(GetCameraRelativePositionWS(worldPos));
		output.color = float4(color, 1.0);
		return output;
	}

	/*
	DebugVaryings DebugVert_StrandParticleMotion(uint instanceID : SV_InstanceID, uint vertexID : SV_VertexID)
	{
	#if LAYOUT_INTERLEAVED
		const uint strandParticleBegin = instanceID;
		const uint strandParticleStride = _StrandCount;
	#else
		const uint strandParticleBegin = instanceID * _StrandParticleCount;
		const uint strandParticleStride = 1;
	#endif

		uint i = strandParticleBegin + strandParticleStride * vertexID;
		float3 worldPos0 = _ParticlePositionPrev[i].xyz;
		float3 worldPos1 = _ParticlePosition[i].xyz;

		float4 clipPos0 = mul(UNITY_MATRIX_PREV_VP, float4(GetCameraRelativePositionWS(worldPos0), 1.0));
		float4 clipPos1 = mul(UNITY_MATRIX_UNJITTERED_VP, float4(GetCameraRelativePositionWS(worldPos1), 1.0));

		float2 ndc0 = clipPos0.xy / clipPos0.w;
		float2 ndc1 = clipPos1.xy / clipPos1.w;

		DebugVaryings output;
		output.positionCS = TransformWorldToHClip(GetCameraRelativePositionWS(worldPos1));
		output.color = float4(0.5 * (ndc1 - ndc0), 0, 0);
		return output;
	}
	*/

	DebugVaryings DebugVert_VolumeDensity(uint vertexID : SV_VertexID)
	{
		uint3 volumeIdx = VolumeFlatIndexToIndex(vertexID);
		float volumeDensity = _VolumeDensity[volumeIdx];
		float3 worldPos = (volumeDensity == 0.0) ? 1e+7 : VolumeIndexToWorld(volumeIdx);

		DebugVaryings output;
		output.positionCS = TransformWorldToHClip(GetCameraRelativePositionWS(worldPos));
		output.color = float4(ColorDensity(volumeDensity), 1.0);
		return output;
	}

	DebugVaryings DebugVert_VolumeGradient(uint vertexID : SV_VertexID)
	{
		uint3 volumeIdx = VolumeFlatIndexToIndex(vertexID >> 1);
		float3 volumeGradient = _VolumePressureGrad[volumeIdx];
		float3 worldPos = VolumeIndexToWorld(volumeIdx);

		if (vertexID & 1)
		{
			worldPos -= volumeGradient;
		}

		DebugVaryings output;
		output.positionCS = TransformWorldToHClip(GetCameraRelativePositionWS(worldPos));
		output.color = float4(ColorGradient(volumeGradient), 1.0);
		return output;
	}

	DebugVaryings DebugVert_VolumeSlice(uint vertexID : SV_VertexID)
	{
		float3 uvw = float3(((vertexID >> 1) ^ vertexID) & 1, vertexID >> 1, _DebugSliceOffset);
		float3 uvwWorld = (_DebugSliceAxis == 0) ? uvw.zxy : (_DebugSliceAxis == 1 ? uvw.xzy : uvw.xyz);
		float3 worldPos = lerp(_VolumeWorldMin, _VolumeWorldMax, uvwWorld);

		uvw = uvwWorld;

		DebugVaryings output;
		output.positionCS = TransformWorldToHClip(GetCameraRelativePositionWS(worldPos));
		output.color = float4(uvw, 1);
		return output;
	}

	float4 DebugFrag(DebugVaryings input) : SV_Target
	{
		return input.color;
	}

	float4 DebugFrag_VolumeSlice(DebugVaryings input) : SV_Target
	{
		float3 uvw = input.color.xyz;

		float3 localPos = VolumeUVWToLocal(uvw);
		float3 localPosFloor = round(localPos + 0.5);

		float3 gridAxis = float3(_DebugSliceAxis != 0, _DebugSliceAxis != 1, _DebugSliceAxis != 2);
		float3 gridDist = gridAxis * abs(localPos - localPosFloor);
		float3 gridWidth = gridAxis * fwidth(localPos);

		if (any(gridDist < gridWidth))
		{
			uint i = ((uint)localPosFloor[_DebugSliceAxis]) % 3;
			return float4(0.2 * float3(i == 0, i == 1, i == 2), _DebugSliceOpacity);
		}

		float volumeDensity = VolumeSampleScalar(_VolumeDensity, uvw);
#if VOLUME_TARGET_INITIAL_POSE || VOLUME_TARGET_INITIAL_POSE_IN_PARTICLES
		float volumeDensity0 = VolumeSampleScalar(_VolumeDensity0, uvw);
#else
		float volumeDensity0 = 1.0;
#endif
		float3 volumeVelocity = VolumeSampleVector(_VolumeVelocity, uvw);
		float volumeDivergence = VolumeSampleScalar(_VolumeDivergence, uvw);
		float volumePressure = VolumeSampleScalar(_VolumePressure, uvw);
		float3 volumePressureGrad = VolumeSampleVector(_VolumePressureGrad, uvw);

#if 0
		float3 worldPos = lerp(_VolumeWorldMin, _VolumeWorldMax, uvw);
		float sd = BoundaryDistance(worldPos);
		if (abs(sd) < 0.1)
		{
			float3 sdNormal = 0.5 + 0.5 * BoundaryNormal(worldPos, sd);
			return float4(sdNormal, _DebugSliceOpacity);
		}
		else
		{
			sd *= 10.0;
			if (sd < 0.0)
			{
				return float4(frac(-sd), 0, frac(-sd), _DebugSliceOpacity);
			}
			else
			{
				return float4(0, frac(sd), frac(sd), _DebugSliceOpacity);
			}
		}
#endif

		// test fake level-set
		/*
		if (_DebugSliceDivider == 2.0)
		{
			float3 step = VolumeLocalToUVW(1.0);
			float d_min = 1e+4;
			float r_max = 0.0;

			for (int i = -1; i <= 1; i++)
			{
				for (int j = -1; j <= 1; j++)
				{
					float3 uvw_xz = float3(
						uvw.x + i * step.x,
						uvw.y,
						uvw.z + j * step.z);

					float vol = abs(VolumeSampleScalar(_VolumeDensity, uvw_xz));
					if (vol > 0.0)
					{
						float vol_d = length(float2(i, j));
						float vol_r = pow((3.0 * vol) / (4.0 * 3.14159), 1.0 / 3.0);

						d_min = min(d_min, vol_d - vol_r);
						r_max = max(r_max, vol_r);
					}
				}
			}

			float4 color_air = float4(0.0, 0.0, 0.0, 1.0);
			float4 color_int = float4(1.0, 0.0, 0.0, 1.0);
			float4 color_ext = float4(1.0, 1.0, 0.0, 1.0);

			return float4(r_max.xxx, 1.0);

			if (d_min == 1e+4)
				return color_air;
			else if (d_min < -0.5)
				return color_int * float4(-d_min.xxx, 1.0);
			else
				return color_ext * float4(1.0 - d_min.xxx, 1.0);
		}
		*/

		float x = uvw.x + _DebugSliceDivider;
		if (x < 1.0)
			return float4(ColorDensity(volumeDensity), _DebugSliceOpacity);
		else if (x < 2.0)
			return float4(float3(1.0, 1.0, 0.0) * ColorDensity(volumeDensity0), _DebugSliceOpacity);
		else if (x < 3.0)
			return float4(ColorVelocity(volumeVelocity), _DebugSliceOpacity);
		else if (x < 4.0)
			return float4(ColorDivergence(volumeDivergence), _DebugSliceOpacity);
		else if (x < 5.0)
			return float4(ColorPressure(volumePressure), _DebugSliceOpacity);
		else
			return float4(ColorGradient(volumePressureGrad), _DebugSliceOpacity);
	}

	DebugVaryings DebugVert_VolumeIsosurface(float3 position : POSITION)
	{
		float3 positionRWS = TransformObjectToWorld(position);
		float3 positionWS = GetAbsolutePositionWS(positionRWS);

		DebugVaryings output;
		output.positionCS = TransformWorldToHClip(positionRWS);
		output.color = float4(positionWS, 1);
		return output;
	}

	float4 DebugFrag_VolumeIsosurface(DebugVaryings input) : SV_Target
	{
		const float3 worldPos = input.color.xyz;
		const float3 worldPosCamera = -GetCameraRelativePositionWS(float3(0, 0, 0));
		const float3 worldDir = normalize(worldPos - worldPosCamera);

		const int numStepsWithinCell = _DebugIsosurfaceSubsteps;
		const int numSteps = _VolumeCells.x * numStepsWithinCell;

		VolumeTraceState trace = VolumeTraceBegin(worldPos, worldDir, 0.5, numStepsWithinCell);

		float3 accuDensity = 0;

		for (int i = 0; i != numSteps; i++)
		{
			if (VolumeTraceStep(trace))
			{
				float rho = VolumeSampleScalar(_VolumeDensity, trace.uvw);
				if (rho > _DebugIsosurfaceDensity)
				{
					float3 gradDensity = normalize(VolumeSampleScalarGradient(_VolumeDensity, trace.uvw));
					return float4(0.5 * gradDensity + 0.5, 1.0);
				}

				accuDensity.r += rho;
			}
			else
			{
				accuDensity.g = 1.0 - (i + 1.0) / numSteps;
				break;
			}
		}

		return float4(saturate(accuDensity), 0.5);
	}

	ENDHLSL

	SubShader
	{
		Cull Off
		ZTest LEqual
		ZWrite On

		Pass// 0 == STRAND PARTICLE POSITION
		{
			HLSLPROGRAM

			#pragma vertex DebugVert_StrandParticle
			#pragma fragment DebugFrag

			ENDHLSL
		}

		Pass// 1 == STRAND PARTICLE WORLD VELOCITY
		{
			HLSLPROGRAM

			#pragma vertex DebugVert_StrandParticleWorldVelocity
			#pragma fragment DebugFrag

			ENDHLSL
		}

		Pass// 2 == VOLUME DENSITY
		{
			HLSLPROGRAM

			#pragma vertex DebugVert_VolumeDensity
			#pragma fragment DebugFrag

			ENDHLSL
		}

		Pass// 3 == VOLUME GRADIENT
		{
			HLSLPROGRAM

			#pragma vertex DebugVert_VolumeGradient
			#pragma fragment DebugFrag

			ENDHLSL
		}

		Pass// 4 == VOLUME SLICE
		{
			Blend SrcAlpha OneMinusSrcAlpha

			HLSLPROGRAM

			#pragma vertex DebugVert_VolumeSlice
			#pragma fragment DebugFrag_VolumeSlice

			ENDHLSL
		}

		Pass// 5 == VOLUME SLICE (BELOW)
		{
			Blend SrcAlpha OneMinusSrcAlpha
			ZTest Greater
			ZWrite Off

			HLSLPROGRAM

			#pragma vertex DebugVert_VolumeSlice
			#pragma fragment DebugFrag_VolumeSlice

			ENDHLSL
		}

		Pass// 6 == VOLUME ISOSURFACE
		{
			Blend SrcAlpha OneMinusSrcAlpha
			Cull Back
			ZTest Always
			ZWrite Off

			HLSLPROGRAM

			#pragma vertex DebugVert_VolumeIsosurface
			#pragma fragment DebugFrag_VolumeIsosurface

			ENDHLSL
		}

		Pass// 7 == STRAND PARTICLE CLUSTERS
		{
			HLSLPROGRAM

			#pragma vertex DebugVert_StrandParticleClusters
			#pragma fragment DebugFrag

			ENDHLSL
		}
	}
}
