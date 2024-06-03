#define PREFER_HDRP_CUSTOMPASS

using System;
using UnityEngine;
using UnityEngine.Rendering;

#if HAS_PACKAGE_UNITY_HDRP 
using UnityEngine.Rendering.HighDefinition;
#endif

namespace Unity.DemoTeam.Hair
{
	public static class HairSimDebugDraw
	{
		static bool s_initialized = false;

		static GameObject s_container = null;

#if UNITY_EDITOR
		[UnityEditor.InitializeOnLoadMethod]
#else
		[RuntimeInitializeOnLoadMethod]
#endif
		static void StaticInitialize()
		{
			if (s_initialized == false)
			{
				Camera.onPreRender += HandlePreRender;
				Camera.onPostRender += HandlePostRender;

				RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
				RenderPipelineManager.endCameraRendering += HandleEndCameraRendering;

				s_container = new GameObject(nameof(HairSimDebugDraw));
				s_container.hideFlags = HideFlags.HideAndDontSave;

#if UNITY_EDITOR
				UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += () => CoreUtils.Destroy(s_container);
#endif

#if HAS_PACKAGE_UNITY_HDRP && PREFER_HDRP_CUSTOMPASS
				var customPassVolume = s_container.AddComponent<CustomPassVolume>();
				{
					customPassVolume.AddPassOfType<HairSimDebugDrawCustomPass>();
					customPassVolume.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;
					customPassVolume.isGlobal = true;
				}
#endif

				s_initialized = true;
			}
		}

		struct DebugFrame : IDisposable
		{
			public CommandBuffer cmd;

			public DebugFrame(Camera camera)
			{
				cmd = CommandBufferPool.Get();
				WriteDebugDrawCommands(cmd);
			}

			public void Dispose()
			{
				CommandBufferPool.Release(cmd);
			}

			public static void WriteDebugDrawCommands(CommandBuffer cmd)
			{
				foreach (var hairInstance in HairInstance.s_instances)
				{
					if (hairInstance != null && hairInstance.isActiveAndEnabled)
						hairInstance.DispatchDraw(cmd, CommandBufferExecutionFlags.None);
				}
			}
		}

		static bool SupportedCamera(Camera camera)
		{
			switch (camera.cameraType)
			{
				case CameraType.Game:
				case CameraType.SceneView:
					return true;

				default:
					return false;
			}
		}

		static void HandlePreRender(Camera camera) { }
		static void HandlePostRender(Camera camera)
		{
			if (SupportedCamera(camera))
			{
				using (var debugFrame = new DebugFrame(camera))
				{
					Graphics.ExecuteCommandBuffer(debugFrame.cmd);
				}
			}
		}

		static void HandleBeginCameraRendering(ScriptableRenderContext context, Camera camera) { }
		static void HandleEndCameraRendering(ScriptableRenderContext context, Camera camera)
		{
			if (SupportedCamera(camera))
			{
#if HAS_PACKAGE_UNITY_HDRP && PREFER_HDRP_CUSTOMPASS
				var hdPipeline = RenderPipelineManager.currentPipeline as HDRenderPipeline;
				if (hdPipeline != null)
				{
					return;// prefer HDRP CustomPass, since HDRP doesn't bind depth before calling EndCameraRendering
				}
#endif

				using (var debugFrame = new DebugFrame(camera))
				{
					context.ExecuteCommandBuffer(debugFrame.cmd);
					context.Submit();// submit immediately, since e.g. URP doesn't submit after calling EndCameraRendering
				}
			}
		}

#if HAS_PACKAGE_UNITY_HDRP && PREFER_HDRP_CUSTOMPASS
		public class HairSimDebugDrawCustomPass : CustomPass
		{
			protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
			{
				base.name = "HairSimDebugDrawCustomPass";
			}

			protected override void Execute(CustomPassContext context)
			{
				DebugFrame.WriteDebugDrawCommands(context.cmd);
			}
		}
#endif
	}
}
