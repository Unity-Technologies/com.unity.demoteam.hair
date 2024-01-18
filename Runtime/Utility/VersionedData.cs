using UnityEngine;

namespace Unity.DemoTeam.Hair
{
	public interface IVersionedDataContext
	{
		// should return version of data (should return negative value if version of data is still pending)
		int version { get; }

		// should return version of implementation (constant)
		int VERSION { get; }

		// should increment version of data towards version of implementation
		void PerformMigrationStep();
	}

	public static class VersionedDataUtility
	{
		static readonly string versionPropertyPath = string.Format("<{0}>k__BackingField", nameof(IVersionedDataContext.version));

		public static void HandleVersionChangeOnValidate<T>(T ctx) where T : UnityEngine.Object, IVersionedDataContext
		{
#if UNITY_EDITOR
			UnityEditor.EditorApplication.delayCall += () =>
			{
				if (ctx != null)
				{
					HandleVersionChange(ctx);
				}
			};
#else
			HandleVersionChange(ctx);
#endif
		}

		public static void HandleVersionChange<T>(T ctx) where T : UnityEngine.Object, IVersionedDataContext
		{
			var versionIni = ctx.version;
			if (versionIni == ctx.VERSION || versionIni < 0)
				return;

			var nameType = ctx.GetType().Name;
			var nameObject = ctx.name;

			Debug.Log(string.Format("{0} ({1}): starting migration...", nameType, nameObject), ctx);

#if UNITY_EDITOR
			// if this is part of a prefab instance, then we need to first handle version changes in underlying prefab
			var isPrefabInstance = UnityEditor.PrefabUtility.IsPartOfPrefabInstance(ctx);
			if (isPrefabInstance)
			{
				// handle version changes in underlying prefab
				Debug.Log(string.Format("{0} ({1}): migrating underlying prefab...", nameType, nameObject), ctx);

				PrefabContentsUtility.UpdateUnderlyingPrefabContents(ctx, verbose: true, (GameObject prefabContentsRoot) =>
				{
					foreach (var prefabVersionedDataContext in prefabContentsRoot.GetComponentsInChildren<T>(includeInactive: true))
					{
						HandleVersionChange(prefabVersionedDataContext);
					}
				});

				// ensure that own version is not inherited from prefab
				var serializedObject = new UnityEditor.SerializedObject(ctx);
				{
					serializedObject.FindProperty(versionPropertyPath).intValue = versionIni;
					serializedObject.ApplyModifiedPropertiesWithoutUndo();
				}
			}
#endif

			// handle version changes
			while (ctx.version < ctx.VERSION)
			{
				Debug.Log(string.Format("{0} ({1}): migrating data from version {2}...", nameType, nameObject, ctx.version), ctx);
				var versionPre = ctx.version;
				{
					ctx.PerformMigrationStep();
				}
				if (versionPre == ctx.version)
				{
					Debug.LogError(string.Format("{0} ({1}): failed to migrate data from version {2} -> {3} (remains at version {2})", nameType, nameObject, ctx.version, ctx.VERSION), ctx);
					break;
				}
			}
			
			if (versionIni < ctx.version)
			{
#if UNITY_EDITOR
				UnityEditor.EditorUtility.SetDirty(ctx);
				UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(ctx);
#endif
				if (ctx.version == ctx.VERSION)
				{
					Debug.Log(string.Format("{0} ({1}): completed migration (now at version {2})", nameType, nameObject, ctx.version), ctx);
				}
			}
		}
	}
}
