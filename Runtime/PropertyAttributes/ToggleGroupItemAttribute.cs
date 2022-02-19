using UnityEngine;

namespace Unity.DemoTeam.Hair
{
	public class ToggleGroupItemAttribute : PropertyAttribute
	{
		public bool withLabel;
		public string withSuffix;
		public bool allowSceneObjects;

		public ToggleGroupItemAttribute(bool withLabel = false, string withSuffix = null, bool allowSceneObjects = true)
		{
			this.withLabel = withLabel;
			this.withSuffix = withSuffix;
			this.allowSceneObjects = allowSceneObjects;
		}
	}
}
