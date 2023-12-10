using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

public class GeometryPass
{
	static readonly ProfilingSampler
		samplerOpaque = new("Opaque Geometry"),
		samplerTransparent = new("Transparent Geometry");

	static readonly ShaderTagId[] shaderTagIDs = {
		new("SRPDefaultUnlit"),
		new("CustomLit")
	};

	static readonly GlobalKeyword shadowPassKeyword = GlobalKeyword.Create("_AGE_OF_SAIL_RP_SHADOW_PASS");
	static readonly GlobalKeyword colorPassKeyword = GlobalKeyword.Create("_AGE_OF_SAIL_RP_COLOR_PASS");
	static readonly int
		SourceId = Shader.PropertyToID("_Source"),
		Source2Id = Shader.PropertyToID("_Source2"),
		ShadowRTId = Shader.PropertyToID("_ShadowRT"),
		BlurPingRTId = Shader.PropertyToID("_BlurPingRT"),
		BlurPongRTId = Shader.PropertyToID("_BlurPongRT"),
		FinalShadowRTId = Shader.PropertyToID("_FinalShadowRT");

	RendererListHandle list, listCopy;
	TextureHandle colorAttachment, depthAttachment;
	AgeOfSailPipelineSettings ageOfSailPipelineSettings;
	Vector2Int bufferSize;

	void Render(RenderGraphContext context)
	{
        context.cmd.SetRenderTarget(
            colorAttachment,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
            depthAttachment,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
		
		context.cmd.DrawRendererList(list);
		context.renderContext.ExecuteCommandBuffer(context.cmd);
		context.cmd.Clear();
	}

	void AgeOfSailRender(RenderGraphContext context)
	{
		// Create Temp RT
		float blurDownsample = ageOfSailPipelineSettings.blurPassDownsample;
		context.cmd.GetTemporaryRT(ShadowRTId, bufferSize.x, bufferSize.y, 24, FilterMode.Bilinear, GraphicsFormat.R8G8B8A8_UNorm);
		context.cmd.GetTemporaryRT(BlurPingRTId, Mathf.FloorToInt(bufferSize.x / blurDownsample), Mathf.FloorToInt(bufferSize.y / blurDownsample), 24, FilterMode.Bilinear, GraphicsFormat.R8G8B8A8_UNorm);
		context.cmd.GetTemporaryRT(BlurPongRTId, Mathf.FloorToInt(bufferSize.x / blurDownsample), Mathf.FloorToInt(bufferSize.y / blurDownsample), 24, FilterMode.Bilinear, GraphicsFormat.R8G8B8A8_UNorm);
		context.cmd.GetTemporaryRT(FinalShadowRTId, bufferSize.x, bufferSize.y, 24, FilterMode.Bilinear, GraphicsFormat.R8G8B8A8_UNorm);

		// Shadow pass (render)
		context.cmd.EnableKeyword(shadowPassKeyword);
        context.cmd.SetRenderTarget(ShadowRTId);
		context.cmd.ClearRenderTarget(true, true, new Color(1.0f, 0.0f, 0.0f, 0.0f));
		
		context.cmd.DrawRendererList(list);
		context.cmd.DisableKeyword(shadowPassKeyword);

		context.renderContext.ExecuteCommandBuffer(context.cmd);
		context.cmd.Clear();

		// Blur pass (image processing)
		context.cmd.SetGlobalFloat("_BlurSigma", ageOfSailPipelineSettings.blurPassRadius);
		Draw(context.cmd, ageOfSailPipelineSettings.Material, 2, ShadowRTId, BlurPingRTId);
		Draw(context.cmd, ageOfSailPipelineSettings.Material, 3, BlurPingRTId, BlurPongRTId);

		// Final shadow pass (image processing)
		context.cmd.SetGlobalFloat("_ShadowThreshold", ageOfSailPipelineSettings.shadowThreshold);
		context.cmd.SetGlobalFloat("_ShadowThresholdSoftness", ageOfSailPipelineSettings.shadowThresholdSoftness);
		context.cmd.SetGlobalTexture(Source2Id, BlurPongRTId);
		Draw(context.cmd, ageOfSailPipelineSettings.Material, 1, ShadowRTId, FinalShadowRTId);

		// Color pass (render)
		context.cmd.EnableKeyword(colorPassKeyword);
        context.cmd.SetRenderTarget(
            colorAttachment,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
            depthAttachment,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);

		context.cmd.DrawRendererList(listCopy);
		context.cmd.DisableKeyword(colorPassKeyword);

		context.cmd.ReleaseTemporaryRT(ShadowRTId);
		context.cmd.ReleaseTemporaryRT(BlurPingRTId);
		context.cmd.ReleaseTemporaryRT(BlurPongRTId);
		context.cmd.ReleaseTemporaryRT(FinalShadowRTId);

		context.renderContext.ExecuteCommandBuffer(context.cmd);
		context.cmd.Clear();
	}

	private static void Draw(CommandBuffer cmd, Material material, int pass, RenderTargetIdentifier from, RenderTargetIdentifier to)
	{
		cmd.SetGlobalTexture(SourceId, from);
		cmd.SetRenderTarget(
			to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
		cmd.DrawProcedural(Matrix4x4.identity, material, pass,
			MeshTopology.Triangles, 3);
	}

	public static void Record(
		RenderGraph renderGraph,
		Camera camera,
		Vector2Int bufferSize,
		CullingResults cullingResults,
		bool useLightsPerObject,
		int renderingLayerMask,
		bool opaque,
		in CameraRendererTextures textures,
		in ShadowTextures shadowTextures,
		AgeOfSailPipelineSettings ageOfSailPipelineSettings)
	{
		ProfilingSampler sampler = opaque ? samplerOpaque : samplerTransparent;

		using RenderGraphBuilder builder = renderGraph.AddRenderPass(
			sampler.name, out GeometryPass pass, sampler);

		pass.list = builder.UseRendererList(renderGraph.CreateRendererList(
			new RendererListDesc(shaderTagIDs, cullingResults, camera)
			{
				sortingCriteria = opaque ?
					SortingCriteria.CommonOpaque :
					SortingCriteria.CommonTransparent,
				rendererConfiguration =
					PerObjectData.ReflectionProbes |
					PerObjectData.Lightmaps |
					PerObjectData.ShadowMask |
					PerObjectData.LightProbe |
					PerObjectData.OcclusionProbe |
					PerObjectData.LightProbeProxyVolume |
					PerObjectData.OcclusionProbeProxyVolume |
					(useLightsPerObject ?
						PerObjectData.LightData | PerObjectData.LightIndices :
						PerObjectData.None),
				renderQueueRange = opaque ?
					RenderQueueRange.opaque : RenderQueueRange.transparent,
				renderingLayerMask = (uint)renderingLayerMask
			}));

		if (ageOfSailPipelineSettings.usePipeline)
		{
			// hack because 'Trying to execute an RendererList (type Renderers) that was already executed this frame. This is not allowed.'
			pass.listCopy = builder.UseRendererList(renderGraph.CreateRendererList(
				new RendererListDesc(shaderTagIDs, cullingResults, camera)
				{
					sortingCriteria = opaque ?
						SortingCriteria.CommonOpaque :
						SortingCriteria.CommonTransparent,
					rendererConfiguration =
						PerObjectData.ReflectionProbes |
						PerObjectData.Lightmaps |
						PerObjectData.ShadowMask |
						PerObjectData.LightProbe |
						PerObjectData.OcclusionProbe |
						PerObjectData.LightProbeProxyVolume |
						PerObjectData.OcclusionProbeProxyVolume |
						(useLightsPerObject ?
							PerObjectData.LightData | PerObjectData.LightIndices :
							PerObjectData.None),
					renderQueueRange = opaque ?
						RenderQueueRange.opaque : RenderQueueRange.transparent,
					renderingLayerMask = (uint)renderingLayerMask
				}));
		}

		pass.colorAttachment = builder.ReadWriteTexture(textures.colorAttachment);
		pass.depthAttachment = builder.ReadWriteTexture(textures.depthAttachment);
		/*if (!opaque)
		{
			if (textures.colorCopy.IsValid())
			{
				pass.colorAttachment = builder.ReadTexture(textures.colorCopy);
			}
			if (textures.depthCopy.IsValid())
			{
				pass.depthAttachment = builder.ReadTexture(textures.depthCopy);
			}
		}*/
		builder.ReadTexture(shadowTextures.directionalAtlas);
		builder.ReadTexture(shadowTextures.otherAtlas);
		
		pass.ageOfSailPipelineSettings = ageOfSailPipelineSettings;
		pass.bufferSize = bufferSize;

		builder.SetRenderFunc<GeometryPass>((pass, context) => { if (pass.ageOfSailPipelineSettings.usePipeline) pass.AgeOfSailRender(context); else pass.Render(context); });
	}
}
