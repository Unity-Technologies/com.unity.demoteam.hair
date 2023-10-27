using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.DemoTeam.Hair
{
    public class HairShaderVariantStripper : IPreprocessShaders
    {
        public int callbackOrder { get; }
        
        ShaderKeyword m_HairVertexStaticVariant;

        public HairShaderVariantStripper()
        {
            // NOTE: Must be kept in sync with the variant defined in HairVertex sub-graph.
            m_HairVertexStaticVariant = new ShaderKeyword("HAIR_VERTEX_STATIC");
        }
        
        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            for (int i = 0; i < data.Count; ++i)
            {
                if (data[i].shaderKeywordSet.IsEnabled(m_HairVertexStaticVariant))
                {
                    data.RemoveAt(i);
                    --i;
                }
            }
        }
    }
}