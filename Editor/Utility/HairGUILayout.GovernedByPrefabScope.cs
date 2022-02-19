using System;
using UnityEngine;
using UnityEditor;

namespace Unity.DemoTeam.Hair
{
	public static partial class HairGUILayout
	{
		public struct GovernedByPrefabScope : IDisposable
		{
			public bool isPrefabInstance;
			public EditorGUI.DisabledScope disableScope;

			public GovernedByPrefabScope(UnityEngine.Object componentOrGameObject) : this(PrefabUtility.IsPartOfPrefabInstance(componentOrGameObject)) { }
			public GovernedByPrefabScope(bool isPrefabInstance)
			{
				this.isPrefabInstance = isPrefabInstance;
				if (isPrefabInstance)
				{
					EditorGUILayout.BeginHorizontal();
					EditorGUILayout.BeginVertical();
				}

				disableScope = new EditorGUI.DisabledScope(isPrefabInstance);
			}

			public void Dispose()
			{
				disableScope.Dispose();

				if (isPrefabInstance)
				{
					EditorGUILayout.EndVertical();
					using (new HairGUI.ColorScope(Color.Lerp(Color.red, Color.white, 0.75f), HairGUI.ColorType.BackgroundColor))
					{
						GUILayout.Label("prefab", HairGUIStyles.statusBoxVertical, GUILayout.Width(20.0f), GUILayout.ExpandHeight(true));
					}
					EditorGUILayout.EndHorizontal();
				}
			}
		}
	}
}
