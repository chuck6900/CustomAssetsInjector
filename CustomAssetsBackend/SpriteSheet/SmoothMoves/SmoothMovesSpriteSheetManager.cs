using System.Numerics;
using AssetsTools.NET;
using AssetsTools.NET.Cpp2IL;
using AssetsTools.NET.Extra;
using CustomAssetsBackend.Classes;
using CustomAssetsBackend.Misc;

namespace CustomAssetsBackend.SpriteSheet.SmoothMoves;

public class SmoothMovesSpriteSheetManager(string il2CppFolderPath) : SpriteSheetManager(il2CppFolderPath)
{
    public override CommonUtils.ReturnCode Load()
    {
        try
        {
            Sprites.Clear();

            var monoBehaviourAsset = GetCachedAssetOfType(UnityAsset.UnityObjectType.MonoBehaviour);
            var texture2dAsset = GetCachedAssetOfType(UnityAsset.UnityObjectType.Texture2D);
            var materialAsset = GetCachedAssetOfType(UnityAsset.UnityObjectType.Material);
            
            if (texture2dAsset == UnityAsset.Empty || monoBehaviourAsset == UnityAsset.Empty || materialAsset == UnityAsset.Empty)
            {
                Logger.Log("An asset is missing! Returning.");
                return CommonUtils.ReturnCode.NoSpriteSheetFound;
            }
            
            var am = CommonUtils.InitAssetManager(Path.GetDirectoryName(monoBehaviourAsset.Path)!);

            // Logger.Log($"Texture2D AssetInfo: {texture2dAsset}", Logger.LogLevel.Debug);
            // Logger.Log($"MonoBehaviour AssetInfo: {monoBehaviourAsset}", Logger.LogLevel.Debug);
            // Logger.Log($"Material AssetInfo: {materialAsset}", Logger.LogLevel.Debug);

            // load texture2d

            Logger.Log("Extracting atlas png..");
            
            this.ExportTexture2D(am, texture2dAsset, CommonUtils.AtlasImagePath);
            
            Logger.Log("Extracting atlas png.. Done!");

            // get texture atlas
            
            var globalMetadataPath = Path.Combine(this.Il2CppFolderPath, "global-metadata.dat");
            var binaryPath = Path.Combine(this.Il2CppFolderPath, "il2cpp.binary");
            
            Logger.Log("Reading MonoBehaviour..");
            
            am.MonoTempGenerator = new Cpp2IlTempGenerator(globalMetadataPath, binaryPath);

            var monoBehaviourFileInst = am.LoadAssetsFile(monoBehaviourAsset.Path);
            var monoBehaviourFile = monoBehaviourFileInst.file;

            var behaviourInf = monoBehaviourFile.GetAssetInfo(monoBehaviourAsset.PathId);
            var behaviourBase = am.GetBaseField(monoBehaviourFileInst, behaviourInf);
            
            // create spritedata list

            var uvs = behaviourBase["uvs.Array"].ToList();
            var textureNames = behaviourBase["textureNames.Array"].ToList();
            var defaultPivotOffsets = behaviourBase["defaultPivotOffsets.Array"].ToList();

            var (resWidth, resHeight) = CommonUtils.GetImageResolution(CommonUtils.AtlasImagePath);
            
            for (int i = 0; i < uvs.Count; i++)
            {
                var spritePosRect = uvs[i];

                var width = spritePosRect["width"].AsFloat * resWidth;
                var height = spritePosRect["height"].AsFloat * resHeight;
                
                var startX = spritePosRect["x"].AsFloat * resWidth;
                var endX = startX + width;

                var y = spritePosRect["y"].AsFloat * resHeight;
                var endY = CommonUtils.MapValues(y, 0, resHeight, resHeight, 0);
                var startY = endY - height;

                var spriteName = textureNames[i].AsString;
                var defaultPivotOffset = new Vector2
                {
                    X = defaultPivotOffsets[i]["x"].AsFloat,
                    Y = defaultPivotOffsets[i]["y"].AsFloat
                };

                var newX = (float)CommonUtils.MapValues(defaultPivotOffset.X, -0.5, 0.5, 0, 1);
                var newY = (float)CommonUtils.MapValues(defaultPivotOffset.Y, -0.5, 0.5, 0, 1);

                var originPoint = new Vector2(newX, newY);

                Sprites.Add(new SpriteData
                {
                    Name = spriteName,

                    StartX = startX,
                    EndX = endX,

                    StartY = startY,
                    EndY = endY,

                    Width = width,
                    Height = height,

                    OriginPoint = originPoint
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
        
        Logger.Log("Successfully loaded the SmoothMoves atlas.");
        return CommonUtils.ReturnCode.Success;
    }
    
    public override CommonUtils.ReturnCode Save()
    {
        var monoBehaviourAsset = GetCachedAssetOfType(UnityAsset.UnityObjectType.MonoBehaviour);
        var texture2dAsset = GetCachedAssetOfType(UnityAsset.UnityObjectType.Texture2D);
        var materialAsset = GetCachedAssetOfType(UnityAsset.UnityObjectType.Material);
        
        if (monoBehaviourAsset == UnityAsset.Empty || texture2dAsset == UnityAsset.Empty || materialAsset == UnityAsset.Empty)
        {
            Logger.Log("An asset is missing! Returning.");
            return CommonUtils.ReturnCode.NoSpriteSheetFound;
        }
        
        var am = CommonUtils.InitAssetManager(Path.GetDirectoryName(monoBehaviourAsset.Path)!);
            
        var textureFileInst = am.LoadAssetsFile(texture2dAsset.Path);
        var textureAssetInfo = textureFileInst.file.GetAssetInfo(texture2dAsset.PathId);
        var textureBaseField = am.GetBaseField(textureFileInst, textureAssetInfo);
            
        Logger.Log("Replacing atlas image..");

        var imagePath = CommonUtils.AtlasImagePath;
        
        var success = TexturePlugin.TextureMain.ReplaceTexture(textureBaseField, imagePath, out var err);

        if (!success)
        {
            Logger.Log("Failed to replace the atlas image!", Logger.LogLevel.Exception, err);
            return CommonUtils.ReturnCode.TextureReplaceFailed;
        }
        
        Logger.Log("Replacing atlas image.. Done!");

        var globalMetadataPath = Path.Combine(this.Il2CppFolderPath, "global-metadata.dat");
        var binaryPath = Path.Combine(this.Il2CppFolderPath, "il2cpp.binary");
            
        am.MonoTempGenerator = new Cpp2IlTempGenerator(globalMetadataPath, binaryPath);
        
        var monoBehaviourFileInst = am.LoadAssetsFile(monoBehaviourAsset.Path);
        var monoBehaviourFile = monoBehaviourFileInst.file;
        
        var behaviourInfo = monoBehaviourFile.GetAssetInfo(monoBehaviourAsset.PathId);
        var behaviourBase = am.GetBaseField(monoBehaviourFileInst, behaviourInfo);
        
        Logger.Log("Reconstructing MonoBehaviour..");
        
        // clear fields, reconstruct data from scratch
        
        var uvs = behaviourBase["uvs.Array"];
        var textureGuids = behaviourBase["textureGUIDs.Array"];
        var textureSizes = behaviourBase["textureSizes.Array"];
        var defaultPivotOffsets = behaviourBase["defaultPivotOffsets.Array"];
        var textureNames = behaviourBase["textureNames.Array"];
        var texturePaths = behaviourBase["texturePaths.Array"];
        
        uvs.Children.Clear();
        textureGuids.Children.Clear();
        textureSizes.Children.Clear();
        defaultPivotOffsets.Children.Clear();
        textureNames.Children.Clear();
        texturePaths.Children.Clear();

        var (resWidth, resHeight) = CommonUtils.GetImageResolution(imagePath);
        
        foreach (var sprite in this.Sprites)
        {
            var uvTemplate = ValueBuilder.DefaultValueFieldFromArrayTemplate(uvs);
            var guidTemplate = ValueBuilder.DefaultValueFieldFromArrayTemplate(textureGuids);
            var sizeTemplate = ValueBuilder.DefaultValueFieldFromArrayTemplate(textureSizes);
            var pivotTemplate = ValueBuilder.DefaultValueFieldFromArrayTemplate(defaultPivotOffsets);
            var nameTemplate = ValueBuilder.DefaultValueFieldFromArrayTemplate(textureNames);
            var pathTemplate = ValueBuilder.DefaultValueFieldFromArrayTemplate(texturePaths);

            // uvs
            uvTemplate["x"].AsFloat = (float)sprite.StartX / resWidth;
            uvTemplate["y"].AsFloat = (float)CommonUtils.MapValues(sprite.EndY, 0, resHeight, resHeight, 0) / resHeight;
            uvTemplate["width"].AsFloat = (float)sprite.Width / resWidth;
            uvTemplate["height"].AsFloat = (float)sprite.Height / resHeight;
            
            uvs.Children.Add(uvTemplate);
            
            // guids
            guidTemplate.AsString = CreateNewSmoothMovesGuid();
            
            textureGuids.Children.Add(guidTemplate);
            
            // texture sizes
            sizeTemplate["x"].AsDouble = sprite.Width;
            sizeTemplate["y"].AsDouble = sprite.Height;
            
            textureSizes.Children.Add(sizeTemplate);
            
            // defaultPivotOffsets (origin point)
            pivotTemplate["x"].AsFloat = (float)CommonUtils.MapValues(sprite.OriginPoint.X, 0, 1, -0.5, 0.5);
            pivotTemplate["y"].AsFloat = (float)CommonUtils.MapValues(sprite.OriginPoint.Y, 0, 1, -0.5, 0.5);
            
            defaultPivotOffsets.Children.Add(pivotTemplate);
            
            // texture names
            nameTemplate.AsString = sprite.Name;
            
            textureNames.Children.Add(nameTemplate);
            
            // texture paths
            pathTemplate.AsString = $"Assets/Heroic/CustomAssetInjector/{sprite.Name}.png";
            
            texturePaths.Children.Add(pathTemplate);
        }
        
        // regenerate lastBuildID because why not
        behaviourBase["lastBuildID"].AsString = DateTime.Now.ToString("yyyyMMddHHmmss") + new Random().Next(0, 1024);
        
        behaviourInfo.SetNewData(behaviourBase);
        
        var newMbAssetPath = Path.GetTempFileName();
            
        using (var writer = new AssetsFileWriter(newMbAssetPath))
        {
            monoBehaviourFile.Write(writer);
        }
            
        am.UnloadAll();
            
        File.Replace(newMbAssetPath, monoBehaviourAsset.Path, null);
            
        Logger.Log("Reconstructing MonoBehaviour.. Done!");
        
        return CommonUtils.ReturnCode.Success;
    }

    public struct Headgear
    {
        public string Name { get; set; }
        public SpriteData? FrontSprite { get; set; }
        public SpriteData? BackSprite { get; set; }
    }

    private void CreateHeadgear(Headgear headgear, string outputPath, string obbPath)
    {
        // Logger.Log($"Currently creating headgear for: '{headgear.HeadgearName}'");
        using var headgearTemplateStream = new MemoryStream(Resources.HeadgearTemplate);
        var destPath = Path.Combine(outputPath, headgear.Name);

        var am = CommonUtils.InitAssetManager(obbPath);
        
        var headgearAsset = am.LoadAssetsFile(headgearTemplateStream, destPath);
        var frontAssetInfo = headgearAsset.file.GetAssetInfo(1);
        var backAssetInfo = headgearAsset.file.GetAssetInfo(2);
        var mainAssetInfo = headgearAsset.file.GetAssetInfo(3);
        var frontBf = am.GetBaseField(headgearAsset, frontAssetInfo);
        var backBf = am.GetBaseField(headgearAsset, backAssetInfo);
            
        var mainAssetBf = am.GetBaseField(headgearAsset, mainAssetInfo);
        mainAssetBf["m_Name"].AsString = headgear.Name;
        mainAssetInfo.SetNewData(mainAssetBf);
        
        Logger.Log("Adding dependencies..");

        var materialAsset = GetCachedAssetOfType(UnityAsset.UnityObjectType.Material);
        var monoBehaviourAsset = GetCachedAssetOfType(UnityAsset.UnityObjectType.MonoBehaviour);

        var dependencies = headgearAsset.file.Metadata.Externals;
        
        // add material dependency to headgear asset
        var materialDependency = dependencies.FirstOrDefault(dep => dep.PathName == Path.GetFileName(materialAsset.Path));
        if (materialDependency == null)
        {
            materialDependency = new AssetsFileExternal
            {
                VirtualAssetPathName = string.Empty,
                PathName = Path.GetFileName(materialAsset.Path),
                OriginalPathName = Path.GetFileName(materialAsset.Path),
                Guid = default,
                Type = AssetsFileExternalType.Normal
            };
            dependencies.Add(materialDependency);
        }

        // add monobehaviour dependency to headgear asset
        var mbDependency = dependencies.FirstOrDefault(dep => dep.PathName == Path.GetFileName(monoBehaviourAsset.Path));
        if (mbDependency == null)
        {
            mbDependency = new AssetsFileExternal
            {
                VirtualAssetPathName = string.Empty,
                PathName = Path.GetFileName(monoBehaviourAsset.Path),
                OriginalPathName = Path.GetFileName(monoBehaviourAsset.Path),
                Guid = default,
                Type = AssetsFileExternalType.Normal
            };
            dependencies.Add(mbDependency);
        }
        
        var matFileId = dependencies.IndexOf(materialDependency);
        var mbFileId = dependencies.IndexOf(mbDependency);

        Logger.Log("Adding dependencies.. Done!");
            
        if (headgear.FrontSprite != null)
        {
            Logger.Log("Setting references on Front GameObject..");
        
            var monoBehaviourPPtr = frontBf["m_Component.Array"][1]["component"];
            var chMeshSprite = am.GetExtAsset(headgearAsset, monoBehaviourPPtr);
            var chMeshSpriteBf = chMeshSprite.baseField;
            chMeshSpriteBf["m_SpriteName"].AsString = headgear.FrontSprite.Name;
            chMeshSpriteBf["m_Width"].AsInt = (int)headgear.FrontSprite.Width;
            chMeshSpriteBf["m_Height"].AsInt = (int)headgear.FrontSprite.Height;
        
            chMeshSpriteBf["m_SmoothMovesAtlas"]["m_FileID"].AsInt = mbFileId;
            chMeshSpriteBf["m_SmoothMovesAtlas"]["m_PathID"].AsLong = monoBehaviourAsset.PathId;
            chMeshSprite.info.SetNewData(chMeshSpriteBf);
        
            var materialPPtr = frontBf["m_Component.Array"][3]["component"];
            var meshRenderer = am.GetExtAsset(headgearAsset, materialPPtr);
            var meshRendererBf = meshRenderer.baseField;
        
            meshRendererBf["m_Materials.Array"][0]["m_FileID"].AsInt = matFileId;
            meshRendererBf["m_Materials.Array"][0]["m_PathID"].AsLong = materialAsset.PathId;
        
            meshRenderer.info.SetNewData(meshRendererBf);
            Logger.Log("Setting references on Front GameObject.. Done!");
        }
        if (headgear.BackSprite != null)
        {
            Logger.Log("Setting references on Back GameObject..");
        
            var monoBehaviourPPtr = backBf["m_Component.Array"][1]["component"];
            var chMeshSprite = am.GetExtAsset(headgearAsset, monoBehaviourPPtr);
            var chMeshSpriteBf = chMeshSprite.baseField;
            chMeshSpriteBf["m_SpriteName"].AsString = headgear.BackSprite.Name;
            chMeshSpriteBf["m_Width"].AsInt = (int)headgear.BackSprite.Width;
            chMeshSpriteBf["m_Height"].AsInt = (int)headgear.BackSprite.Height;
        
            chMeshSpriteBf["m_SmoothMovesAtlas"]["m_FileID"].AsInt = mbFileId;
            chMeshSpriteBf["m_SmoothMovesAtlas"]["m_PathID"].AsLong = monoBehaviourAsset.PathId;
            chMeshSprite.info.SetNewData(chMeshSpriteBf);
        
            var materialPPtr = backBf["m_Component.Array"][3]["component"];
            var meshRenderer = am.GetExtAsset(headgearAsset, materialPPtr);
            var meshRendererBf = meshRenderer.baseField;
        
            meshRendererBf["m_Materials.Array"][0]["m_FileID"].AsInt = matFileId;
            meshRendererBf["m_Materials.Array"][0]["m_PathID"].AsLong = materialAsset.PathId;
        
            meshRenderer.info.SetNewData(meshRendererBf);
            Logger.Log("Setting references on Back GameObject.. Done!");
        }
        Logger.Log("Saving prefab..");
        
        File.Delete(destPath);
        using (var writer = new AssetsFileWriter(destPath))
        {
            headgearAsset.file.Write(writer);
        }
        am.UnloadAll();
            
        Logger.Log("Saving prefab.. Done");
            
        Logger.Log("Assigning AssetID to prefab..");

        var rootSceneAssetPath = Path.Combine(obbPath, "assets/bin/Data/level1");
        var rootSceneAsset = am.LoadAssetsFile(rootSceneAssetPath);
        var headgearDependencyFileId = rootSceneAsset.file.Metadata.Externals.Count + 1;
            
        rootSceneAsset.file.Metadata.Externals.Add(new AssetsFileExternal
        {
            VirtualAssetPathName = string.Empty,
            PathName = Path.GetFileName(destPath),
            OriginalPathName = Path.GetFileName(destPath),
            Guid = default,
            Type = AssetsFileExternalType.Normal
        });
            
        // chraeap = character high-res and equipment asset provider (headgear and equipment)
        
        var chraeapInfo = rootSceneAsset.file.GetAssetInfo(120); // un-hardcode
        var chraeapBf = am.GetBaseField(rootSceneAsset, chraeapInfo);
        
        var editorAssetInfo = ValueBuilder.DefaultValueFieldFromArrayTemplate(chraeapBf["AssetInfos.Array"]);
        editorAssetInfo["NameId"].AsString = headgear.Name;
        editorAssetInfo["AssetLink"]["m_FileID"].AsInt = headgearDependencyFileId;
        editorAssetInfo["AssetLink"]["m_PathID"].AsLong = 3;
        editorAssetInfo["AssetLoadingType"].AsInt = 1; // memory
        
        chraeapBf["AssetInfos.Array"].Children.Add(editorAssetInfo);
            
        chraeapInfo.SetNewData(chraeapBf);

        var newRootSceneAssetPath = Path.GetTempFileName();
        using (var writer = new AssetsFileWriter(newRootSceneAssetPath))
        {
            rootSceneAsset.file.Write(writer);
        }
        am.UnloadAll();
        File.Replace(newRootSceneAssetPath, rootSceneAssetPath, null);
        Logger.Log("Assigning AssetID to prefab.. Done!");
        Logger.Log($"Headgear creation for '{headgear.Name}' done.");
    }

    /// <summary>
    /// Creates a GUID without any dashes.
    /// </summary>
    /// <returns>The GUID</returns>
    public static string CreateNewSmoothMovesGuid() => Guid.NewGuid().ToString().Replace("-", string.Empty);
}