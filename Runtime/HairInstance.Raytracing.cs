using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.DemoTeam.Hair
{
    public static partial class HairInstanceBuilder
    {
        internal static void BuildRayTracingObjects(ref HairInstance.GroupInstance strandGroupInstance, int index, HideFlags hideFlags = HideFlags.NotEditable)
        {
            strandGroupInstance.sceneObjects.rayTracingObjects.rayTracingMeshContainer = CreateContainer("RaytracedStrands:" + index, strandGroupInstance.sceneObjects.groupContainer,        hideFlags);
            strandGroupInstance.sceneObjects.rayTracingObjects.rayTracingMeshFilter    = CreateComponent<MeshFilter>(strandGroupInstance.sceneObjects.rayTracingObjects.rayTracingMeshContainer,   hideFlags);
            strandGroupInstance.sceneObjects.rayTracingObjects.rayTracingMeshRenderer  = CreateComponent<MeshRenderer>(strandGroupInstance.sceneObjects.rayTracingObjects.rayTracingMeshContainer, hideFlags);
        }

        internal static void DestroyRayTracingObjects(ref HairInstance.GroupInstance strandGroupInstance)
        {
            CoreUtils.Destroy(strandGroupInstance.sceneObjects.rayTracingObjects.rayTracingMeshContainer);
            CoreUtils.Destroy(strandGroupInstance.sceneObjects.rayTracingObjects.rayTracingMesh);
        }
    }

    public partial class HairInstance
    {
        [Serializable]
        public struct RaytracingObjects
        {
            public GameObject   rayTracingMeshContainer;
            public MeshFilter   rayTracingMeshFilter;
            public MeshRenderer rayTracingMeshRenderer;
            
            [NonSerialized]
            public Mesh rayTracingMesh;
        }
        
        void InitializeRayTracingData(ref GroupInstance strandGroupInstance)
        {
        }
        
        void UpdateRayTracingState(ref GroupInstance strandGroupInstance)
        {
        }

        void ReleaseRayTracingData(ref GroupInstance strandGroupInstance)
        {
            CoreUtils.Destroy(strandGroupInstance.sceneObjects.rayTracingObjects.rayTracingMesh);
        }
    }
}