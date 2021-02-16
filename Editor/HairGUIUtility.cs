using UnityEngine;
using UnityEditor;

namespace Unity.DemoTeam.Hair
{
	public static class HairGUIUtility
	{
		public struct ColorScope : System.IDisposable
		{
			public Color restoreColor;
			public Type restoreColorType;

			public enum Type
			{
				Color,
				ContentColor,
				BackgroundColor,
			}

			static Color GetColor(Type colorType)
			{
				switch (colorType)
				{
					default:
					case Type.Color: return GUI.color;
					case Type.ContentColor: return GUI.contentColor;
					case Type.BackgroundColor: return GUI.backgroundColor;
				}
			}

			static void SetColor(Type colorType, in Color color)
			{
				switch (colorType)
				{
					default:
					case Type.Color: GUI.color = color; break;
					case Type.ContentColor: GUI.contentColor = color; break;
					case Type.BackgroundColor: GUI.backgroundColor = color; break;
				}
			}

			public ColorScope(Color color, Type colorType = Type.Color)
			{
				this.restoreColor = GetColor(colorType);
				this.restoreColorType = colorType;

				SetColor(colorType, color);
			}

			public void Dispose()
			{
				SetColor(restoreColorType, restoreColor);
			}
		}

		public struct GovernedByPrefabScope : System.IDisposable
		{
			public bool isPrefabInstance;
			public EditorGUI.DisabledScope disableScope;

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

			public GovernedByPrefabScope(Object componentOrGameObject) : this(PrefabUtility.IsPartOfPrefabInstance(componentOrGameObject))
			{
				// foo
			}

			public void Dispose()
			{
				disableScope.Dispose();

				if (isPrefabInstance)
				{
					EditorGUILayout.EndVertical();
					using (new ColorScope(Color.Lerp(Color.red, Color.white, 0.75f), ColorScope.Type.BackgroundColor))
					{
						GUILayout.Label("prefab", HairGUIStyles.statusBoxVertical, GUILayout.Width(20.0f), GUILayout.ExpandHeight(true));
					}
					EditorGUILayout.EndHorizontal();
				}
			}
		}
	}
}
