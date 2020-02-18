using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System.Reflection;

namespace Unity.DemoTeam.Hair
{
	public class HairSimDebugPass : CustomPass
	{
		private int lastSimulationFrame = -1;

		private RTHandle motionVectorsRT;
		private void FindMotionVectorsRT()
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
						motionVectorsRT = fieldInfo_m_MotionVectorsRT.GetValue(m_SharedRTManager) as RTHandle;
					}
				}
			}
		}

		protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
		{
			base.Setup(renderContext, cmd);
			base.name = "HairSimDebugPass";

			FindMotionVectorsRT();
		}

		protected override void Execute(ScriptableRenderContext renderContext, CommandBuffer cmd, HDCamera hdCamera, CullingResults cullingResults)
		{
			int frame = Time.renderedFrameCount;
			if (frame != lastSimulationFrame)
			{
				const float dtMin = 1.0f / 120.0f;
				const float dtMax = 1.0f / 30.0f;

				float dt = Mathf.Clamp(Time.deltaTime, dtMin, dtMax);
				dt = 1.0f / 60.0f;

				foreach (HairSim hairSim in HairSim.instances)
				{
					if (hairSim != null && hairSim.isActiveAndEnabled)
					{
						using (new ProfilingSample(cmd, "HairSim.Step (GPU)"))
							hairSim.Step(cmd, dt);
						using (new ProfilingSample(cmd, "HairSim.Volume (GPU)"))
							hairSim.Volume(cmd, dt);
					}
				}

				lastSimulationFrame = frame;
			}

			Profiler.BeginSample("HairSimDebugPass");
			foreach (HairSim hairSim in HairSim.instances)
			{
				if (hairSim != null && hairSim.isActiveAndEnabled)
				{
					using (new ProfilingSample(cmd, "HairSim.Draw (GPU)"))
					{
						RTHandle cameraColor;
						RTHandle cameraDepth;
						GetCameraBuffers(out cameraColor, out cameraDepth);
						hairSim.Draw(cmd, cameraColor, cameraDepth, motionVectorsRT);
					}
				}
			}
			Profiler.EndSample();
		}

		protected override void Cleanup()
		{
			base.Cleanup();
		}
	}
}