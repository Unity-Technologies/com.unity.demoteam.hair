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
		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => -EditorGUIUtility.standardVerticalSpacing;
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var currentPipelineAsset = GraphicsSettings.currentRenderPipeline;
			if (currentPipelineAsset == null)
				return;

			var layerMaskNames = currentPipelineAsset.renderingLayerMaskNames;
			if (layerMaskNames == null)
				return;

			property.intValue = EditorGUILayout.MaskField(label, property.intValue, layerMaskNames);
		}
	}
#endif
}
