using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[VolumeComponentMenu("Custom/Kawase Blur")]
public class KawaseBlurVolume : VolumeComponent, IPostProcessComponent
{
    [Space]
    public BoolParameter m_Enable = new(false);
    [Space]
    public ClampedIntParameter m_BlurPassNumber = new(2, 2, 15);
    public ClampedIntParameter m_DownSample = new(1, 1, 4);
    
    public bool IsActive() => m_Enable.value;
    public bool IsTileCompatible() => false;
}
