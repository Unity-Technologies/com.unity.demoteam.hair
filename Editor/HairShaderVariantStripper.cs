using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.Build;
using UnityEditor.Rendering;

namespace Unity.DemoTeam.Hair
{
	public class HairShaderVariantStripper : IPreprocessShaders
	{
		ShaderKeyword HAIR_VERTEX_STATIC;

		public HairShaderVariantStripper()
		{
			HAIR_VERTEX_STATIC = new ShaderKeyword("HAIR_VERTEX_STATIC");
		}

		public int callbackOrder { get; }

		public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
		{
			for (int i = data.Count - 1; i >= 0; i--)
			{
				if (data[i].shaderKeywordSet.IsEnabled(HAIR_VERTEX_STATIC))
					data.RemoveAt(i);
			}
		}
	}
}
