using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using CustomAssetsBackend.Classes;
using CustomAssetsBackend.Misc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace CustomAssetsBackend.SpriteSheet;

public abstract class SpriteSheetManager(string il2CppFolderPath)
{ 
    public List<UnityAsset> AssetCache { get; } = new();

    public List<SpriteData> Sprites { get; } = new();

    protected readonly string Il2CppFolderPath = il2CppFolderPath;

    protected void ExportTexture2D(AssetsManager am, UnityAsset texture2dAsset, string imagePath)
    {
        var texture2DFileInst = am.LoadAssetsFile(texture2dAsset.Path);
        var texture2DAtlasFile = texture2DFileInst.file;

        // extract texture2d
        var textureInf = texture2DAtlasFile.GetAssetInfo(texture2dAsset.PathId);
        var textureBase = am.GetBaseField(texture2DFileInst, textureInf);

        var texture = TextureFile.ReadTextureFile(textureBase); // load base field into helper class
        var textureBgraRaw = texture.GetTextureData(texture2DFileInst); // get the raw bgra32 data
        var textureImage = Image.LoadPixelData<Bgra32>(textureBgraRaw, texture.m_Width, texture.m_Height); // use imagesharp to convert to image
        textureImage.Mutate(i => i.Flip(FlipMode.Vertical)); // flip on x-axis
        textureImage.SaveAsPng(imagePath);
    }

    public abstract CommonUtils.ReturnCode Load();

    public abstract CommonUtils.ReturnCode Save();
}