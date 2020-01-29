using UnityEngine;
using UnityEditor;

namespace Unity.DemoTeam.Hair
{
	[CustomEditor(typeof(HairSim)), CanEditMultipleObjects]
	public class HairGenEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			//if (GUILayout.Button("Generate Strands"))
			//{
			//	(target as HairSim).GenerateStrands();
			//}
		}
	}
}
