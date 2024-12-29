using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PixelateRenderPass : ScriptableRenderPass
{
    private ProfilingSampler mProfilingSampler;
    
    private ComputeShader mComputeShader;
    private int mKernelIndex;
    private uint mGroupX, mGroupY;
    private Vector4 mPixelateParams;
    
    private RTHandle mSource, mDestination;
    private RTHandle mTemporaryColorTexture;
    private const string kTemporaryColorTextureName = "_TemporaryColorTexture";
    
    private static readonly int TemporaryColorTextureID = Shader.PropertyToID(kTemporaryColorTextureName);
    private static readonly int PixelateParamsID = Shader.PropertyToID("_PixelateParams");

    public PixelateRenderPass(PixelateSettings settings, string profilerTag)
    {
        mProfilingSampler = new ProfilingSampler(profilerTag);
        
        renderPassEvent = settings.m_RenderPassEvent;
        
        mComputeShader = settings.m_PixelateShader;
        mKernelIndex = mComputeShader.FindKernel("CSMain");
        mComputeShader.GetKernelThreadGroupSizes(mKernelIndex, out mGroupX, out mGroupY, out _);
        
        mPixelateParams.z = settings.m_BlockSize;
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        var renderer = renderingData.cameraData.renderer;
        mSource = mDestination = renderer.cameraColorTargetHandle;
        
        var descriptor = renderingData.cameraData.cameraTargetDescriptor;
        descriptor.depthBufferBits = 0;
        descriptor.enableRandomWrite = true;
        RenderingUtils.ReAllocateIfNeeded(ref mTemporaryColorTexture, descriptor, name:kTemporaryColorTextureName);
        
        mPixelateParams.x = mTemporaryColorTexture.rt.width;
        mPixelateParams.y = mTemporaryColorTexture.rt.height;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get();
        
        using (new ProfilingScope(cmd, mProfilingSampler))
        {
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            
            Blitter.BlitCameraTexture(cmd, mSource, mTemporaryColorTexture);
            
            cmd.SetComputeTextureParam(mComputeShader, mKernelIndex, TemporaryColorTextureID, mTemporaryColorTexture.nameID);
            cmd.SetComputeVectorParam(mComputeShader, PixelateParamsID, mPixelateParams);
            cmd.DispatchCompute(
                mComputeShader, mKernelIndex, 
                Mathf.CeilToInt(mPixelateParams.x / mPixelateParams.z / mGroupX),
                Mathf.CeilToInt(mPixelateParams.y / mPixelateParams.z / mGroupY),
                1);
            
            Blitter.BlitCameraTexture(cmd, mTemporaryColorTexture, mDestination);
        }
        
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        CommandBufferPool.Release(cmd);
    }

    public void Dispose()
    {
        mTemporaryColorTexture?.Release();
    }
}
