using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.DemoTeam.Hair
{
	public class HairSimDebugPass : CustomPass
	{
		protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
		{
			base.Setup(renderContext, cmd);
			base.name = "HairSimDebugPass";
		}

		protected override void Execute(ScriptableRenderContext renderContext, CommandBuffer cmd, HDCamera hdCamera, CullingResults cullingResults)
		{
			const float dtMin = 1.0f / 120.0f;
			const float dtMax = 1.0f / 60.0f;

			Profiler.BeginSample("HairSimDebugPass");
			foreach (HairSim hairSim in HairSim.instances)
			{
				if (hairSim != null && hairSim.isActiveAndEnabled)
				{
					using (new ProfilingSample(cmd, "HairSim.Step (GPU)"))
					{
						hairSim.Step(cmd, dtMax);// Mathf.Clamp(Time.deltaTime, dtMin, dtMax));
					}
					using (new ProfilingSample(cmd, "HairSim.Draw (GPU)"))
					{
						hairSim.Draw(cmd);
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