using UnityEngine;

namespace Unity.DemoTeam.Hair
{
	public class HairSystemResources : ScriptableObject
	{
		static HairSystemResources s_resources;

		public static HairSystemResources Load()
		{
			if (s_resources == null)
				s_resources = Resources.Load<HairSystemResources>("HairSystemResources");

			return s_resources;
		}

		[LineHeader("Kernels")]

		public Shader computeRoots;
		public ComputeShader computeSolver;
		public ComputeShader computeVolume;
		public Shader computeVolumeRaster;
		public ComputeShader computeUpdateMeshVertices;
		public ComputeShader computeBounds;
		
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
