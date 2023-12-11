using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

public class GeometryPass
{
	private enum Pass { Copy, FinalShadowPass, HorizontalBlurPass, VerticalBlurPass }

	static readonly ProfilingSampler
		samplerOpaque = new("Opaque Geometry"),
		samplerTransparent = new("Transparent Geometry");

	static readonly ShaderTagId[] shaderTagIDs = {
		new("SRPDefaultUnlit"),
		new("CustomLit")
	};

	static readonly GlobalKeyword shadowPassKeyword = GlobalKeyword.Create("_AGE_OF_SAIL_RP_SHADOW_PASS");
	public static readonly GlobalKeyword shadowColoredPassKeyword = GlobalKeyword.Create("_AGE_OF_SAIL_RP_SHADOW_COLORED_PASS");
	static readonly GlobalKeyword colorPassKeyword = GlobalKeyword.Create("_AGE_OF_SAIL_RP_COLOR_PASS");
	static readonly int
		SourceId = Shader.PropertyToID("_Source"),
		Source2Id = Shader.PropertyToID("_Source2"),
		Source3Id = Shader.PropertyToID("_Source3"),
		Source4Id = Shader.PropertyToID("_Source4"),
		ShadowRTId = Shader.PropertyToID("_ShadowRT"),
		ShadowSpecThreshRTId = Shader.PropertyToID("_ShadowSpecThreshRT"),
		ShadowDepthRTId = Shader.PropertyToID("_ShadowDepthRT"),
		Blur0PingRTId = Shader.PropertyToID("_Blur0PingRT"),
		Blur1PingRTId = Shader.PropertyToID("_Blur1PingRT"),
		Blur0PongRTId = Shader.PropertyToID("_Blur0PongRT"),
		Blur1PongRTId = Shader.PropertyToID("_Blur1PongRT"),
		FinalShadowRTId = Shader.PropertyToID("_FinalShadowRT"),
		FinalShadowSpecThreshRTId = Shader.PropertyToID("_FinalShadowSpecThreshRT");
	static readonly RenderTargetIdentifier[] ShadowRTs = new RenderTargetIdentifier[2] { ShadowRTId, ShadowSpecThreshRTId };
	static readonly RenderTargetIdentifier[] FinalShadowRTs = new RenderTargetIdentifier[2] { FinalShadowRTId, FinalShadowSpecThreshRTId };
	static readonly RenderTargetIdentifier[] BlurPingRTs = new RenderTargetIdentifier[2] { Blur0PingRTId, Blur1PingRTId };
	static readonly RenderTargetIdentifier[] BlurPongRTs = new RenderTargetIdentifier[2] { Blur0PongRTId, Blur1PongRTId };

	RendererListHandle list, listCopy;
	TextureHandle colorAttachment, depthAttachment, blurBuffer;
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
		context.cmd.GetTemporaryRT(ShadowRTId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, GraphicsFormat.R8G8B8A8_UNorm);
		context.cmd.GetTemporaryRT(ShadowSpecThreshRTId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, GraphicsFormat.R8G8B8A8_UNorm);
		context.cmd.GetTemporaryRT(ShadowDepthRTId, bufferSize.x, bufferSize.y, 24, FilterMode.Bilinear, RenderTextureFormat.Depth);
		context.cmd.GetTemporaryRT(Blur0PingRTId, Mathf.FloorToInt(bufferSize.x / blurDownsample), Mathf.FloorToInt(bufferSize.y / blurDownsample), 24, FilterMode.Bilinear, GraphicsFormat.R8G8B8A8_UNorm);
		context.cmd.GetTemporaryRT(Blur0PongRTId, Mathf.FloorToInt(bufferSize.x / blurDownsample), Mathf.FloorToInt(bufferSize.y / blurDownsample), 24, FilterMode.Bilinear, GraphicsFormat.R8G8B8A8_UNorm);
		context.cmd.GetTemporaryRT(Blur1PingRTId, Mathf.FloorToInt(bufferSize.x / blurDownsample), Mathf.FloorToInt(bufferSize.y / blurDownsample), 24, FilterMode.Bilinear, GraphicsFormat.R8G8B8A8_UNorm);
		context.cmd.GetTemporaryRT(Blur1PongRTId, Mathf.FloorToInt(bufferSize.x / blurDownsample), Mathf.FloorToInt(bufferSize.y / blurDownsample), 24, FilterMode.Bilinear, GraphicsFormat.R8G8B8A8_UNorm);
		context.cmd.GetTemporaryRT(FinalShadowRTId, bufferSize.x, bufferSize.y, 24, FilterMode.Bilinear, GraphicsFormat.R8G8B8A8_UNorm);
		context.cmd.GetTemporaryRT(FinalShadowSpecThreshRTId, bufferSize.x, bufferSize.y, 24, FilterMode.Bilinear, GraphicsFormat.R8G8B8A8_UNorm);

		// Shadow pass (render)
		context.cmd.EnableKeyword(shadowPassKeyword);
		if (ageOfSailPipelineSettings.useColorLights)
		{
			context.cmd.EnableKeyword(shadowColoredPassKeyword);

        	context.cmd.SetRenderTarget(ShadowRTId);
			context.cmd.ClearRenderTarget(false, true, new Color(1.0f, 1.0f, 1.0f, 0.0f));

        	context.cmd.SetRenderTarget(ShadowSpecThreshRTId);
			context.cmd.ClearRenderTarget(false, true, new Color(0.0f, 0.0f, 0.0f, 0.0f));

        	context.cmd.SetRenderTarget(ShadowRTs, ShadowDepthRTId);
			context.cmd.ClearRenderTarget(true, false, Color.clear);
		}
		else
		{
        	context.cmd.SetRenderTarget(
				ShadowRTId,
				RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
				ShadowDepthRTId,
				RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
			context.cmd.ClearRenderTarget(true, true, new Color(1.0f, 0.0f, 0.0f, 0.0f));
		}
		
		context.cmd.DrawRendererList(list);
		context.cmd.DisableKeyword(shadowPassKeyword);

		context.renderContext.ExecuteCommandBuffer(context.cmd);
		context.cmd.Clear();

		context.cmd.SetGlobalFloat("_BlurSigma", ageOfSailPipelineSettings.blurPassRadius);
		if (ageOfSailPipelineSettings.useColorLights)
		{
			// Blur pass (image processing)
			Draw(context.cmd, ageOfSailPipelineSettings.Material, (int)Pass.HorizontalBlurPass, ShadowRTs, BlurPingRTs);
			Draw(context.cmd, ageOfSailPipelineSettings.Material, (int)Pass.VerticalBlurPass, BlurPingRTs, BlurPongRTs);

			// Save the blur pass
			Draw(context.cmd, ageOfSailPipelineSettings.Material, (int)Pass.Copy, BlurPongRTs[1], blurBuffer);
			
			// Final shadow pass (image processing)
			context.cmd.SetGlobalFloat("_ShadowThreshold", ageOfSailPipelineSettings.shadowThreshold);
			context.cmd.SetGlobalFloat("_ShadowThresholdSoftness", ageOfSailPipelineSettings.shadowThresholdSoftness);
			context.cmd.SetGlobalTexture(Source3Id, BlurPongRTs[0]);
			context.cmd.SetGlobalTexture(Source4Id, BlurPongRTs[1]);
			Draw(context.cmd, ageOfSailPipelineSettings.Material, (int)Pass.FinalShadowPass, ShadowRTs, FinalShadowRTs);
		}
		else
		{
			// Blur pass (image processing)
			Draw(context.cmd, ageOfSailPipelineSettings.Material, (int)Pass.HorizontalBlurPass, ShadowRTId, Blur0PingRTId);
			Draw(context.cmd, ageOfSailPipelineSettings.Material, (int)Pass.VerticalBlurPass, Blur0PingRTId, Blur0PongRTId);

			// Save the blur pass
			Draw(context.cmd, ageOfSailPipelineSettings.Material, (int)Pass.Copy, Blur0PongRTId, blurBuffer);
			
			// Final shadow pass (image processing)
			context.cmd.SetGlobalFloat("_ShadowThreshold", ageOfSailPipelineSettings.shadowThreshold);
			context.cmd.SetGlobalFloat("_ShadowThresholdSoftness", ageOfSailPipelineSettings.shadowThresholdSoftness);
			context.cmd.SetGlobalTexture(Source2Id, blurBuffer);
			Draw(context.cmd, ageOfSailPipelineSettings.Material, (int)Pass.FinalShadowPass, ShadowRTId, FinalShadowRTId);
		}

		// Color pass (render)
		context.cmd.EnableKeyword(colorPassKeyword);
        context.cmd.SetRenderTarget(
            colorAttachment,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
            depthAttachment,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);

		context.cmd.DrawRendererList(listCopy);
		context.cmd.DisableKeyword(colorPassKeyword);
		
		if (ageOfSailPipelineSettings.useColorLights)
			context.cmd.DisableKeyword(shadowColoredPassKeyword);

		context.cmd.ReleaseTemporaryRT(ShadowRTId);
		context.cmd.ReleaseTemporaryRT(ShadowSpecThreshRTId);
		context.cmd.ReleaseTemporaryRT(Blur0PingRTId);
		context.cmd.ReleaseTemporaryRT(Blur1PingRTId);
		context.cmd.ReleaseTemporaryRT(Blur0PongRTId);
		context.cmd.ReleaseTemporaryRT(Blur1PongRTId);
		context.cmd.ReleaseTemporaryRT(FinalShadowRTId);
		context.cmd.ReleaseTemporaryRT(FinalShadowSpecThreshRTId);

		context.renderContext.ExecuteCommandBuffer(context.cmd);
		context.cmd.Clear();
	}

	private static void Draw(CommandBuffer cmd, Material material, int pass, RenderTargetIdentifier from, RenderTargetIdentifier to)
	{
		cmd.SetGlobalTexture(SourceId, from);
		cmd.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
		cmd.DrawProcedural(Matrix4x4.identity, material, pass, MeshTopology.Triangles, 3);
	}

	private static void Draw(CommandBuffer cmd, Material material, int pass, RenderTargetIdentifier[] from, RenderTargetIdentifier to)
	{
		cmd.SetGlobalTexture(SourceId, from[0]);
		cmd.SetGlobalTexture(Source2Id, from[1]);
		cmd.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
		cmd.DrawProcedural(Matrix4x4.identity, material, pass, MeshTopology.Triangles, 3);
	}

	private static void Draw(CommandBuffer cmd, Material material, int pass, RenderTargetIdentifier[] from, RenderTargetIdentifier[] to)
	{
		cmd.SetGlobalTexture(SourceId, from[0]);
		cmd.SetGlobalTexture(Source2Id, from[1]);
		cmd.SetRenderTarget(to, BuiltinRenderTextureType.None);
		cmd.DrawProcedural(Matrix4x4.identity, material, pass, MeshTopology.Triangles, 3);
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

		pass.blurBuffer = builder.ReadWriteTexture(textures.blurBuffer);
		
		pass.ageOfSailPipelineSettings = ageOfSailPipelineSettings;
		pass.bufferSize = bufferSize;

		builder.SetRenderFunc<GeometryPass>((pass, context) => { if (pass.ageOfSailPipelineSettings.usePipeline) pass.AgeOfSailRender(context); else pass.Render(context); });
	}
}
