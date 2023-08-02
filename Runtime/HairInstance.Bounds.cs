using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.DemoTeam.Hair
{
	using static HairSimUtility;
    public partial class HairInstance
    {
	    [NonSerialized] public Bounds simulationBounds;

	    static ComputeShader s_computeBoundsCS;
	    ComputeBuffer boundsBuffer;
	    
	    static bool s_loadedBoundsComputeResources = false;
	    
	    static class BoundsKernels
	    {
		    public static int KClearBuffer;
		    public static int KComputeBoundsStrands;
		    public static int KComputeBoundsRoots;
	    }
	    
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

		    InitializeStaticFields(typeof(BoundsKernels), (string s) => s_computeBoundsCS.FindKernel(s));
		    s_loadedBoundsComputeResources = true;
	    }

	    private Vector3 minBoundingCorner;
	    private Vector3 maxBoundingCorner;
	    
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
					if (cmd == null)
					{	
						// No command buffer provided to record commands, fall back to automatic CPU.
						goto case SettingsSystem.BoundsMode.Automatic;
					}
					
					if (boundsBuffer == null)
						boundsBuffer = new ComputeBuffer(6, sizeof(uint), ComputeBufferType.Raw);

					// Clear the bounds buffer
					cmd.SetComputeBufferParam(s_computeBoundsCS, BoundsKernels.KClearBuffer, "_BoundsBuffer", boundsBuffer);
					cmd.DispatchCompute(s_computeBoundsCS,  BoundsKernels.KClearBuffer, 1, 1, 1);

					bool calculateBoundsFromRoots = settingsSystem.approximateBoundsFromRoots;
					int calculateBoundsKernel = calculateBoundsFromRoots
						? BoundsKernels.KComputeBoundsRoots
						: BoundsKernels.KComputeBoundsStrands;
					
					// Compute the min/max position across all strand group instances. 
					for (int i = 0; i != strandGroupInstances.Length; i++)
					{
						var rootMargin = GetStrandScale(strandGroupInstances[i]) * strandGroupInstances[i].groupAssetReference.Resolve().maxStrandLength;
						var pointSize = new Vector3(rootMargin * boundsScale, rootMargin * boundsScale, rootMargin * boundsScale);
						// Bind the particle position buffer
						HairSim.BindSolverData(cmd, s_computeBoundsCS,  calculateBoundsKernel, solverData[i]);

						cmd.SetComputeVectorParam(s_computeBoundsCS, "_BoundsMargin", pointSize);
						cmd.SetComputeBufferParam(s_computeBoundsCS, calculateBoundsKernel, "_BoundsBuffer", boundsBuffer);
						
						int particleCount;
						{
							var strandGroup = strandGroupInstances[i].groupAssetReference.Resolve();
							particleCount = calculateBoundsFromRoots ? strandGroup.strandCount : strandGroup.strandCount * strandGroup.strandParticleCount;
						}
						
						const int groupSize = 64;
						cmd.DispatchCompute(s_computeBoundsCS, calculateBoundsKernel, (particleCount + groupSize - 1) / groupSize, 1, 1);
					}
					
					float OrderedUintToFloat(uint u)
					{
						u = (u >> 31) < 1 ? ~u : (u & ~(1 << 31));
						return BitConverter.ToSingle(BitConverter.GetBytes(u), 0);
					}
					
					// Read-back
					cmd.RequestAsyncReadback(boundsBuffer, request =>
					{
						if (!request.hasError)
						{
							var data = request.GetData<uint>();
							minBoundingCorner = new Vector3(OrderedUintToFloat(data[0]), OrderedUintToFloat(data[1]), OrderedUintToFloat(data[2]));
							maxBoundingCorner = new Vector3(OrderedUintToFloat(data[3]), OrderedUintToFloat(data[4]), OrderedUintToFloat(data[5]));
						}
					});

					if (float.IsNaN(minBoundingCorner.x) || float.IsNaN(maxBoundingCorner.x))
					{
					#if UNITY_EDITOR
						Debug.LogWarning("Automatic GPU Bounds produced an invalid result. Falling back to Automatic CPU.");
					#endif
						goto case SettingsSystem.BoundsMode.Automatic;
					}
					
					bounds.center = 0.5f * (minBoundingCorner + maxBoundingCorner);
					bounds.size   = maxBoundingCorner - minBoundingCorner;
				}
					break;

				case SettingsSystem.BoundsMode.Fixed:
				{
					bounds.center = settingsSystem.boundsCenter + this.transform.position;
					bounds.extents = settingsSystem.boundsExtent * boundsScale;
				}
					break;
			}

			if (settingsSystem.boundsSquare)
			{
				bounds = new Bounds(bounds.center, bounds.size.CMax() * Vector3.one);
			}
		}

		void ReleaseBoundsData()
		{
			boundsBuffer?.Dispose();
			boundsBuffer = null;
		}
    }
}