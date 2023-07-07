using UnityEngine;

namespace Unity.DemoTeam.Hair
{
    public class HairRaytracingResources : ScriptableObject
    {
        static HairRaytracingResources s_resources;

        public static HairRaytracingResources Load()
        {
            if (s_resources == null)
                s_resources = Resources.Load<HairRaytracingResources>("HairRaytracingResources");

            return s_resources;
        }

        [LineHeader("Kernels")]
        public ComputeShader computeUpdateMeshVertices;
    }
}