using System;
using System.Collections.Generic;
using System.Linq;
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
        
        public static Mesh CreateMeshRaytracedTubesIfNull(ref Mesh meshTubes, HideFlags hideFlags, HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount, in Bounds bounds)
        {
            if (meshTubes == null)
                meshTubes = CreateMeshRaytracedTubes(hideFlags, memoryLayout, strandCount, strandParticleCount, bounds);

            return meshTubes;
        }

        static bool HasRayTracingObjects(ref HairInstance.GroupInstance strandGroupInstance)
        {
            return strandGroupInstance.sceneObjects.rayTracingObjects.container != null ||
                   strandGroupInstance.sceneObjects.rayTracingObjects.filter    != null ||
                   strandGroupInstance.sceneObjects.rayTracingObjects.renderer  != null;
        }
        
        static void BuildRayTracingObjects(ref HairInstance.GroupInstance strandGroupInstance, int index, HideFlags hideFlags = HideFlags.NotEditable)
        {
            strandGroupInstance.sceneObjects.rayTracingObjects.container = CreateContainer("RaytracedStrands:" + index, strandGroupInstance.sceneObjects.groupContainer,        hideFlags);
            strandGroupInstance.sceneObjects.rayTracingObjects.filter    = CreateComponent<MeshFilter>(strandGroupInstance.sceneObjects.rayTracingObjects.container,   hideFlags);
            strandGroupInstance.sceneObjects.rayTracingObjects.renderer  = CreateComponent<MeshRenderer>(strandGroupInstance.sceneObjects.rayTracingObjects.container, hideFlags);
        }

        public static void BuildRayTracingObjectsIfNeeded(ref HairInstance.GroupInstance strandGroupInstance, HideFlags hideFlags = HideFlags.NotEditable)
        {
            if (HasRayTracingObjects(ref strandGroupInstance))
                return; 

            BuildRayTracingObjects(ref strandGroupInstance, index: 0, hideFlags);
        }

        public static void DestroyRayTracingObjects(ref HairInstance.GroupInstance strandGroupInstance)
        {
            CoreUtils.Destroy(strandGroupInstance.sceneObjects.rayTracingObjects.container);
            CoreUtils.Destroy(strandGroupInstance.sceneObjects.rayTracingObjects.material);
            CoreUtils.Destroy(strandGroupInstance.sceneObjects.rayTracingObjects.mesh);
        }
    }

    public partial class HairInstance
    {
        static bool s_loadedRaytracingResources = false;

        static ComputeShader s_updateMeshPositionsCS;

        [Serializable]
        public struct RaytracingObjects
        {
            public GameObject   container;
            public MeshFilter   filter;
            public MeshRenderer renderer;

            [NonSerialized] public Material       material;
            [NonSerialized] public Mesh           mesh;
            [NonSerialized] public GraphicsBuffer buffer;
            [NonSerialized] public GraphicsBuffer bufferUV;
            [NonSerialized] public uint           meshInstanceSubdivision;
        }
        
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#else
		[RuntimeInitializeOnLoadMethod]
#endif
        static void LoadRaytracingResources()
        {
            if (s_loadedRaytracingResources)
                return;
            
            var resources = HairRaytracingResources.Load();
            {
                s_updateMeshPositionsCS = resources.computeUpdateMeshVertices;
            }

            s_loadedRaytracingResources = true;
        }
        
        void UpdateRayTracingState(ref GroupInstance strandGroupInstance, HairSim.SolverData solverData, ref Material materialInstance, CommandBuffer cmd)
        {
            if (!settingsSystem.raytracing)
            {
                HairInstanceBuilder.DestroyRayTracingObjects(ref strandGroupInstance);
                return;
            }
            
            HairInstanceBuilder.BuildRayTracingObjectsIfNeeded(ref strandGroupInstance);
            
            ref var rayTracingObjects         = ref strandGroupInstance.sceneObjects.rayTracingObjects;
            ref var rayTracingMesh            = ref rayTracingObjects.mesh;
            ref var rayTracingSubdivision = ref rayTracingObjects.meshInstanceSubdivision;
            
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
                
                // override whatever is being rasterized to use the tube geo representation. 
                {
                    CoreUtils.SetKeyword(rayTracingMaterial, "HAIR_VERTEX_ID_LINES",  false);
                    CoreUtils.SetKeyword(rayTracingMaterial, "HAIR_VERTEX_ID_STRIPS", false);
                    CoreUtils.SetKeyword(rayTracingMaterial, "HAIR_VERTEX_ID_TUBES",  true);
                }

                // this will force disable the renderer for all rendering passes but still cause it to be present 
                // in the acceleration structure. it's a hack.
                rayTracingMaterial.renderQueue = Int32.MaxValue; 
            }

            // update mesh
            var mesh = null as Mesh;
            {
                var subdivision = solverData.cbuffer._StagingSubdivision;
                
                if (subdivision != rayTracingSubdivision)
                {
                    CoreUtils.Destroy(rayTracingMesh);
                    rayTracingSubdivision = subdivision;
                }
                
                if (subdivision > 0)
                    mesh = HairInstanceBuilder.CreateMeshRaytracedTubesIfNull(ref rayTracingObjects.mesh, HideFlags.HideAndDontSave, solverData.memoryLayout, (int)solverData.cbuffer._StrandCount, (int)solverData.cbuffer._StagingVertexCount, new Bounds());
                else
                    mesh = strandGroupInstance.groupAssetReference.Resolve().meshAssetRaytracedTubes;
                
                CoreUtils.SafeRelease(rayTracingObjects.buffer);
                CoreUtils.SafeRelease(rayTracingObjects.bufferUV);
                {
                    mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;

                    var streamIndex  = mesh.GetVertexAttributeStream(VertexAttribute.Position);
                    var streamIndexUV = mesh.GetVertexAttributeStream(VertexAttribute.TexCoord1);

                    if (streamIndex != -1 && streamIndexUV != -1)
                    {
                        rayTracingObjects.buffer   = mesh.GetVertexBuffer(streamIndex);
                        rayTracingObjects.bufferUV = mesh.GetVertexBuffer(streamIndexUV);
                    }
                    else
                    {
                        Debug.LogError("Invalid hair ray tracing mesh.");
                        return;
                    }
                }

                // update mesh vertex positions in the acceleration structure
                if (mesh.vertexCount > 0)
                {
                    const int kernelIndex = 0;
                    
                    // not the greatest way to do this, but not really possible to do any better unless it is done on the native side.
                    foreach (var keywordName in s_updateMeshPositionsCS.enabledKeywords)
                    {
                        cmd.SetKeyword(s_updateMeshPositionsCS, keywordName, false);
                    }
                    
                    // not the greatest way to do this, but not really possible to do any better unless it is done on the native side.
                    foreach (var keywordName in rayTracingMaterial.shaderKeywords)
                    {
                        var keyword = s_updateMeshPositionsCS.keywordSpace.FindKeyword(keywordName);

                        if (keyword.isValid)
                            cmd.SetKeyword(s_updateMeshPositionsCS, keyword, true);
                    }

                    // override whatever is being rasterized to use the tube geo representation. 
                    {
                        var vertexIDKeywords = new LocalKeyword[]
                        {
                            new(s_updateMeshPositionsCS, "HAIR_VERTEX_ID_LINES"),
                            new(s_updateMeshPositionsCS, "HAIR_VERTEX_ID_STRIPS"),
                            new(s_updateMeshPositionsCS, "HAIR_VERTEX_ID_TUBES"),
                        };

                        cmd.SetKeyword(s_updateMeshPositionsCS, vertexIDKeywords[0], false);
                        cmd.SetKeyword(s_updateMeshPositionsCS, vertexIDKeywords[1], false);
                        cmd.SetKeyword(s_updateMeshPositionsCS, vertexIDKeywords[2], true);
                    }
                    
                    // Also need to bind these matrices manually. (TODO: Is it cross-SRP safe?)
                    cmd.SetComputeMatrixParam(s_updateMeshPositionsCS, "unity_ObjectToWorld", rayTracingObjects.container.transform.localToWorldMatrix);
                    cmd.SetComputeMatrixParam(s_updateMeshPositionsCS, "unity_WorldToObject", rayTracingObjects.container.transform.worldToLocalMatrix);
                    
                    cmd.SetComputeParamsFromMaterial(s_updateMeshPositionsCS, kernelIndex, rayTracingMaterial);
                    cmd.SetComputeBufferParam(s_updateMeshPositionsCS, kernelIndex, "_VertexBuffer",   rayTracingObjects.buffer);
                    cmd.SetComputeBufferParam(s_updateMeshPositionsCS, kernelIndex, "_VertexBufferUV", rayTracingObjects.bufferUV);
                    cmd.DispatchCompute(s_updateMeshPositionsCS, kernelIndex, (mesh.vertexCount + 1024 - 1) / 1024, 1, 1);
                }
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
                
                // this flag is required for the acceleration structure to catch updates made by the compute kernel.
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