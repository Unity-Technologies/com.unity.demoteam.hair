using System;
using Accord;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.DemoTeam.Hair
{
    public partial class HairInstance
    {
	    [NonSerialized] public Bounds simulationBounds;

	    static ComputeShader s_computeBoundsCS;
	    static ComputeBuffer s_boundsBuffer;
	    
	    static bool s_loadedBoundsComputeResources = false;
	    
#if UNITY_EDITOR
	    [UnityEditor.InitializeOnLoadMethod]
#else
		[RuntimeInitializeOnLoadMethod]
#endif
	    static void LoadBoundsComputeResources()
	    {
		    if (s_loadedBoundsComputeResources)
			    return;
            
		    var resources = HairSystemResources.Load();
		    {
			    s_computeBoundsCS = resources.computeBounds;
		    }

		    s_loadedBoundsComputeResources = true;
	    }

	    private static Vector3 s_MinBoundingCorner;
	    private static Vector3 s_MaxBoundingCorner;
	    
		void UpdateSimulationBounds(CommandBuffer cmd, bool worldSquare = true, Matrix4x4? worldToLocalTransform = null)
		{
			Debug.Assert(worldSquare == false || worldToLocalTransform == null);

			var boundsScale = settingsSystem.boundsScale ? settingsSystem.boundsScaleValue : 1.25f;
			
			ref var bounds = ref simulationBounds;
			{
				bounds.center = Vector3.zero;
				bounds.extents = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
			}

			switch (settingsSystem.boundsMode)
			{
				case SettingsSystem.BoundsMode.Automatic:
				{
					for (int i = 0; i != strandGroupInstances.Length; i++)
					{
						var rootMargin = GetStrandScale(strandGroupInstances[i]) * strandGroupInstances[i].groupAssetReference.Resolve().maxStrandLength;
						var rootBounds = GetRootBounds(strandGroupInstances[i], worldToLocalTransform);
						{
							rootBounds.Expand(2.0f * rootMargin * boundsScale);
						}

						bounds.Encapsulate(rootBounds);
					}
				}
					break;

				case SettingsSystem.BoundsMode.AutomaticGPU:
				{
					if (s_boundsBuffer == null)
						s_boundsBuffer = new ComputeBuffer(6, sizeof(uint), ComputeBufferType.Raw);

					// Clear the bounds buffer
					cmd.SetComputeBufferParam(s_computeBoundsCS, 0, "_BoundsBuffer", s_boundsBuffer);
					cmd.DispatchCompute(s_computeBoundsCS, 0, 1, 1, 1);
					
					// Compute the min/max position across all strand group instances. 
					for (int i = 0; i != strandGroupInstances.Length; i++)
					{
						// Bind the particle position buffer
						HairSim.BindSolverData(cmd, s_computeBoundsCS, 1, solverData[i]);

						cmd.SetComputeBufferParam(s_computeBoundsCS, 1, "_BoundsBuffer", s_boundsBuffer);
						
						int particleCount;
						{
							var strandGroup = strandGroupInstances[i].groupAssetReference.Resolve();
							particleCount = strandGroup.strandCount * strandGroup.strandParticleCount;
						}
						
						// Push the particle count to avoid min/max with uninitialized memory. 
						cmd.SetComputeIntParam(s_computeBoundsCS, "_ParticleCount", particleCount);

						const int groupSize = 64;
						cmd.DispatchCompute(s_computeBoundsCS, 1, (particleCount + groupSize - 1) / groupSize, 1, 1);
					}
					
					float OrderedUintToFloat(uint u)
					{
						u = (u >> 31) < 1 ? ~u : (u & ~(1 << 31));
						return BitConverter.ToSingle(BitConverter.GetBytes(u), 0);
					}
					
					// Read-back
					cmd.RequestAsyncReadback(s_boundsBuffer, request =>
					{
						if (!request.hasError)
						{
							var data = request.GetData<uint>();
							s_MinBoundingCorner = new Vector3(OrderedUintToFloat(data[0]), OrderedUintToFloat(data[1]), OrderedUintToFloat(data[2]));
							s_MaxBoundingCorner = new Vector3(OrderedUintToFloat(data[3]), OrderedUintToFloat(data[4]), OrderedUintToFloat(data[5]));
						}
					});
					cmd.WaitAllAsyncReadbackRequests();

					bounds.center = 0.5f * (s_MinBoundingCorner + s_MaxBoundingCorner);
					bounds.size   = s_MaxBoundingCorner - s_MinBoundingCorner;

					// goto case SettingsSystem.BoundsMode.Automatic;
				}
					break;

				case SettingsSystem.BoundsMode.Fixed:
				{
					bounds.center = settingsSystem.boundsCenter + this.transform.position;
					bounds.extents = settingsSystem.boundsExtent * boundsScale;
				}
					break;
			}

			if (worldSquare)
			{
				bounds = new Bounds(bounds.center, bounds.size.CMax() * Vector3.one);
			}
		}
    }
}