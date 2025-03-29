using System.Numerics;
using AssetsTools.NET;
using AssetsTools.NET.Cpp2IL;
using AssetsTools.NET.Extra;
using CustomAssetsBackend.Classes;
using CustomAssetsBackend.Misc;

namespace CustomAssetsBackend.SpriteSheet.SmoothMoves;

public struct HeadgearSprite
{
    public SpriteData Data { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 Scale { get; set; }
}

public struct Headgear
{
    public string Name { get; set; }
    public HeadgearSprite FrontSprite { get; set; }
    public HeadgearSprite BackSprite { get; set; }
}

public class SmoothMovesSpriteSheetManager(string il2CppFolderPath, string obbPath) : SpriteSheetManager(il2CppFolderPath, obbPath)
{
    protected override string PathInResources => "Prefabs/0_Generic/1_Interface/BirdEquipment/Headgear/6_CustomAssetsInjector/";

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
            
            var am = InitAssetManager(Path.GetDirectoryName(monoBehaviourAsset.Path)!);

            // Logger.Log($"Texture2D AssetInfo: {texture2dAsset}", Logger.LogLevel.Debug);
            // Logger.Log($"MonoBehaviour AssetInfo: {monoBehaviourAsset}", Logger.LogLevel.Debug);
            // Logger.Log($"Material AssetInfo: {materialAsset}", Logger.LogLevel.Debug);

            // load texture2d

            Logger.Log("Extracting atlas png..");

            ExportTexture2D(am, texture2dAsset, CommonUtils.AtlasImagePath);
            
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

                var guid = behaviourBase["textureGUIDs.Array"][i].AsString;
                var path = behaviourBase["texturePaths.Array"][i].AsString;

                Sprites.Add(new SmoothMovesSpriteData
                {
                    Name = spriteName,

                    StartX = startX,
                    EndX = endX,

                    StartY = startY,
                    EndY = endY,

                    Width = width,
                    Height = height,

                    OriginPoint = originPoint,
                    
                    TextureGuid = guid,
                    TexturePath = path
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
        if (Sprites.Count == 0 || !Sprites.All(s => s is SmoothMovesSpriteData))
        {
            Logger.Log($"No sprites loaded or a sprite is not of type {nameof(SmoothMovesSpriteData)}!");
            return CommonUtils.ReturnCode.NoAtlasLoaded;
        }
        
        var monoBehaviourAsset = GetCachedAssetOfType(UnityAsset.UnityObjectType.MonoBehaviour);
        var texture2dAsset = GetCachedAssetOfType(UnityAsset.UnityObjectType.Texture2D);
        var materialAsset = GetCachedAssetOfType(UnityAsset.UnityObjectType.Material);
        
        if (monoBehaviourAsset == UnityAsset.Empty || texture2dAsset == UnityAsset.Empty || materialAsset == UnityAsset.Empty)
        {
            Logger.Log("An asset is missing! Returning.");
            return CommonUtils.ReturnCode.NoSpriteSheetFound;
        }
        
        var am = InitAssetManager(Path.GetDirectoryName(monoBehaviourAsset.Path)!);
            
        var textureFileInst = am.LoadAssetsFile(texture2dAsset.Path);
        var textureAssetInfo = textureFileInst.file.GetAssetInfo(texture2dAsset.PathId);
        var textureBaseField = am.GetBaseField(textureFileInst, textureAssetInfo);
            
        Logger.Log("Replacing atlas image..");
        
        var success = TexturePlugin.TextureMain.ReplaceTexture(textureBaseField, CommonUtils.AtlasImagePath, out var err);

        if (!success || err != null)
        {
            Logger.Log("Failed to replace the atlas image!", Logger.LogLevel.Exception, err);
            return CommonUtils.ReturnCode.TextureReplaceFailed;
        }

        SaveAssetsFile(am, textureFileInst, textureAssetInfo, textureBaseField);
        
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
        
        foreach (SmoothMovesSpriteData sprite in this.Sprites)
        {
            CreateAndAddSpriteToObb(sprite, behaviourBase);
        }
        
        // regenerate lastBuildID because why not
        behaviourBase["lastBuildID"].AsString = DateTime.Now.ToString("yyyyMMddHHmmss") + new Random().Next(0, 1024);

        SaveAssetsFile(am, monoBehaviourFileInst, behaviourInfo, behaviourBase);
            
        Logger.Log("Reconstructing MonoBehaviour.. Done!");
        
        return CommonUtils.ReturnCode.Success;
    }

    private void CreateAndAddSpriteToObb(SmoothMovesSpriteData sprite, AssetTypeValueField behaviourBase)
    {
        var (resWidth, resHeight) = CommonUtils.GetImageResolution(CommonUtils.AtlasImagePath);
        
        var uvs = behaviourBase["uvs.Array"];
        var textureGuids = behaviourBase["textureGUIDs.Array"];
        var textureSizes = behaviourBase["textureSizes.Array"];
        var defaultPivotOffsets = behaviourBase["defaultPivotOffsets.Array"];
        var textureNames = behaviourBase["textureNames.Array"];
        var texturePaths = behaviourBase["texturePaths.Array"];
        
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
        guidTemplate.AsString = sprite.TextureGuid ?? CreateNewSmoothMovesGuid();
            
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
        pathTemplate.AsString = sprite.TexturePath ?? $"Assets/Heroic/CustomAssetsInjector/{sprite.Name}.png";
            
        texturePaths.Children.Add(pathTemplate);
    }

    public void CreateHeadgear(Headgear headgear, string obbPath)
    {
        // Logger.Log($"Currently creating headgear for: '{headgear.HeadgearName}'");
        using var headgearTemplateStream = new MemoryStream(Resources.HeadgearTemplate);
        var destPath = Path.Combine(obbPath, headgear.Name);

        var am = InitAssetManager(obbPath);
        
        var globalMetadataPath = Path.Combine(this.Il2CppFolderPath, "global-metadata.dat");
        var binaryPath = Path.Combine(this.Il2CppFolderPath, "il2cpp.binary");
            
        am.MonoTempGenerator = new Cpp2IlTempGenerator(globalMetadataPath, binaryPath);
        
        var headgearAsset = am.LoadAssetsFile(headgearTemplateStream, destPath);
        
        var frontAssetInfo = headgearAsset.file.GetAssetInfo(1);
        var backAssetInfo = headgearAsset.file.GetAssetInfo(2);
        var mainAssetInfo = headgearAsset.file.GetAssetInfo(3);
        var frontBf = am.GetBaseField(headgearAsset, frontAssetInfo);
        var backBf = am.GetBaseField(headgearAsset, backAssetInfo);
            
        var mainAssetBf = am.GetBaseField(headgearAsset, mainAssetInfo);
        mainAssetBf["m_Name"].AsString = headgear.Name;
        mainAssetInfo.SetNewData(mainAssetBf);
        
        var materialAsset = GetCachedAssetOfType(UnityAsset.UnityObjectType.Material);
        var monoBehaviourAsset = GetCachedAssetOfType(UnityAsset.UnityObjectType.MonoBehaviour);
        
        Logger.Log("Adding dependencies..");

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
        
        // we add 1 to the index because Metadata.Externals' 0th element = file id 1 because file id 0 = same assets file
        var matFileId = dependencies.IndexOf(materialDependency) + 1;
        var mbFileId = dependencies.IndexOf(mbDependency) + 1;

        Logger.Log("Adding dependencies.. Done!");

        // front scope
        {
            Logger.Log("Updating front GameObject..");
            
            // set transform stuff
            var transformPPtr = frontBf["m_Component.Array"][0]["component"];
            var transformAsset = am.GetExtAsset(headgearAsset, transformPPtr);
            var transformBf = transformAsset.baseField;
            transformBf["m_LocalPosition.x"].AsFloat = headgear.FrontSprite.Position.X;
            transformBf["m_LocalPosition.y"].AsFloat = headgear.FrontSprite.Position.Y;
            
            transformBf["m_LocalScale.x"].AsFloat = headgear.FrontSprite.Scale.X;
            transformBf["m_LocalScale.y"].AsFloat = headgear.FrontSprite.Scale.Y;
            
            transformAsset.info.SetNewData(transformBf);
            
            // set chmeshsprite stuff
            var monoBehaviourPPtr = frontBf["m_Component.Array"][1]["component"];
            var chMeshSprite = am.GetExtAsset(headgearAsset, monoBehaviourPPtr);
            var chMeshSpriteBf = chMeshSprite.baseField;
            chMeshSpriteBf["m_SpriteName"].AsString = headgear.FrontSprite.Data.Name;
            chMeshSpriteBf["m_Width"].AsInt = (int)headgear.FrontSprite.Data.Width;
            chMeshSpriteBf["m_Height"].AsInt = (int)headgear.FrontSprite.Data.Height;

            chMeshSpriteBf["m_SmoothMovesAtlas"]["m_FileID"].AsInt = mbFileId;
            chMeshSpriteBf["m_SmoothMovesAtlas"]["m_PathID"].AsLong = monoBehaviourAsset.PathId;
            chMeshSprite.info.SetNewData(chMeshSpriteBf);

            var materialPPtr = frontBf["m_Component.Array"][3]["component"];
            var meshRenderer = am.GetExtAsset(headgearAsset, materialPPtr);
            var meshRendererBf = meshRenderer.baseField;

            meshRendererBf["m_Materials.Array"][0]["m_FileID"].AsInt = matFileId;
            meshRendererBf["m_Materials.Array"][0]["m_PathID"].AsLong = materialAsset.PathId;

            meshRenderer.info.SetNewData(meshRendererBf);
            Logger.Log("Updating front GameObject.. Done!");
        }

        // back scope
        {
            Logger.Log("Updating back GameObject..");
            
            // set transform stuff
            var transformPPtr = backBf["m_Component.Array"][0]["component"];
            var transformAsset = am.GetExtAsset(headgearAsset, transformPPtr);
            var transformBf = transformAsset.baseField;
            transformBf["m_LocalPosition.x"].AsFloat = headgear.BackSprite.Position.X;
            transformBf["m_LocalPosition.y"].AsFloat = headgear.BackSprite.Position.Y;
            
            transformBf["m_LocalScale.x"].AsFloat = headgear.BackSprite.Scale.X;
            transformBf["m_LocalScale.y"].AsFloat = headgear.BackSprite.Scale.Y;
            
            transformAsset.info.SetNewData(transformBf);
            
            // set chmeshsprite stuff
            var monoBehaviourPPtr = backBf["m_Component.Array"][1]["component"];
            var chMeshSprite = am.GetExtAsset(headgearAsset, monoBehaviourPPtr);
            var chMeshSpriteBf = chMeshSprite.baseField;
            chMeshSpriteBf["m_SpriteName"].AsString = headgear.BackSprite.Data.Name;
            chMeshSpriteBf["m_Width"].AsInt = (int)headgear.BackSprite.Data.Width;
            chMeshSpriteBf["m_Height"].AsInt = (int)headgear.BackSprite.Data.Height;

            chMeshSpriteBf["m_SmoothMovesAtlas"]["m_FileID"].AsInt = mbFileId;
            chMeshSpriteBf["m_SmoothMovesAtlas"]["m_PathID"].AsLong = monoBehaviourAsset.PathId;
            chMeshSprite.info.SetNewData(chMeshSpriteBf);

            var materialPPtr = backBf["m_Component.Array"][3]["component"];
            var meshRenderer = am.GetExtAsset(headgearAsset, materialPPtr);
            var meshRendererBf = meshRenderer.baseField;

            meshRendererBf["m_Materials.Array"][0]["m_FileID"].AsInt = matFileId;
            meshRendererBf["m_Materials.Array"][0]["m_PathID"].AsLong = materialAsset.PathId;

            meshRenderer.info.SetNewData(meshRendererBf);
            Logger.Log("Updating back GameObject.. Done!");
        }
            
        Logger.Log("Assigning AssetID to prefab..");

        var rootSceneAssetPath = Path.Combine(obbPath, "level1");
        var rootSceneAsset = am.LoadAssetsFile(rootSceneAssetPath);

        headgearAsset.file.Metadata.TargetPlatform = rootSceneAsset.file.Metadata.TargetPlatform;
            
        // chraeap = character high-res and equipment asset provider (headgear and equipment)
        
        var chraeapInfo = rootSceneAsset.file.GetAssetInfo(120); // todo: un-hardcode
        var chraeapBf = am.GetBaseField(rootSceneAsset, chraeapInfo);

        // todo: there is probably a better way to check this earlier, check AudioInjector.cs
        if (chraeapBf["AssetInfos.Array"].Children.Any(info => info["NameId"].AsString == headgear.Name))
        {
            // headgear with same name already exists
            Logger.Log("A headgear with this name already exists! Unable to create headgear.");
            return;
        }
        
        using (var writer = new AssetsFileWriter(destPath))
        {
            headgearAsset.file.Write(writer);
        }
        am.UnloadAssetsFile(headgearAsset);
        
        var ggmPath = Path.Combine(ObbPath, "globalgamemanagers");
        var ggmAsset = am.LoadAssetsFile(ggmPath);
        var pathInResources = GetAssetResourcePath(headgear.Name);
        
        ggmAsset.file.Metadata.Externals.Add(new AssetsFileExternal
        {
            VirtualAssetPathName = string.Empty,
            PathName = Path.GetFileName(destPath),
            OriginalPathName = Path.GetFileName(destPath),
            Guid = default,
            Type = AssetsFileExternalType.Normal
        });
        
        SaveToResources(am, ggmAsset, pathInResources, ggmAsset.file.Metadata.Externals.Count, 3);
        
        var editorAssetInfo = ValueBuilder.DefaultValueFieldFromArrayTemplate(chraeapBf["AssetInfos.Array"]);
        editorAssetInfo["NameId"].AsString = headgear.Name;
        editorAssetInfo["AssetLoadingType"].AsInt = (int)LoadingType.FromResources;
        editorAssetInfo["Path"].AsString = pathInResources;
        editorAssetInfo["Extension"].AsString = "prefab";
        
        chraeapBf["AssetInfos.Array"].Children.Add(editorAssetInfo);

        SaveAssetsFile(am, rootSceneAsset, chraeapInfo, chraeapBf);
        
        am.UnloadAll();
        
        Logger.Log("Assigning AssetID to prefab.. Done!");
        Logger.Log($"Headgear creation for '{headgear.Name}' done.");
    }

    /// <summary>
    /// Creates a GUID without any dashes.
    /// </summary>
    /// <returns>The GUID</returns>
    public static string CreateNewSmoothMovesGuid() => Guid.NewGuid().ToString().Replace("-", string.Empty);
}