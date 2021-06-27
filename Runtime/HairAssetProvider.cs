using System;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.DemoTeam.Hair
{
	public interface IHairAssetProvider
	{
		bool GenerateRoots(in HairAssetProvider.GeneratedRoots roots);
	}

	public abstract class HairAssetProvider : ScriptableObject, IHairAssetProvider
	{
		public virtual bool GenerateRoots(in GeneratedRoots roots) => false;

		public struct GeneratedRoots : IDisposable
		{
			public struct StrandParameters
			{
				public float normalizedStrandLength;
				public float normalizedStrandDiameter;
				public float normalizedCurlRadius;
				public float normalizedCurlSlope;

				public static readonly StrandParameters defaults = new StrandParameters()
				{
					normalizedStrandLength = 1.0f,
					normalizedStrandDiameter = 1.0f,
					normalizedCurlRadius = 1.0f,
					normalizedCurlSlope = 1.0f,
				};
			}

			public int strandCount;
			public NativeArray<Vector2> rootUV;
			public NativeArray<Vector3> rootPosition;
			public NativeArray<Vector3> rootDirection;
			public NativeArray<StrandParameters> rootParameters;// R,G,B,A == Strand length, Strand diameter, Curl radius, Curl slope

			public GeneratedRoots(int strandCount, Allocator allocator = Allocator.Temp)
			{
				this.strandCount = strandCount;
				rootUV = new NativeArray<Vector2>(strandCount, allocator, NativeArrayOptions.UninitializedMemory);
				rootPosition = new NativeArray<Vector3>(strandCount, allocator, NativeArrayOptions.UninitializedMemory);
				rootDirection = new NativeArray<Vector3>(strandCount, allocator, NativeArrayOptions.UninitializedMemory);
				rootParameters = new NativeArray<StrandParameters>(strandCount, allocator, NativeArrayOptions.UninitializedMemory);
			}

			public unsafe void GetUnsafePtrs(out Vector3* rootPositionPtr, out Vector3* rootDirectionPtr, out Vector2* rootUVPtr, out StrandParameters* rootParametersPtr)
			{
				rootUVPtr = (Vector2*)rootUV.GetUnsafePtr();
				rootPositionPtr = (Vector3*)rootPosition.GetUnsafePtr();
				rootDirectionPtr = (Vector3*)rootDirection.GetUnsafePtr();
				rootParametersPtr = (StrandParameters*)rootParameters.GetUnsafePtr();
			}

			public void Dispose()
			{
				rootUV.Dispose();
				rootPosition.Dispose();
				rootDirection.Dispose();
				rootParameters.Dispose();
			}
		}

		public struct GeneratedStrands : IDisposable
		{
			public int strandCount;
			public int strandParticleCount;
			public NativeArray<Vector3> particlePosition;

			public GeneratedStrands(int strandCount, int strandParticleCount, Allocator allocator = Allocator.Temp)
			{
				this.strandCount = strandCount;
				this.strandParticleCount = strandParticleCount;
				particlePosition = new NativeArray<Vector3>(strandCount * strandParticleCount, allocator, NativeArrayOptions.UninitializedMemory);
			}

			public unsafe void GetUnsafePtrs(out Vector3* particlePositionPtr)
			{
				particlePositionPtr = (Vector3*)particlePosition.GetUnsafePtr();
			}

			public void Dispose()
			{
				particlePosition.Dispose();
			}
		}
	}
}
