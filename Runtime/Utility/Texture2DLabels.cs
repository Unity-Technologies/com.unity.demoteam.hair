using System;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.DemoTeam.Hair
{
	public unsafe struct Texture2DLabels : IDisposable
	{
		public delegate uint GetLabelDelegate(int x, int y);

		public int dimX;
		public int dimY;

		public NativeArray<uint> texelLabel;
		public uint* texelLabelPtr;

		public uint labelCount;
		public bool labelWrap;

		//TODO optimize me + runs out of memory for clump maps >4k
		public Texture2DLabels(Texture2D texture, TextureWrapMode wrapMode, Allocator allocator)
		{
			dimX = texture.width;
			dimY = texture.height;

			texelLabel = new NativeArray<uint>(dimX * dimY, allocator, NativeArrayOptions.UninitializedMemory);
			texelLabelPtr = (uint*)texelLabel.GetUnsafePtr();

			labelCount = 0;
			labelWrap = (wrapMode == TextureWrapMode.Repeat);

			var texelData = texture.GetRawTextureData<byte>();
			var texelDataPtr = (byte*)texelData.GetUnsafePtr();

			var texelCount = dimX * dimY;
			var texelSize = texelData.Length / texelCount;

			var texelBG = new NativeArray<byte>(texelSize, Allocator.Temp, NativeArrayOptions.ClearMemory);
			var texelBGPtr = (byte*)texelBG.GetUnsafePtr();

			// discover matching neighbours
			Profiler.BeginSample("discover matching neighbours");
			var texelAdjacency = new UnsafeAdjacency(texelCount, 8 * texelCount, Allocator.Temp);
			{
				bool CompareEquals(byte* ptrA, byte* ptrB, int count)
				{
					for (int i = 0; i != count; i++)
					{
						if (ptrA[i] != ptrB[i])
							return false;
					}

					return true;
				};

				void CompareAndConnect(ref UnsafeAdjacency adjacency, byte* valueBasePtr, int valueSize, int indexA, int indexB)
				{
					if (CompareEquals(valueBasePtr + valueSize * indexA, valueBasePtr + valueSize * indexB, valueSize))
					{
						adjacency.Append(indexA, indexB);
						adjacency.Append(indexB, indexA);
					}
				};

				if (wrapMode == TextureWrapMode.Repeat)
				{
					// a b c
					// d e <-- 'e' is sweep index at (x, y)
					for (int y = 0; y != dimY; y++)
					{
						int ym = (y - 1 + dimY) % dimY;
						int yp = (y + 1) % dimY;

						for (int x = 0; x != dimX; x++)
						{
							int xm = (x - 1 + dimX) % dimX;
							int xp = (x + 1) % dimX;

							int i_a = dimX * ym + xm;
							int i_b = dimX * ym + x;
							int i_c = dimX * ym + xp;
							int i_d = dimX * y + xm;
							int i_e = dimX * y + x;

							if (CompareEquals(texelBGPtr, texelDataPtr + texelSize * i_e, texelSize))
								continue;

							// 8-way connected clusters
							{
								CompareAndConnect(ref texelAdjacency, texelDataPtr, texelSize, i_e, i_a);
								CompareAndConnect(ref texelAdjacency, texelDataPtr, texelSize, i_e, i_b);
								CompareAndConnect(ref texelAdjacency, texelDataPtr, texelSize, i_e, i_c);
								CompareAndConnect(ref texelAdjacency, texelDataPtr, texelSize, i_e, i_d);
							}
						}
					}
				}
				else// wrapMode == TextureWrapMode.Clamp
				{
					// a b c
					// d e <-- 'e' is sweep index at (x, y)
					for (int y = 0; y != dimY; y++)
					{
						int ym = Mathf.Max(y - 1);
						int yp = Mathf.Min(y + 1, dimY - 1);

						for (int x = 0; x != dimX; x++)
						{
							int xm = Mathf.Max(x - 1, 0);
							int xp = Mathf.Min(x + 1, dimX - 1);

							int i_a = dimX * ym + xm;
							int i_b = dimX * ym + x;
							int i_c = dimX * ym + xp;
							int i_d = dimX * y + xm;
							int i_e = dimX * y + x;

							if (CompareEquals(texelBGPtr, texelDataPtr + texelSize * i_e, texelSize))
								continue;

							// 8-way connected clusters except at boundary
							if (i_e != i_a) CompareAndConnect(ref texelAdjacency, texelDataPtr, texelSize, i_e, i_a);
							if (i_e != i_b) CompareAndConnect(ref texelAdjacency, texelDataPtr, texelSize, i_e, i_b);
							if (i_e != i_c) CompareAndConnect(ref texelAdjacency, texelDataPtr, texelSize, i_e, i_c);
							if (i_e != i_d) CompareAndConnect(ref texelAdjacency, texelDataPtr, texelSize, i_e, i_d);
						}
					}
				}
			}
			Profiler.EndSample();

			// discover clusters, write label per texel
			Profiler.BeginSample("discover clusters");
			var texelVisitor = new UnsafeBFS(texelCount, Allocator.Temp);
			{
				for (int i = 0; i != texelCount; i++)
				{
					if (texelVisitor.visitedPtr[i])
						continue;

					labelCount++;

					if (texelAdjacency.listsPtr[i].size > 0)
					{
						texelVisitor.Insert(i);

						while (texelVisitor.MoveNext(out int visitedIndex, out int visitedDepth))
						{
							foreach (var adjacentIndex in texelAdjacency[visitedIndex])
							{
								texelVisitor.Insert(adjacentIndex);
							}

							texelLabelPtr[visitedIndex] = labelCount;
						}
					}
					else
					{
						texelVisitor.Ignore(i);
						texelLabelPtr[i] = labelCount;
					}
				}
			}
			Profiler.EndSample();

			// dispose temporary structures
			texelAdjacency.Dispose();
			texelVisitor.Dispose();
			texelBG.Dispose();
		}

		public void Dispose()
		{
			texelLabel.Dispose();
		}

		public uint GetLabelCount()
		{
			return labelCount;
		}

		public uint GetLabel(int x, int y)
		{
			if (labelWrap)
			{
				int Wrap(int a, int n)
				{
					int r = a % n;
					if (r < 0)
						return r + n;
					else
						return r;
				}

				x = Wrap(x, dimX);
				y = Wrap(y, dimY);
			}
			else
			{
				int Clamp(int a, int n)
				{
					return (a < 0) ? 0 : ((a > n - 1) ? (n - 1) : a);
				}

				x = Clamp(x, dimX);
				y = Clamp(y, dimY);
			}

			return texelLabelPtr[x + y * dimX];
		}
	}
}
