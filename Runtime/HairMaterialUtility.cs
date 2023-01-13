using System;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering;

#if HAS_PACKAGE_UNITY_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif
#if HAS_PACKAGE_UNITY_URP
using UnityEngine.Rendering.Universal;
#endif

namespace Unity.DemoTeam.Hair
{
	public static class HairMaterialUtility
	{
		static Material[] s_defaultMaterial = new Material[Enum.GetNames(typeof(PipelineLabel)).Length];

		public enum PipelineLabel
		{
			Builtin,
			Custom,
			HDRP,
			URP,
		}

		public static PipelineLabel GetPipelineLabel(RenderPipeline pipeline)
		{
			if (pipeline != null)
			{
#if HAS_PACKAGE_UNITY_HDRP
				if (pipeline is HDRenderPipeline)
					return PipelineLabel.HDRP;
#endif
#if HAS_PACKAGE_UNITY_URP
				if (pipeline is UniversalRenderPipeline)
					return PipelineLabel.URP;
#endif

				return PipelineLabel.Custom;
			}

			return PipelineLabel.Builtin;
		}

		public static Shader GetPipelineDefaultShader(RenderPipeline pipeline) => GetPipelineDefaultShader(GetPipelineLabel(pipeline));
		public static Shader GetPipelineDefaultShader(PipelineLabel pipelineLabel)
		{
			switch (pipelineLabel)
			{
				case PipelineLabel.Builtin: return HairSimResources.Load().defaultBuiltin;
				case PipelineLabel.Custom: return HairSimResources.Load().defaultCustom;
				case PipelineLabel.HDRP: return HairSimResources.Load().defaultHDRP;
				case PipelineLabel.URP: return HairSimResources.Load().defaultURP;
			}

			return HairSimResources.Load().defaultCustom;
		}

		public static Material GetPipelineDefaultMaterial(RenderPipeline pipeline) => GetPipelineDefaultMaterial(GetPipelineLabel(pipeline));
		public static Material GetPipelineDefaultMaterial(PipelineLabel pipelineLabel)
		{
			var mat = s_defaultMaterial[(int)pipelineLabel];
			if (mat == null)
			{
				var sh = GetPipelineDefaultShader(pipelineLabel);
				if (sh != null)
				{
					mat = s_defaultMaterial[(int)pipelineLabel] = CoreUtils.CreateEngineMaterial(sh);
				}
			}

			return mat;
		}

		public static Material GetCurrentPipelineDefault()
		{
			return GetPipelineDefaultMaterial(RenderPipelineManager.currentPipeline);
		}

		public static bool AnyPassPendingCompilation(Material material)
		{
#if UNITY_EDITOR
			for (int i = 0, n = material.shader.passCount; i != n; i++)
			{
				if (ShaderUtil.IsPassCompiled(material, i) == false)
				{
					return true;
				}
			}
#endif
			return false;
		}

		public static int TryCompileCountPassesPending(Material material)
		{
			var pendingCompilation = 0;
#if UNITY_EDITOR
			for (int i = 0; i != material.passCount; i++)
			{
				if (ShaderUtil.IsPassCompiled(material, i) == false)
				{
					ShaderUtil.CompilePass(material, i);
					pendingCompilation++;
				}
			}
#endif
			return pendingCompilation;
		}

		public enum ReplacementType
		{
			Async,
			Error,
		}

		public static Shader GetReplacementShader(ReplacementType type)
		{
			switch (type)
			{
				case ReplacementType.Async: return HairSimResources.Load().replaceAsync;
				case ReplacementType.Error: return HairSimResources.Load().replaceError;
			}

			return null;
		}
	}
}
