using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.Build;
using UnityEditor.Rendering;

namespace Unity.DemoTeam.Hair
{
	public class HairShaderVariantStripper : IPreprocessShaders
	{
		ShaderKeyword HAIR_VERTEX_LIVE;
		ShaderKeyword HAIR_VERTEX_STATIC;
		ShaderKeyword PROCEDURAL_INSTANCING_ON;

		public HairShaderVariantStripper()
		{
			HAIR_VERTEX_LIVE = new ShaderKeyword(nameof(HAIR_VERTEX_LIVE));
			HAIR_VERTEX_STATIC = new ShaderKeyword(nameof(HAIR_VERTEX_STATIC));
			PROCEDURAL_INSTANCING_ON = new ShaderKeyword(nameof(PROCEDURAL_INSTANCING_ON));
		}

		public int callbackOrder { get; }

		public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
		{
			for (int i = data.Count - 1; i >= 0; i--)
			{
				var keys = data[i].shaderKeywordSet;
				if (keys.IsEnabled(HAIR_VERTEX_STATIC))
				{
					data.RemoveAt(i);
					continue;
				}
#if false
				if (keys.IsEnabled(HAIR_VERTEX_LIVE))
				{
					if (keys.IsEnabled(PROCEDURAL_INSTANCING_ON) == false)
					{
						data.RemoveAt(i);
						continue;
					}
				}
#endif
			}
		}
	}
}
