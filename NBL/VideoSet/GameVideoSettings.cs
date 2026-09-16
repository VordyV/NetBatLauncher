namespace NBL.Models;
public class GameVideoSettings
{
    public int ResolutionWidth { get; set; } = 1920;
    public int ResolutionHeight { get; set; } = 1080;
    public int RefreshRate { get; set; } = 60;
    public int TerrainQuality { get; set; } = 3;
    public int GeometryQuality { get; set; } = 3;
    public int LightingQuality { get; set; } = 3;
    public int DynamicLightingQuality { get; set; } = 3;
    public int DynamicShadowsQuality { get; set; } = 3;
    public int EffectsQuality { get; set; } = 3;
    public int TextureQuality { get; set; } = 3;
    public int TextureFilteringQuality { get; set; } = 3;
    public int AntialiasingSamples { get; set; } = 0;
    public float ViewDistanceScale { get; set; } = 1.0f;
    public bool UseBloom { get; set; } = true;
}
