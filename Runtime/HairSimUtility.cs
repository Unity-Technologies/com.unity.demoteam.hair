using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
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

		public static bool CreateVolume(ref RenderTexture volume, string name, int cells, RenderTextureFormat format)
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

		public static bool CreateVolume(ref RenderTexture volume, string name, int cells, GraphicsFormat format)
		{
			if (volume != null && volume.width == cells && volume.graphicsFormat == format)
				return false;

			if (volume != null)
				volume.Release();

			RenderTextureDescriptor volumeDesc = new RenderTextureDescriptor()
			{
				dimension = TextureDimension.Tex3D,
				width = cells,
				height = cells,
				volumeDepth = cells,
				graphicsFormat = format,
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

		public static void SwapBuffers(ref ComputeBuffer bufferA, ref ComputeBuffer bufferB)
		{
			ComputeBuffer tmp = bufferA;
			bufferA = bufferB;
			bufferB = tmp;
		}

		public static void SwapVolumes(ref RenderTexture volumeA, ref RenderTexture volumeB)
		{
			RenderTexture tmp = volumeA;
			volumeA = volumeB;
			volumeB = tmp;
		}

		public static void InitializeStaticFields<T>(Type type, Func<string, T> construct)
		{
			foreach (var field in type.GetFields())
			{
				field.SetValue(null, construct(field.Name));
			}
		}

		public static void InitializeStructFields<TStruct, T>(ref TStruct data, Func<string, T> construct) where TStruct : struct
		{
			Type type = typeof(TStruct);
			var boxed = (object)data;
			foreach (var field in type.GetFields())
			{
				field.SetValue(boxed, construct(field.Name));
			}
			data = (TStruct)boxed;
		}
	}
}
