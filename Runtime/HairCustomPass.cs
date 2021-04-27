using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.DemoTeam.Hair
{
	public class HairCustomPass : CustomPass
	{
		protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
		{
			base.Setup(renderContext, cmd);
			base.name = "HairDebugDraw";

			base.targetColorBuffer = TargetBuffer.Camera;
			base.targetDepthBuffer = TargetBuffer.Camera;
		}

		protected override void Execute(CustomPassContext context)
		{
			CoreUtils.SetRenderTarget(context.cmd, context.cameraColorBuffer, context.cameraDepthBuffer);

			foreach (var hairInstance in HairInstance.s_instances)
			{
				if (hairInstance != null && hairInstance.isActiveAndEnabled)
					hairInstance.DispatchDraw(context.cmd);
			}
		}

		protected override void Cleanup()
		{
			base.Cleanup();
		}
	}
}
