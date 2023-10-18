using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Unity.Collections;

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

			buffer = new ComputeBuffer(count > 0 ? count : 1, stride, type, ComputeBufferMode.Dynamic);
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

		public static bool CreateReadbackBuffer(ref AsyncReadbackBuffer bufferReadback, in ComputeBuffer buffer)
		{
			if (bufferReadback.buffer.IsCreated && bufferReadback.buffer.Length == (buffer.count * buffer.stride))
				return false;

			if (bufferReadback.buffer.IsCreated)
				bufferReadback.buffer.Dispose();

			//Debug.Log("creating readback buffer w/ length " + (buffer.count * buffer.stride) + " for buffer " + buffer.ToString());

			bufferReadback.buffer = new NativeArray<byte>(buffer.count * buffer.stride, Allocator.Persistent);
			return true;
		}

		public static void ReleaseReadbackBuffer(ref AsyncReadbackBuffer bufferReadback)
		{
			if (bufferReadback.buffer.IsCreated)
				bufferReadback.buffer.Dispose();
		}

		public static RenderTextureDescriptor MakeVolumeDesc(int cellCount, RenderTextureFormat cellFormat)
		{
			return new RenderTextureDescriptor()
			{
				dimension = TextureDimension.Tex3D,
				width = cellCount,
				height = cellCount,
				volumeDepth = cellCount,
				colorFormat = cellFormat,
				enableRandomWrite = true,
				msaaSamples = 1,
			};
		}

		public static RenderTextureDescriptor MakeVolumeDesc(int cellCount, GraphicsFormat cellFormat)
		{
			return new RenderTextureDescriptor()
			{
				dimension = TextureDimension.Tex3D,
				width = cellCount,
				height = cellCount,
				volumeDepth = cellCount,
				graphicsFormat = cellFormat,
				enableRandomWrite = true,
				msaaSamples = 1,
			};
		}

		public static bool CreateVolume(ref RenderTexture volume, string name, int cellCount, RenderTextureFormat cellFormat)
		{
			if (volume != null && volume.width == cellCount && volume.format == cellFormat)
				return false;

			if (volume != null)
				volume.Release();

			//Debug.Log("creating volume " + name);
			volume = new RenderTexture(MakeVolumeDesc(cellCount, cellFormat));
			volume.wrapMode = TextureWrapMode.Clamp;
			volume.hideFlags = HideFlags.HideAndDontSave;
			volume.name = name;
			volume.Create();
			return true;
		}

		public static bool CreateVolume(ref RenderTexture volume, string name, int cellCount, GraphicsFormat cellFormat)
		{
			if (volume != null && volume.width == cellCount && volume.graphicsFormat == cellFormat)
				return false;

			if (volume != null)
				volume.Release();

			//Debug.Log("creating volume " + name);
			volume = new RenderTexture(MakeVolumeDesc(cellCount, cellFormat));
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

		//----------
		// gpu data

		public static void PushComputeBufferData(CommandBuffer cmd, in ComputeBuffer buffer, in Array bufferData)
		{
#if UNITY_2021_1_OR_NEWER
			cmd.SetBufferData(buffer, bufferData);
#else
			cmd.SetComputeBufferData(buffer, bufferData);
#endif
		}

		public static void PushComputeBufferData<T>(CommandBuffer cmd, in ComputeBuffer buffer, in NativeArray<T> bufferData) where T : struct
		{
#if UNITY_2021_1_OR_NEWER
			cmd.SetBufferData(buffer, bufferData);
#else
			cmd.SetComputeBufferData(buffer, bufferData);
#endif
		}

		public static void PushConstantBufferData<T>(CommandBuffer cmd, in ComputeBuffer cbuffer, in T cbufferData) where T : struct
		{
			var cbufferStaging = new NativeArray<T>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
			{
				cbufferStaging[0] = cbufferData;
#if UNITY_2021_1_OR_NEWER
				cmd.SetBufferData(cbuffer, cbufferStaging);
#else
				cmd.SetComputeBufferData(cbuffer, cbufferStaging);
#endif
				cbufferStaging.Dispose();
			}
		}

		public struct BufferUploadContext : IDisposable
		{
			CommandBuffer cmd;
			GraphicsFence cmdFence;

			bool asyncExecution;
			bool asyncUpload;
			bool asyncWait;

			public BufferUploadContext(CommandBuffer cmd, CommandBufferExecutionFlags cmdFlags)
			{
				this.cmd = cmd;

				asyncExecution = cmdFlags.HasFlag(CommandBufferExecutionFlags.AsyncCompute);
				asyncUpload = asyncExecution && (SystemInfo.supportsGraphicsFence == false);
				asyncWait = asyncExecution && (asyncUpload == false);

				if (asyncWait)
					cmdFence = Graphics.CreateGraphicsFence(GraphicsFenceType.AsyncQueueSynchronisation, SynchronisationStageFlags.AllGPUOperations);
				else
					cmdFence = new GraphicsFence();
			}

			public void SetData(ComputeBuffer buffer, Array data)
			{
				if (asyncUpload)
					PushComputeBufferData(cmd, buffer, data);
				else
					buffer.SetData(data);
			}

			public void SetData<T>(ComputeBuffer buffer, in NativeArray<T> data) where T : struct
			{
				if (asyncUpload)
					PushComputeBufferData(cmd, buffer, data);
				else
					buffer.SetData(data);
			}

			public void Dispose()
			{
				if (asyncWait)
					cmd.WaitOnAsyncGraphicsFence(cmdFence);
			}
		}

		//------------------
		// gpu bind targets

		public interface IBindTarget
		{
			void BindConstantBuffer(int nameID, ComputeBuffer cbuffer);
			void BindComputeBuffer(int nameID, ComputeBuffer buffer);
			void BindComputeTexture(int nameID, RenderTexture texture);
			void BindKeyword(string name, bool value);
		}

		public struct BindTargetCompute : IBindTarget
		{
			public ComputeShader cs;
			public int kernel;

			public BindTargetCompute(ComputeShader cs, int kernel)
			{
				this.cs = cs;
				this.kernel = kernel;
			}

			public void BindConstantBuffer(int nameID, ComputeBuffer cbuffer) => cs.SetConstantBuffer(nameID, cbuffer, 0, cbuffer.stride);
			public void BindComputeBuffer(int nameID, ComputeBuffer buffer) => cs.SetBuffer(kernel, nameID, buffer);
			public void BindComputeTexture(int nameID, RenderTexture texture) => cs.SetTexture(kernel, nameID, texture);
			public void BindKeyword(string name, bool value) => CoreUtils.SetKeyword(cs, name, value);
		}

		public struct BindTargetComputeCmd : IBindTarget
		{
			public CommandBuffer cmd;
			public ComputeShader cs;
			public int kernel;

			public BindTargetComputeCmd(CommandBuffer cmd, ComputeShader cs, int kernel)
			{
				this.cmd = cmd;
				this.cs = cs;
				this.kernel = kernel;
			}

			public void BindConstantBuffer(int nameID, ComputeBuffer cbuffer) => cmd.SetComputeConstantBufferParam(cs, nameID, cbuffer, 0, cbuffer.stride);
			public void BindComputeBuffer(int nameID, ComputeBuffer buffer) => cmd.SetComputeBufferParam(cs, kernel, nameID, buffer);
			public void BindComputeTexture(int nameID, RenderTexture texture) => cmd.SetComputeTextureParam(cs, kernel, nameID, texture);
			public void BindKeyword(string name, bool value) => CoreUtils.SetKeyword(cmd, name, value);
		}

		public struct BindTargetGlobal : IBindTarget
		{
			public void BindConstantBuffer(int nameID, ComputeBuffer cbuffer) => Shader.SetGlobalConstantBuffer(nameID, cbuffer, 0, cbuffer.stride);
			public void BindComputeBuffer(int nameID, ComputeBuffer buffer) => Shader.SetGlobalBuffer(nameID, buffer);
			public void BindComputeTexture(int nameID, RenderTexture texture) => Shader.SetGlobalTexture(nameID, texture);
			public void BindKeyword(string name, bool value)
			{
				if (value)
					Shader.EnableKeyword(name);
				else
					Shader.DisableKeyword(name);
			}
		}

		public struct BindTargetGlobalCmd : IBindTarget
		{
			public CommandBuffer cmd;

			public BindTargetGlobalCmd(CommandBuffer cmd)
			{
				this.cmd = cmd;
			}

			public void BindConstantBuffer(int nameID, ComputeBuffer cbuffer) => cmd.SetGlobalConstantBuffer(cbuffer, nameID, 0, cbuffer.stride);
			public void BindComputeBuffer(int nameID, ComputeBuffer buffer) => cmd.SetGlobalBuffer(nameID, buffer);
			public void BindComputeTexture(int nameID, RenderTexture texture) => cmd.SetGlobalTexture(nameID, texture);
			public void BindKeyword(string name, bool value) => CoreUtils.SetKeyword(cmd, name, value);
		}

		public struct BindTargetMaterial : IBindTarget
		{
			public Material mat;

			public BindTargetMaterial(Material mat)
			{
				this.mat = mat;
			}

			public void BindConstantBuffer(int nameID, ComputeBuffer cbuffer) => mat.SetConstantBuffer(nameID, cbuffer, 0, cbuffer.stride);
			public void BindComputeBuffer(int nameID, ComputeBuffer buffer) => mat.SetBuffer(nameID, buffer);
			public void BindComputeTexture(int nameID, RenderTexture texture) => mat.SetTexture(nameID, texture);
			public void BindKeyword(string name, bool value) => CoreUtils.SetKeyword(mat, name, value);
		}

		//--------------
		// gpu readback

		public struct AsyncReadbackBuffer
		{
			public NativeArray<byte> buffer;

			private void Callback(AsyncGPUReadbackRequest request)
			{
				//Debug.Log("callback with request.done = " + request.done + ", request.hasError " + request.hasError);
				if (request.done && request.hasError == false)
				{
					var data = request.GetData<byte>();
					if (data.IsCreated && buffer.IsCreated)
					{
						data.CopyTo(buffer);
					}
				}
			}

			public void ScheduleCopy(CommandBuffer cmd, ComputeBuffer buffer)
			{
				cmd.RequestAsyncReadback(buffer, Callback);
			}

			public void Sync()
			{
				AsyncGPUReadback.WaitAllRequests();
			}

			public NativeArray<T> GetData<T>(bool forceSync = false) where T : struct
			{
				if (forceSync)
				{
					Sync();
				}

				return buffer.Reinterpret<T>(sizeof(byte));
			}
		}

		//--------------
		// gpu commands

		public struct ScopedCommandBuffer : IDisposable
		{
			public CommandBuffer cmd;
			public void Dispose() => CommandBufferPool.Release(cmd);
			public static ScopedCommandBuffer Get() => new ScopedCommandBuffer { cmd = CommandBufferPool.Get() };
			public static implicit operator CommandBuffer(ScopedCommandBuffer scmd) => scmd.cmd;
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
