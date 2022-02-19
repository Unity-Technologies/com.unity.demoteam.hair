using UnityEngine;

namespace Unity.DemoTeam.Hair
{
	public class LinearRampAttribute : PropertyAttribute
	{
		public Rect ranges;
		public LinearRampAttribute(float x0, float y0, float x1, float y1)
		{
			this.ranges = new Rect(x0, y0, x1 - x0, y1 - y0);
		}
	}
}
