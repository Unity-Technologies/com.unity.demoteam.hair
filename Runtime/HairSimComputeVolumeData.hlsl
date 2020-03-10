#ifndef __HAIRSIMCOMPUTE_VOLUMEDATA__
#define __HAIRSIMCOMPUTE_VOLUMEDATA__

#if HAIRSIM_WRITEABLE_VOLUMEDATA
#define HAIRSIM_WRITEABLE_VOLUME RWTexture3D
#else
#define HAIRSIM_WRITEABLE_VOLUME Texture3D
#endif

float3 _VolumeCells;
float3 _VolumeWorldMin;
float3 _VolumeWorldMax;
/*
	_VolumeCells = (3, 3, 3)

			  +---+---+---Q
		 +---+---+---+    |
	+---+---+---+    | ---+
	|   |   |   | ---+    |
	+---+---+---+    | ---+
	|   |   |   | ---+    |
	+---+---+---+    | ---+
	|   |   |   | ---+
	P---+---+---+

	_VolumeWorldMin = P
	_VolumeWorldMax = Q
*/

HAIRSIM_WRITEABLE_VOLUME<int> _AccuDensity;
HAIRSIM_WRITEABLE_VOLUME<int> _AccuVelocityX;// this sure would be nice: https://developer.nvidia.com/unlocking-gpu-intrinsics-hlsl
HAIRSIM_WRITEABLE_VOLUME<int> _AccuVelocityY;
HAIRSIM_WRITEABLE_VOLUME<int> _AccuVelocityZ;

HAIRSIM_WRITEABLE_VOLUME<float> _VolumeDensity;
HAIRSIM_WRITEABLE_VOLUME<float3> _VolumeDensityGrad;
HAIRSIM_WRITEABLE_VOLUME<float4> _VolumeVelocity;

HAIRSIM_WRITEABLE_VOLUME<float> _VolumeDivergence;
HAIRSIM_WRITEABLE_VOLUME<float> _VolumePressure;
HAIRSIM_WRITEABLE_VOLUME<float> _VolumePressureIn;
HAIRSIM_WRITEABLE_VOLUME<float3> _VolumePressureGrad;

SamplerState _Volume_sampler_point_clamp;
SamplerState _Volume_sampler_trilinear_clamp;

#endif//__HAIRSIMCOMPUTE_VOLUMEDATA__
