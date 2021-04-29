using UnityEngine;
using UnityEditor;

namespace Unity.DemoTeam.Hair
{
	public class HairInstanceUpdater
	{
		static bool s_initialized = false;

		[InitializeOnLoadMethod]
		static void StaticInitialize()
		{
			if (s_initialized == false)
			{
				EditorApplication.update += ConditionalPlayerLoop;

				s_initialized = true;
			}
		}

		static void ConditionalPlayerLoop()
		{
			if (Application.isPlaying)
				return;

			foreach (var hairInstance in HairInstance.s_instances)
			{
				if (hairInstance != null &&
					hairInstance.isActiveAndEnabled &&
					hairInstance.GetSimulationActive())
				{
					EditorApplication.QueuePlayerLoopUpdate();
					break;
				}
			}
		}
	}
}
