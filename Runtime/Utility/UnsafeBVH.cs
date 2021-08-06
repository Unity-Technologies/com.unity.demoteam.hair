using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

using static Unity.Mathematics.math;

namespace Unity.DemoTeam.Hair
{
	using Leaf = UnsafeBVH.Leaf;
	using Node = UnsafeBVH.Node;

	public unsafe interface IUnsafeBVHContext
	{
		int GetLeafCount();
		void BuildLeafData(Leaf* leafPtr, int leafCount);
		float DistanceLeafPoint(uint leafIndex, in float3 p);
		float DistanceLeafTrace(uint leafIndex, in float3 p, in float3 r);
	}

	public unsafe struct UnsafeBVH : IDisposable
	{
		public struct Leaf
		{
			public float3 min;// aabb min
			public float3 max;// aabb max
			public uint index;// context data index

			public static readonly Leaf empty = new Leaf
			{
				min = Vector3.positiveInfinity,
				max = Vector3.negativeInfinity,
				index = uint.MaxValue,
			};
		}

		public struct Node
		{
			public Leaf data;
			public uint stepL;
			public uint stepR;

			public static readonly Node empty = new Node
			{
				data = Leaf.empty,
				stepL = 0,
				stepR = 0,
			};

			public bool Contains(in float3 p)
			{
				return all((p >= data.min) & (p <= data.max));
			}
		}

		public NativeArray<Node> nodeData;

		public UnsafeBVH(int leafCapacity, Allocator allocator)
		{
			nodeData = new NativeArray<Node>(leafCapacity * 2 - 1, allocator, NativeArrayOptions.UninitializedMemory);
			nodeData[0] = Node.empty;
		}

		public void Dispose()
		{
			nodeData.Dispose();
		}

		public Node* GetUnsafeRootPtr()
		{
			return (Node*)nodeData.GetUnsafePtr();
		}
	}

	public static unsafe class UnsafeBVHBuilder
	{
		struct LeafComparerX : IComparer<Leaf> { public int Compare(Leaf a, Leaf b) => a.min.x.CompareTo(b.min.x); }
		struct LeafComparerY : IComparer<Leaf> { public int Compare(Leaf a, Leaf b) => a.min.y.CompareTo(b.min.y); }
		struct LeafComparerZ : IComparer<Leaf> { public int Compare(Leaf a, Leaf b) => a.min.z.CompareTo(b.min.z); }

		static readonly LeafComparerX leafComparerX = new LeafComparerX();
		static readonly LeafComparerY leafComparerY = new LeafComparerY();
		static readonly LeafComparerZ leafComparerZ = new LeafComparerZ();

		static Leaf MakeUnion(Leaf* leafPtr, int leafCount)
		{
			if (leafCount > 0)
			{
				var result = leafPtr[0];
				for (int i = 0; i != leafCount; i++)
				{
					result.min = min(result.min, leafPtr[i].min);
					result.max = max(result.max, leafPtr[i].max);
					result.index = uint.MaxValue;
				}
				return result;
			}
			else
			{
				return Leaf.empty;
			}
		}

		static void SortWithinNode(Leaf* leafPtr, int leafCount, UnsafeBVH.Node* nodePtr)
		{
			var size = abs(nodePtr->data.max - nodePtr->data.min);
			if (size.x >= size.y && size.x >= size.z)
				NativeSortExtension.Sort(leafPtr, leafCount, leafComparerX);
			else if (size.y >= size.z)
				NativeSortExtension.Sort(leafPtr, leafCount, leafComparerY);
			else
				NativeSortExtension.Sort(leafPtr, leafCount, leafComparerZ);
		}

		static void SplitWithinNode(Leaf* leafPtr, int leafCount, UnsafeBVH.Node* nodePtr, out int leafCountL, out int leafCountR)
		{
			SortWithinNode(leafPtr, leafCount, nodePtr);

			leafCountL = (leafCount + 1) / 2;
			leafCountR = leafCount - leafCountL;

			//TODO sah?
		}

		static UnsafeBVH.Node* BuildNode(ref UnsafeBVH.Node* nodeWritePtr, Leaf* leafPtr, int leafCount)
		{
			var nodePtr = nodeWritePtr++;

			if (leafCount == 1)
			{
				nodePtr->data = *leafPtr;
				nodePtr->stepL = 0;
				nodePtr->stepR = 0;
			}
			else
			{
				nodePtr->data = MakeUnion(leafPtr, leafCount);

				SplitWithinNode(leafPtr, leafCount, nodePtr, out var leafCountL, out var leafCountR);

				nodePtr->stepL = (leafCountL == 0) ? 0 : (uint)(BuildNode(ref nodeWritePtr, leafPtr, leafCountL) - nodePtr);
				nodePtr->stepR = (leafCountR == 0) ? 0 : (uint)(BuildNode(ref nodeWritePtr, leafPtr + leafCountL, leafCountR) - nodePtr);
			}

			return nodePtr;
		}

		public static UnsafeBVH CreateFromContext<T>(in T context, Allocator allocator) where T : IUnsafeBVHContext
		{
			using (var leafData = new NativeArray<Leaf>(context.GetLeafCount(), allocator))
			{
				var leafPtr = (Leaf*)leafData.GetUnsafePtr();
				var leafCount = leafData.Length;
				{
					context.BuildLeafData(leafPtr, leafCount);
				}

				var bvh = new UnsafeBVH(leafCount, allocator);
				var bvhWritePtr = bvh.GetUnsafeRootPtr();
				{
					BuildNode(ref bvhWritePtr, leafPtr, leafCount);
				}

				return bvh;
			}
		}
	}

	public static unsafe class UnsafeBVHQueries
	{
		public static uint FindClosestLeaf<T>(in T context, in UnsafeBVH bvh, in float3 p) where T : IUnsafeBVHContext
		{
			var bestDist = float.PositiveInfinity;
			var bestIndex = uint.MaxValue;

			FindClosestLeaf(context, bvh.GetUnsafeRootPtr(), p, &bestDist, &bestIndex);

			return bestIndex;
		}

		static void FindClosestLeaf<T>(in T context, UnsafeBVH.Node* nodePtr, in float3 p, float* bestDistPtr, uint* bestIndexPtr) where T : IUnsafeBVHContext
		{
			if (nodePtr->Contains(p))
			{
				// examine leaf
				var leafIndex = nodePtr->data.index;
				if (leafIndex != uint.MaxValue)
				{
					var dist = context.DistanceLeafPoint(leafIndex, p);
					if (dist < *bestDistPtr)
					{
						*bestDistPtr = dist;
						*bestIndexPtr = leafIndex;
					}
				}

				// examine left subtree
				if (nodePtr->stepL != 0)
				{
					FindClosestLeaf(context, nodePtr + nodePtr->stepL, p, bestDistPtr, bestIndexPtr);
				}

				// examine right subtree
				if (nodePtr->stepR != 0)
				{
					FindClosestLeaf(context, nodePtr + nodePtr->stepR, p, bestDistPtr, bestIndexPtr);
				}
			}
		}
	}
}
