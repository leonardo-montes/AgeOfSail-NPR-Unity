using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace AoSA.RenderPipeline
{
	public class FinalCompositingPass
	{
		private static readonly ProfilingSampler m_sampler = new("FinalCompositingPass");

		private static int WarpBloomId = Shader.PropertyToID("_WarpBloom");
		private static int OverlayId = Shader.PropertyToID("_Overlay");
		private static int SaturationId = Shader.PropertyToID("_Saturation");

#if UNITY_EDITOR
		private static int DebugCountId = Shader.PropertyToID("_DebugCount");
#endif

		private static int[] SourceIds = new int[5]
		{
			Shader.PropertyToID("_Source0"),
			Shader.PropertyToID("_Source1"),
			Shader.PropertyToID("_Source2"),
			Shader.PropertyToID("_Source3"),
			Shader.PropertyToID("_Source4")
		};

		private Camera m_camera;
		private AoSARenderPipelineSettings m_settings;
		
		private TextureHandle m_litColorBuffer, m_warpBuffer;
		private TextureHandle[] m_bloomBuffers;
		
#if UNITY_EDITOR
		private TextureHandle m_debugBuffer;
		private TextureHandle[] m_debugBuffers;
#endif

		private Color m_overlay;
		private float m_saturation;

		private void Render(RenderGraphContext context)
		{
#if UNITY_EDITOR
			if (IsDebugRender(m_camera))
				RenderDebug(context.cmd);
			else
				Render(context.cmd);
#else
			Render(context.cmd);
#endif

			context.renderContext.ExecuteCommandBuffer(context.cmd);
			context.cmd.Clear();
		}

		private void Render(CommandBuffer buffer)
		{
			buffer.SetGlobalFloat(WarpBloomId, m_settings.warpBloom ? 1.0f : 0.0f);
			buffer.SetGlobalColor(OverlayId, m_overlay);
			buffer.SetGlobalFloat(SaturationId, m_saturation);
			
			buffer.SetGlobalTexture(SourceIds[0], m_litColorBuffer);
			buffer.SetGlobalTexture(SourceIds[1], m_warpBuffer);
			for (int i = 0; i < m_bloomBuffers.Length; ++i)
				buffer.SetGlobalTexture(SourceIds[2 + i], m_bloomBuffers[i]);

			buffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, BuiltinRenderTextureType.None);
			buffer.DrawProcedural(Matrix4x4.identity, m_settings.Material, (int)AoSARenderPipeline.Pass.FinalCompositingPass, MeshTopology.Triangles, 3);
		}

#if UNITY_EDITOR
		private void RenderDebug (CommandBuffer buffer)
		{
			AoSARenderPipeline.Pass pass;
			string name = SceneView.currentDrawingSceneView.cameraMode.name;
			if (name == AdditionalCameraModes.Warp.ToString() ||
				name == AdditionalCameraModes.LitColor.ToString() ||
				name == AdditionalCameraModes.ShadowedColor.ToString())
			{
				pass = AoSARenderPipeline.Pass.Copy;
				buffer.SetGlobalTexture(SourceIds[0], m_debugBuffer);
			}
			else
			{
				pass = AoSARenderPipeline.Pass.DebugMultiple;
				buffer.SetGlobalInt(DebugCountId, m_debugBuffers.Length);

				for (int i = 0; i < m_debugBuffers.Length; ++i)
					buffer.SetGlobalTexture(SourceIds[i], m_debugBuffers[i]);
			}

			buffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, BuiltinRenderTextureType.None);
			buffer.DrawProcedural(Matrix4x4.identity, m_settings.Material, (int)pass, MeshTopology.Triangles, 3);
		}
#endif

		public static void Record(RenderGraph renderGraph, Camera camera, AoSARenderPipelineSettings settings, Color overlay, float saturation, in CameraRendererTextures textures)
		{
			using RenderGraphBuilder builder = renderGraph.AddRenderPass(m_sampler.name, out FinalCompositingPass pass, m_sampler);
			pass.m_settings = settings;
			pass.m_camera = camera;
			pass.m_overlay = overlay;
			pass.m_saturation = saturation;
			ReadTextures(camera, builder, pass, textures);
			builder.SetRenderFunc<FinalCompositingPass>((pass, context) => pass.Render(context));
		}

#if UNITY_EDITOR
		private static bool IsDebugRender (Camera camera)
		{
			return camera.cameraType == CameraType.SceneView && SceneView.currentDrawingSceneView.cameraMode.drawMode == DrawCameraMode.UserDefined &&
					SceneView.currentDrawingSceneView.cameraMode.section == AdditionalDrawModes.Section;
		}
#endif

		private static void ReadTextures (Camera camera, RenderGraphBuilder builder, FinalCompositingPass pass, in CameraRendererTextures textures)
		{
#if UNITY_EDITOR
			if (IsDebugRender(camera))
			{
				string name = SceneView.currentDrawingSceneView.cameraMode.name;
				if (name == AdditionalCameraModes.Warp.ToString())
					pass.m_debugBuffer = builder.ReadTexture(textures.warpColor);
				else if (name == AdditionalCameraModes.LitColor.ToString())
					pass.m_debugBuffer = builder.ReadTexture(textures.litColorBuffer);
				else if (name == AdditionalCameraModes.ShadowedColor.ToString())
					pass.m_debugBuffer = builder.ReadTexture(textures.shadowedColorBuffer);
				else if (name == AdditionalCameraModes.Shadow.ToString())
					pass.m_debugBuffers = ReadTextures(builder, textures.shadowBuffers);
				else if (name == AdditionalCameraModes.SoftBlur.ToString())
					pass.m_debugBuffers = ReadTextures(builder, textures.softBlurBuffers);
				else if (name == AdditionalCameraModes.HeavyBlur.ToString())
					pass.m_debugBuffers = ReadTextures(builder, textures.heavyBlurBuffers);
			}
			else
			{
				pass.m_litColorBuffer = builder.ReadTexture(textures.litColorBuffer);
				pass.m_warpBuffer = builder.ReadTexture(textures.warpColor);
				pass.m_bloomBuffers = ReadTextures(builder, textures.bloomBuffers);
			}
#else
			pass.m_litColorBuffer = builder.ReadTexture(textures.litColorBuffer);
			pass.m_warpBuffer = builder.ReadTexture(textures.warpColor);
			pass.m_bloomBuffers = ReadTextures(builder, textures.bloomBuffers);
#endif
		}

		private static TextureHandle[] ReadTextures(RenderGraphBuilder builder, in TextureHandle[] textures)
		{
			TextureHandle[] newHandles = new TextureHandle[textures.Length];
			for (int i = 0; i < textures.Length; ++i)
			{
				newHandles[i] = builder.ReadTexture(textures[i]);
			}
			return newHandles;
		}
	}
}