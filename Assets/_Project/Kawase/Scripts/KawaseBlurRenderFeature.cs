using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[System.Serializable]
public class KawaseBlurSettings
{
    public RenderPassEvent m_RenderPassEvent = RenderPassEvent.AfterRenderingSkybox;
    public Shader          m_KawaseBlurShader;
    public bool            m_RenderToFullScreen;
    public string          m_TargetTextureName = "_BlurTexture";
}

public class KawaseBlurRenderFeature : ScriptableRendererFeature
{
    public KawaseBlurSettings m_Settings = new ();
    private KawaseBlurRenderPass mPass;
    
    public override void Create()
    {
        mPass = new KawaseBlurRenderPass(name, m_Settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        KawaseBlurVolume volumeComponent = VolumeManager.instance.stack.GetComponent<KawaseBlurVolume>();
        if (!volumeComponent || !volumeComponent.IsActive()) return;
        
        mPass.Setup(volumeComponent);
        renderer.EnqueuePass(mPass);
    }

    protected override void Dispose(bool disposing)
    {
        mPass.Dispose();
    }
}
