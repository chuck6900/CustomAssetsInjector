using CustomAssetsBackend.Misc;
using CustomAssetsBackend.Audio.FFMpeg;
using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Pipes;

namespace CustomAssetsBackend.Audio.OggVorbis2FSB5Wrapper;

public static class Fsb5Encoder
{
    public const string PathToExecutableInProj = "Audio/OggVorbis2FSB5Wrapper/oggvorbis2fsb5.exe";
    
    public static byte[] GetFsb5DataFromAudio(string audioPath)
    {
        if (!File.Exists(audioPath))
        {
            Logger.Log("Audio file doesn't exist! Unable to get FSB5 data.");
            return [];
        }

        return ConvertToPureOggVorbis(audioPath);
    }
    
    public static byte[] GetFsb5DataFromAudio(byte[] audioData)
    {
        // convert to ogg vorbis
        var oggBytes = ConvertToPureOggVorbis(audioData);

        if (oggBytes.Length == 0)
        {
            Logger.Log("Conversion to OGG failed.");
            return [];
        }
        
        // convert to FSB5
        var tempOggPath = Path.GetTempFileName();
        var tempFsb5DataPath = Path.GetTempFileName();
        var oggVorbis2Fsb5ExePath = Path.GetFullPath(PathToExecutableInProj);
        
        File.WriteAllBytes(tempOggPath, oggBytes);
        
        // arg format: input path, output path, starting loop point (optional), ending loop point (optional)
        string[] args = [tempOggPath, tempFsb5DataPath];
        
        CommonUtils.RunExe(oggVorbis2Fsb5ExePath, args);

        var fsb5Data = File.ReadAllBytes(tempFsb5DataPath);

        if (fsb5Data.Length == 0)
        {
            Logger.Log("Failed to convert to FSB5!");
            return [];
        }
        
        File.Delete(tempOggPath);
        File.Delete(tempFsb5DataPath);

        return fsb5Data;
    }

    public static byte[] ConvertToPureOggVorbis(string audioPath)
    {
        if (!File.Exists(audioPath))
        {
            Logger.Log("Audio file doesn't exist! Unable to convert to OGG.");
            return [];
        }

        var audioData = File.ReadAllBytes(audioPath);
        return ConvertToPureOggVorbis(audioData);
    }

    public static byte[] ConvertToPureOggVorbis(byte[]? audioData)
    {
        var didInit = FFMpegInit.InitFFMpeg();
        if (!didInit)
        {
            Logger.Log("Failed to init FFMpeg! Unable to convert to OGG.");
            return [];
        }
        
        if (audioData == null || audioData.Length == 0)
        {
            Logger.Log("Audio data is empty! Unable to convert to OGG.");
            return [];
        }

        using var oggData = new MemoryStream();
        using var audioDataStream = new MemoryStream(audioData);
        
        // run command
        FFMpegArguments
            .FromPipeInput(new StreamPipeSource(audioDataStream))
            .OutputToPipe(new StreamPipeSink(oggData), options => options
                .WithCustomArgument("-metadata Heroic=\"Powered by the CustomAssetsInjector\"")
                .WithAudioCodec("libvorbis")
                .ForceFormat("ogg")
                .SelectStream(0))
            .ProcessSynchronously();

        var oggBytes = new byte[oggData.Length];

        oggData.Position = 0;
        var bytesRead = oggData.Read(oggBytes, 0, oggBytes.Length);
        
        if (bytesRead != oggBytes.Length)
        {
            Logger.Log("Did not read all bytes!");
            return [];
        }

        return oggBytes;
    }
}