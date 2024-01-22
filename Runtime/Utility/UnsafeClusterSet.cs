#define USE_JOBS

using System;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Jobs;

namespace Unity.DemoTeam.Hair
{
	using static PointSetMath;

	public enum ClusterAllocationPolicy
	{
		Global,
		SplitGlobal,
		SplitBranching,
	}

	public enum ClusterAllocationOrder
	{
		ByHighestError,// approximate
		ByHighestTally,// approximate
		ByHighestTallyBreadthFirst,// approximate
	}

	public enum ClusterVoid
	{
		Remove,
		Preserve,
		Reallocate,
	}

	[BurstCompile]
	public unsafe struct UnsafeClusterSet : IDisposable
	{
		public UnsafeList<Vector3> clusterPosition;
		public UnsafeList<float> clusterWeight;
		public UnsafeList<int> clusterTally;
		public UnsafeList<int> clusterGuide;
		public UnsafeList<int> clusterDepth;
		public UnsafeList<float> clusterCarry;
		public UnsafeList<float> clusterReach;

		public UnsafeList<int> sampleCluster;

		public DataDesc dataDesc;

		public struct DataDesc
		{
			public int clusterCapacity;
			public int clusterCount;
			public int clusterCountCommitted;

			public ClusterVoid clusterVoidPolicy;

			[NativeDisableUnsafePtrRestriction, NoAlias] public Vector3* clusterPositionPtr;// data valid after UpdateCentroids, SelectCentroids
			[NativeDisableUnsafePtrRestriction, NoAlias] public float* clusterWeightPtr;	// data valid after UpdateCentroids, SelectCentroids
			[NativeDisableUnsafePtrRestriction, NoAlias] public int* clusterTallyPtr;		// data valid after AssignSamples, SelectPreassigned
			[NativeDisableUnsafePtrRestriction, NoAlias] public int* clusterGuidePtr;		// data valid after Commit
			[NativeDisableUnsafePtrRestriction, NoAlias] public int* clusterDepthPtr;		// data valid after Commit
			[NativeDisableUnsafePtrRestriction, NoAlias] public float* clusterCarryPtr;		// data valid after Commit
			[NativeDisableUnsafePtrRestriction, NoAlias] public float* clusterReachPtr;		// data valid after Commit

			public int clusterPositionOffset;
			public int clusterPositionStride;
			public int clusterPositionCount;

			public int sampleCount;

			[NativeDisableUnsafePtrRestriction, NoAlias] public Vector3* samplePositionPtr;	// must be assigned prior to each operation
			[NativeDisableUnsafePtrRestriction, NoAlias] public float* sampleWeightPtr;		// must be assigned prior to each operation
			[NativeDisableUnsafePtrRestriction, NoAlias] public int* sampleResolvePtr;		// must be assigned prior to each operation
			[NativeDisableUnsafePtrRestriction, NoAlias] public int* sampleClusterPtr;		// data valid after AssignSamples, SelectPreassigned

			public int samplePositionOffset;
			public int samplePositionStride;
			public int samplePositionCount;

			public Unity.Mathematics.Random randSeq;
		}

		public UnsafeClusterSet(int clusterCapacity, ClusterVoid clusterVoidPolicy, int sampleCount, int samplePositionOffset, int samplePositionStride, int samplePositionCount, Allocator allocator)
		{
			this.clusterPosition = new UnsafeList<Vector3>(clusterCapacity * samplePositionCount, allocator, NativeArrayOptions.UninitializedMemory);
			this.clusterWeight = new UnsafeList<float>(clusterCapacity, allocator, NativeArrayOptions.UninitializedMemory);
			this.clusterTally = new UnsafeList<int>(clusterCapacity, allocator, NativeArrayOptions.UninitializedMemory);
			this.clusterGuide = new UnsafeList<int>(clusterCapacity, allocator, NativeArrayOptions.UninitializedMemory);
			this.clusterDepth = new UnsafeList<int>(clusterCapacity, allocator, NativeArrayOptions.UninitializedMemory);
			this.clusterCarry = new UnsafeList<float>(clusterCapacity, allocator, NativeArrayOptions.UninitializedMemory);
			this.clusterReach = new UnsafeList<float>(clusterCapacity, allocator, NativeArrayOptions.UninitializedMemory);

			this.sampleCluster = new UnsafeList<int>(sampleCount, allocator, NativeArrayOptions.UninitializedMemory);

			//.dataDesc
			{
				this.dataDesc.clusterCapacity = clusterCapacity;
				this.dataDesc.clusterCount = 0;
				this.dataDesc.clusterCountCommitted = 0;

				this.dataDesc.clusterVoidPolicy = clusterVoidPolicy;

				this.dataDesc.clusterPositionPtr = this.clusterPosition.Ptr;
				this.dataDesc.clusterWeightPtr = this.clusterWeight.Ptr;
				this.dataDesc.clusterTallyPtr = this.clusterTally.Ptr;
				this.dataDesc.clusterGuidePtr = this.clusterGuide.Ptr;
				this.dataDesc.clusterDepthPtr = this.clusterDepth.Ptr;
				this.dataDesc.clusterCarryPtr = this.clusterCarry.Ptr;
				this.dataDesc.clusterReachPtr = this.clusterReach.Ptr;

				this.dataDesc.clusterPositionOffset = samplePositionCount;
				this.dataDesc.clusterPositionStride = 1;
				this.dataDesc.clusterPositionCount = samplePositionCount;

				this.dataDesc.sampleCount = sampleCount;

				this.dataDesc.samplePositionPtr = null; // must be assigned prior to each operation
				this.dataDesc.sampleResolvePtr = null;  // must be assigned prior to each operation
				this.dataDesc.sampleWeightPtr = null;   // must be assigned prior to each operation
				this.dataDesc.sampleClusterPtr = this.sampleCluster.Ptr;

				this.dataDesc.samplePositionOffset = samplePositionOffset;
				this.dataDesc.samplePositionStride = samplePositionStride;
				this.dataDesc.samplePositionCount = samplePositionCount;

				this.dataDesc.randSeq = new Unity.Mathematics.Random(137);//TODO seed by param
			}

			for (int k = 0; k != clusterCapacity; k++)
			{
				this.dataDesc.clusterTallyPtr[k] = -1;
				this.dataDesc.clusterGuidePtr[k] = -1;
			}

			for (int i = 0; i != sampleCount; i++)
			{
				this.dataDesc.sampleClusterPtr[i] = -1;
			}
		}

		public void Dispose()
		{
			clusterPosition.Dispose();
			clusterWeight.Dispose();
			clusterTally.Dispose();
			clusterGuide.Dispose();
			clusterCarry.Dispose();
			clusterReach.Dispose();

			sampleCluster.Dispose();
		}

		void EnsureCapacity(int clusterCapacity)
		{
			if (clusterCapacity > dataDesc.clusterCapacity)
			{
				clusterPosition.Resize(clusterCapacity * dataDesc.clusterPositionCount);
				clusterWeight.Resize(clusterCapacity);
				clusterTally.Resize(clusterCapacity);
				clusterGuide.Resize(clusterCapacity);
				clusterDepth.Resize(clusterCapacity);
				clusterCarry.Resize(clusterCapacity);
				clusterReach.Resize(clusterCapacity);

				dataDesc.clusterPositionPtr = clusterPosition.Ptr;
				dataDesc.clusterWeightPtr = clusterWeight.Ptr;
				dataDesc.clusterTallyPtr = clusterTally.Ptr;
				dataDesc.clusterGuidePtr = clusterGuide.Ptr;
				dataDesc.clusterDepthPtr = clusterDepth.Ptr;
				dataDesc.clusterCarryPtr = clusterCarry.Ptr;
				dataDesc.clusterReachPtr = clusterReach.Ptr;

				dataDesc.clusterCapacity = clusterCapacity;

				for (int k = dataDesc.clusterCount; k != dataDesc.clusterCapacity; k++)
				{
					dataDesc.clusterTallyPtr[k] = -1;
					dataDesc.clusterGuidePtr[k] = -1;
				}
			}
		}

		static int CountEmptyClusters(ref DataDesc dataDesc)
		{
			var emptyClusterCount = 0;
			{
				for (int k = 0; k != dataDesc.clusterCount; k++)
				{
					if (dataDesc.clusterTallyPtr[k] == 0)
					{
						emptyClusterCount++;
					}
				}
			}
			return emptyClusterCount;
		}

		static bool FindClosestCluster(ref DataDesc dataDesc, int i, out float minDistSq, out int minDistIndex)
		{
			minDistSq = float.PositiveInfinity;
			minDistIndex = -1;

			for (int k = 0; k != dataDesc.clusterCount; k++)
			{
				var distSq = PsDistanceSq(
					dataDesc.clusterPositionPtr + dataDesc.clusterPositionOffset * k,
					dataDesc.clusterPositionStride,
					dataDesc.samplePositionPtr + dataDesc.samplePositionOffset * dataDesc.sampleResolvePtr[i],
					dataDesc.samplePositionStride,
					dataDesc.samplePositionCount);

				if (minDistSq > distSq)
				{
					minDistSq = distSq;
					minDistIndex = k;
				}
			}

			return (minDistIndex != -1);
		}

		static bool FindClosestSample(ref DataDesc dataDesc, int k, out float minDistSq, out int minDistIndex)
		{
			minDistSq = float.PositiveInfinity;
			minDistIndex = -1;

			for (int i = 0; i != dataDesc.sampleCount; i++)
			{
				var distSq = PsDistanceSq(
					dataDesc.clusterPositionPtr + dataDesc.clusterPositionOffset * k,
					dataDesc.clusterPositionStride,
					dataDesc.samplePositionPtr + dataDesc.samplePositionOffset * dataDesc.sampleResolvePtr[i],
					dataDesc.samplePositionStride,
					dataDesc.samplePositionCount);

				if (minDistSq > distSq)
				{
					minDistSq = distSq;
					minDistIndex = k;
				}
			}

			return (minDistIndex != -1);
		}

		static UnsafeList<float> FindAllClustersMaxErrorSq(ref DataDesc dataDesc, Allocator allocator)
		{
			var maxErrorSq = new UnsafeList<float>(dataDesc.clusterCount, allocator, NativeArrayOptions.UninitializedMemory);
			var maxErrorSqPtr = maxErrorSq.Ptr;
			{
				for (int k = 0; k != dataDesc.clusterCount; k++)
				{
					maxErrorSqPtr[k] = float.NegativeInfinity;
				}

				for (int i = 0; i != dataDesc.sampleCount; i++)
				{
					var k = dataDesc.sampleClusterPtr[i];
					if (k != -1)
					{
						var errorSq = PsDistanceSq(
							dataDesc.clusterPositionPtr + dataDesc.clusterPositionOffset * k,
							dataDesc.clusterPositionStride,
							dataDesc.samplePositionPtr + dataDesc.samplePositionOffset * dataDesc.sampleResolvePtr[i],
							dataDesc.samplePositionStride,
							dataDesc.samplePositionCount);

						if (maxErrorSqPtr[k] < errorSq)
							maxErrorSqPtr[k] = errorSq;
					}
				}
			}
			return maxErrorSq;
		}

		static UnsafeList<uint> FindAllClustersMaxErrorSortKey(ref DataDesc dataDesc, Allocator allocator)
		{
			var maxErrorSortKey = new UnsafeList<uint>(dataDesc.clusterCount, allocator, NativeArrayOptions.UninitializedMemory);
			var maxErrorSortKeyPtr = maxErrorSortKey.Ptr;

			using (var maxError = FindAllClustersMaxErrorSq(ref dataDesc, allocator))
			{
				var maxErrorPtr = maxError.Ptr;
				var maxErrorLimit = 0.0f;
				{
					for (int k = 0; k != dataDesc.clusterCount; k++)
					{
						var maxErrorValue = maxErrorPtr[k];
						if (maxErrorValue > 0.0f)
						{
							maxErrorValue = Mathf.Sqrt(maxErrorValue);
							maxErrorPtr[k] = maxErrorValue;
						}

						if (maxErrorLimit < maxErrorValue)
							maxErrorLimit = maxErrorValue;
					}

					if (maxErrorLimit == 0.0f)
						maxErrorLimit = 1.0f;
				}

				for (int k = 0; k != dataDesc.clusterCount; k++)
				{
					var maxErrorNorm = maxErrorPtr[k] / maxErrorLimit;
					if (maxErrorNorm > 0.0f)
					{
						maxErrorSortKeyPtr[k] = (uint)(uint.MaxValue * (double)maxErrorNorm);
					}
					else
					{
						maxErrorSortKeyPtr[k] = 0;
					}
				}
			}

			return maxErrorSortKey;
		}

		// select n additional cluster centroids from set of samples by k-means++ initialization
		void SelectCentroids(int n)
		{
			// see "k-means++: The Advantages of Careful Seeding"
			// http://ilpubs.stanford.edu:8090/778/1/2006-13.pdf

			using (var longOperation = new LongOperationScope("Picking centroids"))
			using (var state = new SelectCentroidState(dataDesc, Allocator.Temp))
			{
				var nTotal = n;

				// pick first centroid with uniform probability
				if (dataDesc.clusterCount < dataDesc.clusterCapacity && dataDesc.clusterCount == 0 && n-- > 0)
				{
					var i = dataDesc.randSeq.NextInt(0, dataDesc.sampleCount);
					{
						state.exclusionMask.Ptr[i] = true;

						PsCopyA(
							dataDesc.clusterPositionPtr + dataDesc.clusterPositionOffset * 0,
							dataDesc.clusterPositionStride,
							dataDesc.samplePositionPtr + dataDesc.samplePositionOffset * dataDesc.sampleResolvePtr[i],
							dataDesc.samplePositionStride,
							dataDesc.samplePositionCount);

						dataDesc.clusterCount++;
					}

					longOperation.UpdateStatus("Picked", 1, nTotal);
				}

				// pick remaining centroids with higher probability further from already chosen centroids
				while (dataDesc.clusterCount < dataDesc.clusterCapacity && n-- > 0)
				{
					if (SelectCentroid(dataDesc.clusterCount, state))
					{
						dataDesc.clusterCount++;
					}
					else
					{
						break;// ... there are no more candidates
					}

					longOperation.UpdateStatus("Picked", (nTotal - n), nTotal);
				}
			}
			// using (...)
		}

		struct SelectCentroidState : IDisposable
		{
			public UnsafeList<bool> exclusionMask;
			public UnsafeList<int> candidateSample;
			public UnsafeList<float> candidateWeight;

			public SelectCentroidState(in DataDesc dataDesc, Allocator allocator)
			{
				exclusionMask = new UnsafeList<bool>(dataDesc.sampleCount, allocator, NativeArrayOptions.ClearMemory);
				candidateSample = new UnsafeList<int>(dataDesc.sampleCount, allocator, NativeArrayOptions.UninitializedMemory);
				candidateWeight = new UnsafeList<float>(dataDesc.sampleCount, allocator, NativeArrayOptions.UninitializedMemory);

				// exclude existing guides from selection
				var exclusionMaskPtr = exclusionMask.Ptr;
				{
					for (int k = 0; k != dataDesc.clusterCount; k++)
					{
						var i = dataDesc.clusterGuidePtr[k];
						if (i != -1)
						{
							exclusionMaskPtr[i] = true;
						}
					}
				}
			}

			public void Dispose()
			{
				exclusionMask.Dispose();
				candidateSample.Dispose();
				candidateWeight.Dispose();
			}
		}

		struct SelectCentroidCandidate
		{
			public int sample;
			public float weight;
		}

		// select k-th cluster centroid from set of samples by k-means++ initialization
		bool SelectCentroid(int k, in SelectCentroidState state)
		{
			var exclusionMaskPtr = state.exclusionMask.Ptr;
			var candidateSamplePtr = state.candidateSample.Ptr;
			var candidateWeightPtr = state.candidateWeight.Ptr;
			var candidateCount = 0;

			// gather candidates by squared distance to closest centroid
#if USE_JOBS
			fixed (DataDesc* dataDescPtr = &dataDesc)
			{
				var job = new SelectCentroidCandidatesJob
				{
					dataDescPtr = dataDescPtr,
					exclusionMaskPtr = exclusionMaskPtr,
					candidateCountPtr = &candidateCount,
					candidateSamplePtr = candidateSamplePtr,
					candidateWeightPtr = candidateWeightPtr,
				};

				var jobHandle = job.Schedule(dataDesc.sampleCount, 64);
				{
					JobHandle.ScheduleBatchedJobs();
					jobHandle.Complete();
				}

				//TODO simplify
				// sort candidates by sample index to maintain ordering
				using (var candidateKeys = new UnsafeList<ulong>(candidateCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
				using (var candidateSampleCopy = new UnsafeList<int>(candidateCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
				using (var candidateWeightCopy = new UnsafeList<float>(candidateCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
				{
					var candidateKeysPtr = candidateKeys.Ptr;
					var candidateSampleCopyPtr = candidateSampleCopy.Ptr;
					var candidateWeightCopyPtr = candidateWeightCopy.Ptr;

					for (int i = 0; i != candidateCount; i++)
					{
						var sortSample = (ulong)candidateSamplePtr[i] << 32;
						var sortLookup = (ulong)i;
						{
							candidateKeysPtr[i] = sortSample | sortLookup;
							candidateSampleCopyPtr[i] = candidateSamplePtr[i];
							candidateWeightCopyPtr[i] = candidateWeightPtr[i];
						}
					}

					NativeSortExtension.Sort(candidateKeysPtr, candidateCount);

					for (int i = 0; i != candidateCount; i++)
					{
						var resolve = (int)(candidateKeysPtr[i] & 0xffffffffuL);
						{
							candidateSamplePtr[i] = candidateSampleCopyPtr[resolve];
							candidateWeightPtr[i] = candidateWeightCopyPtr[resolve];
						}
					}
				}
			}
#else
			for (int i = 0; i != dataDesc.sampleCount; i++)
			{
				if (exclusionMaskPtr[i])
					continue;

				if (FindClosestCluster(ref dataDesc, i, out var minDistSq, out var minDistIndex))
				{
					candidateSamplePtr[candidateCount] = i;
					candidateWeightPtr[candidateCount] = minDistSq;
					candidateCount++;
				}
			}
#endif

			// failure if there were no candidates
			if (candidateCount == 0)
				return false;

			// compute accumulated weights until current index
			for (int i = 1; i != candidateCount; i++)
			{
				candidateWeightPtr[i] = candidateWeightPtr[i - 1] + candidateWeightPtr[i];
			}

			// weighted search for k-th centroid
			var candidateWeightSum = candidateWeightPtr[candidateCount - 1];
			{
				var searchValue = candidateWeightSum * dataDesc.randSeq.NextFloat();
				var searchIndex = NativeSortExtension.BinarySearch(candidateWeightPtr, candidateCount, searchValue);
				if (searchIndex < 0)
					searchIndex = ~searchIndex;

				var i = candidateSamplePtr[searchIndex];
				{
					exclusionMaskPtr[i] = true;

					PsCopyA(
						dataDesc.clusterPositionPtr + dataDesc.clusterPositionOffset * k,
						dataDesc.clusterPositionStride,
						dataDesc.samplePositionPtr + dataDesc.samplePositionOffset * dataDesc.sampleResolvePtr[i],
						dataDesc.samplePositionStride,
						dataDesc.samplePositionCount);
				}
			}

			// success
			return true;
		}

		[BurstCompile]
		struct SelectCentroidCandidatesJob : IJobParallelFor
		{
			[NativeDisableUnsafePtrRestriction, NoAlias] public DataDesc* dataDescPtr;
			[NativeDisableUnsafePtrRestriction, NoAlias] public bool* exclusionMaskPtr;
			[NativeDisableUnsafePtrRestriction, NoAlias] public int* candidateSamplePtr;
			[NativeDisableUnsafePtrRestriction, NoAlias] public float* candidateWeightPtr;
			[NativeDisableUnsafePtrRestriction, NoAlias] public int* candidateCountPtr;

			public void Execute(int i)
			{
				if (exclusionMaskPtr[i])
					return;

				if (FindClosestCluster(ref *dataDescPtr, i, out var minDistSq, out var minDistIndex))
				{
					var j = System.Threading.Interlocked.Increment(ref *candidateCountPtr) - 1;
					{
						candidateSamplePtr[j] = i;
						candidateWeightPtr[j] = minDistSq;
					}
				}
			}
		}

		// assign samples to clusters by closest centroid
		int AssignSamples()
		{
			var reassignedCount = 0;
			{
				UnsafeUtility.MemClear(dataDesc.clusterTallyPtr, sizeof(int) * dataDesc.clusterCount);

#if USE_JOBS
				fixed (DataDesc* dataDescPtr = &dataDesc)
				{
					var job = new AssignSamplesJob
					{
						dataDescPtr = dataDescPtr,
						reassignedCountPtr = &reassignedCount
					};

					var jobHandle = job.Schedule(dataDesc.sampleCount, 64);
					{
						JobHandle.ScheduleBatchedJobs();
						jobHandle.Complete();
					}
				}
#else
				for (int i = 0; i != dataDesc.sampleCount; i++)
				{
					if (AssignSample(ref dataDesc, i, out var k))
					{
						reassignedCount++;
					}

					if (k != -1)
					{
						dataDesc.clusterTallyPtr[k]++;
					}
				}
#endif
			}

			if (reassignedCount > 0)
			{
				UpdateVoid();
			}

			return reassignedCount;
		}

		[BurstCompile]
		struct AssignSamplesJob : IJobParallelFor
		{
			[NativeDisableUnsafePtrRestriction, NoAlias] public DataDesc* dataDescPtr;
			[NativeDisableUnsafePtrRestriction, NoAlias] public int* reassignedCountPtr;

			public void Execute(int i)
			{
				if (AssignSample(ref *dataDescPtr, i, out var k))
				{
					System.Threading.Interlocked.Increment(ref *reassignedCountPtr);
				}

				if (k != -1)
				{
					System.Threading.Interlocked.Increment(ref dataDescPtr->clusterTallyPtr[k]);
				}
			}
		}

		// assign i-th sample to cluster with closest centroid, returns true if reassigned
		static bool AssignSample(ref DataDesc dataDesc, int i, out int k)
		{
			// do not reassign samples that are guides in committed clusters
			var kExisting = dataDesc.sampleClusterPtr[i];
			if (kExisting != -1 && kExisting < dataDesc.clusterCountCommitted)
			{
				if (dataDesc.clusterGuidePtr[kExisting] == i)
				{
					k = kExisting;
					return false;
				}
			}

			// assign sample to closest cluster
			if (FindClosestCluster(ref dataDesc, i, out var minDistSq, out var minDistIndex))
			{
				k = minDistIndex;
				if (dataDesc.sampleClusterPtr[i] != minDistIndex)
				{
					dataDesc.sampleClusterPtr[i] = minDistIndex;
					return true;
				}
			}
			else
			{
				k = -1;
			}

			return false;
		}

		// update empty clusters according to strategy specified on initialization
		void UpdateVoid()
		{
			switch (dataDesc.clusterVoidPolicy)
			{
				// displace empty clusters to effectively remove them from solution
				case ClusterVoid.Remove:
					{
						for (int k = 0; k != dataDesc.clusterCount; k++)
						{
							if (dataDesc.clusterTallyPtr[k] > 0)
								continue;

							PsClearA(
								dataDesc.clusterPositionPtr + dataDesc.clusterPositionOffset * k,
								dataDesc.clusterPositionStride,
								Vector3.positiveInfinity,
								dataDesc.clusterPositionCount);
						}
					}
					break;

				// preserve empty clusters so they have a chance of reentering
				case ClusterVoid.Preserve:
					break;

				// reallocate empty clusters by k-means++ initialization
				case ClusterVoid.Reallocate:
					{
						using (var state = new SelectCentroidState(dataDesc, Allocator.Temp))
						{
							for (int k = 0; k != dataDesc.clusterCount; k++)
							{
								if (dataDesc.clusterTallyPtr[k] > 0)
									continue;

								if (SelectCentroid(k, state) == false)
								{
									break;// ... there are no more candidates
								}
							}
						}
					}
					break;
			}
		}

		// update cluster centroids from assigned samples
		void UpdateCentroids()
		{
			if (dataDesc.clusterCount == 0)
				return;

			// clear centroids
			for (int k = 0; k != dataDesc.clusterCount; k++)
			{
				if (dataDesc.clusterTallyPtr[k] > 0)
				{
					PsClearA(
						dataDesc.clusterPositionPtr + dataDesc.clusterPositionOffset * k,
						dataDesc.clusterPositionStride,
						Vector3.zero,
						dataDesc.clusterPositionCount);

					dataDesc.clusterWeightPtr[k] = 0.0f;
				}
			}

			// add samples to weighted sum
			//TODO either list per cluster, or split the work
			for (int i = 0; i != dataDesc.sampleCount; i++)
			{
				var k = dataDesc.sampleClusterPtr[i];
				var w = dataDesc.sampleWeightPtr[dataDesc.sampleResolvePtr[i]];
				{
					PsMulAddA(
						dataDesc.clusterPositionPtr + dataDesc.clusterPositionOffset * k,
						dataDesc.clusterPositionStride,
						dataDesc.samplePositionPtr + dataDesc.samplePositionOffset * dataDesc.sampleResolvePtr[i],
						dataDesc.samplePositionStride, w,
						dataDesc.samplePositionCount);

					dataDesc.clusterWeightPtr[k] += w;
				}
			}

			// weighted average to obtain centroids
			for (int k = 0; k != dataDesc.clusterCount; k++)
			{
				if (dataDesc.clusterTallyPtr[k] > 0)
				{
					PsMulA(
						dataDesc.clusterPositionPtr + dataDesc.clusterPositionOffset * k,
						dataDesc.clusterPositionStride,
						1.0f / dataDesc.clusterWeightPtr[k],
						dataDesc.clusterPositionCount);
				}
			}

			// committed centroids cannot stray too far from guides
			//TODO replace hard snap with limit
			//TODO limit to half the distance to closest adj. centroid (to avoid stealing from other committed clusters)
			for (int k = 0; k != dataDesc.clusterCountCommitted; k++)
			{
				var i = dataDesc.clusterGuidePtr[k];
				if (i != -1)
				{
					PsCopyA(
						dataDesc.clusterPositionPtr + dataDesc.clusterPositionOffset * k,
						dataDesc.clusterPositionStride,
						dataDesc.samplePositionPtr + dataDesc.samplePositionOffset * dataDesc.sampleResolvePtr[i],
						dataDesc.samplePositionStride,
						dataDesc.samplePositionCount);
				}
			}
		}

		public bool ExpandProcedural(int clusterCount, ClusterAllocationPolicy clusterSelectionPolicy, ClusterAllocationOrder clusterSelectionOrder, int clusterIterations)
		{
			var appendCount = clusterCount - dataDesc.clusterCount;
			if (appendCount <= 0)
				return false;

			EnsureCapacity(clusterCount);

			if (clusterSelectionPolicy != ClusterAllocationPolicy.Global)
			{
				// bootstrap empty set before splitting
				if (dataDesc.clusterCount == 0)
				{
					ExpandProcedural(1, ClusterAllocationPolicy.Global, clusterSelectionOrder, 0);
					appendCount--;
				}

				// handle large splits as multiple chunks (prevents some otherwise expensive global searches in first few levels)
				while (appendCount > 0)
				{
					var intermediateClusterCount = dataDesc.clusterCount;
					var intermediateSplitThreshold = 64 * intermediateClusterCount;
					if (intermediateSplitThreshold < appendCount)
					{
						ExpandProcedural(dataDesc.clusterCount + intermediateSplitThreshold, clusterSelectionPolicy, clusterSelectionOrder, clusterIterations);
						appendCount -= (dataDesc.clusterCount - intermediateClusterCount);
					}
					else
					{
						break;
					}
				}
			}

			switch (clusterSelectionPolicy)
			{
				case ClusterAllocationPolicy.Global:
					{
						SelectCentroids(appendCount);
						AssignSamples();
						UpdateCentroids();
						Refine(clusterIterations);
					}
					break;

				case ClusterAllocationPolicy.SplitGlobal:
					{
						SplitClusters(appendCount, clusterSelectionOrder, 0);
						Refine(clusterIterations);
					}
					break;

				case ClusterAllocationPolicy.SplitBranching:
					{
						SplitClusters(appendCount, clusterSelectionOrder, clusterIterations);
					}
					break;
			}

			return true;
		}

		public bool ExpandPreassigned(int clusterCount, in UnsafeList<int> sampleCluster)
		{
			var appendCount = clusterCount - dataDesc.clusterCount;
			if (appendCount <= 0)
				return false;

			EnsureCapacity(clusterCount);

			using (var preassignedClusterSet = new UnsafeClusterSet(clusterCount, ClusterVoid.Preserve, dataDesc.sampleCount, dataDesc.samplePositionOffset, dataDesc.samplePositionStride, dataDesc.samplePositionCount, Allocator.Temp))
			using (var preassignedClusterResolve = new UnsafeList<int>(clusterCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			{
				// build preassigned set
				var preassignedDataDescPtr = &preassignedClusterSet.dataDesc;
				{
					preassignedDataDescPtr->samplePositionPtr = dataDesc.samplePositionPtr;
					preassignedDataDescPtr->sampleWeightPtr = dataDesc.sampleWeightPtr;
					preassignedDataDescPtr->sampleResolvePtr = dataDesc.sampleResolvePtr;
				}

				var sampleClusterPtr = sampleCluster.Ptr;
				{
					UnsafeUtility.MemClear(preassignedClusterSet.dataDesc.clusterTallyPtr, sizeof(int) * preassignedClusterSet.dataDesc.clusterCapacity);

					for (int i = 0; i != preassignedClusterSet.dataDesc.sampleCount; i++)
					{
						var k = sampleClusterPtr[preassignedClusterSet.dataDesc.sampleResolvePtr[i]];
						if (k < preassignedClusterSet.dataDesc.clusterCapacity)
						{
							preassignedClusterSet.dataDesc.sampleClusterPtr[i] = k;
							preassignedClusterSet.dataDesc.clusterTallyPtr[k]++;
						}
						else
						{
							Debug.LogWarning("Skipping entry from preassigned set: Preassigned cluster index exceeds capacity of allocated cluster set.");
						}
					}

					preassignedDataDescPtr->clusterCount = clusterCount;
				}

				preassignedClusterSet.UpdateVoid();
				preassignedClusterSet.UpdateCentroids();

				var preassignedClusterResolvePtr = preassignedClusterResolve.Ptr;
				{
					for (int k = 0; k != preassignedClusterSet.dataDesc.clusterCount; k++)
					{
						preassignedClusterResolvePtr[k] = -1;
					}
				}

				// map preassigned clusters to existing and append only those that cannot be mapped, so as to not override existing committed structure
				using (var centroidClusterSet = new UnsafeClusterSet(dataDesc.clusterCapacity, ClusterVoid.Preserve, preassignedClusterSet.dataDesc.clusterCount, preassignedClusterSet.dataDesc.clusterPositionOffset, preassignedClusterSet.dataDesc.clusterPositionStride, preassignedClusterSet.dataDesc.clusterPositionCount, Allocator.Temp))
				using (var centroidSampleWeight = new UnsafeList<float>(preassignedClusterSet.dataDesc.clusterCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
				using (var centroidSampleResolve = new UnsafeList<int>(preassignedClusterSet.dataDesc.clusterCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
				{
					// map centroids of preassigned clusters to existing clusters
					var centroidDataDescPtr = &centroidClusterSet.dataDesc;
					var centroidSampleWeightPtr = centroidSampleWeight.Ptr;
					var centroidSampleResolvePtr = centroidSampleResolve.Ptr;
					{
						// copy existing cluster centroids
						UnsafeUtility.MemCpy(centroidClusterSet.dataDesc.clusterPositionPtr, dataDesc.clusterPositionPtr, sizeof(Vector3) * clusterPosition.Length);
						{
							centroidDataDescPtr->clusterCount = dataDesc.clusterCount;
						}

						// bind preassigned centroids as sample data
						centroidDataDescPtr->samplePositionPtr = preassignedClusterSet.dataDesc.clusterPositionPtr;
						centroidDataDescPtr->sampleWeightPtr = centroidSampleWeightPtr;
						centroidDataDescPtr->sampleResolvePtr = centroidSampleResolvePtr;

						for (int ki = 0; ki != preassignedClusterSet.dataDesc.clusterCount; ki++)
						{
							centroidSampleWeightPtr[ki] = 1.0f;
							centroidSampleResolvePtr[ki] = ki;
						}

						// construct the map
						centroidClusterSet.AssignSamples();
					}

					// make space for empty existing clusters (that are not the best fit for any preassigned cluster)
					var emptyCount = CountEmptyClusters(ref *centroidDataDescPtr);
					if (emptyCount > 0)
					{
						EnsureCapacity(dataDesc.clusterCapacity + emptyCount);
					}

					// identify secondary preassigned clusters (that are not the best fit for the closest existing cluster)
					centroidClusterSet.Commit();

					// append secondary preassigned clusters
					for (int ki = 0; ki != centroidClusterSet.dataDesc.sampleCount; ki++)
					{
						var k = centroidClusterSet.dataDesc.sampleClusterPtr[ki];
						if (k == -1 || ki != centroidClusterSet.dataDesc.clusterGuidePtr[k])// not guide => secondary
						{
							var kAppend = dataDesc.clusterCount++;

							// copy centroid
							PsCopyA(
								dataDesc.clusterPositionPtr + dataDesc.clusterPositionOffset * kAppend,
								dataDesc.clusterPositionStride,
								preassignedClusterSet.dataDesc.clusterPositionPtr + preassignedClusterSet.dataDesc.clusterPositionOffset * ki,
								preassignedClusterSet.dataDesc.clusterPositionStride,
								preassignedClusterSet.dataDesc.clusterPositionCount);

							// initialize tally
							dataDesc.clusterTallyPtr[kAppend] = 0;

							// queue resolve
							preassignedClusterResolvePtr[ki] = kAppend;
						}
					}
				}
				// using (...)

				// resolve sample cluster assignments for appended clusters
				var reassignedCount = 0;
				{
					for (int i = 0; i != dataDesc.sampleCount; i++)
					{
						var kPreassigned = preassignedClusterSet.dataDesc.sampleClusterPtr[i];
						var kResolved = preassignedClusterResolvePtr[kPreassigned];
						if (kResolved != -1)
						{
							// do not reassign samples that are guides in committed clusters
							var kExisting = dataDesc.sampleClusterPtr[i];
							if (kExisting != -1 && kExisting < dataDesc.clusterCountCommitted)
							{
								if (dataDesc.clusterGuidePtr[kExisting] == i)
								{
									continue;
								}
							}

							// reassign to appended cluster
							//TODO update tallies in separate pass instead of inc/dec?
							if (kExisting != -1)
							{
								dataDesc.clusterTallyPtr[kExisting]--;
							}

							dataDesc.sampleClusterPtr[i] = kResolved;
							dataDesc.clusterTallyPtr[kResolved]++;
							reassignedCount++;
						}
					}
				}
				if (reassignedCount > 0)
				{
					UpdateCentroids();

					//Debug.Log("---");
					//for (int k = 0; k != dataDesc.clusterCount; k++)
					//{
					//	Debug.Log("cluster " + k + " -> guide " + dataDesc.clusterGuidePtr[k] + " tally " + dataDesc.clusterTallyPtr[k]);
					//}
				}
			}
			// using (...)

			return true;
		}

		void SplitClusters(int n, ClusterAllocationOrder splitPreference, int splitClusterIterations)
		{
			using (var longOperation = new LongOperationScope("Splitting clusters"))
			using (var splitWorkDesc = new SplitClustersWorkDesc(ref dataDesc, n, splitPreference, Allocator.Temp))
			{
				var splitKeysPtr = splitWorkDesc.splitKeys.Ptr;
				var splitCountPtr = splitWorkDesc.splitCount.Ptr;
				var splitOffsetPtr = splitWorkDesc.splitOffset.Ptr;

				var splitWorkTotal = splitWorkDesc.splitWorkTotal;
				var splitWorkUnits = splitWorkDesc.splitWorkUnits;

				// early out if no splits were ordered
				if (splitWorkUnits == 0)
				{
					return;
				}

				// execute the splits
				//TODO split in parallel?
				for (int j = 0; j != splitWorkUnits; j++)
				{
					longOperation.UpdateStatus("Split", j + 1, splitWorkUnits);

					var k = (int)(splitKeysPtr[j] & 0xffffffffuL);

					var splitClusterCount = 1 + splitCountPtr[j];
					var splitSampleCount = dataDesc.clusterTallyPtr[k];

					using (var splitClusterSet = new UnsafeClusterSet(splitClusterCount, dataDesc.clusterVoidPolicy, splitSampleCount, dataDesc.samplePositionOffset, dataDesc.samplePositionStride, dataDesc.samplePositionCount, Allocator.Temp))
					using (var splitSampleResolve = new UnsafeList<int>(splitSampleCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
					{
						// bind sample indices from parent cluster
						var splitSampleResolvePtr = splitSampleResolve.Ptr;
						var splitSampleResolveCount = 0;
						{
							for (int i = 0; i != dataDesc.sampleCount && splitSampleResolveCount < splitSampleCount; i++)
							{
								if (dataDesc.sampleClusterPtr[i] == k)
								{
									splitSampleResolvePtr[splitSampleResolveCount++] = i;
								}
							}
						}

						var splitDataDescPtr = &splitClusterSet.dataDesc;
						{
							splitDataDescPtr->samplePositionPtr = dataDesc.samplePositionPtr;
							splitDataDescPtr->sampleWeightPtr = dataDesc.sampleWeightPtr;
							splitDataDescPtr->sampleResolvePtr = splitSampleResolvePtr;
						}

						// inject first centroid from parent cluster
						{
							PsCopyA(
								splitDataDescPtr->clusterPositionPtr + splitDataDescPtr->clusterPositionOffset * 0,
								splitDataDescPtr->clusterPositionStride,
								dataDesc.clusterPositionPtr + dataDesc.clusterPositionOffset * k,
								dataDesc.clusterPositionStride,
								dataDesc.clusterPositionCount);

							splitDataDescPtr->clusterCount++;
						}

						// inject first guide from split cluster as well, if split cluster was a committed cluster
						var parentClusterCommitted = (k < dataDesc.clusterCountCommitted);
						if (parentClusterCommitted)
						{
							splitDataDescPtr->clusterGuidePtr[0] = NativeArrayExtensions.IndexOf<int, int>(splitSampleResolvePtr, splitSampleResolveCount, dataDesc.clusterGuidePtr[k]);
							splitDataDescPtr->clusterDepthPtr[0] = dataDesc.clusterDepthPtr[k];
							splitDataDescPtr->clusterCountCommitted++;
						}

						// select remaining centroids by k-means++ initialization
						splitClusterSet.SelectCentroids(splitClusterCount - 1);
						splitClusterSet.AssignSamples();
						splitClusterSet.UpdateCentroids();
						splitClusterSet.Refine(splitClusterIterations);

						// transfer centroids to parent set
						{
							// first centroid replaces existing entry
							PsCopyA(
								dataDesc.clusterPositionPtr + dataDesc.clusterPositionOffset * k,
								dataDesc.clusterPositionStride,
								splitDataDescPtr->clusterPositionPtr + splitDataDescPtr->clusterPositionOffset * 0,
								splitDataDescPtr->clusterPositionStride,
								splitDataDescPtr->clusterPositionCount);

							dataDesc.clusterWeightPtr[k] = splitDataDescPtr->clusterWeightPtr[0];
							dataDesc.clusterTallyPtr[k] = splitDataDescPtr->clusterTallyPtr[0];

							// remaining centroids appended at specific offset
							for (int m = 1; m != splitClusterCount; m++)
							{
								var km = splitOffsetPtr[j] + m - 1;

								PsCopyA(
									dataDesc.clusterPositionPtr + dataDesc.clusterPositionOffset * km,
									dataDesc.clusterPositionStride,
									splitDataDescPtr->clusterPositionPtr + splitDataDescPtr->clusterPositionOffset * m,
									splitDataDescPtr->clusterPositionStride,
									splitDataDescPtr->clusterPositionCount);

								dataDesc.clusterWeightPtr[km] = splitDataDescPtr->clusterWeightPtr[m];
								dataDesc.clusterTallyPtr[km] = splitDataDescPtr->clusterTallyPtr[m];
							}
						}

						// transfer assignments to parent set
						for (int i = 0; i != splitDataDescPtr->sampleCount; i++)
						{
							var m = splitDataDescPtr->sampleClusterPtr[i];
							if (m == 0)
							{
								// samples in first cluster are already assigned
								//dataDesc.sampleClusterPtr[splitSampleResolvePtr[i]] = k;
							}
							else
							{
								// samples in remaining clusters need to be reassigned
								dataDesc.sampleClusterPtr[splitSampleResolvePtr[i]] = splitOffsetPtr[j] + m - 1;
							}
						}
					}
					// using (...)
				}

				// count the added clusters
				dataDesc.clusterCount += splitWorkTotal;
			}
			// using (...)
		}

		struct SplitClustersWorkDesc : IDisposable
		{
			public UnsafeList<ulong> splitKeys;
			public UnsafeList<int> splitCount;
			public UnsafeList<int> splitOffset;

			public int splitWorkTotal;
			public int splitWorkUnits;

			public SplitClustersWorkDesc(ref DataDesc dataDesc, int n, ClusterAllocationOrder splitPreference, Allocator allocator)
			{
				splitKeys = new UnsafeList<ulong>(dataDesc.clusterCount, allocator, NativeArrayOptions.UninitializedMemory);
				splitCount = new UnsafeList<int>(dataDesc.clusterCount, allocator, NativeArrayOptions.ClearMemory);
				splitOffset = new UnsafeList<int>(dataDesc.clusterCount, allocator, NativeArrayOptions.UninitializedMemory);

				splitWorkTotal = 0;
				splitWorkUnits = 0;

				if (dataDesc.clusterCount == 0)
				{
					return;
				}

				var splitKeysPtr = splitKeys.Ptr;
				var splitCountPtr = splitCount.Ptr;
				var splitOffsetPtr = splitOffset.Ptr;

				void BubbleFirstIndex(ulong* valuePtr, int valueCount) => BubbleSingleIndex(valuePtr, valueCount, 0);
				void BubbleSingleIndex(ulong* valuePtr, int valueCount, int i)
				{
					var valueMoving = valuePtr[i++];

					for (; i != valueCount; i++)
					{
						var valueStep = valuePtr[i];
						if (valueStep < valueMoving)
						{
							valuePtr[i - 1] = valueStep;
						}
						else
						{
							valuePtr[i - 1] = valueMoving;
							return;
						}
					}

					if (i == valueCount)
					{
						valuePtr[valueCount - 1] = valueMoving;
					}
				}

				switch (splitPreference)
				{
					case ClusterAllocationOrder.ByHighestError:
						using (var maxErrorSortKey = FindAllClustersMaxErrorSortKey(ref dataDesc, allocator))
						{
							var maxErrorSortKeyPtr = maxErrorSortKey.Ptr;

							// sort clusters by maximum error (descending)
							for (int k = 0; k != dataDesc.clusterCount; k++)
							{
								var sortError = (ulong)(uint.MaxValue - maxErrorSortKeyPtr[k]) << 32;// store error descending
								var sortIndex = (ulong)k;
								{
									splitKeysPtr[k] = sortError | sortIndex;
								}
							}

							NativeSortExtension.Sort(splitKeysPtr, dataDesc.clusterCount);

							// distribute splits ordered by maximum error, shrinking maximum error with each split
							while (n-- > 0)
							{
								var k = (int)(splitKeysPtr[0] & 0xffffffffuL);
								var c = dataDesc.clusterTallyPtr[k] - splitCountPtr[k];
								if (c > 1)// must have at least one free sample available for centroid selection
								{
									var approxError = maxErrorSortKeyPtr[k];
									{
										//TODO intuitively, the reduced error can be expressed as the approximate half-radius of one of the smaller clusters
										//TODO find the approximate radius of the smaller clusters
										approxError /= (2 + (uint)splitCountPtr[k]);
										if (c <= 2)
										{
											approxError = 0;// zero sort key (to effectively push to back) if this is the last free sample
										}
									}

									splitKeysPtr[0] &= 0xffffffffuL;
									splitKeysPtr[0] |= (ulong)(uint.MaxValue - approxError) << 32;// store error descending
									splitCountPtr[k]++;

									BubbleFirstIndex(splitKeysPtr, dataDesc.clusterCount);
								}
								else
								{
									n = 0;// no further splits possible
								}
							}
						}
						break;

					case ClusterAllocationOrder.ByHighestTally:
						{
							// sort clusters by tally (descending)
							for (int k = 0; k != dataDesc.clusterCount; k++)
							{
								var sortTally = (ulong)(uint.MaxValue - (uint)dataDesc.clusterTallyPtr[k]) << 32;// store tally descending
								var sortIndex = (ulong)k;
								{
									splitKeysPtr[k] = sortTally | sortIndex;
								}
							}

							NativeSortExtension.Sort(splitKeysPtr, dataDesc.clusterCount);

							// distribute splits ordered by tally, shrinking tally with each split
							while (n-- > 0)
							{
								var k = (int)(splitKeysPtr[0] & 0xffffffffuL);
								var c = dataDesc.clusterTallyPtr[k] - splitCountPtr[k];
								if (c > 1)// must have at least one free sample available for centroid selection
								{
									var approxTally = (uint)dataDesc.clusterTallyPtr[k];
									{
										approxTally /= (2 + (uint)splitCountPtr[k]);
										if (c <= 2)
										{
											approxTally = 0;// zero sort key (to effectively push to back) if this is the last free sample
										}
									}

									splitKeysPtr[0] &= 0xffffffffuL;
									splitKeysPtr[0] |= (ulong)(uint.MaxValue - approxTally) << 32;// store tally descending
									splitCountPtr[k]++;

									BubbleFirstIndex(splitKeysPtr, dataDesc.clusterCount);
								}
								else
								{
									n = 0;// no further splits possible
								}
							}
						}
						break;

					case ClusterAllocationOrder.ByHighestTallyBreadthFirst:
						{
							// sort clusters by tally (descending)
							for (int k = 0; k != dataDesc.clusterCount; k++)
							{
								var sortTally = (ulong)(int.MaxValue - dataDesc.clusterTallyPtr[k]) << 32;// store tally descending
								var sortIndex = (ulong)k;
								{
									splitKeysPtr[k] = sortTally | sortIndex;
								}
							}

							NativeSortExtension.Sort(splitKeysPtr, dataDesc.clusterCount);

							// distribute splits breadth first, with remainder distributed to clusters with highest tally
							while (n > 0)
							{
								for (int j = 0; j != dataDesc.clusterCount && n > 0; j++, n--)
								{
									var k = (int)(splitKeysPtr[j] & 0xffffffffuL);
									var c = dataDesc.clusterTallyPtr[k] - splitCountPtr[k];
									if (c > 1)// must have at least one free sample available for centroid selection
									{
										splitCountPtr[k]++;
									}
									else
									{
										if (j == 0)
											n = 0;// no further splits possible

										break;
									}
								}
							}

						}
						break;
				}

				// prepare work units (compact)
				for (int k = 0; k != dataDesc.clusterCount; k++)
				{
					var nk = splitCountPtr[k];
					if (nk > 0)
					{
						var j = splitWorkUnits++;
						{
							splitKeysPtr[j] = (ulong)k;
							splitCountPtr[j] = nk;
							splitWorkTotal += nk;
						}
					}
				}

				// compute work unit offsets
				if (splitWorkUnits > 0)
				{
					splitOffsetPtr[0] = dataDesc.clusterCount;

					for (int j = 1; j != splitWorkUnits; j++)
					{
						splitOffsetPtr[j] = splitOffsetPtr[j - 1] + splitCountPtr[j - 1];
					}
				}
			}

			public void Dispose()
			{
				splitKeys.Dispose();
				splitCount.Dispose();
				splitOffset.Dispose();
			}
		}

		// refine clusters by k-means iteration
		public void Refine(int maxIterations)
		{
			if (maxIterations == 0)
				return;

			using (var longOperation = new LongOperationScope("Refining clusters"))
			{
				var t = 0.0f;
				var tt = 0.0f;

				for (int m = 0; m != maxIterations; m++)
				{
					longOperation.UpdateStatus("Iteration " + (m + 1) + " ...", tt);

					var reassignedCount = AssignSamples();
					if (reassignedCount > 0)
					{
						UpdateCentroids();

						t = (dataDesc.sampleCount - reassignedCount) / (float)dataDesc.sampleCount;
						tt = t * t;
					}
					else
					{
						break;
					}
				}
			}
		}

		// commit clusters to guides (also reduces future working set)
		public void Commit()
		{
			// compact and trim so live clusters are consecutive in range [0, clusterCount)
			CompactAndTrim();

			// pick one guide per cluster based on minimum weighted distance to centroid
			using (var minDist = new UnsafeList<float>(dataDesc.clusterCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			{
				var minDistPtr = minDist.Ptr;
				{
					for (int k = 0; k != dataDesc.clusterCount; k++)
					{
						minDistPtr[k] = float.PositiveInfinity;
					}
				}

				for (int i = 0; i != dataDesc.sampleCount; i++)
				{
					var k = dataDesc.sampleClusterPtr[i];
					if (k < dataDesc.clusterCountCommitted)
					{
						continue;// skip committed clusters (since they have a fixed guide assigned to them)
					}

					var w = dataDesc.sampleWeightPtr[i];
					{
						var dist = PsDistance(
							dataDesc.clusterPositionPtr + dataDesc.clusterPositionOffset * k,
							dataDesc.clusterPositionStride,
							dataDesc.samplePositionPtr + dataDesc.samplePositionOffset * dataDesc.sampleResolvePtr[i],
							dataDesc.samplePositionStride,
							dataDesc.samplePositionCount) / w;

						if (minDistPtr[k] > dist)
						{
							minDistPtr[k] = dist;
							dataDesc.clusterGuidePtr[k] = i;
							dataDesc.clusterDepthPtr[k] = dataDesc.clusterCount;// this is useful as a sorting key
						}
					}
				}
			}

			// find cluster carry (weight of all samples in cluster relative to weight of cluster guide)
			for (int k = 0; k != dataDesc.clusterCount; k++)
			{
				var i = dataDesc.clusterGuidePtr[k];
				if (i != -1)
				{
					dataDesc.clusterCarryPtr[k] = dataDesc.clusterWeightPtr[k] / dataDesc.sampleWeightPtr[i];
				}
			}

			// snap centroids to guides
			for (int k = 0; k != dataDesc.clusterCount; k++)
			{
				var i = dataDesc.clusterGuidePtr[k];
				if (i != -1)
				{
					PsCopyA(
						dataDesc.clusterPositionPtr + dataDesc.clusterPositionOffset * k,
						dataDesc.clusterPositionStride,
						dataDesc.samplePositionPtr + dataDesc.samplePositionOffset * dataDesc.sampleResolvePtr[i],
						dataDesc.samplePositionStride,
						dataDesc.samplePositionCount);
				}
			}

			// find cluster reach (maximum sample distance to centroid)
			//TODO recompute & use actual (not snapped) cluster centroids as error origins
			//TODO maybe replace with radius of planar convex hull?
			using (var maxErrorSq = FindAllClustersMaxErrorSq(ref dataDesc, Allocator.Temp))
			{
				var maxErrorSqPtr = maxErrorSq.Ptr;
				{
					for (int k = 0; k != dataDesc.clusterCount; k++)
					{
						dataDesc.clusterReachPtr[k] = (maxErrorSqPtr[k] > 0.0f) ? Mathf.Sqrt(maxErrorSqPtr[k]) : 0.0f;
					}
				}
			}

			// reduce working set
			dataDesc.clusterCountCommitted = dataDesc.clusterCount;
		}

		// compact cluster data by moving empty clusters to tail end of set and shrinking set
		void CompactAndTrim()
		{
			// early out if there are no empty clusters
			var emptyClusterCount = CountEmptyClusters(ref dataDesc);
			if (emptyClusterCount == 0)
				return;

			//Debug.Log("found " + emptyClusterCount + " empty clusters (out of " + dataDesc.clusterCount + ") => compacting set ...");

			// move empty clusters to tail
			using (var remappedClusterIndices = new UnsafeList<int>(dataDesc.clusterCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			{
				var remappedClusterIndicesPtr = remappedClusterIndices.Ptr;

				for (int k = 0; k < dataDesc.clusterCount; k++)
				{
					if (dataDesc.clusterTallyPtr[k] > 0)
					{
						remappedClusterIndicesPtr[k] = k;
						continue;
					}

					// swap empty cluster with live tail (if any)
					var kLive = dataDesc.clusterCount;
					{
						while (--kLive > k)
						{
							if (dataDesc.clusterTallyPtr[kLive] == 0)
								continue;

							PsCopyA(
								dataDesc.clusterPositionPtr + dataDesc.clusterPositionOffset * k,
								dataDesc.clusterPositionStride,
								dataDesc.clusterPositionPtr + dataDesc.clusterPositionOffset * kLive,
								dataDesc.clusterPositionStride,
								dataDesc.clusterPositionCount);

							dataDesc.clusterWeightPtr[k] = dataDesc.clusterWeightPtr[kLive];
							dataDesc.clusterTallyPtr[k] = dataDesc.clusterTallyPtr[kLive];
							dataDesc.clusterGuidePtr[k] = dataDesc.clusterGuidePtr[kLive];
							dataDesc.clusterDepthPtr[k] = dataDesc.clusterDepthPtr[kLive];
							dataDesc.clusterCarryPtr[k] = dataDesc.clusterCarryPtr[kLive];
							dataDesc.clusterReachPtr[k] = dataDesc.clusterReachPtr[kLive];

							remappedClusterIndicesPtr[kLive] = k;
							break;
						}
					}

					dataDesc.clusterCount = kLive;
				}

				// apply remapped cluster indices
				for (int i = 0; i != dataDesc.sampleCount; i++)
				{
					dataDesc.sampleClusterPtr[i] = remappedClusterIndicesPtr[dataDesc.sampleClusterPtr[i]];
				}
			}
			// using (...)
		}

		/* old preassigned method, kept for reference
		// inject guides from prior committed set
		public bool InjectGuidesFrom(in ClusterSet clusterSet)
		{
			using (var clusterUpdated = new UnsafeList<bool>(dataDesc.clusterCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
			{
				var clusterUpdatedPtr = clusterUpdated.Ptr;
				var clusterUpdatedCount = 0;

				// loop over and inject prior clusters
				for (int k = 0; k != clusterSet.dataDesc.clusterCount; k++)
				{
					// get prior cluster guide and depth
					var priorGuide = clusterSet.dataDesc.clusterGuidePtr[k];
					var priorDepth = clusterSet.dataDesc.clusterDepthPtr[k];

					// get current cluster for prior guide
					var kCurrent = dataDesc.sampleClusterPtr[priorGuide];

					// update current cluster if not already updated
					if (clusterUpdatedPtr[kCurrent] == false)
					{
						// transfer both guide and depth so we can sort clusters by depth first and guide second
						dataDesc.clusterGuidePtr[kCurrent] = priorGuide;
						dataDesc.clusterDepthPtr[kCurrent] = priorDepth;

						// mark as updated
						clusterUpdatedPtr[kCurrent] = true;
						clusterUpdatedCount++;
					}
				}

				// success if all guides were injected
				return (clusterUpdatedCount == clusterSet.dataDesc.clusterCount);
			}
			// using (...)
		}
		*/
	}

	public unsafe struct PointSetMath
	{
		public static void PsClearA(Vector3* pointsA, int strideA, Vector3 value, int count)
		{
			for (int i = 0; i != count; i++)
			{
				pointsA[i * strideA] = value;
			}
		}

		public static void PsCopyA(Vector3* pointsA, int strideA, Vector3* pointsB, int strideB, int count)
		{
			for (int i = 0; i != count; i++)
			{
				pointsA[i * strideA] = pointsB[i * strideB];
			}
		}

		public static void PsAddA(Vector3* pointsA, int strideA, Vector3* pointsB, int strideB, int count)
		{
			for (int i = 0; i != count; i++)
			{
				pointsA[i * strideA] += pointsB[i * strideB];
			}
		}

		public static void PsSubA(Vector3* pointsA, int strideA, Vector3* pointsB, int strideB, int count)
		{
			for (int i = 0; i != count; i++)
			{
				pointsA[i * strideA] -= pointsB[i * strideB];
			}
		}

		public static void PsMulA(Vector3* pointsA, int strideA, float scalarB, int count)
		{
			for (int i = 0; i != count; i++)
			{
				pointsA[i * strideA] *= scalarB;
			}
		}

		public static void PsMulAddA(Vector3* pointsA, int strideA, Vector3* pointsB, int strideB, float scalarC, int count)
		{
			for (int i = 0; i != count; i++)
			{
				pointsA[i * strideA] += pointsB[i * strideB] * scalarC;
			}
		}

		public static float PsDot(Vector3* pointsA, int strideA, Vector3* pointsB, int strideB, int count)
		{
			var sum = 0.0f;
			for (int i = 0; i != count; i++)
			{
				ref readonly var a = ref pointsA[i * strideA];
				ref readonly var b = ref pointsB[i * strideB];
				sum += (a.x * b.x);
				sum += (a.y * b.y);
				sum += (a.z * b.z);
			}
			return sum;
		}

		public static float PsNorm(Vector3* pointsA, int strideA, int count) => Mathf.Sqrt(PsNormSq(pointsA, strideA, count));
		public static float PsNormSq(Vector3* pointsA, int strideA, int count)
		{
			var sum = 0.0f;
			for (int i = 0; i != count; i++)
			{
				ref readonly var a = ref pointsA[i * strideA];
				sum += (a.x * a.x);
				sum += (a.y * a.y);
				sum += (a.z * a.z);
			}
			return sum;
		}

		public static float PsNormL1(Vector3* pointsA, int strideA, int count)
		{
			var sum = 0.0f;
			for (int i = 0; i != count; i++)
			{
				ref readonly var a = ref pointsA[i * strideA];
				sum += Mathf.Abs(a.x);
				sum += Mathf.Abs(a.y);
				sum += Mathf.Abs(a.z);
			}
			return sum;
		}

		public static float PsDistance(Vector3* pointsA, int strideA, Vector3* pointsB, int strideB, int count) => Mathf.Sqrt(PsDistanceSq(pointsA, strideA, pointsB, strideB, count));
		public static float PsDistanceSq(Vector3* pointsA, int strideA, Vector3* pointsB, int strideB, int count)
		{
			var sum = 0.0f;
			for (int i = 0; i != count; i++)
			{
				ref readonly var a = ref pointsA[i * strideA];
				ref readonly var b = ref pointsB[i * strideB];
				var dx = a.x - b.x;
				var dy = a.y - b.y;
				var dz = a.z - b.z;
				sum += (dx * dx);
				sum += (dy * dy);
				sum += (dz * dz);
			}
			return sum;
		}

		public static float PsDistanceL1(Vector3* pointsA, int strideA, Vector3* pointsB, int strideB, int count)
		{
			var sum = 0.0f;
			for (int i = 0; i != count; i++)
			{
				ref readonly var a = ref pointsA[i * strideA];
				ref readonly var b = ref pointsB[i * strideB];
				var dx = a.x - b.x;
				var dy = a.y - b.y;
				var dz = a.z - b.z;
				sum += Mathf.Abs(dx);
				sum += Mathf.Abs(dy);
				sum += Mathf.Abs(dz);
			}
			return sum;
		}

		public static float PsCosineSimilarity(Vector3* pointsA, int strideA, Vector3* pointsB, int strideB, int count)
		{
			var dotAB = PsDot(pointsA, strideA, pointsB, strideB, count);
			var normA = PsNorm(pointsA, strideA, count);
			var normB = PsNorm(pointsB, strideB, count);
			return dotAB / (normA * normB);
		}
	}
}
