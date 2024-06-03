using UnityEngine;
using UnityEditor;

namespace Unity.DemoTeam.Hair
{
	public static class HairInstanceEditorRefresh
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

			var alwaysRefresh = AnySceneViewAlwaysRefresh();
			if (alwaysRefresh == false)
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

		static bool AnySceneViewAlwaysRefresh()
		{
			var sceneViews = SceneView.sceneViews;
			var sceneViewsActive = 0;

			for (int i = 0; i != sceneViews.Count; i++)
			{
				var sceneView = sceneViews[i] as SceneView;
				if (sceneView != null && sceneView.sceneViewState.alwaysRefreshEnabled)
				{
					sceneViewsActive++;
				}
			}

			return (sceneViewsActive > 0);
		}
	}
}
