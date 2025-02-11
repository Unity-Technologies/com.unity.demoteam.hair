using System.Runtime.CompilerServices;
using UnityEngine;
using Unity.Mathematics;
using static Unity.DemoTeam.Hair.AffineUtility;

#if true
public class AffineInterpolate : MonoBehaviour
{
	public Transform transformA;
	public Transform transformB;

	[Range(3, 100)]
	public int samples = 3;

	public Transform contact;

	private void OnDrawGizmos()
	{
		if (transformA == null || transformB == null)
			return;

		// Ma * M = Mb
		// M = Ma^-1 Mb
		var Ma = (float4x4)transformA.localToWorldMatrix;// curr
		var Mb = (float4x4)transformB.localToWorldMatrix;// prev

		var M = math.mul(AffineInverse4x4(Ma), Mb);
		var q = svd.svdRotation((float3x3)M);

		for (int i = 0; i != samples; i++)
		{
			var t = (float)i / (samples - 1);
			var t_prev = (float)(i - 1) / (samples - 1);

			var M_t = AffineInterpolate4x4(M, q.value, t);
			var M_t_prev = AffineInterpolate4x4(M, q.value, t_prev);

			var Mb_t = AffineMul4x4(Ma, M_t);
			var Mb_t_prev = AffineMul4x4(Ma, M_t_prev);

			Gizmos.color = Color.Lerp(Color.Lerp(Color.clear, Color.red, 0.75f), Color.Lerp(Color.clear, Color.green, 0.75f), t);
			Gizmos.matrix = Mb_t;
			Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
			Gizmos.color = Color.red;
			Gizmos.DrawRay(-0.6f * Vector3.one, 0.25f * Vector3.right);
			Gizmos.color = Color.green;
			Gizmos.DrawRay(-0.6f * Vector3.one, 0.25f * Vector3.up);
			Gizmos.color = Color.blue;
			Gizmos.DrawRay(-0.6f * Vector3.one, 0.25f * Vector3.forward);
		}

		if (contact == null)
			return;

		// x = Ma^-1 * xa
		// x(t) = M(t) * Ma^-1 * xa
		var x = math.transform(AffineInverse4x4(Ma), contact.position);

		for (int i = 1; i != samples; i++)
		{
			var t = (float)i / (samples - 1);
			var t_prev = (float)(i - 1) / (samples - 1);

			var M_t = AffineInterpolate4x4(M, q.value, t);
			var M_t_prev = AffineInterpolate4x4(M, q.value, t_prev);

			var Mb_t = AffineMul4x4(Ma, M_t);
			var Mb_t_prev = AffineMul4x4(Ma, M_t_prev);

			var Mb_t_inv = AffineInverse4x4(Mb_t);
			var Mb_t_inv_step = AffineMul4x4(Mb_t_prev, Mb_t_inv);

			var x_t = math.transform(Mb_t, x);
			var x_t_prev = math.transform(Mb_t_inv_step, x_t);

			Gizmos.matrix = Matrix4x4.identity;
			Gizmos.color = Color.Lerp(Color.magenta, Color.yellow, t);
			Gizmos.DrawLine(x_t, x_t_prev);
		}
	}
}
#endif
