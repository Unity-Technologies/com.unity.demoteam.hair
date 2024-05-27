using UnityEngine;
using Unity.Mathematics;

namespace Unity.DemoTeam.Hair
{
	public static class AffineUtility
	{
		public static float3x3 AffineInterpolateUpper3x3(float3x3 A, float4 q, float t)
		{
			static float3x3 lerp(float3x3 a, float3x3 b, float t) => math.float3x3(
				math.lerp(a.c0, b.c0, t),
				math.lerp(a.c1, b.c1, t),
				math.lerp(a.c2, b.c2, t));

			// A = QR
			// Q^-1 A = R

			float3x3 Q_inv = math.float3x3(math.conjugate(q));
			float3x3 R = math.mul(Q_inv, A);
			float3x3 I = math.float3x3(
				1.0f, 0.0f, 0.0f,
				0.0f, 1.0f, 0.0f,
				0.0f, 0.0f, 1.0f);

			float3x3 Q_t = math.float3x3(math.slerp(quaternion.identity, q, t));
			float3x3 R_t = lerp(I, R, t);
			float3x3 A_t = math.mul(Q_t, R_t);

			return A_t;
		}

		public static float3x4 AffineInterpolate3x4(float3x4 M, float4 q, float t)
		{
			// M = | A T |

			float3x3 A_t = AffineInterpolateUpper3x3(math.float3x3(M.c0, M.c1, M.c2), q, t);
			float3 T_t = M.c3 * t;

			return math.float3x4(
				A_t.c0.x, A_t.c1.x, A_t.c2.x, T_t.x,
				A_t.c0.y, A_t.c1.y, A_t.c2.y, T_t.y,
				A_t.c0.z, A_t.c1.z, A_t.c2.z, T_t.z);
		}

		public static float4x4 AffineInterpolate4x4(float4x4 M, float4 q, float t)
		{
			// M = | A T |
			//     | 0 1 |

			float3x3 A_t = AffineInterpolateUpper3x3((float3x3)M, q, t);
			float3 T_t = M.c3.xyz * t;

			return math.float4x4(
				A_t.c0.x, A_t.c1.x, A_t.c2.x, T_t.x,
				A_t.c0.y, A_t.c1.y, A_t.c2.y, T_t.y,
				A_t.c0.z, A_t.c1.z, A_t.c2.z, T_t.z,
				0.0f, 0.0f, 0.0f, 1.0f);
		}

		public static float3x3 AffineInverseUpper3x3(float3x3 A)
		{
			float3 c0 = A.c0;
			float3 c1 = A.c1;
			float3 c2 = A.c2;

			float3 cp0x1 = math.cross(c0, c1);
			float3 cp1x2 = math.cross(c1, c2);
			float3 cp2x0 = math.cross(c2, c0);

			return math.float3x3(cp1x2, cp2x0, cp0x1) / math.dot(c0, cp1x2);
		}

		public static float4x4 AffineInverse4x4(float4x4 M)
		{
			// | A T |
			// | 0 1 |

			float3x3 A_inv = AffineInverseUpper3x3((float3x3)M);
			float3 T_inv = -math.mul(A_inv, M.c3.xyz);

			return math.float4x4(
				A_inv.c0.x, A_inv.c1.x, A_inv.c2.x, T_inv.x,
				A_inv.c0.y, A_inv.c1.y, A_inv.c2.y, T_inv.y,
				A_inv.c0.z, A_inv.c1.z, A_inv.c2.z, T_inv.z,
				0.0f, 0.0f, 0.0f, 1.0f);
		}

		public static float3x4 AffineMul3x4(float3x4 Ma, float3x4 Mb)
		{
			// Ma x Mb  =  | A Ta |  x  | B Tb |
			//             | 0 1  |     | 0 1  |
			//
			//          =  | mul(A,B)  mul(A,Tb)+Ta |
			//             | 0         1            |

			float3x3 A = math.float3x3(Ma.c0, Ma.c1, Ma.c2);
			float3x3 B = math.float3x3(Mb.c0, Mb.c1, Mb.c3);

			float3x3 AB = math.mul(A, B);
			float3 ATb = math.mul(A, Mb.c3);
			float3 Ta = Ma.c3;

			return math.float3x4(
				AB.c0.x, AB.c1.x, AB.c2.x, ATb.x + Ta.x,
				AB.c0.y, AB.c1.y, AB.c2.y, ATb.y + Ta.y,
				AB.c0.z, AB.c1.z, AB.c2.z, ATb.z + Ta.z);
		}

		public static float4x4 AffineMul4x4(float4x4 Ma, float4x4 Mb)
		{
			return math.mul(Ma, Mb);
		}
	}
}
