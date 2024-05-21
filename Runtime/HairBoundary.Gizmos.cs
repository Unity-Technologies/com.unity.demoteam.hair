using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.DemoTeam.Hair
{
	public partial class HairBoundary
	{
		void OnDrawGizmosSelected()
		{
			var data = new RuntimeData();
			{
				if (TryGetData(this, ref data))
				{
					DrawGizmosRuntimeData(data);
				}
			}
		}

		static void DrawGizmosRuntimeData(in RuntimeData data)
		{
			switch (data.type)
			{
				case RuntimeData.Type.Shape: DrawGizmosRuntimeShape(data); break;
				case RuntimeData.Type.SDF: DrawGizmosRuntimeSDF(data); break;
			}
		}

		static void DrawGizmosRuntimeShape(in RuntimeData data)
		{
			Gizmos.color = Color.red;
			Gizmos.matrix = data.xform.matrix;
			Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

			Gizmos.color = Color.yellow;
			Gizmos.matrix = Matrix4x4.identity;

			switch (data.shape.type)
			{
				case RuntimeShape.Type.Capsule:
					{
						var worldCenterA = data.shape.data.pA;
						var worldCenterB = data.shape.data.pB;
						var worldRadius = data.shape.data.tA;
						{
							Gizmos.DrawWireSphere(worldCenterA, worldRadius);
							Gizmos.DrawWireSphere(worldCenterB, worldRadius);
							Gizmos.DrawLine(worldCenterA, worldCenterB);
						}
					}
					break;
				case RuntimeShape.Type.Sphere:
					{
						var worldCenter = data.shape.data.pA;
						var worldRadius = data.shape.data.tA;
						{
							Gizmos.DrawWireSphere(worldCenter, worldRadius);
						}
					}
					break;
				case RuntimeShape.Type.Torus:
					{
						var worldCenter = data.shape.data.pA;
						var worldAxis = data.shape.data.pB;
						var worldRadiusAxis = data.shape.data.tA;
						var worldRadiusRing = data.shape.data.tB;
						{
							var basisX = (Mathf.Abs(worldAxis.y) > 1.0f - 1e-4f) ? Vector3.right : Vector3.Normalize(Vector3.Cross(worldAxis, Vector3.up));
							var basisY = worldAxis;
							var basisZ = Vector3.Cross(basisX, worldAxis);

							var axisSteps = 11;
							var axisStep = Quaternion.AngleAxis(360.0f / axisSteps, basisY);
							var axisVec = worldRadiusAxis * basisX;

							var ringSteps = 5;
							var ringStep = Quaternion.AngleAxis(360.0f / ringSteps, basisZ);
							var ringVec = worldRadiusRing * basisX;

							var axisRot = Quaternion.identity;
							var axisPos = axisRot * axisVec + worldCenter;

							unsafe
							{
								var ring_0 = stackalloc Vector3[ringSteps];
								var ring_i = stackalloc Vector3[ringSteps];

								for (int i = 0; i != axisSteps; i++)
								{
									var ringRot = axisRot;
									var ringPos = axisPos + ringRot * ringVec;

									for (int j = 0; j != ringSteps; j++)
									{
										var ringPosPrev = ringPos;

										ringRot = ringRot * ringStep;
										ringPos = axisPos + ringRot * ringVec;

										Gizmos.DrawLine(ringPosPrev, ringPos);

										if (i == 0)
										{
											ring_0[j] = ringPos;
											ring_i[j] = ringPos;
										}
										else
										{
											Gizmos.DrawLine(ring_i[j], ringPos);
											ring_i[j] = ringPos;
										}
									}

									axisRot = axisRot * axisStep;
									axisPos = axisRot * axisVec + worldCenter;
								}

								for (int j = 0; j != ringSteps; j++)
								{
									Gizmos.DrawLine(ring_0[j], ring_i[j]);
								}
							}
						}
					}
					break;
				case RuntimeShape.Type.Cube:
					{
						var localCenter = Vector3.zero;
						var localExtent = Vector3.one;
						{
							Gizmos.matrix = data.xform.matrix;
							Gizmos.DrawWireCube(localCenter, localExtent);
						}
					}
					break;
			}
		}

		static void DrawGizmosRuntimeSDF(in RuntimeData data)
		{
			var localCenter = 0.5f * Vector3.one;
			var localExtent = Vector3.one;
			{
				Gizmos.color = Color.red;
				Gizmos.matrix = data.xform.matrix;
				Gizmos.DrawWireCube(localCenter, localExtent);
			}
		}
	}
}
