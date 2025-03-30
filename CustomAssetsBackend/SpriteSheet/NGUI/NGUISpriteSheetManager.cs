using AssetsTools.NET;
using AssetsTools.NET.Cpp2IL;
using AssetsTools.NET.Extra;
using CustomAssetsBackend.Classes;
using CustomAssetsBackend.Misc;

namespace CustomAssetsBackend.SpriteSheet.NGUI;

public class NGUISpriteSheetManager(string il2CppFolderPath, string obbPath) : SpriteSheetManager(il2CppFolderPath, obbPath)
{
    public override CommonUtils.ReturnCode Load()
    {
        AssetsManager? am = null;
        try
        {
            Sprites.Clear();

            var uiAtlasAsset = GetCachedAssetOfType(UnityAsset.UnityObjectType.UIAtlas);
            var texture2dAsset = GetCachedAssetOfType(UnityAsset.UnityObjectType.Texture2D);
            var materialAsset = GetCachedAssetOfType(UnityAsset.UnityObjectType.Material);

            if (uiAtlasAsset == UnityAsset.Empty || texture2dAsset == UnityAsset.Empty ||
                materialAsset == UnityAsset.Empty)
            {
                Logger.Log("An asset is missing! Returning.");
                return CommonUtils.ReturnCode.NoSpriteSheetFound;
            }

            am = InitAssetManager(Path.GetDirectoryName(uiAtlasAsset.Path)!);

            Logger.Log("Extracting atlas png..");

            ExportTexture2D(am, texture2dAsset, CommonUtils.AtlasImagePath);

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

                var borderLeft = sprite["borderLeft"].AsInt;
                var borderRight = sprite["borderRight"].AsInt;
                var borderTop = sprite["borderTop"].AsInt;
                var borderBottom = sprite["borderBottom"].AsInt;

                var paddingLeft = sprite["paddingLeft"].AsInt;
                var paddingRight = sprite["paddingRight"].AsInt;
                var paddingTop = sprite["paddingTop"].AsInt;
                var paddingBottom = sprite["paddingBottom"].AsInt;

                Sprites.Add(new NGUISpriteData
                {
                    Name = sprite["name"].AsString,
                    StartX = startX,
                    EndX = startX + width,
                    StartY = startY,
                    EndY = startY + height,
                    Width = width,
                    Height = height,

                    BorderLeft = borderLeft,
                    BorderRight = borderRight,
                    BorderTop = borderTop,
                    BorderBottom = borderBottom,

                    PaddingLeft = paddingLeft,
                    PaddingRight = paddingRight,
                    PaddingTop = paddingTop,
                    PaddingBottom = paddingBottom
                });
            }

            Logger.Log("Reading MonoBehaviour.. Done!");
        }
        catch (Exception err)
        {
            Logger.Log("Unknown error occured during spritesheet loading.", Logger.LogLevel.Exception, err);
            return CommonUtils.ReturnCode.UnknownError;
        }
        finally
        {
            am?.UnloadAll();
        }

        Logger.Log("Successfully loaded the NGUI atlas.");
        return CommonUtils.ReturnCode.Success;
    }

    public override CommonUtils.ReturnCode Save()
    {
        if (Sprites.Count == 0 || !Sprites.All(s => s is NGUISpriteData))
        {
            Logger.Log($"No sprites loaded or a sprite is not of type {nameof(NGUISpriteData)}!");
            return CommonUtils.ReturnCode.NoAtlasLoaded;
        }
        
        var uiAtlasAssets = AssetCache.Where(asset => asset.ObjectType == UnityAsset.UnityObjectType.UIAtlas).ToList();
        var texture2dAsset = GetCachedAssetOfType(UnityAsset.UnityObjectType.Texture2D);
        var materialAsset = GetCachedAssetOfType(UnityAsset.UnityObjectType.Material);
        
        if (uiAtlasAssets.Count <= 0 || texture2dAsset == UnityAsset.Empty || materialAsset == UnityAsset.Empty)
        {
            Logger.Log("An asset is missing! Returning.");
            return CommonUtils.ReturnCode.NoSpriteSheetFound;
        }
        
        var am = InitAssetManager(Path.GetDirectoryName(uiAtlasAssets.First().Path)!);
            
        var textureFileInst = am.LoadAssetsFile(texture2dAsset.Path);
        var textureAssetInfo = textureFileInst.file.GetAssetInfo(texture2dAsset.PathId);
        var textureBaseField = am.GetBaseField(textureFileInst, textureAssetInfo);
            
        Logger.Log("Replacing atlas image..");

        var imagePath = Path.Combine(CommonUtils.HomeAppDataPath, "atlas.png");
        
        var success = TexturePlugin.TextureMain.ReplaceTexture(textureBaseField, imagePath, out var err);

        if (!success || err != null)
        {
            Logger.Log("Failed to replace the atlas image!", Logger.LogLevel.Exception, err);
            am.UnloadAll();
            return CommonUtils.ReturnCode.TextureReplaceFailed;
        }

        SaveAssetsFile(am, textureFileInst, textureAssetInfo, textureBaseField);
        
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
            
            foreach (NGUISpriteData sprite in this.Sprites)
            {
                var spriteTemplate = ValueBuilder.DefaultValueFieldFromArrayTemplate(mSprites);
                
                spriteTemplate["name"].AsString = sprite.Name;
                spriteTemplate["x"].AsInt = (int)Math.Round(sprite.StartX);
                spriteTemplate["y"].AsInt = (int)Math.Round(sprite.StartY);
                spriteTemplate["width"].AsInt = (int)Math.Round(sprite.Width);
                spriteTemplate["height"].AsInt = (int)Math.Round(sprite.Height);

                spriteTemplate["borderLeft"].AsInt = sprite.BorderLeft;
                spriteTemplate["borderRight"].AsInt = sprite.BorderRight;
                spriteTemplate["borderTop"].AsInt = sprite.BorderTop;
                spriteTemplate["borderBottom"].AsInt = sprite.BorderBottom;
                
                spriteTemplate["paddingLeft"].AsInt = sprite.PaddingLeft;
                spriteTemplate["paddingRight"].AsInt = sprite.PaddingRight;
                spriteTemplate["paddingTop"].AsInt = sprite.PaddingTop;
                spriteTemplate["paddingBottom"].AsInt = sprite.PaddingBottom;

                mSprites.Children.Add(spriteTemplate);
            }

            SaveAssetsFile(am, uiAtlasFileInst, uiAtlasInfo, atlasBase);
        }

        Logger.Log("Reconstructing sprite data.. Done!");

        return CommonUtils.ReturnCode.Success;
    }
}