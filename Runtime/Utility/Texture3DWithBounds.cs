using UnityEngine;

namespace Unity.DemoTeam.Hair
{
	[CreateAssetMenu]
	public class Texture3DWithBounds : ScriptableObject
	{
		public Texture3D texture;
		public Bounds bounds;
	}
}
