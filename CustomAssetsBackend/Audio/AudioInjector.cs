using AssetsTools.NET;
using AssetsTools.NET.Cpp2IL;
using AssetsTools.NET.Extra;
using CustomAssetsBackend.Audio.OggVorbis2FSB5Wrapper;
using CustomAssetsBackend.Misc;

namespace CustomAssetsBackend.Audio;

public enum AssetBundleIndex
{
    A = 0,
    B = 1,
    C = 2,
    D = 3,
    E = 4,
    F = 5
}
    
public enum SoundParameterType
{
    Looping = 0,
    OneShot = 1,
    CurrentPrimaryMusicSource = 2,
    Crossfade = 3,
    CurrentSecondaryMusicSource = 4
}

public enum Channel
{
    Music,
    Sound
}
    
public class SoundParameter(SoundParameterType type, float value)
{
    public SoundParameterType Type { get; set; } = type;
    public float Value { get; set; } = value;
}

public class Sound(string nameId)
{
    public string NameId { get; set; } = nameId;
    public List<string> AssetNames => new List<string> { NameId };
    public Channel ChannelId { get; set; }
    public float Volume { get; set; } = 1f;
    public List<SoundParameter> Params { get; } = new List<SoundParameter>();
    public AssetBundleIndex AssetPriority { get; set; }

    public override string ToString() => NameId;
}

public class AudioInjector(string il2CppFolderPath, string obbPath) : UnityAssetManager(il2CppFolderPath, obbPath)
{
    protected override string PathInResources => "Sound/CustomAssetsInjector/";

    private List<string> m_SoundNameCache = new List<string>();

    public CommonUtils.ReturnCode InitSoundNameCache(bool reload = false)
    {
        if (!reload && m_SoundNameCache.Count != 0)
        {
            Logger.Log("Sound cache already initialized!");
            return CommonUtils.ReturnCode.AlreadyInitialized;
        }
        
        m_SoundNameCache.Clear();
        
        var am = InitAssetManager(ObbPath);
        
        var globalMetadataPath = Path.Combine(this.Il2CppFolderPath, "global-metadata.dat");
        var binaryPath = Path.Combine(this.Il2CppFolderPath, "il2cpp.binary");
            
        am.MonoTempGenerator = new Cpp2IlTempGenerator(globalMetadataPath, binaryPath);
        
        var rootSceneAssetPath = Path.Combine(ObbPath, "level1");
        var rootSceneAsset = am.LoadAssetsFile(rootSceneAssetPath);

        var soundManagerGameObject = rootSceneAsset.file.GetAssetsOfType(AssetClassID.GameObject)
            .FirstOrDefault(go => am.GetBaseField(rootSceneAsset, go)["m_Name"].AsString == "4_Audio");
        var soundManagerGameObjectBf = am.GetBaseField(rootSceneAsset, soundManagerGameObject);
        var soundManagerPPtr = soundManagerGameObjectBf["m_Component.Array"][1]["component"];
        var soundManager = am.GetExtAsset(rootSceneAsset, soundManagerPPtr);
        var soundBf = soundManager.baseField;

        foreach (var sound in soundBf["Sounds.Array"].Children)
        {
            m_SoundNameCache.Add(sound["NameId"].AsString);
        }
        
        am.UnloadAll();

        return CommonUtils.ReturnCode.Success;
    }

    public bool SoundAlreadyExists(string soundName)
    {
        if (m_SoundNameCache.Count == 0)
            return false;

        return m_SoundNameCache.Contains(soundName);
    }
    
    public CommonUtils.ReturnCode Inject(Sound sound, byte[] soundData)
    {
        var am = InitAssetManager(ObbPath);
        
        var globalMetadataPath = Path.Combine(this.Il2CppFolderPath, "global-metadata.dat");
        var binaryPath = Path.Combine(this.Il2CppFolderPath, "il2cpp.binary");
            
        am.MonoTempGenerator = new Cpp2IlTempGenerator(globalMetadataPath, binaryPath);
        
        Logger.Log($"Checking SoundManager for '{sound.NameId}'");
        
        var rootSceneAssetPath = Path.Combine(ObbPath, "level1");
        var rootSceneAsset = am.LoadAssetsFile(rootSceneAssetPath);
        var currentPlatform = rootSceneAsset.file.Metadata.TargetPlatform;

        var soundManagerGameObject = rootSceneAsset.file.GetAssetsOfType(AssetClassID.GameObject)
            .FirstOrDefault(go => am.GetBaseField(rootSceneAsset, go)["m_Name"].AsString == "4_Audio");
        var soundManagerGameObjectBf = am.GetBaseField(rootSceneAsset, soundManagerGameObject);
        var soundManagerPPtr = soundManagerGameObjectBf["m_Component.Array"][1]["component"];
        var soundManager = am.GetExtAsset(rootSceneAsset, soundManagerPPtr);
        var soundBf = soundManager.baseField;

        var duplicateSound = soundBf["Sounds.Array"].Children
            .FirstOrDefault(info => info["NameId"].AsString == sound.NameId);
        if (duplicateSound != null)
        {
            Logger.Log($"Checking SoundManager for '{sound.NameId}'.. done! Removing duplicate..");
            soundBf["Sounds.Array"].Children.Remove(duplicateSound);
        }
        
        Logger.Log("Converting to FSB5..");
        
        // convert to fsb5
        var fsb5Data = Fsb5Encoder.GetFsb5DataFromAudio(soundData);
        if (fsb5Data.Length == 0)
        {
            Logger.Log("FSB5 encode failed! unable to create the audio.");
            return CommonUtils.ReturnCode.Fsb5EncodeFailed;
        }
        
        Logger.Log("Converting to FSB5.. done!");

        var fsb5FilePath = Path.Combine(ObbPath, sound.NameId + ".resource");
        var audioClipAssetPath = fsb5FilePath.Replace(".resource", ".assets").Trim();
        File.WriteAllBytes(fsb5FilePath, fsb5Data);

        Logger.Log("Creating AudioClip asset..");
        using (var audioClipTemplateStream = new MemoryStream(Resources.AudioClipTemplate))
        {
            var audioClipAsset = am.LoadAssetsFile(audioClipTemplateStream, audioClipAssetPath);
            var audioClipInfo = audioClipAsset.file.GetAssetInfo(1);
            var audioClipBf = am.GetBaseField(audioClipAsset, audioClipInfo);
            
            audioClipAsset.file.Metadata.TargetPlatform = currentPlatform;

            audioClipBf["m_Name"].AsString = sound.NameId;
            audioClipBf["m_Resource.m_Source"].AsString = Path.GetFileName(fsb5FilePath);
            audioClipBf["m_Resource.m_Size"].AsULong = (ulong)fsb5Data.LongLength;
        
            SaveAssetsFile(am, audioClipAsset, audioClipInfo, audioClipBf);
        }
        Logger.Log("Creating AudioClip asset.. done! ");
        
        // add item to sounds array
        Logger.Log("Adding sound to SoundManager..");
        CreateAndAddSoundToArray(sound, soundBf);
        soundManager.info.SetNewData(soundBf);
        Logger.Log("Adding sound to SoundManager.. done!");
        
        // add item to the appropriate AssetProvider
        Logger.Log($"Adding sound to asset provider {sound.AssetPriority}..");
        CreateAndAddAudiosToAssetProvider(am, sound, soundBf, rootSceneAsset, audioClipAssetPath);
        Logger.Log($"Adding sound to asset provider {sound.AssetPriority}.. done!");
        
        Logger.Log($"Sound '{sound.NameId}' successfully injected!");
        return CommonUtils.ReturnCode.Success;
    }
    
    private void CreateAndAddSoundToArray(Sound sound, AssetTypeValueField soundBf)
    {
        var soundsArray = soundBf["Sounds.Array"];
        var soundTemplate = ValueBuilder.DefaultValueFieldFromArrayTemplate(soundsArray);

        soundTemplate["NameId"].AsString = sound.NameId;
        
        foreach (var assetName in sound.AssetNames)
        {
            var stringTemplate = ValueBuilder.DefaultValueFieldFromArrayTemplate(soundTemplate["AssetNames.Array"]);
            stringTemplate.AsString = assetName;
            
            soundTemplate["AssetNames.Array"].Children.Add(stringTemplate);
        }

        soundTemplate["ChannelId"].AsInt = (int)sound.ChannelId;
        soundTemplate["Muted"].AsBool = false;
        soundTemplate["Volume"].AsFloat = sound.Volume;
        
        foreach (var soundParam in sound.Params)
        {
            var paramTemplate = ValueBuilder.DefaultValueFieldFromArrayTemplate(soundTemplate["Params.Array"]);
            paramTemplate["Type"].AsInt = (int)soundParam.Type;
            paramTemplate["Value"].AsFloat = soundParam.Value;
            
            soundTemplate["Params.Array"].Children.Add(paramTemplate);
        }
        
        soundTemplate["AssetPrioritiy"].AsInt = (int)sound.AssetPriority; // need to copy the chimera typo...
        
        soundsArray.Children.Add(soundTemplate);
    }

    private void CreateAndAddAudiosToAssetProvider(AssetsManager am, Sound sound, AssetTypeValueField soundBf, AssetsFileInstance rootSceneAsset, string audioClipAssetPath)
    {
        var soundAssetProviderPPtr = soundBf["m_AudioAssetProviders.Array"][(int)sound.AssetPriority]["AssetProvider"];
        var soundAssetProvider = am.GetExtAsset(rootSceneAsset, soundAssetProviderPPtr);
        var soundAssetProviderBf = soundAssetProvider.baseField;
        var assetProviderInfos = soundAssetProviderBf["AssetInfos.Array"];
        
        var ggmPath = Path.Combine(ObbPath, "globalgamemanagers");
        var ggmAsset = am.LoadAssetsFile(ggmPath);

        foreach (var assetName in sound.AssetNames)
        {
            var pathInResources = GetAssetResourcePath(assetName);
            
            var assetInfoTemplate = ValueBuilder.DefaultValueFieldFromArrayTemplate(assetProviderInfos);
            assetInfoTemplate["NameId"].AsString = assetName;
            assetInfoTemplate["AssetLoadingType"].AsInt = (int)LoadingType.FromResources;
            assetInfoTemplate["Path"].AsString = pathInResources;
            assetInfoTemplate["Extension"].AsString = "ogg";
            assetProviderInfos.Children.Add(assetInfoTemplate);
            
            ggmAsset.file.Metadata.Externals.Add(new AssetsFileExternal
            {
                VirtualAssetPathName = string.Empty,
                PathName = Path.GetFileName(audioClipAssetPath),
                OriginalPathName = Path.GetFileName(audioClipAssetPath),
                Guid = default,
                Type = AssetsFileExternalType.Normal
            });
            
            SaveToResources(am, ggmAsset, pathInResources, ggmAsset.file.Metadata.Externals.Count, 1);
        }
        
        SaveAssetsFile(am, rootSceneAsset, soundAssetProvider.info, soundAssetProvider.baseField);
    }
}