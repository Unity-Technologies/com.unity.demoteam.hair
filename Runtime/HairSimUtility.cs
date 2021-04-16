using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace Unity.DemoTeam.Hair
{
	public static class HairSimUtility
	{
		//---------------
		// gpu resources

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

		//------------------
		// gpu push targets

		public interface IPushTarget
		{
			void PushConstantBuffer(int nameID, ComputeBuffer cbuffer);
			void PushComputeBuffer(int nameID, ComputeBuffer buffer);
			void PushComputeTexture(int nameID, RenderTexture texture);
			void PushKeyword(string name, bool value);
		}

		public struct PushTargetCompute : IPushTarget
		{
			public ComputeShader cs;
			public int kernel;

			public PushTargetCompute(ComputeShader cs, int kernel)
			{
				this.cs = cs;
				this.kernel = kernel;
			}

			public void PushConstantBuffer(int nameID, ComputeBuffer cbuffer) => cs.SetConstantBuffer(nameID, cbuffer, 0, cbuffer.stride);
			public void PushComputeBuffer(int nameID, ComputeBuffer buffer) => cs.SetBuffer(kernel, nameID, buffer);
			public void PushComputeTexture(int nameID, RenderTexture texture) => cs.SetTexture(kernel, nameID, texture);
			public void PushKeyword(string name, bool value) => CoreUtils.SetKeyword(cs, name, value);
		}

		public struct PushTargetComputeCmd : IPushTarget
		{
			public CommandBuffer cmd;
			public ComputeShader cs;
			public int kernel;

			public PushTargetComputeCmd(CommandBuffer cmd, ComputeShader cs, int kernel)
			{
				this.cmd = cmd;
				this.cs = cs;
				this.kernel = kernel;
			}

			public void PushConstantBuffer(int nameID, ComputeBuffer cbuffer) => cmd.SetComputeConstantBufferParam(cs, nameID, cbuffer, 0, cbuffer.stride);
			public void PushComputeBuffer(int nameID, ComputeBuffer buffer) => cmd.SetComputeBufferParam(cs, kernel, nameID, buffer);
			public void PushComputeTexture(int nameID, RenderTexture texture) => cmd.SetComputeTextureParam(cs, kernel, nameID, texture);
			public void PushKeyword(string name, bool value) => CoreUtils.SetKeyword(cmd, name, value);
		}

		public struct PushTargetGlobal : IPushTarget
		{
			public void PushConstantBuffer(int nameID, ComputeBuffer cbuffer) => Shader.SetGlobalConstantBuffer(nameID, cbuffer, 0, cbuffer.stride);
			public void PushComputeBuffer(int nameID, ComputeBuffer buffer) => Shader.SetGlobalBuffer(nameID, buffer);
			public void PushComputeTexture(int nameID, RenderTexture texture) => Shader.SetGlobalTexture(nameID, texture);
			public void PushKeyword(string name, bool value)
			{
				if (value)
					Shader.EnableKeyword(name);
				else
					Shader.DisableKeyword(name);
			}
		}

		public struct PushTargetGlobalCmd : IPushTarget
		{
			public CommandBuffer cmd;

			public PushTargetGlobalCmd(CommandBuffer cmd)
			{
				this.cmd = cmd;
			}

			public void PushConstantBuffer(int nameID, ComputeBuffer cbuffer) => cmd.SetGlobalConstantBuffer(cbuffer, nameID, 0, cbuffer.stride);
			public void PushComputeBuffer(int nameID, ComputeBuffer buffer) => cmd.SetGlobalBuffer(nameID, buffer);
			public void PushComputeTexture(int nameID, RenderTexture texture) => cmd.SetGlobalTexture(nameID, texture);
			public void PushKeyword(string name, bool value) => CoreUtils.SetKeyword(cmd, name, value);
		}

		public struct PushTargetMaterial : IPushTarget
		{
			public Material mat;

			public PushTargetMaterial(Material mat)
			{
				this.mat = mat;
			}

			public void PushConstantBuffer(int nameID, ComputeBuffer cbuffer) => mat.SetConstantBuffer(nameID, cbuffer, 0, cbuffer.stride);
			public void PushComputeBuffer(int nameID, ComputeBuffer buffer) => mat.SetBuffer(nameID, buffer);
			public void PushComputeTexture(int nameID, RenderTexture texture) => mat.SetTexture(nameID, texture);
			public void PushKeyword(string name, bool value) => CoreUtils.SetKeyword(mat, name, value);
		}

		//------------
		// reflection

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

		public static void EnumerateFields(Type type, Action<int, System.Reflection.FieldInfo> visit)
		{
			var fields = type.GetFields();
			for (int i = 0; i != fields.Length; i++)
			{
				visit(i, fields[i]);
			}
		}
	}
}
