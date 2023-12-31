using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

namespace AoSA.RenderPipeline
{
	public class ColorShadowPass
	{
		private static readonly ProfilingSampler Sampler = new("Color Shadow Pass (Opaque Geometry)");

		private static readonly ShaderTagId[] ShaderTagIDs = { new("ColorShadowPass") };

		private Camera m_camera;

		private RendererListHandle m_list;
		private TextureHandle m_litColorBuffer, m_shadowedColorBuffer, m_depthAttachment;
		private TextureHandle[] m_shadowBuffers;
		private Vector4[] m_lightColors;

		private void Render(RenderGraphContext context)
		{
			RenderTargetIdentifier[] buffers = new RenderTargetIdentifier[2 + m_shadowBuffers.Length];

			AddAndClearRenderTarget(context.cmd, ref buffers, 0, m_litColorBuffer, m_camera.clearFlags == CameraClearFlags.Color ? RemoveAlpha(m_camera) : Color.clear);
			AddAndClearRenderTarget(context.cmd, ref buffers, 1, m_shadowedColorBuffer, Color.clear);
			for (int i = 0; i < m_shadowBuffers.Length; ++i)
			{
				Color clearColor = new Color(m_lightColors[i * 2].w, 0.0f, m_lightColors[i * 2].w, 0.0f);
				AddAndClearRenderTarget(context.cmd, ref buffers, i + 2, m_shadowBuffers[i], clearColor);
			}

			context.cmd.SetRenderTarget(buffers, m_depthAttachment);
			context.cmd.ClearRenderTarget(true, false, Color.clear);

			context.cmd.DrawRendererList(m_list);

			context.renderContext.ExecuteCommandBuffer(context.cmd);
			context.cmd.Clear();
		}

		private Color RemoveAlpha(Camera camera)
		{
			Color newColor = camera.backgroundColor.linear;
			newColor.a = 0.0f;
			return newColor;
		}

		private void AddAndClearRenderTarget(CommandBuffer cmd, ref RenderTargetIdentifier[] buffers, int id, TextureHandle handle, Color clearColor)
		{
			buffers[id] = new RenderTargetIdentifier(handle);

			cmd.SetRenderTarget(buffers[id], RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
			cmd.ClearRenderTarget(true, true, clearColor);
		}

		public static RendererListHandle Record(RenderGraph renderGraph, Camera camera, CullingResults cullingResults, in CameraRendererTextures textures,
			in ShadowTextures shadowTextures, in Vector4[] lightColors)
		{
			using RenderGraphBuilder builder = renderGraph.AddRenderPass(Sampler.name, out ColorShadowPass pass, Sampler);

			RendererListHandle listHandle = renderGraph.CreateRendererList(
				new RendererListDesc(ShaderTagIDs, cullingResults, camera)
				{
					sortingCriteria = SortingCriteria.CommonOpaque,
					rendererConfiguration = PerObjectData.None,
					renderQueueRange = RenderQueueRange.opaque
				});

			pass.m_list = builder.UseRendererList(listHandle);

			pass.m_camera = camera;
			pass.m_lightColors = lightColors;

			pass.m_shadowBuffers = ReadWriteTextures(builder, textures.shadowBuffers);
			pass.m_litColorBuffer = builder.ReadWriteTexture(textures.litColorBuffer);
			pass.m_shadowedColorBuffer = builder.ReadWriteTexture(textures.shadowedColorBuffer);
			pass.m_depthAttachment = builder.ReadWriteTexture(textures.depthAttachment);
			builder.ReadTexture(shadowTextures.directionalAtlas);

			builder.SetRenderFunc<ColorShadowPass>((pass, context) => pass.Render(context));

			return listHandle;
		}

		private static TextureHandle[] ReadWriteTextures(RenderGraphBuilder builder, in TextureHandle[] textures)
		{
			TextureHandle[] newHandles = new TextureHandle[textures.Length];
			for (int i = 0; i < textures.Length; ++i)
			{
				newHandles[i] = builder.ReadWriteTexture(textures[i]);
			}
			return newHandles;
		}
	}
}