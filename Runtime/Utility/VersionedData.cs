using UnityEngine;

namespace Unity.DemoTeam.Hair
{
	public interface IVersionedDataContext
	{
		// should return version of data
		int version { get; }

		// should return version of implementation (constant)
		int VERSION { get; }

		// should increment version of data towards version of implementation
		void PerformMigrationStep();
	}

	public static class VersionedDataUtility
	{
		public static void HandleVersionChange<T>(T ctx) where T : UnityEngine.Object, IVersionedDataContext
		{
			var versionInitial = ctx.version;
			if (versionInitial != ctx.VERSION)
			{
				Debug.Log(string.Format("{0} ({1}): starting migration...", ctx.GetType().Name, ctx.name, ctx.version, ctx.VERSION), ctx);
			}
			while (ctx.VERSION > ctx.version)
			{
				Debug.Log(string.Format("{0} ({1}): migrating from version {2}...", ctx.GetType().Name, ctx.name, ctx.version), ctx);
				var versionCurrent = ctx.version;
				{
					ctx.PerformMigrationStep();
				}
				if (versionCurrent == ctx.version)
				{
					Debug.LogError(string.Format("{0} ({1}): failed to migrate from version {2} -> {3} (asset remains at version {2})", ctx.GetType().Name, ctx.name, ctx.version, ctx.VERSION), ctx);
					break;
				}
			}
			if (versionInitial != ctx.version)
			{
				Debug.Log(string.Format("{0} ({1}): completed migration", ctx.GetType().Name, ctx.name), ctx);
#if UNITY_EDITOR
				UnityEditor.EditorUtility.SetDirty(ctx);
#endif
			}
		}
	}
}
