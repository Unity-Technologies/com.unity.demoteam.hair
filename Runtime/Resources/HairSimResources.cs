using UnityEngine;

namespace Unity.DemoTeam.Hair
{
	public class HairSimResources : ScriptableObject
	{
		static HairSimResources s_resources;

		public static HairSimResources Load()
		{
			if (s_resources == null)
				s_resources = Resources.Load<HairSimResources>("HairSimResources");

			return s_resources;
		}

		[LineHeader("Kernels")]

		public Shader computeRoots;
		public ComputeShader computeSolver;

		public ComputeShader computeVolume;
		public Shader computeVolumeRaster;

		[LineHeader("Debugging")]

		public Shader debugDraw;
		public Mesh debugDrawCube;

		[LineHeader("Materials")]

		public Shader defaultBuiltin;
		public Shader defaultCustom;
		public Shader defaultHDRP;
		public Shader defaultURP;
		public Shader replaceAsync;
		public Shader replaceError;
	}
}
