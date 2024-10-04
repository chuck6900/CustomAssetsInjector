using System.Globalization;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace TexturePlugin;

#nullable disable
public static class TextureMain
{
    public static bool ReplaceTexture(AssetTypeValueField baseField, string newImagePath, out Exception ex)
    {
        ex = null;
        if (baseField == null)
            return false;

        var tex = TextureFile.ReadTextureFile(baseField);

        try
        {
            tex.SetTextureData(newImagePath);
            tex.WriteTo(baseField);
        }
        catch (Exception err)
        {
            ex = err;
            return false;
        }
        
        return true;
    }
}