using UnityEngine;

namespace Unity.DemoTeam.Hair
{
	public interface IVersionedDataContainer
	{
		// must return version of data
		int version { get; }

		// must return version of implementation (code)
		int VERSION { get; }

		// must attempt to increment version of data towards version of implementation
		void TryIncrementVersion();
	}

	public static class VersionedDataUtility
	{
		public static void HandleVersionChange<T>(T ctx) where T : UnityEngine.Object, IVersionedDataContainer
		{
			var versionInitial = ctx.version;
			while (ctx.VERSION > ctx.version)
			{
				Debug.Log(string.Format("{0}: migrating from version {1}...", ctx.name, ctx.version), ctx);
				var versionCurrent = ctx.version;
				{
					ctx.TryIncrementVersion();
				}
				if (versionCurrent == ctx.version)
				{
					Debug.LogError(string.Format("{0}: failed to migrate from version {1} -> {2} (asset remains at version {1})", ctx.name, ctx.version, ctx.VERSION), ctx);
					break;
				}
			}
			if (versionInitial != ctx.version)
			{
				// data version changed, force re-serialization?
			}
		}
	}
}
