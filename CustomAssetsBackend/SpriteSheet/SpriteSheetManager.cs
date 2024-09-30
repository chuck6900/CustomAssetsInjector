using AssetsTools.NET;
using AssetsTools.NET.Cpp2IL;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using CustomAssetsBackend.Classes;
using CustomAssetsBackend.Misc;
using CustomAssetsBackend.SpriteSheet.SmoothMoves;
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

    public CommonUtils.ReturnCode Import(string inputFilePath)
    {
        var objType = this is SmoothMovesSpriteSheetManager
            ? UnityAsset.UnityObjectType.MonoBehaviour
            : UnityAsset.UnityObjectType.UIAtlas;
        
        var dataAsset = AssetCache.FirstOrDefault(asset => asset.ObjectType == objType) ?? UnityAsset.Empty;
        if (this.Sprites.Count == 0 || this.AssetCache.Count == 0 || dataAsset == UnityAsset.Empty)
        {
            Logger.Log("No atlas is loaded! Please load an atlas before importing.");
            return CommonUtils.ReturnCode.NoAtlasLoaded;
        }
        
        // load asset
        var am = CommonUtils.InitAssetManager(Path.GetDirectoryName(dataAsset.Path)!);
            
        var globalMetadataPath = Path.Combine(this.Il2CppFolderPath, "global-metadata.dat");
        var binaryPath = Path.Combine(this.Il2CppFolderPath, "il2cpp.binary");
            
        am.MonoTempGenerator = new Cpp2IlTempGenerator(globalMetadataPath, binaryPath);

        var monoBehaviourFileInst = am.LoadAssetsFile(dataAsset.Path);
        var behaviourInf = monoBehaviourFileInst.file.GetAssetInfo(dataAsset.PathId);
        
        Logger.Log("Parsing JSON..");
        
        // open input file
        using var fs = File.OpenRead(inputFilePath);
        using var sr = new StreamReader(fs);

        // import data
        var tempField = am.GetTemplateBaseField(monoBehaviourFileInst, behaviourInf);
        var newBaseFieldBytes = new AssetImportExport().ImportJsonAsset(tempField, sr, out var err);

        if (newBaseFieldBytes == null || err != null)
        {
            am.UnloadAll();
            Logger.Log("Import failed! The JSON file may be invalid.", Logger.LogLevel.Exception, err);
            return CommonUtils.ReturnCode.ImportFailed;
        }
        
        behaviourInf.SetNewData(newBaseFieldBytes);
        
        var tmpOutPath = Path.GetTempFileName();
            
        using (var writer = new AssetsFileWriter(tmpOutPath))
        {
            monoBehaviourFileInst.file.Write(writer);
        }
        
        am.UnloadAll();
        
        File.Replace(tmpOutPath, dataAsset.Path, null);
        
        // reload
        var returnCode = Load();

        if (returnCode != CommonUtils.ReturnCode.Success)
        {
            Logger.Log("Failed to reload the atlas after importing!");
            return returnCode;
        }
        
        Logger.Log("Successfully imported the sprite data.");
        return CommonUtils.ReturnCode.Success;
    }

    public CommonUtils.ReturnCode Export(string outputFilePath)
    {
        var objType = this is SmoothMovesSpriteSheetManager
            ? UnityAsset.UnityObjectType.MonoBehaviour
            : UnityAsset.UnityObjectType.UIAtlas;
        
        var dataAsset = AssetCache.FirstOrDefault(asset => asset.ObjectType == objType) ?? UnityAsset.Empty;
        if (this.Sprites.Count == 0 || this.AssetCache.Count == 0 || dataAsset == UnityAsset.Empty)
        {
            Logger.Log("No atlas is loaded! Please load an atlas before exporting.");
            return CommonUtils.ReturnCode.NoAtlasLoaded;
        }
        
        // load asset
        
        var am = CommonUtils.InitAssetManager(Path.GetDirectoryName(dataAsset.Path)!);
            
        var globalMetadataPath = Path.Combine(this.Il2CppFolderPath, "global-metadata.dat");
        var binaryPath = Path.Combine(this.Il2CppFolderPath, "il2cpp.binary");
            
        Logger.Log("Reading atlas data..");
            
        am.MonoTempGenerator = new Cpp2IlTempGenerator(globalMetadataPath, binaryPath);

        var monoBehaviourFileInst = am.LoadAssetsFile(dataAsset.Path);
        var behaviourInf = monoBehaviourFileInst.file.GetAssetInfo(dataAsset.PathId);
        var behaviourBase = am.GetBaseField(monoBehaviourFileInst, behaviourInf);
        
        // open output file
        using var fs = File.Open(outputFilePath, FileMode.Create);
        using var sw = new StreamWriter(fs);

        // dump
        new AssetImportExport().DumpJsonAsset(sw, behaviourBase);
        
        am.UnloadAll();

        Logger.Log("Successfully exported the sprite data.");
        return CommonUtils.ReturnCode.Success;
    }
}