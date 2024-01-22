using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.DemoTeam.Hair
{
#if UNITY_EDITOR
	public static class PrefabContentsUtility
	{
		public delegate void FnUpdatePrefabContents(GameObject prefabContentsRoot);

		struct UpdateUnderlyingPrefabScope : IDisposable
		{
			public UnityEngine.Object prefabInstanceMember;
			public string prefabPath;
			public bool verbose;

			public UpdateUnderlyingPrefabScope(UnityEngine.Object prefabInstanceMember, bool verbose)
			{
				this.prefabInstanceMember = prefabInstanceMember;
				this.prefabPath = UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabInstanceMember);
				this.verbose = verbose;

				if (verbose)
				{
					Debug.Log(string.Format("{0} ({1}): entering prefab '{2}'...", prefabInstanceMember.GetType().Name, prefabInstanceMember.name, prefabPath), prefabInstanceMember);
				}
			}

			public void Dispose()
			{
				if (verbose)
				{
					Debug.Log(string.Format("{0} ({1}): exiting prefab '{2}'...", prefabInstanceMember.GetType().Name, prefabInstanceMember.name, prefabPath), prefabInstanceMember);
				}
			}
		}

		public static void UpdateUnderlyingPrefabContents(UnityEngine.Object prefabInstanceMember, bool verbose, FnUpdatePrefabContents fnUpdatePrefabContents)
		{
			using (var prefabScope = new UpdateUnderlyingPrefabScope(prefabInstanceMember, verbose))
			{
				UpdateUnderlyingPrefabContents(prefabScope.prefabPath, fnUpdatePrefabContents);
			}
		}

		public static void UpdateUnderlyingPrefabContents(string prefabPath, FnUpdatePrefabContents fnUpdatePrefabContents)
		{
#if UNITY_2021_2_OR_NEWER
			var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
#else
			var prefabStage = UnityEditor.Experimental.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
#endif
			if (prefabStage != null && prefabStage.assetPath == prefabPath)
			{
				// update contents of prefab in prefab stage
				fnUpdatePrefabContents(prefabStage.prefabContentsRoot);

				// force a save to ensure that prefab instances receive changes
				{
					UnityEditor.PrefabUtility.SaveAsPrefabAsset(prefabStage.prefabContentsRoot, prefabPath);
					prefabStage.ClearDirtiness();
				}
			}
			else
			{
				using (var prefabEditScope = new UnityEditor.PrefabUtility.EditPrefabContentsScope(prefabPath))
				{
					// update contents of prefab on disk
					fnUpdatePrefabContents(prefabEditScope.prefabContentsRoot);
				}
			}
		}
	}
#endif
}
