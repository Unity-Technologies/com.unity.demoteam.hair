using UnityEngine;

namespace Unity.DemoTeam.Hair
{
	public class HairSimResources : ScriptableObject
	{
		static HairSimResources s_asset;

		public static HairSimResources Load()
		{
			if (s_asset == null)
				s_asset = Resources.Load<HairSimResources>("HairSimResources");

			return s_asset;
		}

		public Shader computeRoots;
		public ComputeShader computeSolver;
		public ComputeShader computeVolume;
		public Shader computeVolumeRaster;
		public Shader debugDraw;
	}
}
