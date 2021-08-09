using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.DemoTeam.Hair
{
	public unsafe struct UnsafeBFS : IDisposable
	{
		public int currentNodeIndex;
		public int currentNodeDepth;

		public NativeArray<ulong> pending;// pending[i] == (nodeDepth << 32) | nodeIndex
		public ulong* pendingPtr;
		public int pendingHead;
		public int pendingTail;

		public NativeArray<bool> visited;
		public bool* visitedPtr;

		public UnsafeBFS(int nodeCount, Allocator allocator)
		{
			currentNodeIndex = -1;
			currentNodeDepth = -1;

			pending = new NativeArray<ulong>(nodeCount, allocator, NativeArrayOptions.UninitializedMemory);
			pendingPtr = (ulong*)pending.GetUnsafePtr();
			pendingHead = 0;
			pendingTail = 0;

			visited = new NativeArray<bool>(nodeCount, allocator, NativeArrayOptions.ClearMemory);
			visitedPtr = (bool*)visited.GetUnsafePtr();
		}

		public void Dispose()
		{
			pending.Dispose();
			visited.Dispose();
		}

		public void Clear()
		{
			currentNodeIndex = -1;
			currentNodeDepth = -1;

			pendingHead = 0;
			pendingTail = 0;

			UnsafeUtility.MemClear(visitedPtr, sizeof(bool) * visited.Length);
		}

		public void Ignore(int nodeIndex)
		{
			visitedPtr[nodeIndex] = true;
		}

		public void Insert(int nodeIndex)
		{
			if (visitedPtr[nodeIndex])
				return;

			ulong packedIndex = (ulong)nodeIndex;
			ulong packedDepth = (ulong)(currentNodeDepth + 1) << 32;

			pendingPtr[pendingTail++] = packedDepth | packedIndex;
			visitedPtr[nodeIndex] = true;
		}

		public bool MoveNext(out int nodeIndex, out int nodeDepth)
		{
			if (pendingHead != pendingTail)
			{
				ulong packed = pendingPtr[pendingHead++];
				nodeIndex = currentNodeIndex = (int)(packed & 0xffffffffuL);
				nodeDepth = currentNodeDepth = (int)(packed >> 32);
				return true;
			}
			else
			{
				nodeIndex = currentNodeIndex = -1;
				nodeDepth = currentNodeDepth = -1;
				return false;
			}
		}
	}
}
