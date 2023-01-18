using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

namespace Unity.DemoTeam.Hair
{
	public static partial class HairGUILayout
	{
		public static void RenderingLayerMask(Rect position, GUIContent label, SerializedProperty property, params GUILayoutOption[] options)
		{
			RenderingLayerMask(EditorGUILayout.GetControlRect(options), label, property);
		}

		public static int RenderingLayerMask(Rect position, GUIContent label, int mask, params GUILayoutOption[] options)
		{
			return RenderingLayerMask(EditorGUILayout.GetControlRect(options), label, mask);
		}
	}

	public static partial class HairGUI
	{
		static string[] s_renderingLayerMaskNames = new string[32];
		static string[] s_renderingLayerMaskNamesDefault = new string[]
		{
			"Default",
			"Unused Layer 1",
			"Unused Layer 2",
			"Unused Layer 3",
			"Unused Layer 4",
			"Unused Layer 5",
			"Unused Layer 6",
			"Unused Layer 7",
			"Unused Layer 8",
			"Unused Layer 9",
			"Unused Layer 10",
			"Unused Layer 11",
			"Unused Layer 12",
			"Unused Layer 13",
			"Unused Layer 14",
			"Unused Layer 15",
			"Unused Layer 16",
			"Unused Layer 17",
			"Unused Layer 18",
			"Unused Layer 19",
			"Unused Layer 20",
			"Unused Layer 21",
			"Unused Layer 22",
			"Unused Layer 23",
			"Unused Layer 24",
			"Unused Layer 25",
			"Unused Layer 26",
			"Unused Layer 27",
			"Unused Layer 28",
			"Unused Layer 29",
			"Unused Layer 30",
			"Unused Layer 31",
			"Unused Layer 32",
		};

		public static void RenderingLayerMask(Rect position, GUIContent label, SerializedProperty property)
		{
			label = EditorGUI.BeginProperty(position, label, property);
			{
				EditorGUI.BeginChangeCheck();

				var mask = property.intValue;
				{
					mask = RenderingLayerMask(position, label, mask);
				}

				if (EditorGUI.EndChangeCheck())
				{
					property.intValue = mask;
				}
			}
			EditorGUI.EndProperty();
		}

		public static int RenderingLayerMask(Rect position, GUIContent label, int mask)
		{
			var currentPipelineAsset = GraphicsSettings.currentRenderPipeline;
			if (currentPipelineAsset != null)
			{
				var renderingLayerMaskNames = currentPipelineAsset.renderingLayerMaskNames;
				if (renderingLayerMaskNames != null)
				{
					int n = s_renderingLayerMaskNames.Length;
					int j = Mathf.Min(n, renderingLayerMaskNames.Length);

					for (int i = 0; i != j; i++)
					{
						s_renderingLayerMaskNames[i] = renderingLayerMaskNames[i];
					}

					for (int i = j; i != n; i++)
					{
						s_renderingLayerMaskNames[i] = s_renderingLayerMaskNamesDefault[i];
					}

					return EditorGUI.MaskField(position, label, mask, s_renderingLayerMaskNames);
				}
			}

			using (new EditorGUI.DisabledScope(true))
			{
				return EditorGUI.IntField(position, label, mask);
			}
		}
	}
}
