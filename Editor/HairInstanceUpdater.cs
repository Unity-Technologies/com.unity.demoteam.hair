using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Unity.DemoTeam.Hair
{
	public class HairInstanceUpdater
	{
		static bool s_initialized = false;
		static bool s_initializedSceneViews = false;

		static HashSet<SceneView> s_sceneViews = new HashSet<SceneView>();

		[InitializeOnLoadMethod]
		static void StaticInitialize()
		{
			if (s_initialized == false)
			{
				EditorApplication.update += ConditionalPlayerLoop;
				SceneView.duringSceneGui += AddSceneView;

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

		static void AddSceneView(SceneView sceneView)
		{
			if (s_sceneViews.Contains(sceneView) == false)
				s_sceneViews.Add(sceneView);

			s_initializedSceneViews = true;
		}

		static bool AnySceneViewAlwaysRefresh()
		{
			s_sceneViews.RemoveWhere(sceneView => (sceneView == null));

			foreach (var sceneView in s_sceneViews)
			{
				if (sceneView.sceneViewState.alwaysRefreshEnabled)
				{
					return true;
				}
			}

			return !s_initializedSceneViews;
		}
	}
}
