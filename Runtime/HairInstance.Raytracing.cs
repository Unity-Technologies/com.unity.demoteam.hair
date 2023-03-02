using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using RayTracingMode = UnityEngine.Experimental.Rendering.RayTracingMode;

namespace Unity.DemoTeam.Hair
{
    public static partial class HairInstanceBuilder
    {
        public static Mesh CreateMeshRaytracedTubes(HideFlags hideFlags, HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount, in Bounds bounds)
        {
            var meshTubes = new Mesh();
            {
                meshTubes.hideFlags = hideFlags;
                meshTubes.name = "X-RaytracedTubes";
                BuildMeshTubes(meshTubes, memoryLayout, strandCount, strandParticleCount, bounds, buildForRaytracing: true);
            }
            return meshTubes;
        }
        
        static void BuildRayTracingObjects(ref HairInstance.GroupInstance strandGroupInstance, int index, HideFlags hideFlags = HideFlags.NotEditable)
        {
            strandGroupInstance.sceneObjects.rayTracingObjects.container = CreateContainer("RaytracedStrands:" + index, strandGroupInstance.sceneObjects.groupContainer,        hideFlags);
            strandGroupInstance.sceneObjects.rayTracingObjects.filter    = CreateComponent<MeshFilter>(strandGroupInstance.sceneObjects.rayTracingObjects.container,   hideFlags);
            strandGroupInstance.sceneObjects.rayTracingObjects.renderer  = CreateComponent<MeshRenderer>(strandGroupInstance.sceneObjects.rayTracingObjects.container, hideFlags);
        }

        static void DestroyRayTracingObjects(ref HairInstance.GroupInstance strandGroupInstance)
        {
            CoreUtils.Destroy(strandGroupInstance.sceneObjects.rayTracingObjects.container);
            CoreUtils.Destroy(strandGroupInstance.sceneObjects.rayTracingObjects.material);
            CoreUtils.Destroy(strandGroupInstance.sceneObjects.rayTracingObjects.mesh);
        }
    }

    public partial class HairInstance
    {
        [Serializable]
        public struct RaytracingObjects
        {
            public GameObject   container;
            public MeshFilter   filter;
            public MeshRenderer renderer;

            [NonSerialized] 
            public Material material;
            
            [NonSerialized]
            public Mesh mesh;
        }
        
        void UpdateRayTracingState(ref GroupInstance strandGroupInstance, ref Material materialInstance)
        {
            ref var rayTracingObjects = ref strandGroupInstance.sceneObjects.rayTracingObjects;
            
            // update material instance
            ref var rayTracingMaterial = ref rayTracingObjects.material;
            {
                if (rayTracingMaterial == null)
                {
                    rayTracingMaterial = new Material(materialInstance);
                    rayTracingMaterial.name += "(Raytracing)";
                    rayTracingMaterial.hideFlags = HideFlags.HideAndDontSave;
                }

                rayTracingMaterial.shader = materialInstance.shader;
                rayTracingMaterial.CopyPropertiesFromMaterial(materialInstance);
                
                // this will force disable the renderer for all rendering passes but still cause it to be present 
                // in the acceleration structure. it's a bit of a hack.
                rayTracingMaterial.renderQueue = Int32.MaxValue; 
            }

            // select mesh
            var mesh = null as Mesh;
            {
                mesh = strandGroupInstance.groupAssetReference.Resolve().meshAssetRaytracedTubes;
                
            }

            // update mesh 
            ref var meshFilter = ref rayTracingObjects.filter;
            {
                if (meshFilter.sharedMesh != mesh)
                    meshFilter.sharedMesh = mesh;
            }

            // update mesh renderer
            ref var meshRenderer = ref rayTracingObjects.renderer;
            {
                meshRenderer.enabled = (settingsSystem.strandRenderer != SettingsSystem.StrandRenderer.Disabled);
                meshRenderer.sharedMaterial = rayTracingMaterial;
                meshRenderer.shadowCastingMode = settingsSystem.strandShadows;
                meshRenderer.renderingLayerMask = (uint)settingsSystem.strandLayers;
                meshRenderer.motionVectorGenerationMode = settingsSystem.motionVectors;
                
                // this flag is required for the acceleration structure to catch updates from the compute kernel.
                meshRenderer.rayTracingMode = RayTracingMode.DynamicGeometry;
            }
        }

        void ReleaseRayTracingData(ref GroupInstance strandGroupInstance)
        {
            CoreUtils.Destroy(strandGroupInstance.sceneObjects.rayTracingObjects.material);
            CoreUtils.Destroy(strandGroupInstance.sceneObjects.rayTracingObjects.mesh);
        }
    }
}