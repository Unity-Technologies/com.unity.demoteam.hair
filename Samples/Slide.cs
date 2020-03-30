using UnityEngine;

[ExecuteInEditMode]
public class Slide : MonoBehaviour
{
	public float position = 0.0f;

	public Vector3 extent;

	[Min(float.Epsilon)]
	public float period;// seconds

	private void LateUpdate()
	{
		float dt = Time.deltaTime;

		position += dt / period;

		if (float.IsNaN(position) || float.IsInfinity(position))
			position = 0.0f;

		transform.localPosition = extent * Mathf.Sin(position * Mathf.PI * 2.0f);
	}
}
