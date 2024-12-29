using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable]
public class DualKawaseBlurSettings
{
    public RenderPassEvent m_RenderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    public ComputeShader   m_DualKawaseBlurShader;
    public bool            m_CopyToFrameBuffer = true;
    public string          m_TargetTextureName = "_BlurTexture";
}

public class DualKawaseBlurRenderFeature : ScriptableRendererFeature
{
    [SerializeField]
    private DualKawaseBlurSettings   m_Settings = new ();
    private DualKawaseBlurRenderPass mPass;
    
    public override void Create()
    {
        if (m_Settings.m_DualKawaseBlurShader == null) return;
        
        mPass = new DualKawaseBlurRenderPass(name, m_Settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        DualKawaseBlur volumeComponent = VolumeManager.instance.stack.GetComponent<DualKawaseBlur>();
        if (!volumeComponent || !volumeComponent.IsActive()) return;
        
        mPass.Setup(volumeComponent);
        renderer.EnqueuePass(mPass);
    }
}
