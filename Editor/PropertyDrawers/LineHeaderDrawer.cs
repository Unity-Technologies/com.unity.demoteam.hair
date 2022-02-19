using UnityEngine;
using UnityEditor;

namespace Unity.DemoTeam.Hair
{
	public static partial class PropertyDrawers
	{
		[CustomPropertyDrawer(typeof(LineHeaderAttribute))]
		public class LineHeaderDrawer : DecoratorDrawer
		{
			public override float GetHeight() => HairGUI.lineHeaderHeight;
			public override void OnGUI(Rect position)
			{
				var header = attribute as LineHeaderAttribute;
				{
					HairGUI.LineHeader(position, header.label);
				}
			}
		}
	}
}
