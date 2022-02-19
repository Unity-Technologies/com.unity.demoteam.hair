using UnityEngine;

namespace Unity.DemoTeam.Hair
{
	public enum LinearRampStyle
	{
		LinearDecreasing,
		LinearIncreasing,
		SmoothDecreasing,
		SmoothIncreasing,
	}

	public class LinearRampWidgetAttribute : PropertyAttribute
	{
		public float min;
		public float max;
		public LinearRampStyle style;

		public LinearRampWidgetAttribute(float min, float max, LinearRampStyle style = LinearRampStyle.LinearDecreasing)
		{
			this.min = min;
			this.max = max;
			this.style = style;
		}
	}
}
