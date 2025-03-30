using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using CustomAssetsBackend.Classes;
using CustomAssetsBackend.Misc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace CustomAssetsBackend;

public class UnityAssetManager(string il2CppFolderPath, string obbPath)
{
    protected readonly string Il2CppFolderPath = il2CppFolderPath;
    
    protected readonly string ObbPath = obbPath;

    protected virtual string PathInResources => "CustomAssetsInjector/";

    protected enum LoadingType
    {
        FromResources,
        FromMemory,
        FromBundle,
        FromStreamedBundle
    }
    
    protected enum UnityAssetBuildTarget
    {
        iOS = 9,
        Android = 13
    }

    protected static void SaveAssetsFile(AssetsManager am, AssetsFileInstance file, AssetFileInfo info, AssetTypeValueField baseField)
    {
        info.SetNewData(baseField);
        
        var newMbAssetPath = Path.GetTempFileName();
            
        using (var writer = new AssetsFileWriter(newMbAssetPath))
        {
            file.file.Write(writer);
        }

        am.UnloadAssetsFile(file);
        
        File.Copy(newMbAssetPath, file.path, true);
        File.Delete(newMbAssetPath);
    }

    protected static void SaveToResources(AssetsManager am, AssetsFileInstance ggmAsset, string pathInResources, int fileId, long pathId)
    {
        var resourceManagerInfo = ggmAsset.file.GetAssetInfo(13);
        var resourceManagerBf = am.GetBaseField(ggmAsset, resourceManagerInfo);
        var containerArray = resourceManagerBf["m_Container.Array"];
        
        var dependencyTemplate = ValueBuilder.DefaultValueFieldFromArrayTemplate(containerArray);
        dependencyTemplate["first"].AsString = pathInResources;
        dependencyTemplate["second.m_FileID"].AsInt = fileId;
        dependencyTemplate["second.m_PathID"].AsLong = pathId;
        containerArray.Children.Add(dependencyTemplate);
        
        SaveAssetsFile(am, ggmAsset, resourceManagerInfo, resourceManagerBf);
    }

    protected static void ExportTexture2D(AssetsManager am, UnityAsset texture2dAsset, string imagePath)
    {
        var texture2DFileInst = am.LoadAssetsFile(texture2dAsset.Path);
        var texture2DAtlasFile = texture2DFileInst.file;
        
        var textureInf = texture2DAtlasFile.GetAssetInfo(texture2dAsset.PathId);
        var textureBase = am.GetBaseField(texture2DFileInst, textureInf);

        var texture = TextureFile.ReadTextureFile(textureBase);
        var encTextureData = texture.FillPictureData(texture2DFileInst);
        var success = texture.DecodeTextureImage(encTextureData, imagePath, ImageExportType.Png);
        if (!success)
        {
            Logger.Log($"Failed to save '{textureBase["m_Name"]}' to '{imagePath}'!");
        }
    }
    
    protected string GetAssetResourcePath(string nameId)
    {
        // ALWAYS USE LOWERCASE!!!!
        // if you use uppercase, due to case conversions unity does (see below), it is IMPOSSIBLE to load the audio!!!!
        return Path.Combine(PathInResources, nameId).ToLower(); 
        
        // when Resources.Load(string) is called, it converts the input string to lowercase and then compares it to every item in ResourceManager
        // if the desired resource's path is not stored in lowercase (e.g. Sound/CustomAssetsInjector/TestSound_1) then
        // when the game compares the two strings, the comparison will fail
        // (Sound/CustomAssetsInjector/TestSound_1 != sound/customassetsinjector/testsound_1), therefore making it impossible to load the asset
    }
    
    /// <summary>
    /// Initializes an <see cref="AssetsManager"/> with the necessary class database.
    /// </summary>
    /// <returns>An <see cref="AssetsManager"/> instance with a loaded class database.</returns>
    public static AssetsManager InitAssetManager(string obbPath)
    {
        // create an AssetsManager
        var am = new AssetsManager();
        using (var classData = new MemoryStream(Resources.ClassDatabase))
            am.LoadClassPackage(classData);
            
        // load globalgamemanagers so we can load a class database for the unity version
        var ggm = am.LoadAssetsFile(Path.Combine(obbPath, "globalgamemanagers"), false);
        am.LoadClassDatabaseFromPackage(ggm.file.Metadata.UnityVersion);

        am.UnloadAssetsFile(ggm);
        
        return am;
    }
}