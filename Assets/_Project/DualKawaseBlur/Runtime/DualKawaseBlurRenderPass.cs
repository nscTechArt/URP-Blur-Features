using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DualKawaseBlurRenderPass : ScriptableRenderPass
{
    public DualKawaseBlurRenderPass(string featureName, DualKawaseBlurSettings settings)
    {
        // initialize
        // ----------
        mProfilingSampler = new ProfilingSampler(featureName);
        renderPassEvent   = settings.m_RenderPassEvent;
        mSettings         = settings;
        
        // shader related
        // --------------
        mPassShader           = settings.m_DualKawaseBlurShader;
        mDownSampleBlurKernel = mPassShader.FindKernel("DownSampleBlur");
        mUpSampleBlurKernel   = mPassShader.FindKernel("UpSampleBlur");
        mLinearLerpKernel   = mPassShader.FindKernel("LinearLerp");
    }

    public void Setup(DualKawaseBlur volumeComponent)
    {
        mVolumeComponent = volumeComponent;
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        // update camera color texture
        // ---------------------------
        mCameraColorTexture = renderingData.cameraData.renderer.cameraColorTargetHandle;
        
        // update descriptor of blur textures
        // ----------------------------------
        mDescriptor = renderingData.cameraData.cameraTargetDescriptor;
        mDescriptor.depthBufferBits = 0;
        mDescriptor.msaaSamples = 1;
        mDescriptor.enableRandomWrite = true;
        
        // update screen size
        // ------------------
        mOriginalSize = new Vector2Int(mDescriptor.width, mDescriptor.height);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get();
        using (new ProfilingScope(cmd, mProfilingSampler))
        {
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            // create lists to store temporary textures and sizes
            // --------------------------------------------------
            List<int> textureIDs = new();
            List<Vector2Int> textureSizes = new();
            
            // create final target blur texture
            // --------------------------------
            int finalTextureID = Shader.PropertyToID(mSettings.m_TargetTextureName);
            cmd.GetTemporaryRT(finalTextureID, mDescriptor);
            // keep track
            textureIDs.Add(finalTextureID);
            textureSizes.Add(mOriginalSize);
            
            // figure out blur iterations and blending ratio
            // ---------------------------------------------
            float blurFactor = mVolumeComponent.m_BlurRadius.value * mVolumeComponent.m_BlurIntensity.value + 1.0f;
            float blurAmount = Mathf.Log(blurFactor, 2.0f);
            int   blurIterations = Mathf.FloorToInt(blurAmount);
            float ratio = blurAmount - blurIterations;
            
            // downsample blur
            // ---------------
            Vector2Int sourceTextureSize = mOriginalSize;
            RenderTargetIdentifier sourceTextureID = mCameraColorTexture.nameID;
            for (int i = 0; i <= blurIterations; i++)
            {
                // create a new target texture
                // ---------------------------
                int targetTextureID = Shader.PropertyToID(kBlurTextureName + i);
                // plus one is necessary to zero thread group count
                Vector2Int targetTextureSize = new((sourceTextureSize.x + 1) / 2, (sourceTextureSize.y + 1) / 2);
                mDescriptor.width = targetTextureSize.x;
                mDescriptor.height = targetTextureSize.y;
                cmd.GetTemporaryRT(targetTextureID, mDescriptor);
                // keep track
                textureIDs.Add(targetTextureID);
                textureSizes.Add(targetTextureSize);
                
                // do the kawase blur
                // ------------------
                DownSampleBlur(cmd, sourceTextureID, targetTextureID, targetTextureSize);
                
                // update the last size and ID
                // ---------------------------
                sourceTextureSize = targetTextureSize;
                sourceTextureID = targetTextureID;
            }
            
            // upsample
            // --------
            if (blurIterations != 0)
            {
                // create an intermediate texture for linear lerp
                // ----------------------------------------------
                int intermediateTextureID = Shader.PropertyToID(kBlurTextureName + blurIterations + 1);
                Vector2Int intermediateTextureSize = textureSizes[blurIterations];
                mDescriptor.width = intermediateTextureSize.x;
                mDescriptor.height = intermediateTextureSize.y;
                cmd.GetTemporaryRT(intermediateTextureID, mDescriptor);

                for (int i = blurIterations + 1; i >= 1; i--)
                {
                    int sourceID = textureIDs[i];
                    Vector2Int sourceSize = textureSizes[i];
                    int targetID = i == blurIterations + 1 ? intermediateTextureID : textureIDs[i - 1];
                    Vector2Int targetSize = textureSizes[i - 1];
                    
                    // do the kawase blur
                    // ------------------
                    UpSampleBlur(cmd, sourceID, targetID, targetSize);
                    
                    // do the linear lerp
                    // ------------------
                    if (i == blurIterations + 1)
                    {
                        Linear(cmd, textureIDs[i - 1], intermediateTextureID, targetSize, ratio);
                        (intermediateTextureID, textureIDs[i - 1]) = (textureIDs[i - 1], intermediateTextureID);
                    }
                    
                    // release current temporary texture
                    // ---------------------------------
                    cmd.ReleaseTemporaryRT(sourceID);
                }
                
                // release the intermediate texture
                // --------------------------------
                cmd.ReleaseTemporaryRT(intermediateTextureID);
            }
            else
            {
                UpSampleBlur(cmd, textureIDs[1], textureIDs[0], textureSizes[0]);
                Linear(cmd, mCameraColorTexture.nameID, textureIDs[0], textureSizes[0], ratio);
            }
            
            // blit the final result
            // ---------------------
            cmd.Blit(finalTextureID, mCameraColorTexture.nameID);
            cmd.ReleaseTemporaryRT(finalTextureID);
        }
        
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        CommandBufferPool.Release(cmd);
    }

    private Vector4 GetTextureSizeParams(Vector2Int size)
    {
        return new Vector4(size.x, size.y, 1.0f / size.x, 1.0f / size.y);
    }
    
    private void DownSampleBlur(CommandBuffer cmd, 
        RenderTargetIdentifier source, RenderTargetIdentifier target, Vector2Int targetSize)
    {
        // pass data to shader
        // -------------------
        cmd.SetComputeTextureParam(mPassShader, mDownSampleBlurKernel, _SourceTexture, source);
        cmd.SetComputeTextureParam(mPassShader, mDownSampleBlurKernel, RWTargetTextureID, target);
        cmd.SetComputeVectorParam(mPassShader, TargetSizeID, GetTextureSizeParams(targetSize));
        cmd.SetComputeFloatParam(mPassShader, OffsetID, 1);
        
        // dispatch shader
        // ---------------
        mPassShader.GetKernelThreadGroupSizes(mDownSampleBlurKernel, out uint x, out uint y, out uint _);
        cmd.DispatchCompute(mPassShader, mDownSampleBlurKernel,
            Mathf.CeilToInt((float)targetSize.x / x),
            Mathf.CeilToInt((float)targetSize.y / y),
            1);
    }

    private void UpSampleBlur(CommandBuffer cmd,
        RenderTargetIdentifier source, RenderTargetIdentifier target, Vector2Int targetSize)
    {
        // pass data to shader
        // -------------------
        cmd.SetComputeTextureParam(mPassShader, mUpSampleBlurKernel, _SourceTexture, source);
        cmd.SetComputeTextureParam(mPassShader, mUpSampleBlurKernel, RWTargetTextureID, target);
        cmd.SetComputeVectorParam(mPassShader, TargetSizeID, GetTextureSizeParams(targetSize));
        cmd.SetComputeFloatParam(mPassShader, OffsetID, 1);

        // dispatch shader
        // ---------------
        mPassShader.GetKernelThreadGroupSizes(mUpSampleBlurKernel, out uint x, out uint y, out uint _);
        int threadGroupX = Mathf.CeilToInt((float)targetSize.x / x);
        int threadGroupY = Mathf.CeilToInt((float)targetSize.y / y);
        cmd.DispatchCompute(mPassShader, mUpSampleBlurKernel, threadGroupX, threadGroupY, 1);
    }

    private void Linear(CommandBuffer cmd,
        RenderTargetIdentifier source, RenderTargetIdentifier target, Vector2Int sourceSize, float offset)
    {
        // pass data to shader
        // -------------------
        cmd.SetComputeTextureParam(mPassShader, mLinearLerpKernel, _SourceTexture, source);
        cmd.SetComputeTextureParam(mPassShader, mLinearLerpKernel, RWTargetTextureID, target);
        cmd.SetComputeVectorParam(mPassShader, _SourceSize, GetTextureSizeParams(sourceSize));
        cmd.SetComputeFloatParam(mPassShader, OffsetID, offset);
        
        // dispatch shader
        // ---------------
        mPassShader.GetKernelThreadGroupSizes(mLinearLerpKernel, out uint x, out uint y, out uint _);
        int threadGroupX = Mathf.CeilToInt((float)sourceSize.x / x);
        int threadGroupY = Mathf.CeilToInt((float)sourceSize.y / y);
        cmd.DispatchCompute(mPassShader, mLinearLerpKernel, threadGroupX, threadGroupY, 1);
    }

    public void Dispose()
    {
        mTemporaryColorTexture?.Release();
    }

    // profiling related
    // -----------------
    private ProfilingSampler     mProfilingSampler;
    // pass shader related
    // -------------------
    private ComputeShader mPassShader;
    private int mDownSampleBlurKernel;
    private int mUpSampleBlurKernel;
    private int mLinearLerpKernel;
    // volume component related
    // ------------------------
    private DualKawaseBlur mVolumeComponent;
    // render pass related
    // -------------------
    private RTHandle mCameraColorTexture;
    private RTHandle mTemporaryColorTexture;
    private RenderTextureDescriptor mDescriptor;
    private Vector2Int mOriginalSize;

    private DualKawaseBlurSettings mSettings;

    // constants
    // ---------
    private const string kBlurTextureName = "_BlurTexture";
    // cached shader property IDs
    // --------------------------
    private static readonly int _SourceTexture = Shader.PropertyToID("_SourceTexture");
    private static readonly int _SourceSize = Shader.PropertyToID("_SourceSize");
    private static readonly int TargetSizeID = Shader.PropertyToID("_TargetSize");
    private static readonly int OffsetID = Shader.PropertyToID("_Offset");
    private static readonly int RWTargetTextureID = Shader.PropertyToID("_RW_TargetTexture");
}
