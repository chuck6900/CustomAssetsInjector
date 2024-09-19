using AssetsTools.NET;
using AssetsTools.NET.Cpp2IL;
using AssetsTools.NET.Extra;
using CustomAssetsBackend.Classes;
using CustomAssetsBackend.Misc;

namespace CustomAssetsBackend.SpriteSheet.NGUI;

public class NGUISpriteSheetManager(string il2CppFolderPath) : SpriteSheetManager(il2CppFolderPath)
{
    public override CommonUtils.ReturnCode Load()
    {
        try
        {
            UnityAsset uiAtlasAsset =
                AssetCache.FirstOrDefault(asset => asset.ObjectType == UnityAsset.UnityObjectType.UIAtlas) ??
                UnityAsset.Empty;
            UnityAsset texture2dAsset =
                AssetCache.FirstOrDefault(asset => asset.ObjectType == UnityAsset.UnityObjectType.Texture2D) ??
                UnityAsset.Empty;
            UnityAsset materialAsset =
                AssetCache.FirstOrDefault(asset => asset.ObjectType == UnityAsset.UnityObjectType.Material) ??
                UnityAsset.Empty;

            if (uiAtlasAsset == UnityAsset.Empty || texture2dAsset == UnityAsset.Empty || materialAsset == UnityAsset.Empty)
            {
                Logger.Log("An asset is missing! Returning.");
                return CommonUtils.ReturnCode.NoSpriteSheetFound;
            }
            
            var am = CommonUtils.InitAssetManager(Path.GetDirectoryName(uiAtlasAsset.Path)!);

            Logger.Log("Extracting atlas png..");

            this.ExportTexture2D(am, texture2dAsset, CommonUtils.AtlasImagePath);

            Logger.Log("Extracting atlas png.. Done!");

            var globalMetadataPath = Path.Combine(this.Il2CppFolderPath, "global-metadata.dat");
            var binaryPath = Path.Combine(this.Il2CppFolderPath, "il2cpp.binary");
            
            Logger.Log("Reading MonoBehaviour..");
            
            am.MonoTempGenerator = new Cpp2IlTempGenerator(globalMetadataPath, binaryPath);

            // load uiatlas monobehaviour

            var uiAtlasFileInst = am.LoadAssetsFile(uiAtlasAsset.Path);
            var uiAtlasFile = uiAtlasFileInst.file;

            var uiAtlasInf = uiAtlasFile.GetAssetInfo(uiAtlasAsset.PathId);
            var atlasBase = am.GetBaseField(uiAtlasFileInst, uiAtlasInf);

            // parse sprites and add them to spritedata list

            var sprites = atlasBase["mSprites.Array"].ToList();

            foreach (var sprite in sprites)
            {
                var startX = sprite["x"].AsInt;
                var startY = sprite["y"].AsInt;
                var width = sprite["width"].AsInt;
                var height = sprite["height"].AsInt;
                Sprites.Add(new SpriteData
                {
                    Name = sprite["name"].AsString,
                    StartX = startX,
                    EndX = startX + width,
                    StartY = startY,
                    EndY = startY + height,
                    Width = width,
                    Height = height
                });
            }
            
            Logger.Log("Reading MonoBehaviour.. Done!");
            
            am.UnloadAll();
        }
        catch (Exception err)
        {
            Logger.Log("Unknown error occured during spritesheet loading.", Logger.LogLevel.Exception, err);
            return CommonUtils.ReturnCode.UnknownError;
        }

        Logger.Log("Successfully loaded the NGUI atlas.");
        return CommonUtils.ReturnCode.Success;
    }

    public override CommonUtils.ReturnCode Save()
    {
        var uiAtlasAssets = AssetCache.Where(asset => asset.ObjectType == UnityAsset.UnityObjectType.UIAtlas).ToList();
        var texture2dAsset = AssetCache.FirstOrDefault(asset => asset.ObjectType == UnityAsset.UnityObjectType.Texture2D) ?? UnityAsset.Empty;
        var materialAsset = AssetCache.FirstOrDefault(asset => asset.ObjectType == UnityAsset.UnityObjectType.Material) ?? UnityAsset.Empty;
        
        if (uiAtlasAssets.Count <= 0 || texture2dAsset == UnityAsset.Empty || materialAsset == UnityAsset.Empty)
        {
            Logger.Log("An asset is missing! Returning.");
            return CommonUtils.ReturnCode.NoSpriteSheetFound;
        }
        
        var am = CommonUtils.InitAssetManager(Path.GetDirectoryName(uiAtlasAssets.First().Path)!);
            
        var textureFileInst = am.LoadAssetsFile(texture2dAsset.Path);
        var textureAssetInfo = textureFileInst.file.GetAssetInfo(texture2dAsset.PathId);
        var textureBaseField = am.GetBaseField(textureFileInst, textureAssetInfo);
            
        Logger.Log("Replacing atlas image..");

        var imagePath = Path.Combine(CommonUtils.HomeAppDataPath, "atlas.png");
        
        var newBaseField = TexturePlugin.TextureMain.ReplaceTexture(textureFileInst, textureBaseField, imagePath);
        textureAssetInfo.SetNewData(newBaseField);

        var tempTex2dAssetPath = texture2dAsset.Path + "-tmp";
        
        using (var writer = new AssetsFileWriter(tempTex2dAssetPath))
        {
            textureFileInst.file.Write(writer);
        }
            
        am.UnloadAll();
        
        File.Replace(tempTex2dAssetPath, texture2dAsset.Path, null);
        
        Logger.Log("Replacing atlas image.. Done!");

        var globalMetadataPath = Path.Combine(this.Il2CppFolderPath, "global-metadata.dat");
        var binaryPath = Path.Combine(this.Il2CppFolderPath, "il2cpp.binary");
            
        am.MonoTempGenerator = new Cpp2IlTempGenerator(globalMetadataPath, binaryPath);

        Logger.Log("Reconstructing sprite data..");
        
        foreach (var uiAtlasAsset in uiAtlasAssets)
        {
            var uiAtlasFileInst = am.LoadAssetsFile(uiAtlasAsset.Path);
            var uiAtlasFile = uiAtlasFileInst.file;

            var uiAtlasInfo = uiAtlasFile.GetAssetInfo(uiAtlasAsset.PathId);
            var atlasBase = am.GetBaseField(uiAtlasFileInst, uiAtlasInfo);

            // clear fields, reconstruct data from scratch

            var mSprites = atlasBase["mSprites.Array"];

            mSprites.Children.Clear();
            
            foreach (var sprite in this.Sprites)
            {
                var spriteTemplate = ValueBuilder.DefaultValueFieldFromArrayTemplate(mSprites);
                
                spriteTemplate["name"].AsString = sprite.Name;
                spriteTemplate["x"].AsInt = (int)Math.Round(sprite.StartX);
                spriteTemplate["y"].AsInt = (int)Math.Round(sprite.StartY);
                spriteTemplate["width"].AsInt = (int)Math.Round(sprite.Width);
                spriteTemplate["height"].AsInt = (int)Math.Round(sprite.Height);

                mSprites.Children.Add(spriteTemplate);
            }

            uiAtlasInfo.SetNewData(atlasBase);

            var tempAssetFilePath = $"{Path.ChangeExtension(uiAtlasAsset.Path, null)}-{uiAtlasAsset.Name}-tmp";

            using (var writer = new AssetsFileWriter(tempAssetFilePath))
            {
                uiAtlasFile.Write(writer);
            }

            am.UnloadAssetsFile(uiAtlasFileInst);

            File.Replace(tempAssetFilePath, uiAtlasAsset.Path, null);
        }

        Logger.Log("Reconstructing sprite data.. Done!");

        return CommonUtils.ReturnCode.Success;
    }
}