using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.DemoTeam.Hair
{
	public static class HairSimUtility
	{
		public static bool CreateBuffer(ref ComputeBuffer buffer, string name, int count, int stride, ComputeBufferType type = ComputeBufferType.Default)
		{
			if (buffer != null && buffer.count == count && buffer.stride == stride && buffer.IsValid())
				return false;

			if (buffer != null)
				buffer.Release();

			buffer = new ComputeBuffer(count, stride, type);
			buffer.name = name;
			return true;
		}

		public static void ReleaseBuffer(ref ComputeBuffer buffer)
		{
			if (buffer != null)
			{
				buffer.Release();
				buffer = null;
			}
		}

		public static void SwapBuffers(ref ComputeBuffer bufferA, ref ComputeBuffer bufferB)
		{
			ComputeBuffer tmp = bufferA;
			bufferA = bufferB;
			bufferB = tmp;
		}

		public static bool CreateVolume(ref RenderTexture volume, string name, int cells, RenderTextureFormat format = RenderTextureFormat.Default)
		{
			if (volume != null && volume.width == cells && volume.format == format)
				return false;

			if (volume != null)
				volume.Release();

			RenderTextureDescriptor volumeDesc = new RenderTextureDescriptor()
			{
				dimension = TextureDimension.Tex3D,
				width = cells,
				height = cells,
				volumeDepth = cells,
				colorFormat = format,
				enableRandomWrite = true,
				msaaSamples = 1,
			};

			//Debug.Log("creating volume " + name);
			volume = new RenderTexture(volumeDesc);
			volume.wrapMode = TextureWrapMode.Clamp;
			volume.hideFlags = HideFlags.HideAndDontSave;
			volume.name = name;
			volume.Create();
			return true;
		}

		public static void ReleaseVolume(ref RenderTexture volume)
		{
			if (volume != null)
			{
				volume.Release();
				volume = null;
			}
		}

		public static void SwapVolumes(ref RenderTexture volumeA, ref RenderTexture volumeB)
		{
			RenderTexture tmp = volumeA;
			volumeA = volumeB;
			volumeB = tmp;
		}

		public static void UpdateConstantBuffer<T>(ComputeBuffer buffer, ref T contents) where T : struct
		{
			using (NativeArray<T> tmp = new NativeArray<T>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			{
				unsafe
				{
					UnsafeUtility.CopyStructureToPtr(ref contents, tmp.GetUnsafePtr());
					buffer.SetData(tmp);
				}
			}
		}
	}
}
