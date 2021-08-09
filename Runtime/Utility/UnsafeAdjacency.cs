using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.DemoTeam.Hair
{
	public unsafe struct UnsafeAdjacency : IDisposable
	{
		public struct LinkedIndexList
		{
			public int head;
			public int size;
		}

		public struct LinkedIndexItem
		{
			public int next;
			public int prev;
			public int data;
		}

		public NativeArray<LinkedIndexList> lists;
		public LinkedIndexList* listsPtr;

		public UnsafeList<LinkedIndexItem> items;
		public LinkedIndexItem* itemsPtr;

		public int listCount;
		public int itemCount;

		public UnsafeAdjacency(int listCapacity, int itemCapacity, Allocator allocator)
		{
			lists = new NativeArray<LinkedIndexList>(listCapacity, allocator, NativeArrayOptions.UninitializedMemory);
			listsPtr = (LinkedIndexList*)lists.GetUnsafePtr();

			items = new UnsafeList<LinkedIndexItem>(itemCapacity, allocator, NativeArrayOptions.UninitializedMemory);
			itemsPtr = (LinkedIndexItem*)items.Ptr;

			listCount = lists.Length;
			itemCount = 0;

			Clear();
		}

		public void Dispose()
		{
			lists.Dispose();
			items.Dispose();
		}

		public void Clear()
		{
			listCount = lists.Length;
			itemCount = 0;

			for (int i = 0; i != listCount; i++)
			{
				listsPtr[i].head = -1;
				listsPtr[i].size = 0;
			}
		}

		public void Append(int listIndex, int value)
		{
			if (itemCount == items.Capacity)
			{
				items.Resize(itemCount * 2, NativeArrayOptions.UninitializedMemory);
				itemsPtr = (LinkedIndexItem*)items.Ptr;
			}

			int headIndex = listsPtr[listIndex].head;
			if (headIndex == -1)
			{
				int itemIndex = itemCount++;

				itemsPtr[itemIndex] = new LinkedIndexItem
				{
					next = itemIndex,
					prev = itemIndex,
					data = value,
				};

				listsPtr[listIndex].head = itemIndex;
				listsPtr[listIndex].size = 1;
			}
			else
			{
				int itemIndex = itemCount++;
				int tailIndex = itemsPtr[headIndex].prev;

				itemsPtr[itemIndex] = new LinkedIndexItem
				{
					next = headIndex,
					prev = tailIndex,
					data = value,
				};

				itemsPtr[tailIndex].next = itemIndex;
				itemsPtr[headIndex].prev = itemIndex;

				listsPtr[listIndex].size++;
			}
		}

		public void AppendMove(int listIndex, int listOther)
		{
			int headOther = listsPtr[listOther].head;
			if (headOther != -1)
			{
				int headIndex = listsPtr[listIndex].head;
				if (headIndex != -1)
				{
					int tailIndex = itemsPtr[headIndex].prev;
					int tailOther = itemsPtr[headOther].prev;

					itemsPtr[headIndex].prev = tailOther;
					itemsPtr[tailIndex].next = headOther;

					itemsPtr[headOther].prev = tailIndex;
					itemsPtr[tailOther].next = headIndex;

					listsPtr[listIndex].size += listsPtr[listOther].size;
				}
				else
				{
					listsPtr[listIndex].head = listsPtr[listOther].head;
					listsPtr[listIndex].size = listsPtr[listOther].size;
				}

				listsPtr[listOther].head = -1;
				listsPtr[listOther].size = 0;
			}
		}

		public int GetCount(int listIndex)
		{
			return listsPtr[listIndex].size;
		}

		public LinkedIndexEnumerable this[int listIndex]
		{
			get
			{
				return new LinkedIndexEnumerable(itemsPtr, listsPtr[listIndex].head);
			}
		}

		public struct LinkedIndexEnumerable : IEnumerable<int>
		{
			public LinkedIndexItem* itemsPtr;
			public int headIndex;

			public LinkedIndexEnumerable(LinkedIndexItem* itemsPtr, int headIndex)
			{
				this.itemsPtr = itemsPtr;
				this.headIndex = headIndex;
			}

			public LinkedIndexEnumerator GetEnumerator()
			{
				return new LinkedIndexEnumerator(itemsPtr, headIndex, -1);
			}

			IEnumerator<int> IEnumerable<int>.GetEnumerator()
			{
				return GetEnumerator();
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}
		}

		public struct LinkedIndexEnumerator : IEnumerator<int>
		{
			public LinkedIndexItem* itemsPtr;
			public int headIndex;
			public int itemIndex;

			public LinkedIndexEnumerator(LinkedIndexItem* itemsPtr, int headIndex, int itemIndex)
			{
				this.itemsPtr = itemsPtr;
				this.headIndex = headIndex;
				this.itemIndex = itemIndex;
			}

			public int Current
			{
				get { return itemsPtr[itemIndex].data; }
			}

			object IEnumerator.Current
			{
				get { return Current; }
			}

			public bool MoveNext()
			{
				if (itemIndex == -1)
				{
					itemIndex = headIndex;
					return (itemIndex != -1);
				}
				else
				{
					itemIndex = itemsPtr[itemIndex].next;
					return (itemIndex != headIndex);// stop if we've come full circle
				}
			}

			public int ReadNext()
			{
				if (MoveNext())
					return Current;
				else
					return -1;
			}

			public void Reset()
			{
				itemIndex = headIndex;
			}

			public void Dispose()
			{
				// foo
			}
		}
	}
}
