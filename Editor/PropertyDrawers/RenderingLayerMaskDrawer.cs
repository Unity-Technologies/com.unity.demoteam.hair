using UnityEngine;
using UnityEditor;

namespace Unity.DemoTeam.Hair
{
	public static partial class PropertyDrawers
	{
		[CustomPropertyDrawer(typeof(RenderingLayerMaskAttribute))]
		public class RenderingLayerMaskDrawer : PropertyDrawer
		{
			public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) => HairGUI.RenderingLayerMask(position, label, property);
		}
	}
}
