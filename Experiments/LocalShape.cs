#define DEBUG_BEND
//#define DEBUG_BEND_RMF

using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using static Quat;
using static Constraints;

public class LocalShape : MonoBehaviour
{
	public bool load;

	[Range(0.001f, 1.0f)]
	public float stepLength;

	[Range(0.0f, 90.0f)]
	public float degreesTwist;
	[Range(0.0f, 90.0f)]
	public float degreesBend;

	[Min(1)]
	public int iterationCount = 1;

	public enum IterationMode
	{
		SingleForward,
		BlendedSplit,
		BlendedBefore,
		BlendedAfter,
		ForwardSplit,
		ForwardBefore,
		ForwardAfter,
		ReverseSplit,
		ReverseBefore,
		ReverseAfter,
	}

	public IterationMode iterationMode;
	[Range(-1, 23)]
	public int iterationStopAt = -1;

	public bool plainMul = false;
	public float2 plainScale = 0;
	public bool lerpW = false;

	[Range(0.0f, 1.0f)]
	public float stiffness = 1.0f;
	public StiffnessMode stiffnessMode;
	public enum StiffnessMode
	{
		Constant,
		IterationCount,
		Iteration,
	};

	public AnimationCurve stiffnessCurve = AnimationCurve.Constant(0.0f, 1.0f, 1.0f);

	public Mode mode;
	public enum Mode
	{
		GlobalRotation,
		LocalRotation,
		LocalBendTwist,
		ReferenceBendTwist,
	};

	public bool flatten = false;
	public float flattenDegrees = 45.0f;

	const int _StrandParticleCount = 24;

	float4[] _ParticlePosition = new float4[_StrandParticleCount];
	float4[] _ParticlePositionCheck = new float4[_StrandParticleCount];

	float4[] _InitialRootFrame = new float4[1];
	float4[] _InitialParticleOffset = new float4[_StrandParticleCount];
	float4[] _InitialParticleFrameDelta = new float4[_StrandParticleCount];

	const int strandIndex = 0;
	const int strandParticleBegin = 0;
	const int strandParticleStride = 1;
	const int strandParticleEnd = _StrandParticleCount - 1;

	float visualizationOffset => 5.0f * stepLength;

	private void InitParticles()
	{
		// make a curly thing
		{
			var bendTwist =
				QMul(
					MakeQuaternionFromAxisAngle(Vector3.up, Mathf.Deg2Rad * degreesTwist),
					MakeQuaternionFromAxisAngle(Vector3.right, Mathf.Deg2Rad * degreesBend)
				);

			var bendTwistCheck =
				Quaternion.AngleAxis(degreesTwist, Vector3.up) *
				Quaternion.AngleAxis(degreesBend, Vector3.right);

			var accuBendTwist = MakeQuaternionIdentity();
			var accuBendTwistCheck = Quaternion.identity;

			if (flatten)
			{
				accuBendTwist = QMul(accuBendTwist, MakeQuaternionFromAxisAngle(Vector3.forward, flattenDegrees * Mathf.Deg2Rad));
				accuBendTwistCheck = accuBendTwistCheck * Quaternion.AngleAxis(flattenDegrees, Vector3.forward);
			}

			var accuPos = float3(0,0,0);// (float3)transform.position;
			var accuPosCheck = Vector3.zero;//(Vector3)transform.position;

			for (uint i = strandParticleBegin; i != strandParticleEnd; i += strandParticleStride)
			{
				_ParticlePosition[i].xyz = accuPos;
				_ParticlePositionCheck[i].xyz = accuPosCheck;

				accuPos += stepLength * QMul(accuBendTwist, Vector3.up);
				accuPosCheck += stepLength * (accuBendTwistCheck * Vector3.up);

				accuBendTwist = QMul(accuBendTwist, bendTwist);
				accuBendTwistCheck = accuBendTwistCheck * bendTwistCheck;
			}
		}

		// load a curly thing....
		if (load)
		{
			stepLength = 0.006518265f;

			var x = new float[] { -0.0539647266f, 1.66329408f, 0.0420717821f, -0.05892205f, 1.663796f, 0.0462742671f, -0.065399535f, 1.66450787f, 0.0464253053f, -0.06908573f, 1.66530728f, 0.04110922f, -0.068440184f, 1.66279137f, 0.0351307876f, -0.06692716f, 1.66022718f, 0.02933222f, -0.07266835f, 1.657283f, 0.0302585065f, -0.07648469f, 1.65256929f, 0.03264698f, -0.07852129f, 1.64724529f, 0.029485492f, -0.0799734f, 1.64489079f, 0.0235833619f, -0.08198029f, 1.6421032f, 0.0180435f, -0.08148313f, 1.64316154f, 0.0244560242f, -0.07879324f, 1.6388979f, 0.028588051f, -0.07937992f, 1.6362195f, 0.02267451f, -0.08365847f, 1.633886f, 0.0183459837f, -0.08308538f, 1.63372445f, 0.01185498f, -0.08264584f, 1.63011241f, 0.00644685933f, -0.08222081f, 1.62916529f, 1.17910095E-05f, -0.08783199f, 1.62645793f, -0.00190465129f, -0.08868069f, 1.6237092f, -0.00775377825f, -0.0832980648f, 1.62057388f, -0.009673343f, -0.08594932f, 1.62508285f, -0.0135627184f, -0.0869903862f, 1.62739885f, -0.0195660777f, -0.0857876f, 1.62626815f, -0.0258718319f };
			var n = x.Length / 3;

			x = new float[] { -0.5f, 0f, 0f, -0.5103446f, 0f, 0.00374020683f, -0.512277842f, 0f, 0.0145690013f, -0.503866434f, 0f, 0.021657588f, -0.4935218f, 0f, 0.01791738f, -0.4915886f, 0f, 0.00708858855f, -0.5f, 0f, -3.38009426E-10f, -0.5103446f, 0f, 0.00374021f, -0.512277842f, 0f, 0.0145690031f, -0.503866434f, 0f, 0.021657588f, -0.4935218f, 0f, 0.0179173835f, -0.4915886f, 0f, 0.007088582f, -0.5f, 0f, -6.76018852E-10f, -0.5103446f, 0f, 0.00374020776f, -0.512277842f, 0f, 0.01456901f, -0.503866434f, 0f, 0.02165759f, -0.4935218f, 0f, 0.017917376f, -0.4915886f, 0f, 0.00708858436f, -0.5f, 0f, -9.221217E-11f, -0.5103446f, 0f, 0.00374020543f, -0.512277842f, 0f, 0.0145689966f, -0.503866434f, 0f, 0.02165759f, -0.4935218f, 0f, 0.0179173723f, -0.4915886f, 0f, 0.00708857831f };
			stepLength = 0.0110000074f;

			var o = new Vector3(x[0], x[1], x[2]);

			for (int i = 0; i != n; i++)
			{
				_ParticlePosition[i].x = x[3 * i];
				_ParticlePosition[i].y = x[3 * i + 1];
				_ParticlePosition[i].z = x[3 * i + 2];

				_ParticlePositionCheck[i] = _ParticlePosition[i];
			}

			//Debug.Log(n);

		}

		// calc initial particle offsets (from root)
		for (uint i = strandParticleBegin; i != strandParticleEnd; i += strandParticleStride)
		{
			_InitialParticleOffset[i].xyz = _ParticlePosition[i].xyz - _ParticlePosition[strandParticleBegin].xyz;
		}

		// calc initial strand root material frame
		{
			float3 rootDir0 = normalize(_InitialParticleOffset[strandParticleBegin + strandParticleStride].xyz);
			float3 rootDirMaterialFrame = float3(0, 1, 0);

			_InitialRootFrame[strandIndex] = MakeQuaternionFromTo(rootDirMaterialFrame, rootDir0);
		}

		// calc initial particle material frame deltas
		{
			float4 rootFrame = _InitialRootFrame[strandIndex];
			float4 rootFrameInv = QInverse(rootFrame);

			// root
			_InitialParticleFrameDelta[strandParticleBegin] = MakeQuaternionIdentity();

			// root+1
			float3 r0 = -float3(0, 1, 0);
			float3 r1 = 0;
			float3 r2 = QMul(rootFrameInv, _InitialParticleOffset[strandParticleBegin + strandParticleStride].xyz);
			{
#if DEBUG_BEND
				_InitialParticleFrameDelta[strandParticleBegin + strandParticleStride] = NextQuaternionFromBend(r0, r1, r2, MakeQuaternionIdentity());
#else
				_InitialParticleFrameDelta[strandParticleBegin + strandParticleStride] = MakeQuaternionFromBend(r0, r1, r2);
#endif
			}

			// root+2..
			for (uint i = strandParticleBegin + strandParticleStride * 2; i != strandParticleEnd; i += strandParticleStride)
			{
				r0 = r1;
				r1 = r2;
				r2 = QMul(rootFrameInv, _InitialParticleOffset[i].xyz);

#if DEBUG_BEND
				_InitialParticleFrameDelta[i] = NextQuaternionFromBend(r0, r1, r2, _InitialParticleFrameDelta[i - strandParticleStride]);
#else
				_InitialParticleFrameDelta[i] = normalize(QMul(MakeQuaternionFromBend(r0, r1, r2), _InitialParticleFrameDelta[i - strandParticleStride]));
#endif
			}

			// reverse to finalize
			{
				for (uint i = strandParticleEnd - strandParticleStride; i != strandParticleBegin; i -= strandParticleStride)
				{
					float4 q0 = _InitialParticleFrameDelta[i - strandParticleStride];
					float4 q1 = _InitialParticleFrameDelta[i];

					float4 delta = QMul(QInverse(q0), q1);
					float4 delta_add = delta + MakeQuaternionIdentity();
					float4 delta_sub = delta - MakeQuaternionIdentity();

					if (dot(delta_sub, delta_sub) > dot(delta_add, delta_add))
					{
						delta *= -1.0f;
					}

					_InitialParticleFrameDelta[i] = delta;// normalize(QMul(QConjugate(q0), q1));
				}
			}
		}
	}

	float GetParticleInterval(int strandIndex)
	{
		return stepLength;
	}

	private void OnDrawGizmos()
	{
		Gizmos.matrix = transform.localToWorldMatrix;

		// test
		/*
		{
			// x = -y
			// y = x
			// z = -w
			// w = z
			//float4 q_e_3_bar = float4(-q.y, q.x, -q.w, q.z);//compute q*e_3.conjugate (cheaper than quaternion product)

			float4 q = float4(11, 22, 33, 44);
			float4 e3_bar = QConjugate(float4(0, 1, 0, 0));
			float4 e3_bar_ref = QConjugate(float4(0, 0, 1, 0));

			float4 q_e3_bar = QMul(q, e3_bar);
			float4 q_e3_bar_ref = QMul(q, e3_bar_ref);

			Debug.Log("----");
			Debug.Log("q = " + q);
			Debug.Log("q_e3_bar = " + q_e3_bar);
			Debug.Log("q_e3_bar_ref = " + q_e3_bar_ref);
			Debug.Log("TARGET_ref = " + float4(-22,11,-44,33));
		}
		*/

		InitParticles();

		float4 rootTransform = MakeQuaternion(transform.rotation);
		float4 rootFrame = normalize(QMul(rootTransform, _InitialRootFrame[0]));

		float3 rootPos = _ParticlePosition[strandParticleBegin].xyz;
		float3 rootDir = QMul(rootFrame, float3(0, 1, 0));

		// draw
		{
			var offsetCheck = float3(visualizationOffset * 2, 0, 0);

			for (uint i = strandParticleBegin + strandParticleStride; i != strandParticleEnd; i += strandParticleStride)
			{
				Gizmos.color = Color.Lerp(Color.white, Color.clear, 0.5f);
				Gizmos.DrawLine(_ParticlePositionCheck[i].xyz + offsetCheck, _ParticlePositionCheck[i - strandParticleStride].xyz + offsetCheck);
				Gizmos.color = Color.green;
				Gizmos.DrawLine(_ParticlePosition[i].xyz, _ParticlePosition[i - strandParticleStride].xyz);
			}
		}

		// modify
		{
			if (load)
			{
				var dt = 1.0f / 60.0f;
				var dv = dt * dt * (float3)Physics.gravity;

				/*if (false)
				{
					for (uint i = strandParticleBegin + strandParticleStride; i != strandParticleEnd; i += strandParticleStride)
					{
						_ParticlePosition[i].xyz += dv;

						Gizmos.color = Color.Lerp(Color.yellow, Color.clear, 0.5f);
						Gizmos.DrawLine(_ParticlePosition[i - strandParticleStride].xyz, _ParticlePosition[i].xyz);
					}
				}*/
			}
			else
			{
				for (uint i = strandParticleBegin + strandParticleStride; i != strandParticleEnd; i += strandParticleStride)
				{
					uint step = i / strandParticleStride;
					float3 dn = float3(0, step * stepLength, 0);

					_ParticlePosition[i].xyz = rootPos + QMul(rootTransform, dn);

					Gizmos.color = Color.Lerp(Color.yellow, Color.clear, 0.5f);
					Gizmos.DrawLine(_ParticlePosition[i - strandParticleStride].xyz, _ParticlePosition[i].xyz);
				}
			}
		}

		// show deltas
		{
			var offset = float3(visualizationOffset, 0, 0);
			var origin = _ParticlePosition[strandParticleBegin].xyz;

			for (uint i = strandParticleBegin + strandParticleStride; i != strandParticleEnd; i += strandParticleStride)
			{
				Gizmos.color = Color.Lerp(Color.cyan, Color.clear, 0.5f);
				Gizmos.DrawRay(_ParticlePosition[i - strandParticleStride].xyz - offset, stepLength * QMul(_InitialParticleFrameDelta[i], Vector3.up));
			}

			// reconstruction
			float4 q0 = QMul(rootFrame, _InitialParticleFrameDelta[strandParticleBegin]);
			float3 p0 = 0;
			float3 p1 = origin;

			float3 tangent = stepLength * float3(0, 1, 0);

			for (uint i = strandParticleBegin + strandParticleStride; i != strandParticleEnd; i += strandParticleStride)
			{
				q0 = QMul(q0, _InitialParticleFrameDelta[i]);

				p0 = p1;
				p1 = p0 + QMul(q0, tangent);

				Gizmos.color = Color.Lerp(Color.magenta, Color.clear, 0.2f);
				Gizmos.DrawLine(p0 + offset, p1 + offset);
			}
		}

		// apply deltas
		UnityEngine.Profiling.Profiler.BeginSample("solve");

		//Debug.Log("---- solve frame ----");

		for (int k = 0; k != iterationCount; k++)
		{
			float kS;
			switch (stiffnessMode)
			{
				default:
				case StiffnessMode.Constant:
					kS = stiffness;
					break;
				case StiffnessMode.Iteration:
					kS = 1.0f - pow(saturate(1.0f - stiffness), 1.0f / (1 + k));
					break;
				case StiffnessMode.IterationCount:
					kS = 1.0f - pow(saturate(1.0f - stiffness), 1.0f / iterationCount);
					break;
			}

			// root
			float3 p0 = rootPos - rootDir * GetParticleInterval(strandIndex) * 3.0f;
			float3 p1 = rootPos - rootDir * GetParticleInterval(strandIndex) * 2.0f;
			float3 p2 = rootPos - rootDir * GetParticleInterval(strandIndex);
			float3 p3 = rootPos;

			float4 accuFrame = normalize(QMul(rootFrame, _InitialParticleFrameDelta[strandParticleBegin]));
			float3 accuTangent = GetParticleInterval(strandIndex) * float3(0, 1, 0);

			float4 q0 = QMul(accuFrame, MakeQuaternionTwistIdentity());
			float4 q1 = accuFrame;//MakeQuaternionIdentity();
			float4 q2 = QMul(accuFrame, MakeQuaternionTwistIdentity());
			float4 q3 = accuFrame;

			float w0 = 0.0f;
			float w1 = 0.0f;
			float w2 = 0.0f;
			float w3 = 0.0f;

			float kS_orig = kS;

			// root+1..
			uint i;
			for (i = strandParticleBegin + strandParticleStride; i != strandParticleEnd; i += strandParticleStride)
			{
				float n = (i - strandParticleBegin) / strandParticleStride;
				float t = n / (_StrandParticleCount - 1);
				float c = stiffnessCurve.Evaluate(t);
				//kS = kS_orig * ;

				p0 = p1;
				p1 = p2;
				p2 = p3;
				p3 = _ParticlePosition[i].xyz;

				w0 = w1;
				w1 = w2;
				w2 = w3;
				w3 = 1.0f;

				//ApplyDistanceConstraint(GetParticleInterval(strandIndex), kS, w2, w3, ref p2, ref p3);

				q0 = q1;
				q1 = q2;
				q2 = q3;
#if DEBUG_BEND
				q3 = NextQuaternionFromBend(p1, p2, p3, q2);
#else
				q3 = QMul(MakeQuaternionFromBend(p1, p2, p3), q2);
#endif

				// solve i
				{
					float wq2 = w2;
					float wq3 = w3;
					if (lerpW)
					{
						wq2 = lerp(w1, w2, 1e-6f);
						wq3 = lerp(w2, w3, 1e-6f);
					}
					wq2 = 1.0f;
					wq3 = 1.0f;

					switch (iterationMode)
					{
						case IterationMode.SingleForward:
							{
								ApplyReferenceBendTwist(_InitialParticleFrameDelta[i], kS, wq2, wq3, ref q2, ref q3);
								ApplyReferenceStretchShear(GetParticleInterval(strandIndex), kS, w2, w3, wq3, ref p2, ref p3, ref q3, plainMul, plainScale);
							}
							break;
						
						//---- BL
						case IterationMode.BlendedSplit:
							{
								float scale_w = 2.0f;
								float scale_k = 1.0f;
								float3 pX = p2;
								ApplyReferenceStretchShear(GetParticleInterval(strandIndex), scale_k * kS, w1, scale_w * w2, wq2, ref p1, ref pX, ref q2, plainMul, plainScale);
								ApplyReferenceBendTwist(_InitialParticleFrameDelta[i - 0 * strandParticleStride], kS, wq2, wq3, ref q2, ref q3);
								ApplyReferenceStretchShear(GetParticleInterval(strandIndex), scale_k * kS, scale_w * w2, w3, wq3, ref p2, ref p3, ref q3, plainMul, plainScale);
								p2 = lerp(pX, p2, 0.5f);
							}
							break;
						case IterationMode.BlendedBefore:
							{
								float scale_w = 2.0f;
								float scale_k = 1.0f;
								float3 pX = p2;
								ApplyReferenceStretchShear(GetParticleInterval(strandIndex), scale_k * kS, w1, scale_w * w2, wq2, ref p1, ref pX, ref q2, plainMul, plainScale);
								ApplyReferenceStretchShear(GetParticleInterval(strandIndex), scale_k * kS, scale_w * w2, w3, wq3, ref p2, ref p3, ref q3, plainMul, plainScale);
								ApplyReferenceBendTwist(_InitialParticleFrameDelta[i - 0 * strandParticleStride], kS, wq2, wq3, ref q2, ref q3);
								if (i == strandParticleEnd - strandParticleStride)
								{
									ApplyEdgeVectorConstraint(QMul(q3, float3(0, 1, 0)) * GetParticleInterval(strandIndex), scale_k * kS, w2, w3, ref p2, ref p3);
								}
								p2 = lerp(pX, p2, 0.5f);
							}
							break;
						case IterationMode.BlendedAfter:
							{
								float scale_w = 2.0f;
								float scale_k = 0.5f;
								float3 pX = p2;
								ApplyReferenceBendTwist(_InitialParticleFrameDelta[i - 0 * strandParticleStride], kS, wq2, wq3, ref q2, ref q3);
								ApplyReferenceStretchShear(GetParticleInterval(strandIndex), scale_k * kS, w1, scale_w * w2, wq2, ref p1, ref pX, ref q2, plainMul, plainScale);
								ApplyReferenceStretchShear(GetParticleInterval(strandIndex), scale_k * kS, scale_w * w2, w3, wq3, ref p2, ref p3, ref q3, plainMul, plainScale);
								p2 = lerp(pX, p2, 0.5f);
							}
							break;

						//---- FWD
						case IterationMode.ForwardSplit:
							{
								float scale_w = 1.0f;
								float scale_k = 1.0f;
								ApplyReferenceStretchShear(GetParticleInterval(strandIndex), scale_k * kS, w1, scale_w * w2, wq2, ref p1, ref p2, ref q2, plainMul, plainScale);
								ApplyReferenceBendTwist(_InitialParticleFrameDelta[i - 0 * strandParticleStride], kS, wq2, wq3, ref q2, ref q3);
								ApplyReferenceStretchShear(GetParticleInterval(strandIndex), scale_k * kS, scale_w * w2, w3, wq3, ref p2, ref p3, ref q3, plainMul, plainScale);
							}
							break;
						case IterationMode.ForwardBefore:
							{
								float scale_w = 1.0f;
								float scale_k = 1.0f;
								ApplyReferenceStretchShear(GetParticleInterval(strandIndex), scale_k * kS, w1, scale_w * w2, wq2, ref p1, ref p2, ref q2, plainMul, plainScale);
								ApplyReferenceStretchShear(GetParticleInterval(strandIndex), scale_k * kS, scale_w * w2, w3, wq3, ref p2, ref p3, ref q3, plainMul, plainScale);
								ApplyReferenceBendTwist(_InitialParticleFrameDelta[i - 0 * strandParticleStride], kS, 0.5f * wq2, wq3, ref q2, ref q3);
								if (i == strandParticleEnd - strandParticleStride)
								{
									//ApplyReferenceStretchShear(GetParticleInterval(strandIndex), scale_k * kS, w2, w3, wq3, ref p2, ref p3, ref q3, plainMul);
									ApplyEdgeVectorConstraint(QMul(q3, float3(0, 1, 0)) * GetParticleInterval(strandIndex), scale_k * kS, w2, w3, ref p2, ref p3);
									//p3 = p2 + QMul(q3, float3(0, 1, 0)) * GetParticleInterval(strandIndex);
								}
							}
							break;
						case IterationMode.ForwardAfter:
							{
								float scale_w = 1.0f;
								float scale_k = 0.5f;
								ApplyReferenceBendTwist(_InitialParticleFrameDelta[i - 0 * strandParticleStride], kS, wq2, wq3, ref q2, ref q3);
								ApplyReferenceStretchShear(GetParticleInterval(strandIndex), scale_k * kS, w1, scale_w * w2, wq2, ref p1, ref p2, ref q2, plainMul, plainScale);
								ApplyReferenceStretchShear(GetParticleInterval(strandIndex), scale_k * kS, scale_w * w2, w3, wq3, ref p2, ref p3, ref q3, plainMul, plainScale);
							}
							break;

						//---- RWD
						case IterationMode.ReverseSplit:
							{
								float scale_w = 1.0f;
								float scale_k = 1.0f;
								ApplyReferenceStretchShear(GetParticleInterval(strandIndex), scale_k * kS, scale_w * w2, w3, wq3, ref p2, ref p3, ref q3, plainMul, plainScale);
								ApplyReferenceBendTwist(_InitialParticleFrameDelta[i - 0 * strandParticleStride], kS, wq2, wq3, ref q2, ref q3);
								ApplyReferenceStretchShear(GetParticleInterval(strandIndex), scale_k * kS, w1, scale_w * w2, wq2, ref p1, ref p2, ref q2, plainMul, plainScale);
							}
							break;
						case IterationMode.ReverseBefore:
							{
								bool debug = false;// ((i + 5) >= strandParticleEnd - strandParticleStride) && (k == iterationCount - 1);
								float scale_w = 1.0f;
								float scale_k = 1.0f;// 0.75f * 0.666f;
								//wq2 = 1.0f;
								//wq3 = 1.0f;
								ApplyReferenceStretchShear(GetParticleInterval(strandIndex), scale_k * kS, scale_w * w2, w3, wq3, ref p2, ref p3, ref q3, plainMul, plainScale);
								ApplyReferenceStretchShear(GetParticleInterval(strandIndex), scale_k * kS, w1, scale_w * w2, wq2, ref p1, ref p2, ref q2, plainMul, plainScale);
								//ApplyReferenceBendTwist(_InitialParticleFrameDelta[i - 0 * strandParticleStride], kS, 0.6666667f * wq2, wq3, ref q2, ref q3, debug : debug);
								ApplyReferenceBendTwist(_InitialParticleFrameDelta[i - 0 * strandParticleStride], kS, 0.5f * wq2, wq3, ref q2, ref q3, debug: debug);
								if (i == strandParticleEnd - strandParticleStride)
								{
									//ApplyReferenceStretchShear(GetParticleInterval(strandIndex), scale_k * kS, w2, w3, wq3, ref p2, ref p3, ref q3, plainMul);
									ApplyEdgeVectorConstraint(QMul(q3, float3(0, 1, 0)) * GetParticleInterval(strandIndex), scale_k * kS, w2, w3, ref p2, ref p3);
									//p3 = p2 + QMul(q3, float3(0, 1, 0)) * GetParticleInterval(strandIndex);
								}
							}
							break;
						case IterationMode.ReverseAfter:
							{
								float scale_w = 1.0f;
								float scale_k = 0.5f;
								ApplyReferenceBendTwist(_InitialParticleFrameDelta[i - 0 * strandParticleStride], kS, wq2, wq3, ref q2, ref q3);
								ApplyReferenceStretchShear(GetParticleInterval(strandIndex), scale_k * kS, scale_w * w2, w3, wq3, ref p2, ref p3, ref q3, plainMul, plainScale);
								ApplyReferenceStretchShear(GetParticleInterval(strandIndex), scale_k * kS, w1, scale_w * w2, wq2, ref p1, ref p2, ref q2, plainMul, plainScale);
							}
							break;
					}


				}

				if (k == iterationCount - 1)
				{
					Gizmos.color = Color.magenta;
					Gizmos.DrawRay(0.5f * (p0 + p1), (0.2f * stepLength) * QMul(q1, float3(1, 0, 0)));
					Gizmos.color = Color.cyan;
					Gizmos.DrawRay(0.5f * (p0 + p1), (0.2f * stepLength) * QMul(q1, float3(0, 0, 1)));
				}

				if (i > strandParticleBegin + 2 * strandParticleStride)
				{
					_ParticlePosition[i - strandParticleStride * 2].xyz = p1;
				}

				if (k == iterationCount - 1 && iterationStopAt != -1)
				{
					var j = (i - strandParticleBegin) / strandParticleStride;
					if (j >= iterationStopAt)
					{
						i += strandParticleStride;
						break;
					}
				}
			}

			if (k == iterationCount - 1)
			{
				Gizmos.color = Color.magenta;
				Gizmos.DrawRay(0.5f * (p1 + p2), (0.2f * stepLength) * QMul(q2, float3(1, 0, 0)));
				Gizmos.color = Color.cyan;
				Gizmos.DrawRay(0.5f * (p1 + p2), (0.2f * stepLength) * QMul(q2, float3(0, 0, 1)));

				Gizmos.color = Color.magenta;
				Gizmos.DrawRay(0.5f * (p2 + p3), (0.25f * stepLength) * QMul(q3, float3(1, 0, 0)));
				Gizmos.color = Color.cyan;
				Gizmos.DrawRay(0.5f * (p2 + p3), (0.25f * stepLength) * QMul(q3, float3(0, 0, 1)));
			}

			if (i > strandParticleBegin + strandParticleStride * 2)
				_ParticlePosition[i - strandParticleStride * 2].xyz = p2;
			if (i > strandParticleBegin + strandParticleStride)
				_ParticlePosition[i - strandParticleStride].xyz = p3;
		}

		UnityEngine.Profiling.Profiler.EndSample();

		// draw again
		for (uint i = strandParticleBegin + strandParticleStride; i != strandParticleEnd; i += strandParticleStride)
		{
			Gizmos.color = Color.Lerp(Color.red, Color.clear, 0.25f);
			Gizmos.DrawLine(_ParticlePosition[i].xyz, _ParticlePosition[i - strandParticleStride].xyz);
			Gizmos.DrawWireSphere(_ParticlePosition[i].xyz, stepLength * 0.02f);
		}
	}
}

public static class Constraints
{
	const float w_EPSILON = 1e-7f;

	// ---- paper ----

	public static void SolveReferenceBendTwist(
		float4 darboux0, in float stiffness,
		in float w0, in float w1,
		in float4 q0, in float4 q1,
		ref float4 d0, ref float4 d1, bool debug)
	{
		// see: "Position and Orientation Based Cosserat Rods" by T. Kugelstadt and E. Schömer
		// https://www.cg.informatik.uni-mainz.de/files/2016/06/Position-and-Orientation-Based-Cosserat-Rods.pdf

		float4 darboux = QMul(QConjugate(q0), q1);

		// apply eq. 32 + 33 to pick closest delta
		float4 delta_add = (darboux + darboux0);
		float4 delta_sub = (darboux - darboux0);
		float4 delta = (dot(delta_add, delta_add) < dot(delta_sub, delta_sub)) ? delta_add : delta_sub;

		if (dot(delta, delta) < 1e-7)
			return;

		//delta *= 0.5f;
		if (debug)
			Debug.Log("||delta|| = " + length(delta));

		// apply eq. 40 to calc corrections
		float W_inv = stiffness / (w0 + w1 + w_EPSILON);

		delta.w = 0.0f;// zero scalar part
		d0 += (w0 * W_inv) * QMul(q1, delta);
		d1 -= (w1 * W_inv) * QMul(q0, delta);
	}

	public static void SolveReferenceStretchShear(
		float distance0, in float stiffness,
		in float w0, in float w1, in float wq,
		in float3 p0, in float3 p1, in float4 q,
		ref float3 d0, ref float3 d1, ref float4 dq, bool plainMul, float2 plainScale)
	{
		// see: "Position and Orientation Based Cosserat Rods" by T. Kugelstadt and E. Schömer
		// https://www.cg.informatik.uni-mainz.de/files/2016/06/Position-and-Orientation-Based-Cosserat-Rods.pdf

		float3 e3 = float3(0, 1, 0);

		// apply eq. 31 to obtain change vector
		float3 r = (p1 - p0) / distance0 - QMul(q, e3);

		// apply eq. 37 to calc corrections
		float W_inv = stiffness / (w0 + w1 + 4.0f * wq * distance0 * distance0 + w_EPSILON);

		d0 += (w0 * W_inv * distance0) * r;
		d1 -= (w1 * W_inv * distance0) * r;
		dq += (wq * W_inv * distance0 * distance0 * 2.0f) * QMul(float4(r, 0), QMul(q, QInverse(float4(e3, 0))));
	}

	public static void ApplyReferenceBendTwist(
		in float4 darboux0, in float stiffness,
		in float w0, in float w1,
		ref float4 q0, ref float4 q1, bool debug = false)
	{
		float4 d0 = 0;
		float4 d1 = 0;
		SolveReferenceBendTwist(darboux0, stiffness, w0, w1, q0, q1, ref d0, ref d1, debug);
		q0 = normalize(q0 + d0);
		q1 = normalize(q1 + d1);
	}

	public static void ApplyReferenceStretchShear(
		in float distance0, in float stiffness,
		in float w0, in float w1, in float wq,
		ref float3 p0, ref float3 p1, ref float4 q, bool plainMul, float2 plainScale)
	{
		float3 d0 = 0;
		float3 d1 = 0;
		float4 dq = 0;
		SolveReferenceStretchShear(distance0, stiffness, w0, w1, wq, p0, p1, q, ref d0, ref d1, ref dq, plainMul, plainScale);
		p0 += d0;
		p1 += d1;
		q = normalize(q + dq);
	}

	public static void SolveEdgeVectorConstraint(
		in float3 v0, in float stiffness,
		in float w0, in float w1,
		in float3 p0, in float3 p1,
		ref float3 d0, ref float3 d1)
	{
		float3 r = (p0 + v0) - p1;

		float W_inv = stiffness / (w0 + w1 + w_EPSILON);

		d0 -= (w0* W_inv) * r;
		d1 += (w1* W_inv) * r;
	}

	public static void ApplyEdgeVectorConstraint(
		in float3 v0, in float stiffness,
		in float w0, in float w1,
		ref float3 p0, ref float3 p1)
	{
		float3 d0 = 0.0f;
		float3 d1 = 0.0f;
		SolveEdgeVectorConstraint(v0, stiffness, w0, w1, p0, p1, ref d0, ref d1);
		p0 += d0;
		p1 += d1;
	}

	public static void SolveDualEdgeVectorConstraint(
		in float3 v0, in float3 v1, in float stiffness,
		in float w0, in float w1, in float w2,
		in float3 p0, in float3 p1, in float3 p2,
		ref float3 d0, ref float3 d1, ref float3 d2)
	{
		float3 r = (p0 - 2.0f * p1 + p2 + v0 - v1);

		float W_inv = (1.0f * stiffness) / (w0 + 4.0f * w1 + w2 + w_EPSILON);

		d0 -= (w0* W_inv) * r;
		d1 += (w1* W_inv * 2.0f) * r;
		d2 -= (w2* W_inv) * r;
	}

	public static void ApplyDualEdgeVectorConstraint(
		in float3 v0, in float3 v1, in float stiffness,
		in float w0, in float w1, in float w2,
		ref float3 p0, ref float3 p1, ref float3 p2)
	{
		float3 d0 = 0.0f;
		float3 d1 = 0.0f;
		float3 d2 = 0.0f;
		SolveDualEdgeVectorConstraint(v0, v1, stiffness, w0, w1, w2, p0, p1, p2, ref d0, ref d1, ref d2);
		p0 += d0;
		p1 += d1;
		p2 += d2;
	}

	public static void SolveDistanceConstraint(
		in float distance0, in float stiffness,
		in float w0, in float w1,
		in float3 p0, in float3 p1,
		ref float3 d0, ref float3 d1)
	{
		//      d0                      d1
		//    .----.                  .----.
		// p0 ------><--------------><------ p1
		//           \______________/
		//               distance0

		float3 r = p1 - p0;
		float rd_inv = max(0.0f, rsqrt(dot(r, r)));

		float delta = 1.0f - (distance0 * rd_inv);
		float W_inv = (delta * stiffness) / (w0 + w1 + w_EPSILON);

		d0 += (w0* W_inv) * r;
		d1 -= (w1* W_inv) * r;
	}

	public static void ApplyDistanceConstraint(in float distance0, in float stiffness, in float w0, in float w1, ref float3 p0, ref float3 p1)
	{
		float3 d0 = 0.0f;
		float3 d1 = 0.0f;
		SolveDistanceConstraint(distance0, stiffness, w0, w1, p0, p1, ref d0, ref d1);
		p0 += d0;
		p1 += d1;
	}
}

public static class Quat
{
	public static float4 QConjugate(float4 q)
	{
		return q * float4(-1.0f, -1.0f, -1.0f, 1.0f);
	}

	public static float4 QInverse(float4 q)
	{
		return QConjugate(q) * rcp(dot(q, q));
	}

	public static float4 QMul(float4 a, float4 b)
	{
		float4 q = float4(0,0,0,0);
		q.xyz = a.w * b.xyz + b.w * a.xyz + cross(a.xyz, b.xyz);
		q.w = a.w * b.w - dot(a.xyz, b.xyz);
		return q;
	}

	public static float3 QMul(float4 q, float3 v)
	{
		float3 t = 2.0f * cross(q.xyz, v);
		return v + q.w * t + cross(q.xyz, t);
	}

	public static float4 MakeQuaternion(Quaternion q)
	{
		return float4(q.x, q.y, q.z, q.w);
	}

	public static float4 QNlerp(float4 a, float4 b, float t)
	{
		float d = dot(a, b);
		if (d < 0.0)
		{
			b = -b;
		}

		return normalize(lerp(a, b, t));
	}

	public static float4 QSlerp(float4 a, float4 b, float t)
	{
		float d = dot(a, b);
		if (d < 0.0)
		{
			d = -d;
			b = -b;
		}

		if (d < (1.0 - 1e-5))
		{
			float2 w = sin(acos(d) * float2(1.0f - t, t)) * rsqrt(1.0f - d * d);
			return a * w.x + b * w.y;
		}
		else
		{
			return normalize(lerp(a, b, t));
		}
	}

	public static float4 MakeQuaternionIdentity()
	{
		return float4(0.0f, 0.0f, 0.0f, 1.0f);
	}

	public static float4 MakeQuaternionTwistIdentity()
	{
		//return MakeQuaternionIdentity();
		return float4(0.0f, 1.0f, 0.0f, 0.0f);
	}

	public static float4 MakeQuaternionFromAxisAngle(float3 axis, float angle)
	{
		float sina = sin(0.5f * angle);
		float cosa = cos(0.5f * angle);
		return float4(axis * sina, cosa);
	}

	public static float4 MakeQuaternionFromTo(float3 u, float3 v)
	{
		float4 q = 0;
		float s = 1.0f + dot(u, v);
		if (s < 1e-7)// if 'u' and 'v' are parallel opposing
		{
			q.xyz = abs(u.x) > abs(u.z) ? float3(-u.y, u.x, 0.0f) : float3(0.0f, -u.z, u.y);
			q.w = 0.0f;
		}
		else
		{
			q.xyz = cross(u, v);
			q.w = s;
		}
		return normalize(q);
	}

	public static float4 MakeQuaternionFromToWithFallback(float3 u, float3 v, float3 w)
	{
		float4 q = 0;
		float s = 1.0f + dot(u, v);
		if (s < 1e-7)// if 'u' and 'v' are parallel opposing
		{
			q.xyz = w;
			q.w = 0.0f;
		}
		else
		{
			q.xyz = cross(u, v);
			q.w = s;
		}
		return normalize(q);
	}

	public static float4 MakeQuaternionFromBend(float3 p0, float3 p1, float3 p2)
	{
		float3 u = normalize(p1 - p0);
		float3 v = normalize(p2 - p1);
		return MakeQuaternionFromTo(u, v);
	}

	public static float4 NextQuaternionFromBend(float3 p0, float3 p1, float3 p2, float4 q1)
	{
#if DEBUG_BEND_RMF
		return NextQuaternionFromBendRMF(p0, p1, p2, q1);
#else
		return NextQuaternionFromBendSA(p0, p1, p2, q1);
#endif
	}

	public static float4 NextQuaternionFromBendSA(float3 p0, float3 p1, float3 p2, float4 q1)
	{
		float3 u = QMul(q1, float3(0, 1, 0));
		float3 v = normalize(p2 - p1);
		//u = normalize(p1 - p0);

		float4 rotTangent = MakeQuaternionFromToWithFallback(u, v, QMul(q1, float3(1, 0, 0)));
		float4 rotTwist = MakeQuaternionTwistIdentity();
		//rotTwist = MakeQuaternionIdentity();

		return normalize(QMul(rotTangent, QMul(q1, rotTwist)));

		//return QMul(MakeQuaternionFromToWithFallback(u, v, QMul(q1, float3(1, 0, 0))), q1);
	}

	public static float4 NextQuaternionFromBendRMF(float3 p0, float3 p1, float3 p2, float4 q1)
	{
		// see: "Computation of Rotation Minimizing Frames" by W. Wang, B. Jüttler, D. Zheng and Y. Liu
		// https://www.microsoft.com/en-us/research/wp-content/uploads/2016/12/Computation-of-rotation-minimizing-frames.pdf

		float3 localNormal = float3(0, 0, 1);
		float3 localTangent = float3(0, 1, 0);
		float3 localBitangent = float3(1, 0, 0);

		float3 v1 = normalize(p2 - p1);
		float3 ri = QMul(q1, localBitangent);
		float3 ti = QMul(q1, localTangent);// normalize(p1 - p0);

		float3 rLi = reflect(ri, v1);
		float3 tLi = reflect(ti, v1);

		float3 t2 = v1;
		float3 v2 = normalize(t2 - tLi);
		float3 r2 = reflect(rLi, v2);
		float3 s2 = cross(r2, t2);

#if true
		// build frame
		float4 rotTangent = MakeQuaternionFromToWithFallback(localTangent, t2, ri);
		float4 rotTangentTwist = MakeQuaternionFromToWithFallback(QMul(rotTangent, localNormal), -s2, t2);
		return QMul(rotTangentTwist, rotTangent);
#else
		// rotate existing frame
		float4 rotTangent = MakeQuaternionFromToWithFallback(ti, t2, ri);
		q1 = QMul(rotTangent, q1);
		float4 rotTangentTwist = MakeQuaternionFromToWithFallback(QMul(q1, localBitangent), r2, t2);
		q1 = QMul(rotTangentTwist, q1);
		return q1;
#endif
	}

	public static float4 MakeQuaternionLookAt(float3 forward, float3 up)
	{
		float3 localForward = float3(0.0f, 0.0f, 1.0f);
		float3 localUp = float3(0.0f, 1.0f, 0.0f);

		float4 rotForward = MakeQuaternionFromTo(localForward, forward);
		float4 rotForwardTwist = MakeQuaternionFromToWithFallback(QMul(rotForward, localUp), up, forward);

		return QMul(rotForwardTwist, rotForward);
	}
}
