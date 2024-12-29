using UnityEngine;
using UnityEngine.Rendering.Universal;

[System.Serializable]
public class PixelateSettings
{
    public RenderPassEvent m_RenderPassEvent = RenderPassEvent.AfterRendering;
    public ComputeShader m_PixelateShader;
    [Range(2, 30)]
    public int m_BlockSize = 10;
}

public class PixelateRenderFeature : ScriptableRendererFeature
{
    [SerializeField]
    private PixelateSettings m_Settings = new ();
    private PixelateRenderPass mPass;
    
    public override void Create()
    {
        mPass = new PixelateRenderPass(m_Settings, name);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(mPass);
    }

    protected override void Dispose(bool disposing)
    {
        mPass.Dispose();
    }
}
