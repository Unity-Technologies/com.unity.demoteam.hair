using UnityEngine;

[ExecuteInEditMode]
public class Orbit : MonoBehaviour
{
	public float positionPitch = 0.0f;
	public float positionYaw = 0.0f;

	public float extentPitch = 25.0f;

	[Min(float.Epsilon)] public float periodPitch = 1.0f;// seconds
	[Min(float.Epsilon)] public float periodYaw = 10.0f;// yaw

	private void LateUpdate()
	{
		float dt = Time.deltaTime;

		positionPitch += dt / periodPitch;
		positionPitch -= Mathf.Floor(positionPitch);

		positionYaw += dt / periodYaw;
		positionYaw -= Mathf.Floor(positionYaw);

		if (float.IsNaN(positionPitch))
			positionPitch = 0.0f;
		if (float.IsNaN(positionYaw))
			positionYaw = 0.0f;

		float degreesPitch = extentPitch * Mathf.Sin(positionPitch * Mathf.PI * 2.0f);
		float degreesYaw = 360.0f * positionYaw;

		transform.localRotation = Quaternion.Euler(degreesPitch, degreesYaw, 0.0f);
	}
}
