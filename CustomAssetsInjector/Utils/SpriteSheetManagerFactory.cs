using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetsTools.NET.Cpp2IL;
using AssetsTools.NET.Extra;
using Avalonia.Media;
using CustomAssetsBackend.Classes;
using CustomAssetsBackend.Misc;
using CustomAssetsBackend.SpriteSheet;
using CustomAssetsBackend.SpriteSheet.NGUI;
using CustomAssetsBackend.SpriteSheet.SmoothMoves;
using CustomAssetsInjector.Services;

namespace CustomAssetsInjector.Utils;

public class CachedAsset
{
    public required string Name { get; set; }
    public bool IsSmoothMovesSpriteSheet { get; set; }
    public bool IsLowRes { get; set; }
    public List<UnityAsset> Assets { get; set; } = new();

    public bool AnyAssetIsNull()
    {
        return Assets.Any(asset => asset == null || asset == UnityAsset.Empty);
    }
}

public static class SpriteSheetManagerFactory
{
    private static List<CachedAsset> m_SpriteSheetCache = new();

    private static void LoadCacheFromPrefs()
    {
        var prefs = PreferenceService.GetPrefs();
        var newCachedItems = prefs.AssetCache.Except(m_SpriteSheetCache);
        
        m_SpriteSheetCache.AddRange(newCachedItems);
    }
    
    private static void SaveCacheToPrefs()
    {
        var prefs = PreferenceService.GetPrefs();

        var newCachedItems = m_SpriteSheetCache.Except(prefs.AssetCache);
        prefs.AssetCache.AddRange(newCachedItems);
        
        PreferenceService.SetPrefs(prefs);
        PreferenceService.SavePrefs();
    }
    
    public static (CommonUtils.ReturnCode, SpriteSheetManager?) CreateSpriteSheetManager(string atlasName, bool lowRes)
    {
        // load cache
        if (m_SpriteSheetCache.Count <= 0)
            LoadCacheFromPrefs();
        
        // remove suffixes
        atlasName = atlasName
            .Replace("_LR", string.Empty)
            .Replace("_HR", string.Empty)
            .Replace("_low", string.Empty);
            
        // find the atlas file in the obb
            
        if (!CommonUtils.DirectoryExistsWithFiles(AppBundleManager.ObbExtractFolderPath))
            return (CommonUtils.ReturnCode.NoObb, null);
            
        // create an AssetsManager
        var am = CommonUtils.InitAssetManager(AppBundleManager.ObbExtractFolderPath);
        
        var globalMetadataPath = Path.Combine(AppBundleManager.Il2CppExtractFolderPath, "global-metadata.dat");
        var binaryPath = Path.Combine(AppBundleManager.Il2CppExtractFolderPath, "il2cpp.binary");
            
        am.MonoTempGenerator = new Cpp2IlTempGenerator(globalMetadataPath, binaryPath);

        var nguiSpriteSheetsFound = 0;
        var foundSmoothMovesSpriteSheet = false;
        
        var nguiMgr = new NGUISpriteSheetManager(AppBundleManager.Il2CppExtractFolderPath);
        var smoothMovesManager = new SmoothMovesSpriteSheetManager(AppBundleManager.Il2CppExtractFolderPath);
        
        // check cache
        var cachedAsset = m_SpriteSheetCache.FirstOrDefault(cache => cache.Name == atlasName && cache.IsLowRes == lowRes);
        
        if (cachedAsset != null)
        {
            Logger.Log("Spritesheet found in cache!");

            ProgressService.UpdateProgress(
                ProgressService.SpriteSheetLoadingProgressId, 
                1, 
                false, 
                0, 
                1, 
                "Spritesheet found in cache! ({1:0}%)", 
                // set it to green if its an ngui atlas because there are no low-res atlases for ngui
                !cachedAsset.IsSmoothMovesSpriteSheet ? Colors.SeaGreen : null);
            
            SpriteSheetManager selectedMgr = cachedAsset.IsSmoothMovesSpriteSheet ? smoothMovesManager : nguiMgr;
            
            selectedMgr.AssetCache.AddRange(cachedAsset.Assets);
            
            return (CommonUtils.ReturnCode.Success, selectedMgr);
        }

        // dont load scene or resource files
        var obbAssets = Directory.GetFiles(AppBundleManager.ObbExtractFolderPath)
             .Where(path =>
                 !path.Contains("level") &&
                 !path.Contains(".resource") &&
                 !path.Contains(".resS"))
             .ToList();
        
        Logger.Log("Searching OBB..");
        var failedCount = 0;
        for (var i = 0; i < obbAssets.Count; i++)
        {
            try
            {
                var path = obbAssets[i];

                var loadedAsset = am.LoadAssetsFile(path, false);
                
                if (CheckForNguiAtlas(am, loadedAsset, nguiMgr, atlasName))
                    nguiSpriteSheetsFound++;

                foundSmoothMovesSpriteSheet = CheckForSmoothMovesAtlas(am, loadedAsset, smoothMovesManager, atlasName, lowRes);
                
                // smooth moves is guaranteed to have 1 atlas, so whenever we find one we can break
                // with ngui, we check if we have found at least 2 ngui atlases before breaking
                // since ngui has 2 atlases at max, we can be 100% sure we found everything
                // this means for spritesheets with only 1 atlas, we wont be able to take the shortcut
                if (foundSmoothMovesSpriteSheet || nguiSpriteSheetsFound >= 2)
                    break;
                
                ProgressService.UpdateProgress(
                    ProgressService.SpriteSheetLoadingProgressId,
                    i,
                    false,
                    0,
                    obbAssets.Count - 1,
                    "Searching obb: {0}/{3} files searched ({1:0}%), " + failedCount + "/{3} failed.");
            }
            catch (Exception err)
            {
                // exceptions can happen from trying to read a file that isnt actually a unity asset
                // catch it and continue
                failedCount++;
            }
        }
        
        Logger.Log("Searching OBB.. Done!");
        
        SpriteSheetManager spriteSheetMgr = nguiSpriteSheetsFound > 0 ? nguiMgr : smoothMovesManager;
        
        GetTexture2DFromMaterial(am, spriteSheetMgr, atlasName, lowRes);
        
        am.UnloadAll();
        
        // if no atlas found or if it failed to add the asset to cache
        if ((nguiSpriteSheetsFound <= 0 && !foundSmoothMovesSpriteSheet) || !AddAssetsToCache(spriteSheetMgr, atlasName, lowRes))
        {
            ProgressService.UpdateProgress(ProgressService.SpriteSheetLoadingProgressId, 
                1,
                false,
                0,
                1,
                $"Error: No spritesheet found for atlas '{atlasName}'!",
                Colors.Firebrick);
            
            return (CommonUtils.ReturnCode.NoSpriteSheetFound, null);
        }
        
        ProgressService.UpdateProgress(
            ProgressService.SpriteSheetLoadingProgressId,
            1,
            false,
            0,
            1,
            $"Found spritesheet for atlas '{atlasName}'.",
            // set it to green if its an ngui atlas because there are no low-res atlases for ngui
            nguiSpriteSheetsFound > 0 ? Colors.SeaGreen : null);

        return (CommonUtils.ReturnCode.Success, spriteSheetMgr);
    }

    private static void GetTexture2DFromMaterial(AssetsManager am, SpriteSheetManager spriteSheetMgr, string atlasName, bool isLowRes)
    {
        var mats = spriteSheetMgr.AssetCache.Where(asset => asset.ObjectType == UnityAsset.UnityObjectType.Material).ToList();
        
        for (var i = 0; i < mats.Count; i++)
        {
            ProgressService.UpdateProgress(
                ProgressService.SpriteSheetLoadingProgressId,
                i,
                false,
                0,
                mats.Count - 1,
                "Finalizing assets: {0}/{3} assets checked ({1:0}%)");
            
            var material = mats[i];
            var matFile = am.LoadAssetsFile(material.Path, true);
            var matAssetInfo = matFile.file.GetAssetInfo(material.PathId);
            var matBaseField = am.GetBaseField(matFile, matAssetInfo);

            if (matBaseField["m_SavedProperties.m_TexEnvs.Array"].Children.Count > 1)
            {
                // make sure the material doesn't have more than 2 textures on the shader
                // with the first being the tex, and the second being the ancient/shiny tex
                continue;
            }

            var mainTexPPtr = matBaseField["m_SavedProperties.m_TexEnvs.Array"][0]["second.m_Texture"];

            var texAsset = am.GetExtAsset(matFile, mainTexPPtr);
            var texBf = texAsset.baseField;
            var texName = texBf["m_Name"].AsString;

            var placeholderT2DAsset = new UnityAsset
            {
                ObjectType = UnityAsset.UnityObjectType.Texture2D,
                Name = texName,
                Path = texAsset.file.path,
                PathId = texAsset.info.PathId
            };

            var validForSmoothMoves = texName.Contains(isLowRes ? "_LR" : "_HR") && texName.Contains(atlasName);
            var validForNgui = !texName.Contains("_HR") && !texName.Contains("_LR") && texName == atlasName;

            if (validForSmoothMoves || validForNgui)
            {
                // remove the temp cache mat so we only ever have 1 material
                spriteSheetMgr.AssetCache.Remove(material);
                
                spriteSheetMgr.AssetCache.Add(placeholderT2DAsset);
                spriteSheetMgr.AssetCache.Add(material);
                break;
            }
        }
    }
    
    private static bool CheckForNguiAtlas(AssetsManager am, AssetsFileInstance loadedAsset, NGUISpriteSheetManager nguiMgr, string atlasName)
    {
        var atlasGameObjects = loadedAsset.file.GetAssetsOfType(AssetClassID.GameObject) // get all gameobjects
            .Select(go => am.GetBaseField(loadedAsset, go)) // get base fields of gameobjects
            .Where(goBf => goBf["m_Name"].AsString == atlasName) // get all gameobjects named after the atlas
            .ToList();

        if (atlasGameObjects.Count <= 0) 
            return false;
        
        // check for UIAtlas component
        loadedAsset = am.LoadAssetsFile(loadedAsset.path, true); // reload the asset with all dependencies so we can check components
        foreach (var atlasGo in atlasGameObjects)
        {
            var atlasGoComponents = atlasGo["m_Component.Array"];
            foreach (var component in atlasGoComponents)
            {
                // iterate through components
                var componentExtInst = am.GetExtAsset(loadedAsset, component["component"]);
                var componentBf = componentExtInst.baseField;

                if (componentBf.TypeName != "MonoBehaviour")
                    continue;

                var monoBehaviourScriptBf = am.GetExtAsset(loadedAsset, componentBf["m_Script"]).baseField;

                if (monoBehaviourScriptBf["m_ClassName"].AsString != "UIAtlas")
                    continue;
                
                // add to ngui asset cache
                nguiMgr.AssetCache.Add(new UnityAsset
                {
                    ObjectType = UnityAsset.UnityObjectType.UIAtlas,
                    Name = atlasGo["m_Name"].AsString,
                    Path = componentExtInst.file.path,
                    PathId = componentExtInst.info.PathId
                });
                
                // get material
                var materialPPtr = componentBf["material"];
                var materialAsset = am.GetExtAsset(loadedAsset, materialPPtr);
                
                nguiMgr.AssetCache.Add(new UnityAsset
                {
                    ObjectType = UnityAsset.UnityObjectType.Material,
                    Name = materialAsset.baseField["m_Name"].AsString,
                    Path = materialAsset.file.path,
                    PathId = materialAsset.info.PathId
                });

                return true;
            }
        }

        return false;
    }
    
    private static bool CheckForSmoothMovesAtlas(AssetsManager am, AssetsFileInstance loadedAsset, SmoothMovesSpriteSheetManager smoothMovesManager, string atlasName, bool isLowRes)
    {
        // iterate through all monobehaviours in the file
        var allMonoBehaviours = loadedAsset.file.GetAssetsOfType(AssetClassID.MonoBehaviour);

        foreach (var monoBehaviour in allMonoBehaviours)
        {
            var monoBehaviourBf = am.GetBaseField(loadedAsset, monoBehaviour);

            var mbName = monoBehaviourBf["m_Name"].AsString;

            if (isLowRes ? mbName != atlasName + "_low" : mbName != atlasName)
                continue;

            var mbUnityAsset = new UnityAsset
            {
                ObjectType = UnityAsset.UnityObjectType.MonoBehaviour,
                Name = mbName,
                Path = loadedAsset.path,
                PathId = monoBehaviour.PathId
            };
            
            smoothMovesManager.AssetCache.Add(mbUnityAsset);

            // get material
            var materialPPtr = monoBehaviourBf["material"];
            var materialAsset = am.GetExtAsset(loadedAsset, materialPPtr);

            var matUnityAsset = new UnityAsset
            {
                ObjectType = UnityAsset.UnityObjectType.Material,
                Name = materialAsset.baseField["m_Name"].AsString,
                Path = materialAsset.file.path,
                PathId = materialAsset.info.PathId
            };

            smoothMovesManager.AssetCache.Add(matUnityAsset);

            return true;
        }

        return false;
    }

    private static bool AddAssetsToCache(SpriteSheetManager mgr, string atlasName, bool isLowRes)
    {
        var newCachedAsset = new CachedAsset
        {
            Name = atlasName,
            IsLowRes = isLowRes,
            IsSmoothMovesSpriteSheet = mgr is SmoothMovesSpriteSheetManager,
            Assets = mgr.AssetCache
        };

        if (newCachedAsset.AnyAssetIsNull())
            return false;
        
        m_SpriteSheetCache.Add(newCachedAsset);
        
        SaveCacheToPrefs();

        return true;
    }
}