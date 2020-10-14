using UnityEngine;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;

namespace Unity.DemoTeam.Hair
{
	[CustomPassDrawer(typeof(HairSimDebugPass))]
	class HairSimDebugPassDrawer : CustomPassDrawer
	{
		protected override PassUIFlag commonPassUIFlags => PassUIFlag.All;

		protected override float GetPassHeight(SerializedProperty customPass)
		{
			return base.GetPassHeight(customPass);
		}

		protected override void DoPassGUI(SerializedProperty customPass, Rect rect)
		{
			base.DoPassGUI(customPass, rect);
		}

		protected override void Initialize(SerializedProperty customPass)
		{
			base.Initialize(customPass);
		}
	}
}
