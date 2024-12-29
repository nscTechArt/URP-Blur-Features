using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class KawaseBlurRenderPass : ScriptableRenderPass
{
    private static readonly int KawaseBlurOffsetID = Shader.PropertyToID("_KawaseBlurOffset");

    public KawaseBlurRenderPass(string featureName, KawaseBlurSettings settings)
    {
        mProfilingSampler = new ProfilingSampler(featureName);
        
        renderPassEvent = settings.m_RenderPassEvent;
        mPassMaterial = CoreUtils.CreateEngineMaterial(settings.m_KawaseBlurShader);
        mSettings = settings;
    }

    public void Setup(KawaseBlurVolume volumeComponent)
    {
        mVolumeComponent = volumeComponent;
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        // setup temporary render texture
        // ------------------------------
        var descriptor = renderingData.cameraData.cameraTargetDescriptor;
        descriptor.depthBufferBits = 0;
        descriptor.width /= mVolumeComponent.m_DownSample.value;
        descriptor.height /= mVolumeComponent.m_DownSample.value;
        RenderingUtils.ReAllocateIfNeeded(ref mTemporaryTexture1, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: kTemporaryTextureOneName);
        RenderingUtils.ReAllocateIfNeeded(ref mTemporaryTexture2, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: kTemporaryTextureTwoName);
        
        // setup source and destination render textures
        // --------------------------------------------
        var renderer = renderingData.cameraData.renderer;
        // since we are doing blur, mSource should be camera color
        mSource = renderer.cameraColorTargetHandle;
        // mDestination depends on whether we are rendering to full screen or not
        if (mSettings.m_RenderToFullScreen)
        {
            mDestination = renderer.cameraColorTargetHandle;
        }
        else
        {
            RenderingUtils.ReAllocateIfNeeded(ref mDestination, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: mSettings.m_TargetTextureName);
        }
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType == CameraType.Preview) return;
        
        CommandBuffer cmd = CommandBufferPool.Get();
        using (new ProfilingScope(cmd, mProfilingSampler))
        {
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            
            // blur pass
            // ---------
            cmd.SetGlobalFloat(KawaseBlurOffsetID, 1.5f);
            Blitter.BlitCameraTexture(cmd, mSource, mTemporaryTexture1, mPassMaterial, 0);
            for (int i = 1; i < mVolumeComponent.m_BlurPassNumber.value - 1; i++)
            {
                cmd.SetGlobalFloat(KawaseBlurOffsetID, 0.5f + i);
                Blitter.BlitCameraTexture(cmd, mTemporaryTexture1, mTemporaryTexture2, mPassMaterial, 0);
                (mTemporaryTexture1, mTemporaryTexture2) = (mTemporaryTexture2, mTemporaryTexture1);
            }
            
            // final pass
            // ----------
            cmd.SetGlobalFloat(KawaseBlurOffsetID, 0.5f + mVolumeComponent.m_BlurPassNumber.value - 1);
            if (mSettings.m_RenderToFullScreen)
            {
                Blitter.BlitCameraTexture(cmd, mTemporaryTexture1, mDestination, mPassMaterial, 0);
            }
            else
            {
                Blitter.BlitCameraTexture(cmd, mTemporaryTexture1, mTemporaryTexture2, mPassMaterial, 0);
                cmd.SetGlobalTexture(mSettings.m_TargetTextureName, mTemporaryTexture2);
            }
        }
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        CommandBufferPool.Release(cmd);
    }

    public void Dispose()
    {
        CoreUtils.Destroy(mPassMaterial);
        mPassMaterial = null;
        
        mTemporaryTexture1?.Release();
        mTemporaryTexture2?.Release();
    }
    
    private ProfilingSampler mProfilingSampler;
    private Material mPassMaterial;
    private KawaseBlurSettings mSettings;
    private KawaseBlurVolume mVolumeComponent;
    
    private RTHandle mSource, mDestination;
    private RTHandle mTemporaryTexture1, mTemporaryTexture2;
    
    // constants
    // ---------
    private const string kTemporaryTextureOneName = "_TemporaryTexture1";
    private const string kTemporaryTextureTwoName = "_TemporaryTexture2";
}
