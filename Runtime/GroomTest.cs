using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Formats.Alembic.Importer;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

public class GroomTest : MonoBehaviour
{
	public AlembicCurves curveSet;

	public int debugIndex;
	public int debugCount;

	public int debugGroupSize;

	void OnDrawGizmos()
	{
		if (curveSet != null)
		{
			GizmosDrawCurves(curveSet);
		}
	}

	void GizmosDrawCurves(AlembicCurves curveSet)
	{
		var bufferPos = curveSet.Positions;
		var bufferPosOffset = curveSet.PositionsOffsetBuffer;

		int curveCount = bufferPosOffset.Count;
		if (curveCount == 0)
			return;

		int curveLength = bufferPos.Count / curveCount;
		if (curveLength < 2)
			return;

		debugIndex = Mathf.Clamp(debugIndex, 0, curveCount - 1);
		debugCount = Mathf.Clamp(debugCount, 0, curveCount - debugIndex);

		if (debugIndex >= 0 && debugCount > 0)
		{
			var oldColor = Gizmos.color;

			for (int k = 0; k != debugCount; k++)
			{
				int rootIndex = debugIndex + k;
				int rootOffset = bufferPosOffset[rootIndex];

				if (debugGroupSize > 0 && rootIndex % debugGroupSize == 0)
				{
					var t = (rootIndex / debugGroupSize) / 7.0f;
					Gizmos.color = Color.HSVToRGB(t - Mathf.Floor(t), 1.0f, 1.0f);
				}

				for (int i = 1; i != curveLength; i++)
				{
					Gizmos.DrawLine(bufferPos[rootOffset + i - 1], bufferPos[rootOffset + i]);
				}
			}

			Gizmos.color = oldColor;
		}
	}
}
