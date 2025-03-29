using CustomAssetsBackend.Misc;
using FFMpegCore;

namespace CustomAssetsBackend.Audio.FFMpeg;

public static class FFMpegInit
{
    private const string FFMpegPathInProj = "./Audio/FFMpeg/";
    
    public static bool InitFFMpeg()
    {
        if (!File.Exists(Path.GetFullPath(FFMpegPathInProj + "ffmpeg.exe")))
        {
            Logger.Log($"FFMPeg executable not found in {Path.GetFullPath(FFMpegPathInProj)}!");
            // todo: maybe add the option to download ffmpeg?
            return false;
        }
        
        GlobalFFOptions.Configure(
            new FFOptions
            {
                BinaryFolder = FFMpegPathInProj, 
                TemporaryFilesFolder = System.IO.Path.GetTempPath()
            });

        return true;
    }
}