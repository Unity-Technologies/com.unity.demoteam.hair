using System;
using UnityEngine;

[ExecuteAlways]
public class Groom : MonoBehaviour
{
	public GroomAsset groomAsset;
	public GameObject[] groomContainers;

	public Hash128 groomChecksum = new Hash128();

	void Update()
	{
		if (groomAsset != null)
		{
			if (groomChecksum != groomAsset.checksum)
			{
				GroomUtility.BuildGroomInstance(this, groomAsset);
				groomChecksum = groomAsset.checksum;
			}
		}
		else
		{
			GroomUtility.ClearGroomInstance(this);
			groomChecksum = new Hash128();
		}
	}
}

//move to GroomEditorUtility ?
public static class GroomUtility
{
	public static void ClearGroomInstance(Groom groom)
	{
		if (groom.groomContainers == null)
			return;

		foreach (var groomObject in groom.groomContainers)
		{
			if (groomObject != null)
			{
				GameObject.DestroyImmediate(groomObject);
			}
		}

		groom.groomContainers = null;
	}

	public static void BuildGroomInstance(Groom groom, GroomAsset groomAsset)
	{
		ClearGroomInstance(groom);

		var strandGroups = groomAsset.strandGroups;
		if (strandGroups == null)
			return;

		// prep groom containers
		groom.groomContainers = new GameObject[strandGroups.Length];

		// build groom containers
		for (int i = 0; i != strandGroups.Length; i++)
		{
			var groupObject = new GameObject();
			{
				groupObject.name = "Group:" + i;
				groupObject.transform.SetParent(groom.transform, worldPositionStays: false);

				var linesContainer = new GameObject();
				{
					linesContainer.name = "Lines:" + i;
					linesContainer.transform.SetParent(groupObject.transform, worldPositionStays: false);

					var lineFilter = linesContainer.AddComponent<MeshFilter>();
					{
						lineFilter.sharedMesh = strandGroups[i].meshAssetLines;
					}

					var lineRenderer = linesContainer.AddComponent<MeshRenderer>();
					{
						lineRenderer.sharedMaterial = groomAsset.settings.defaultMaterial;
					}
				}

				var rootsContainer = new GameObject();
				{
					rootsContainer.name = "Roots:" + i;
					rootsContainer.transform.SetParent(groupObject.transform, worldPositionStays: false);

					var rootFilter = rootsContainer.AddComponent<MeshFilter>();
					{
						rootFilter.sharedMesh = strandGroups[i].meshAssetRoots;
					}

					//var rootAttachment = rootObject.AddComponent<SkinAttachment>();
				}
			}

			groom.groomContainers[i] = groupObject;
		}
	}
}
