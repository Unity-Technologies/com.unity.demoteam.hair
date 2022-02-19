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
				var layerMaskNames = currentPipelineAsset.renderingLayerMaskNames;
				if (layerMaskNames != null)
				{
					return EditorGUI.MaskField(position, label, mask, layerMaskNames);
				}
			}

			using (new EditorGUI.DisabledScope(true))
			{
				return EditorGUI.IntField(position, label, mask);
			}
		}
	}
}
