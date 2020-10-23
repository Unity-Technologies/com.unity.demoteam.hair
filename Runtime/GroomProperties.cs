using UnityEngine;

namespace Unity.DemoTeam.Hair
{
	[CreateAssetMenu]
	public class GroomProperties : ScriptableObject
	{
		public HairSim.StrandSettings settingsStrand;
		public HairSim.SolverSettings settingsSolver = HairSim.SolverSettings.defaults;
		public HairSim.VolumeSettings settingsVolume = HairSim.VolumeSettings.defaults;
	}
}
