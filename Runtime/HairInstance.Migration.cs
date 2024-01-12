using UnityEngine;

namespace Unity.DemoTeam.Hair
{
	public partial class HairInstance : IVersionedDataContainer
	{
		const int VERSION_IMPL = 1;

		[field: SerializeField]
		public int version { get; private set; } = VERSION_IMPL;
		public int VERSION => VERSION_IMPL;

		public void TryIncrementVersion()
		{
			switch (version)
			{
				case 0:
					PerformMigration_0();
					version = 1;
					break;

				case 1:
					// latest
					break;
			}
		}
	}
}
