using UnityEngine;

namespace Unity.DemoTeam.Hair
{
	public class LineHeaderAttribute : PropertyAttribute
	{
		public GUIContent label;
		public LineHeaderAttribute(string label = null)
		{
			if (label != null)
				this.label = new GUIContent(label);
			else
				this.label = null;
		}
	}
}
