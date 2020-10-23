using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.DemoTeam.Hair
{
	public class HairCustomPass : CustomPass
	{
		public Dispatch dispatch;

		[Flags]
		public enum Dispatch
		{
			Step = 1 << 0,
			Draw = 1 << 1,
		}

		private int lastSimulationFrame = -1;

		private RTHandle cameraMotionVectorBuffer;
		private void FindCameraMotionVectorBuffer()
		{
			var fieldInfo_m_SharedRTManager = typeof(HDRenderPipeline).GetField("m_SharedRTManager", BindingFlags.NonPublic | BindingFlags.Instance);
			if (fieldInfo_m_SharedRTManager != null)
			{
				//Debug.Log("FindMotionVectorsRT : " + fieldInfo_m_SharedRTManager);
				var m_SharedRTManager = fieldInfo_m_SharedRTManager.GetValue(RenderPipelineManager.currentPipeline as HDRenderPipeline);
				if (m_SharedRTManager != null)
				{
					var fieldInfo_m_MotionVectorsRT = m_SharedRTManager.GetType().GetField("m_MotionVectorsRT", BindingFlags.NonPublic | BindingFlags.Instance);
					if (fieldInfo_m_MotionVectorsRT != null)
					{
						//Debug.Log("FindMotionVectorsRT : " + fieldInfo_m_MotionVectorsRT);
						cameraMotionVectorBuffer = fieldInfo_m_MotionVectorsRT.GetValue(m_SharedRTManager) as RTHandle;
					}
				}
			}
		}

		protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
		{
			base.Setup(renderContext, cmd);
			base.name = "HairPass (" + dispatch + ")";

			if (dispatch.HasFlag(Dispatch.Draw))
			{
				base.targetColorBuffer = TargetBuffer.Camera;
				base.targetDepthBuffer = TargetBuffer.Camera;
			}
			else
			{
				base.targetColorBuffer = TargetBuffer.None;
				base.targetDepthBuffer = TargetBuffer.None;
			}

			FindCameraMotionVectorBuffer();
		}

		protected override void Execute(CustomPassContext context)
		{
			if (dispatch.HasFlag(Dispatch.Step))
			{
				int frame = Time.renderedFrameCount;
				if (frame != lastSimulationFrame)
				{
					var dt = Time.deltaTime;
					if (dt != 0.0f)
					{
						const float dtMin = 1.0f / 120.0f;
						const float dtMax = 1.0f / 30.0f;

						dt = Mathf.Clamp(dt, dtMin, dtMax);
						dt = 1.0f / 60.0f;

						foreach (var hair in Groom.s_instances)
						{
							if (hair != null && hair.isActiveAndEnabled)
								hair.DispatchStep(context.cmd, dt);
						}
					}

					lastSimulationFrame = frame;
				}
			}
			else
			{
				lastSimulationFrame = -1;
			}

			if (dispatch.HasFlag(Dispatch.Draw))
			{
				foreach (var hair in Groom.s_instances)
				{
					if (hair != null && hair.isActiveAndEnabled)
					{
						hair.DispatchDraw(context.cmd, context.cameraColorBuffer, context.cameraDepthBuffer, cameraMotionVectorBuffer);
					}
				}
			}
		}

		protected override void Cleanup()
		{
			base.Cleanup();
		}
	}
}
