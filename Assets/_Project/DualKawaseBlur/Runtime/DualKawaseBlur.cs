using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[VolumeComponentMenu("Custom/Dual Kawase Blur")]
public class DualKawaseBlur : VolumeComponent, IPostProcessComponent
{
    [Space]
    public BoolParameter m_Enable = new(false);
    [Space]
    public ClampedFloatParameter m_BlurRadius = new(32.0f, 0.0f, 255.0f);
    public ClampedFloatParameter m_BlurIntensity = new(0.0f, 0.0f, 1.0f);
    
    public bool IsActive() => m_Enable.value && m_BlurIntensity.value > 0.0f;
    public bool IsTileCompatible() => false;
}
