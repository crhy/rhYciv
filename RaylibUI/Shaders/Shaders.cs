using RhyCiv.Engine;
using Raylib_CSharp.Shaders;

namespace RaylibUI;

public static class Shaders
{
    public static Shader Grayscale;
    public static Shader ColorCorrection;
    private static int _brightnessLocation, _saturationLocation, _gammaLocation;

    public static void Load()
    {
        Grayscale = Shader.Load(
            AssetPaths.Resolve("Shaders", "base.vs"),
            AssetPaths.Resolve("Shaders", "grayscale.fs")
        );
        ColorCorrection = Shader.Load(
            AssetPaths.Resolve("Shaders", "base.vs"),
            AssetPaths.Resolve("Shaders", "color-correction.fs")
        );
        if (ColorCorrection.IsValid())
        {
            _brightnessLocation = ColorCorrection.GetLocation("brightness");
            _saturationLocation = ColorCorrection.GetLocation("saturation");
            _gammaLocation = ColorCorrection.GetLocation("gamma");
        }
        else
        {
            _brightnessLocation = _saturationLocation = _gammaLocation = -1;
        }
        SetColorCorrection(Settings.Brightness, Settings.Saturation, Settings.Gamma);
    }

    public static void SetColorCorrection(float brightness, float saturation, float gamma)
    {
        if (_brightnessLocation >= 0) ColorCorrection.SetValue(_brightnessLocation, brightness, ShaderUniformDataType.Float);
        if (_saturationLocation >= 0) ColorCorrection.SetValue(_saturationLocation, saturation, ShaderUniformDataType.Float);
        if (_gammaLocation >= 0) ColorCorrection.SetValue(_gammaLocation, gamma, ShaderUniformDataType.Float);
    }

    public static void Unload()
    {
        Grayscale.Unload();
        ColorCorrection.Unload();
    }
}
