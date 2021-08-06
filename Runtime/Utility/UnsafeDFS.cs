using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.DemoTeam.Hair
{
	public unsafe struct UnsafeDFS : IDisposable
	{
		public int nodeIndex;
		public int nodeDepth;

		public NativeArray<ulong> pending;// pending[i] == (nodeDepth << 32) | nodeIndex
		public ulong* pendingPtr;
		public int pendingHead;

		public NativeArray<bool> visited;
		public bool* visitedPtr;

		public UnsafeDFS(int nodeCount, Allocator allocator)
		{
			nodeIndex = -1;
			nodeDepth = -1;

			pending = new NativeArray<ulong>(nodeCount, allocator, NativeArrayOptions.UninitializedMemory);
			pendingPtr = (ulong*)pending.GetUnsafePtr();
			pendingHead = 0;

			visited = new NativeArray<bool>(nodeCount, allocator, NativeArrayOptions.ClearMemory);
			visitedPtr = (bool*)visited.GetUnsafePtr();
		}

		public void Dispose()
		{
			pending.Dispose();
			visited.Dispose();
		}

		public void Reset()
		{
			nodeIndex = -1;
			nodeDepth = -1;

			pendingHead = 0;

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
			ulong packedDepth = (ulong)(nodeDepth + 1) << 32;

			pendingPtr[++pendingHead] = packedDepth | packedIndex;
			visitedPtr[nodeIndex] = true;
		}

		public bool MoveNext(out int nodeIndex, out int nodeDepth)
		{
			if (pendingHead > 0)
			{
				ulong packed = pendingPtr[--pendingHead];
				this.nodeIndex = nodeIndex = (int)(packed & 0xffffffffuL);
				this.nodeDepth = nodeDepth = (int)(packed >> 32);
				return true;
			}
			else
			{
				this.nodeIndex = nodeIndex = -1;
				this.nodeDepth = nodeDepth = -1;
				return false;
			}
		}
	}
}
