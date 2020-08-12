#ifndef __HAIRSIM_SOLVERDATA__
#define __HAIRSIM_SOLVERDATA__

#if HAIRSIM_WRITEABLE_SOLVERDATA
#define HAIRSIM_WRITEABLE_BUFFER RWStructuredBuffer
#else
#define HAIRSIM_WRITEABLE_BUFFER StructuredBuffer
#endif

float4x4 _LocalToWorld;
float4x4 _LocalToWorldInvT;

uint _StrandCount;
uint _StrandParticleCount;
float _StrandParticleInterval;
float _StrandParticleVolume;
float _StrandParticleContrib;// TODO remove

float _DT;
uint _Iterations;
float _Stiffness;
float _Relaxation;
float _Damping;
float _Gravity;
float _Repulsion;
float _Friction;
float _BendingCurvature;
float _DampingFTL;

StructuredBuffer<float4> _RootPosition;
StructuredBuffer<float4> _RootTangent;

StructuredBuffer<float4> _ParticlePositionPrev;
StructuredBuffer<float4> _ParticleVelocityPrev;

HAIRSIM_WRITEABLE_BUFFER<float4> _ParticlePosition;
HAIRSIM_WRITEABLE_BUFFER<float4> _ParticlePositionCorr;
HAIRSIM_WRITEABLE_BUFFER<float4> _ParticleVelocity;

#endif//__HAIRSIM_SOLVERDATA__
