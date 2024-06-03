using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.DemoTeam.Hair
{
	//TODO either add something like this or a more automatic build-time approach
	/*
	public class HairTopologyPrefetch : MonoBehaviour
	{
		public bool onAwake = true;
		public bool onUpdate = true;

		public HairTopologyDesc[] descriptions;

		void Fetch(bool cond) { if (cond) Fetch(); }
		void Fetch()
		{
			if (descriptions != null)
				return;

			for (int i = 0; i != descriptions.Length; i++)
			{
				var mesh = HairTopologyCache.GetSharedMesh(descriptions[i]);
			}
		}

		void Awake() => Fetch(onAwake);
		void Update() => Fetch(onUpdate);
	}
	*/
}
