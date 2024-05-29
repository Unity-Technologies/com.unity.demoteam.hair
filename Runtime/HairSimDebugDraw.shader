Shader "Hidden/HairSimDebugDraw"
{
	HLSLINCLUDE

	#pragma target 5.0

	#pragma multi_compile __ VOLUME_TARGET_INITIAL_POSE VOLUME_TARGET_INITIAL_POSE_IN_PARTICLES
	// 0 == uniform target density
	// 1 == non uniform target density

	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	#include "Packages/com.unity.shadergraph/ShaderGraphLibrary/ShaderVariables.hlsl"
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

	#include "HairSimData.hlsl"
	#include "HairSimComputeConfig.hlsl"
	#include "HairSimComputeSolverBoundaries.hlsl"
	#include "HairSimComputeSolverQuaternion.hlsl"
	#include "HairSimComputeVolumeUtility.hlsl"
	#include "HairSimComputeVolumeProbe.hlsl"
	#include "HairSimDebugDrawColors.hlsl"

	int _DebugCluster;
	uint _DebugSliceAxis;
	float _DebugSliceOffset;
	float _DebugSliceDivider;
	float _DebugSliceOpacity;
	float _DebugIsosurfaceDensity;
	uint _DebugIsosurfaceSubsteps;

	struct DebugVaryings
	{
		float4 positionCS : SV_POSITION;
		float pointSize : PSIZE;
		float4 color : TEXCOORD0;
	};

	float4 WorldToClip(float3 worldPos)
	{
		return TransformWorldToHClip(worldPos);
	}

	float4 FilterClusters(float4 worldPos, uint strandIndex, in LODIndices lodDesc)
	{
		if (_DebugCluster >= 0)
		{
			int guideIndexLo = _LODGuideIndex[(lodDesc.lodIndexLo * _StrandCount) + strandIndex];
			int guideIndexHi = _LODGuideIndex[(lodDesc.lodIndexHi * _StrandCount) + strandIndex];

			if (guideIndexLo != _DebugCluster && guideIndexHi != _DebugCluster)
			{
				return asfloat(0x7FC00000u);// => NaN
			}
		}

		return worldPos;
	}

	DebugVaryings DebugVert_StrandRootFrame(uint instanceID : SV_InstanceID, uint vertexID : SV_VertexID)
	{
		const LODIndices lodDesc = _SolverLODStage[SOLVERLODSTAGE_PHYSICS];

		const uint strandIndex = instanceID;
		const uint strandParticleBegin = strandIndex * _StrandParticleOffset;
		const uint strandParticleStride = _StrandParticleStride;

		float3 worldPos = _RootPositionNext[strandIndex].xyz;

		float4 rootFrame = _RootFrame[strandIndex];
		float3 rootFrameAxis = float3(
			(vertexID < 2) ? 1.0 : 0.0,
			(vertexID & 2) ? 1.0 : 0.0,
			(vertexID & 4) ? 1.0 : 0.0);

		if (vertexID & 1)
		{
			worldPos += (0.25 * _GroupMaxParticleInterval * _RootScale[strandIndex].x) * QMul(rootFrame, rootFrameAxis);
		}

		DebugVaryings output;
		{
			output.positionCS = FilterClusters(WorldToClip(worldPos), strandIndex, lodDesc);
			output.pointSize = 1.0;
			output.color = float4(rootFrameAxis, 1.0);
		}

		return output;
	}

	DebugVaryings DebugVert_StrandParticlePosition(uint instanceID : SV_InstanceID, uint vertexID : SV_VertexID)
	{
		const LODIndices lodDesc = _SolverLODStage[SOLVERLODSTAGE_PHYSICS];

		const uint strandIndex = instanceID;
		const uint strandParticleBegin = strandIndex * _StrandParticleOffset;
		const uint strandParticleStride = _StrandParticleStride;

		uint i = strandParticleBegin + strandParticleStride * vertexID;
		float3 worldPos = _ParticlePosition[i].xyz;

		DebugVaryings output;
		{
			output.positionCS = FilterClusters(WorldToClip(worldPos), strandIndex, lodDesc);
			output.pointSize = 1.0;
			output.color = float4(ColorCycle(strandIndex, _StrandCount), 1.0);
		}

		return output;
	}

	DebugVaryings DebugVert_StrandParticleVelocity(uint instanceID : SV_InstanceID, uint vertexID : SV_VertexID)
	{
		const LODIndices lodDesc = _SolverLODStage[SOLVERLODSTAGE_PHYSICS];

		const uint strandIndex = instanceID;
		const uint strandParticleBegin = instanceID * _StrandParticleOffset;
		const uint strandParticleStride = _StrandParticleStride;

#if SECOND_ORDER_UPDATE
		uint i = strandParticleBegin + strandParticleStride * (vertexID >> 2);
#else
		uint i = strandParticleBegin + strandParticleStride * (vertexID >> 1);
#endif
		float3 worldPos = _ParticlePosition[i].xyz;

#if SECOND_ORDER_UPDATE
		if (vertexID & 2)
		{
			if (!(vertexID & 1))
			{
				worldPos -= _DT * _ParticleVelocityPrev[i].xyz;
			}
		}
		else
#endif
		{
			if (vertexID & 1)
			{
				worldPos += _DT * _ParticleVelocity[i].xyz;
			}
		}

		DebugVaryings output;
		output.positionCS = FilterClusters(WorldToClip(worldPos), strandIndex, lodDesc);
		output.pointSize = 1.0;
		output.color = float4(0.0, vertexID & 1, vertexID & 2, 1.0);
		return output;

	}

	DebugVaryings DebugVert_StrandParticleClusters(uint instanceID : SV_InstanceID, uint vertexID : SV_VertexID)
	{
		const LODIndices lodDesc = _SolverLODStage[SOLVERLODSTAGE_PHYSICS];

		const uint strandIndex = instanceID;
		const uint strandParticleBegin = strandIndex * _StrandParticleOffset;
		const uint strandParticleStride = _StrandParticleStride;

		uint i = strandParticleBegin + strandParticleStride * (vertexID >> 1);

		uint strandIndexLo = _LODGuideIndex[(lodDesc.lodIndexLo * _StrandCount) + strandIndex];
		uint strandIndexHi = _LODGuideIndex[(lodDesc.lodIndexHi * _StrandCount) + strandIndex];

		const uint strandParticleBeginLo = strandIndexLo * _StrandParticleOffset;
		const uint strandParticleBeginHi = strandIndexHi * _StrandParticleOffset;

		float3 worldPosLo = _ParticlePosition[strandParticleBeginLo + strandParticleStride * (vertexID >> 1)].xyz;
		float3 worldPosHi = _ParticlePosition[strandParticleBeginHi + strandParticleStride * (vertexID >> 1)].xyz;
		float3 worldPos = lerp(worldPosLo, worldPosHi, lodDesc.lodBlendFrac);

		float3 colorLo = ColorCycle(strandIndexLo, _LODGuideCount[_LODCount - 1]);
		float3 colorHi = ColorCycle(strandIndexHi, _LODGuideCount[_LODCount - 1]);
		float3 color = lerp(colorLo, colorHi, lodDesc.lodBlendFrac);

		if (vertexID & 1)
		{
			worldPos = _ParticlePosition[i].xyz;
		}

		DebugVaryings output;
		output.positionCS = FilterClusters(WorldToClip(worldPos), strandIndex, lodDesc);
		output.pointSize = 1.0;
		output.color = float4(color, 1.0);
		return output;
	}

	DebugVaryings DebugVert_VolumeCellDensity(uint vertexID : SV_VertexID)
	{
		const VolumeLODGrid lodGrid = _VolumeLODStage[VOLUMELODSTAGE_RESOLVE];

		uint3 volumeIdx = VolumeFlatIndexToIndex(vertexID);
		float volumeDensity = _VolumeDensity[volumeIdx];
		float3 worldPos = (volumeDensity == 0.0) ? 1e+7 : VolumeIndexToWorld(lodGrid, volumeIdx);

		DebugVaryings output;
		output.positionCS = WorldToClip(worldPos);
		output.pointSize = 1.0;
		output.color = float4(ColorDensity(volumeDensity), 1.0);
		return output;
	}

	DebugVaryings DebugVert_VolumeCellGradient(uint vertexID : SV_VertexID)
	{
		const VolumeLODGrid lodGrid = _VolumeLODStage[VOLUMELODSTAGE_RESOLVE];

		uint3 volumeIdx = VolumeFlatIndexToIndex(vertexID >> 1);
		float3 volumeGradient = _VolumePressureGrad[volumeIdx];
		float3 worldPos = VolumeIndexToWorld(lodGrid, volumeIdx);

		if (vertexID & 1)
		{
			worldPos -= volumeGradient;
		}

		DebugVaryings output;
		output.positionCS = WorldToClip(worldPos);
		output.pointSize = 1.0;
		output.color = float4(ColorGradient(volumeGradient), 1.0);
		return output;
	}

	DebugVaryings DebugVert_VolumeSlice(uint vertexID : SV_VertexID)
	{
		const VolumeLODGrid lodGrid = _VolumeLODStage[VOLUMELODSTAGE_RESOLVE];

		float3 uvw = float3(((vertexID >> 1) ^ vertexID) & 1, vertexID >> 1, _DebugSliceOffset);
		float3 uvwWorld = (_DebugSliceAxis == 0) ? uvw.zxy : (_DebugSliceAxis == 1 ? uvw.xzy : uvw.xyz);
		float3 worldPos = VolumeUVWToWorld(lodGrid, uvwWorld);

		uvw = uvwWorld;

		DebugVaryings output;
		output.positionCS = WorldToClip(worldPos);
		output.pointSize = 1.0;
		output.color = float4(uvw, 1);
		return output;
	}
	
	DebugVaryings DebugVert_VolumeIsosurface(float3 position : POSITION)
	{
		float3 worldPos = TransformObjectToWorld(position);

		DebugVaryings output;
		output.positionCS = WorldToClip(worldPos);
		output.pointSize = 1.0;
		output.color = float4(worldPos, 1);
		return output;
	}

	float4 DebugFrag(DebugVaryings input) : SV_Target
	{
		return input.color;
	}

	float4 DebugFrag_VolumeSlice(DebugVaryings input) : SV_Target
	{
		const VolumeLODGrid lodGrid = _VolumeLODStage[VOLUMELODSTAGE_RESOLVE];

		float3 uvw = input.color.xyz;

		float3 localPos = VolumeUVWToLocal(lodGrid, uvw);
		float3 localPosFloor = round(localPos + 0.5);

		float3 gridNormal = float3(_DebugSliceAxis == 0, _DebugSliceAxis == 1, _DebugSliceAxis == 2);
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
		float4 volumeThicknessSH = _VolumeScattering.SampleLevel(_Volume_trilinear_clamp, uvw, 0);
		float volumeThickness = DecodeStrandCount(/*gridNormal*/float3(1,0,0), volumeThicknessSH);
		float3 volumeImpulse = VolumeSampleVector(_VolumeImpulse, uvw);

#if 0
		float3 worldPos = VolumeUVWToWorld(lodGrid, uvw);
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
			float3 step = VolumeLocalToUVW(lodGrid, 1.0);
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

					float vol = abs(VolumeSampleScalar(lodGrid, _VolumeDensity, uvw_xz));
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
		else if (x < 6.0)
			return float4(ColorGradient(volumePressureGrad), _DebugSliceOpacity);
		else if (x < 7.0)
			return float4(ColorDensity(0.1 * volumeThickness), _DebugSliceOpacity);
		else
			return float4(ColorVelocity(volumeImpulse), _DebugSliceOpacity);
	}

	float4 DebugFrag_VolumeIsosurface(DebugVaryings input) : SV_Target
	{
		const VolumeLODGrid lodGrid = _VolumeLODStage[VOLUMELODSTAGE_RESOLVE];

		const float3 worldPos = input.color.xyz;
		const float3 worldPosCamera = -GetCameraRelativePositionWS(float3(0, 0, 0));
		const float3 worldDir = normalize(worldPos - worldPosCamera);

		const int numStepsWithinCell = _DebugIsosurfaceSubsteps;
		const int numSteps = lodGrid.volumeCellCount.x * numStepsWithinCell;

		VolumeTraceState trace = VolumeTraceBegin(lodGrid, worldPos, worldDir, 0.5, numStepsWithinCell);

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

		Pass// 0 == STRAND ROOT FRAME
		{
			HLSLPROGRAM

			#pragma vertex DebugVert_StrandRootFrame
			#pragma fragment DebugFrag

			ENDHLSL
		}

		Pass// 1 == STRAND PARTICLE POSITION
		{
			HLSLPROGRAM

			#pragma vertex DebugVert_StrandParticlePosition
			#pragma fragment DebugFrag

			ENDHLSL
		}

		Pass// 1 == STRAND PARTICLE VELOCITY
		{
			HLSLPROGRAM

			#pragma vertex DebugVert_StrandParticleVelocity
			#pragma fragment DebugFrag

			ENDHLSL
		}

		Pass// 7 == STRAND PARTICLE CLUSTERS
		{
			HLSLPROGRAM

			#pragma vertex DebugVert_StrandParticleClusters
			#pragma fragment DebugFrag

			ENDHLSL
		}

		Pass// 2 == VOLUME CELL DENSITY
		{
			HLSLPROGRAM

			#pragma vertex DebugVert_VolumeCellDensity
			#pragma fragment DebugFrag

			ENDHLSL
		}

		Pass// 3 == VOLUME CELL GRADIENT
		{
			HLSLPROGRAM

			#pragma vertex DebugVert_VolumeCellGradient
			#pragma fragment DebugFrag

			ENDHLSL
		}

		Pass// 4 == VOLUME SLICE (ABOVE)
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
	}
}
