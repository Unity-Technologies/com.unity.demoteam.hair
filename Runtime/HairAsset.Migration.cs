using UnityEngine;

namespace Unity.DemoTeam.Hair
{
	public partial class HairAsset : IVersionedDataContext
	{
		const int VERSION_IMPL = 2;

		[field: SerializeField]
		public int version { get; private set; } = -1;
		public int VERSION => VERSION_IMPL;

		public void PerformMigrationStep()
		{
			switch (version)
			{
				case 0:
					PerformMigration_0();
					version = VERSION_IMPL;
					break;

				case 1:
					PerformMigration_1();
					version = VERSION_IMPL;
					break;

				case VERSION_IMPL:
					// at latest version
					break;
			}
		}
	}
}
