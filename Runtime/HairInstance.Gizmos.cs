using UnityEngine;

namespace Unity.DemoTeam.Hair
{
	public partial class HairInstance
	{
		void OnDrawGizmos()
		{
			if (solverData == null)
				return;

			// show volume bounds
			{
				Gizmos.color = Color.Lerp(Color.white, Color.clear, 0.5f);
				Gizmos.matrix = Matrix4x4.identity;

				var volumeBounds = HairSim.GetVolumeBounds(volumeData);
				{
					Gizmos.DrawWireCube(volumeBounds.center, volumeBounds.size);
				}
			}
		}

		void OnDrawGizmosSelected()
		{
			if (solverData == null)
				return;

			// show root bounds
			{
				Gizmos.color = Color.Lerp(Color.blue, Color.clear, 0.5f);
				Gizmos.matrix = Matrix4x4.identity;

				for (int i = 0; i != strandGroupInstances.Length; i++)
				{
					var rootMeshFilter = strandGroupInstances[i].sceneObjects.rootMeshFilter;
					if (rootMeshFilter != null)
					{
						var rootMesh = rootMeshFilter.sharedMesh;
						if (rootMesh != null)
						{
							var rootBounds = rootMesh.bounds;
							{
								Gizmos.matrix = rootMeshFilter.transform.localToWorldMatrix;
								Gizmos.DrawWireCube(rootBounds.center, rootBounds.size);
								Gizmos.matrix = Matrix4x4.identity;
							}
						}
					}
				}
			}

			// show solver gravity
			{
				Gizmos.color = Color.Lerp(Color.cyan, Color.clear, 0.5f);
				Gizmos.matrix = Matrix4x4.identity;

				for (int i = 0; i != solverData.Length; i++)
				{
					var solverBounds = HairSim.GetSolverBounds(solverData[i], volumeData);
					var solverGravity = volumeData.constantsEnvironment._WorldGravity * solverData[i].constants._GravityScale;
					{
						Gizmos.DrawRay(solverBounds.center, solverGravity * 0.1f);
					}
				}
			}

			// show solver bounds
			{
				Gizmos.color = Color.Lerp(Color.green, Color.clear, 0.5f);
				Gizmos.matrix = Matrix4x4.identity;

				for (int i = 0; i != solverData.Length; i++)
				{
					var solverBounds = HairSim.GetSolverBounds(solverData[i], volumeData);
					{
						Gizmos.DrawWireCube(solverBounds.center, solverBounds.size);
					}
				}
			}

			/*
#if UNITY_EDITOR
			// show participating solid boundaries
			{
				var boundaries = HairBoundaryUtility.Gather(settingsVolume.boundariesPriority, volumeSort: true, settingsVolume.boundariesCollect, GetSimulationBounds(), Quaternion.identity, settingsVolume.boundariesCollectMode == HairSim.VolumeSettings.CollectMode.IncludeColliders);

				for (int i = 0; i != boundaries.Count; i++)
				{
					var boundaryData = boundaries[i];
					var boundaryOrigin = boundaryData.xform.matrix.MultiplyPoint3x4(Vector3.zero);
					var boundaryDistance = HairBoundaryUtility.SdBoundary(volumeCenter, boundaryData);
					var boundaryColor = (i < HairSim.MAX_BOUNDARIES) ? Color.green : Color.red;

					Gizmos.color = boundaryColor;
					Gizmos.DrawLine(volumeCenter, boundaryOrigin);

					UnityEditor.Handles.color = boundaryColor;
					UnityEditor.Handles.Label(boundaryOrigin, "d[" + i + "]: " + boundaryDistance);
				}
			}
#endif
			*/

			// show participating wind emitters
			if (settingsVolumetrics.windPropagation)
			{
				foreach (var emitter in HairWind.s_emitters)
				{
					if (emitter != null && emitter.isActiveAndEnabled)
						emitter.DrawGizmos(Time.time, Time.deltaTime);// TODO replace with wind emitter clock
				}
			}
		}
	}
}
