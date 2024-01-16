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
			if (versionInitial < ctx.VERSION)
			{
				Debug.Log(string.Format("{0} ({1}): starting migration...", ctx.GetType().Name, ctx.name, ctx.version, ctx.VERSION), ctx);
			}
			else
			{
				return;
			}

#if UNITY_EDITOR
			var isPrefabInstance = UnityEditor.PrefabUtility.IsPartOfPrefabInstance(ctx);
			if (isPrefabInstance)
			{
				var prefabPath = UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(ctx);
				{
					Debug.Log(string.Format("{0} ({1}): rebuilding governing prefab '{1}'...", ctx.GetType().Name, ctx.name, prefabPath), ctx);

					using (var prefabEditScope = new UnityEditor.PrefabUtility.EditPrefabContentsScope(prefabPath))
					{
						foreach (var prefabVersionedData in prefabEditScope.prefabContentsRoot.GetComponentsInChildren<T>(includeInactive: true))
						{
							HandleVersionChange(prefabVersionedData);
						}
					}
				}

				// ensure that version is not fetched from prefab
				var serializedObject = new UnityEditor.SerializedObject(ctx);
				{
					serializedObject.FindProperty(nameof(ctx.version)).intValue = versionInitial;
					serializedObject.ApplyModifiedPropertiesWithoutUndo();
				}
			}
#endif

			while (ctx.version < ctx.VERSION)
			{
				Debug.Log(string.Format("{0} ({1}): migrating data from version {2}...", ctx.GetType().Name, ctx.name, ctx.version), ctx);
				var versionCurrent = ctx.version;
				{
					ctx.PerformMigrationStep();
				}
				if (versionCurrent == ctx.version)
				{
					Debug.LogError(string.Format("{0} ({1}): failed to migrate data from version {2} -> {3} (remains at version {2})", ctx.GetType().Name, ctx.name, ctx.version, ctx.VERSION), ctx);
					break;
				}
			}

			if (versionInitial < ctx.version)
			{
				Debug.Log(string.Format("{0} ({1}): completed migration", ctx.GetType().Name, ctx.name, ctx.version), ctx);
#if UNITY_EDITOR
				UnityEditor.EditorUtility.SetDirty(ctx);
				UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(ctx);
#endif
			}
		}
	}
}
