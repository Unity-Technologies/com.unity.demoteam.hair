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
		float SqDistanceLeafPoint(uint leafIndex, in float3 p);
		float SqDistanceLeafTrace(uint leafIndex, in float3 p, in float3 r);
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

		static void SortWithinNode(Leaf* leafPtr, int leafCount, Node* nodePtr)
		{
			var size = abs(nodePtr->data.max - nodePtr->data.min);
			if (size.x >= size.y && size.x >= size.z)
				NativeSortExtension.Sort(leafPtr, leafCount, leafComparerX);
			else if (size.y >= size.z)
				NativeSortExtension.Sort(leafPtr, leafCount, leafComparerY);
			else
				NativeSortExtension.Sort(leafPtr, leafCount, leafComparerZ);
		}

		static void SplitWithinNode(Leaf* leafPtr, int leafCount, Node* nodePtr, out int leafCountL, out int leafCountR)
		{
			SortWithinNode(leafPtr, leafCount, nodePtr);

			leafCountL = (leafCount + 1) / 2;
			leafCountR = leafCount - leafCountL;

			//TODO sah?
		}

		static Node* BuildNode(ref Node* nodeWritePtr, Leaf* leafPtr, int leafCount)
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
		public static float SqDistanceNodePoint(Node* nodePtr, in float3 p)
		{
			// see: "distance functions" by Inigo Quilez
			// https://www.iquilezles.org/www/articles/distfunctions/distfunctions.htm

			var b = 0.5f * (nodePtr->data.max - nodePtr->data.min);
			var c = 0.5f * (nodePtr->data.max + nodePtr->data.min);
			var q = abs(p - c) - b;
			var d = length(max(q, 0.0f)) + min(max(q.x, max(q.y, q.z)), 0.0f);

			return (d * d);
		}

		public static uint FindClosestLeaf<T>(in T context, in UnsafeBVH bvh, in float3 p) where T : IUnsafeBVHContext
		{
			var bestDist = float.PositiveInfinity;
			var bestIndex = uint.MaxValue;

			FindClosestLeaf(context, bvh.GetUnsafeRootPtr(), p, &bestDist, &bestIndex);

			return bestIndex;
		}

		static void FindClosestLeaf<T>(in T context, Node* nodePtr, in float3 p, float* bestDistPtr, uint* bestIndexPtr) where T : IUnsafeBVHContext
		{
			// examine leaf data
			var leafIndex = nodePtr->data.index;
			if (leafIndex != uint.MaxValue)
			{
				var distSq = context.SqDistanceLeafPoint(leafIndex, p);
				if (distSq < *bestDistPtr)
				{
					*bestDistPtr = distSq;
					*bestIndexPtr = leafIndex;
				}
			}

			// examine subtrees (closest first)
			var distL = (nodePtr->stepL != 0) ? SqDistanceNodePoint(nodePtr + nodePtr->stepL, p) : float.PositiveInfinity;
			var distR = (nodePtr->stepR != 0) ? SqDistanceNodePoint(nodePtr + nodePtr->stepR, p) : float.PositiveInfinity;
			if (distR < distL)
			{
				if (distR < *bestDistPtr) FindClosestLeaf(context, nodePtr + nodePtr->stepR, p, bestDistPtr, bestIndexPtr);
				if (distL < *bestDistPtr) FindClosestLeaf(context, nodePtr + nodePtr->stepL, p, bestDistPtr, bestIndexPtr);
			}
			else
			{
				if (distL < *bestDistPtr) FindClosestLeaf(context, nodePtr + nodePtr->stepL, p, bestDistPtr, bestIndexPtr);
				if (distR < *bestDistPtr) FindClosestLeaf(context, nodePtr + nodePtr->stepR, p, bestDistPtr, bestIndexPtr);
			}
		}
	}
}
