using UnityEngine;
using Unity.Collections;

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
				Gizmos.color = Color.Lerp(Color.clear, Color.white, 0.5f);
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
				Gizmos.color = Color.Lerp(Color.clear, Color.blue, 0.5f);
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
				Gizmos.color = Color.Lerp(Color.clear, Color.cyan, 0.5f);
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
#if false
			{
				Gizmos.color = Color.Lerp(Color.clear, Color.green, 0.5f);
				Gizmos.matrix = Matrix4x4.identity;

				for (int i = 0; i != solverData.Length; i++)
				{
					var solverBounds = HairSim.GetSolverBounds(solverData[i], volumeData);
					{
						Gizmos.DrawWireCube(solverBounds.center, solverBounds.size);
					}
				}
			}
#endif

			// show solver lod indicators
			{
				var camera = Camera.current;
				var cameraPosition = camera.transform.position;
				var cameraRotation = camera.transform.rotation;
				var cameraForward = cameraRotation * Vector3.forward;
				var cameraOrtho = camera.orthographic;

				for (int i = 0; i != solverData.Length; i++)
				{
					var solverBounds = HairSim.GetSolverBounds(solverData[i], volumeData);
					var solverLODPhysics = HairSim.GetSolverLODSelection(solverData[i], HairSim.SolverLODStage.Physics);
					var solverLODRendering = HairSim.GetSolverLODSelection(solverData[i], HairSim.SolverLODStage.Rendering);
					{
						var planarBoundsCenter = solverBounds.center;
						var planarBoundsExtent = solverBounds.extents.CMax();
						var planarBoundsDepth = cameraOrtho ? 1.0f : Mathf.Max(Vector3.Dot(cameraForward, planarBoundsCenter - cameraPosition), camera.nearClipPlane);

						var viewportExtentUnitDepth = cameraOrtho ? camera.orthographicSize : Mathf.Tan(0.5f * Mathf.Deg2Rad * camera.GetGateFittedFieldOfView());
						var viewportExtent = planarBoundsDepth * viewportExtentUnitDepth;
						var viewportScale = viewportExtent / planarBoundsExtent;

						/* TODO minify indicator up close?
						//var planarBoundsP00 = planarBoundsCenter - cameraRotation * (new Vector3(-planarBoundsExtent, -planarBoundsExtent, 0.0f));
						//var planarBoundsP10 = planarBoundsCenter - cameraRotation * (new Vector3( planarBoundsExtent, -planarBoundsExtent, 0.0f));
						//var planarBoundsP01 = planarBoundsCenter - cameraRotation * (new Vector3(-planarBoundsExtent,  planarBoundsExtent, 0.0f));
						//var planarBoundsP11 = planarBoundsCenter - cameraRotation * (new Vector3( planarBoundsExtent,  planarBoundsExtent, 0.0f));
						//var viewDistanceRef = new Vector2(0.5f, 0.5f);
						//var viewDistanceP00 = ((Vector2)camera.WorldToViewportPoint(planarBoundsP00) - viewDistanceRef).Abs().CMax() * 2.0f;
						//var viewDistanceP10 = ((Vector2)camera.WorldToViewportPoint(planarBoundsP10) - viewDistanceRef).Abs().CMax() * 2.0f;
						//var viewDistanceP01 = ((Vector2)camera.WorldToViewportPoint(planarBoundsP01) - viewDistanceRef).Abs().CMax() * 2.0f;
						//var viewDistanceP11 = ((Vector2)camera.WorldToViewportPoint(planarBoundsP11) - viewDistanceRef).Abs().CMax() * 2.0f;
						//var viewDistanceMax = Mathf.Max(viewDistanceP00, Mathf.Max(viewDistanceP10, Mathf.Max(viewDistanceP01, viewDistanceP11)));
						//var minify = Mathf.InverseLerp(0.75f, 1.0f, viewDistanceMax);
						//if (minify > 0.0f)
						//{
						//}
						*/

						Gizmos.matrix = Matrix4x4.TRS(planarBoundsCenter, cameraRotation, new Vector3(2.0f * planarBoundsExtent, 2.0f * planarBoundsExtent, 0.0f));

						//using (new UnityEditor.Handles.DrawingScope(Gizmos.matrix))
						//{
						//	UnityEditor.Handles.Label(0.5f * (Vector3.left + Vector3.up), string.Format("{0}[{1}]", strandGroupInstances[i].groupAssetReference.hairAsset.name, strandGroupInstances[i].groupAssetReference.hairAssetGroupIndex));
						//}

						static void GizmosDrawLODIndicator(in HairSim.LODIndices lodDesc, in NativeArray<float> lodThreshold, in Vector2 p0, in Vector2 rx, in Vector2 ry, in Color color)
						{
							// draw sections
							var t0 = 0.0f;
							{
								for (int j = 0; j != lodThreshold.Length; j++)
								{
									var t = lodThreshold[j];
									var m = (t + t0) * 0.5f;
									var w = (t - t0);

									Gizmos.color = Color.Lerp(Color.clear, Color.Lerp(Color.black, color, t), 0.5f);
									Gizmos.DrawCube(p0 + rx * m + ry * 0.5f, rx * w + ry);

									t0 = t;
								}
							}

							// draw cursor
							Gizmos.color = Color.cyan;
							Gizmos.DrawRay(p0 + rx * lodDesc.lodValue, ry);

							// draw frame
							//Gizmos.color = Color.Lerp(Color.clear, color, 0.5f);
							//Gizmos.DrawWireCube(p0 + (rx + ry) * 0.5f, rx + ry);
						}

						float indicatorHeight = 0.005f * viewportScale;
						float indicatorOffset = 0.5f + indicatorHeight;

						GizmosDrawLODIndicator(solverLODRendering, solverData[i].lodThreshold, Vector2.down * 0.5f + Vector2.right * indicatorOffset, Vector2.up, Vector2.left * indicatorHeight, Color.green);
						GizmosDrawLODIndicator(solverLODPhysics, solverData[i].lodThreshold, Vector2.left * 0.5f + Vector2.down * indicatorOffset, Vector2.right, Vector2.up * indicatorHeight, Color.magenta);

						Gizmos.color = Color.Lerp(Color.clear, Color.green, 0.5f);
						Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
					}
				}
			}

#if false
#if UNITY_EDITOR
			// show participating solid boundaries
			{
				var volumeBounds = HairSim.GetVolumeBounds(volumeData);
				var volumeBoundaries = SpatialComponentFilter<HairBoundary, HairBoundary.RuntimeData, HairBoundaryProxy>.Gather(settingsEnvironment.boundaryResident, settingsEnvironment.boundaryCapture, volumeBounds, settingsEnvironment.boundaryCaptureLayer, volumeSort: false, settingsEnvironment.boundaryCaptureMode == HairSim.SettingsEnvironment.BoundaryCaptureMode.IncludeColliders);

				for (int i = 0; i != volumeBoundaries.Count; i++)
				{
					var boundaryData = volumeBoundaries[i];
					var boundaryOrigin = boundaryData.xform.matrix.MultiplyPoint3x4(Vector3.zero);
					var boundaryDistance = HairBoundaryUtility.SdBoundary(volumeBounds.center, boundaryData);
					var boundaryColor = (i < HairSim.Conf.MAX_BOUNDARIES) ? Color.Lerp(Color.white, Color.clear, 0.5f) : Color.red;

					UnityEditor.Handles.color = boundaryColor;
					UnityEditor.Handles.DrawLine(volumeBounds.center, boundaryOrigin);
					UnityEditor.Handles.Label(boundaryOrigin, string.Format("d[{0}]: {1}", 0, boundaryDistance));
				}
			}
#endif
#endif

			// show participating wind emitters
			if (settingsVolumetrics.windPropagation)
			{
				var volumeBounds = HairSim.GetVolumeBounds(volumeData);
				var volumeEmitters = SpatialComponentFilter<HairWind, HairWind.RuntimeData, HairWindProxy>.Gather(settingsEnvironment.emitterResident, settingsEnvironment.emitterCapture, volumeBounds, settingsEnvironment.emitterCaptureLayer, volumeSort: false, settingsEnvironment.emitterCaptureMode == HairSim.SettingsEnvironment.EmitterCaptureMode.IncludeWindZones);

				for (int i = 0; i != Mathf.Min(volumeEmitters.Count, HairSim.Conf.MAX_EMITTERS); i++)
				{
					HairWind.DrawGizmosRuntimeData(volumeEmitters[i], Time.time, Time.deltaTime, active: true, selected: false);
				}
			}
		}
	}
}
