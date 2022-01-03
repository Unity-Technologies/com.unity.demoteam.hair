using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.DemoTeam.Hair
{
	public class RenderingLayerMaskAttribute : PropertyAttribute { }

#if UNITY_EDITOR
	[CustomPropertyDrawer(typeof(RenderingLayerMaskAttribute))]
	public class RenderingLayerMaskAttributeDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var currentPipelineAsset = GraphicsSettings.currentRenderPipeline;
			if (currentPipelineAsset == null)
				return;

			var layerMaskNames = currentPipelineAsset.renderingLayerMaskNames;
			if (layerMaskNames == null)
				return;

			label = EditorGUI.BeginProperty(position, label, property);
			{
				EditorGUI.BeginChangeCheck();

				var mask = property.intValue;
				{
					mask = EditorGUI.MaskField(position, label, mask, layerMaskNames);
				}

				if (EditorGUI.EndChangeCheck())
				{
					property.intValue = mask;
				}
			}
			EditorGUI.EndProperty();
		}
	}
#endif
}
