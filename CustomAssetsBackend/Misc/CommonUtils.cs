using System.Diagnostics;
using AssetsTools.NET.Extra;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace CustomAssetsBackend.Misc;

/// <summary>
/// Provides useful utilities to help perform various tasks.
/// </summary>
public static class CommonUtils
{
    public enum ReturnCode
    {
        UnknownError = -1,
        Success,
        NoObb,
        NoSpriteSheetFound,
        NoSpritesLoaded,
        Cancelled
    }

    public static readonly string HomeAppDataPath = Path.Combine(GetAppDataPath(), "CustomAssetsInjector");
    
    public static readonly string AtlasImagePath = Path.Combine(HomeAppDataPath, "atlas.png");
    
    /// <summary>
    /// Gets the width and height of an image at the specified path.
    /// </summary>
    /// <param name="imagePath">The path to the image file</param>
    /// <returns>The width and height of the image</returns>
    public static (int, int) GetImageResolution(string imagePath)
    {
        using var image = Image.Load<Rgba32>(imagePath);
        
        return (image.Width, image.Height);
    }

    public static async Task CopyFileAsync(string sourceFileName, string destFileName, bool overwrite = false)
    {
        var openForReading = new FileStreamOptions { Mode = FileMode.Open };
        await using var source = new FileStream(sourceFileName, openForReading);

        var createForWriting = new FileStreamOptions
        {
            Mode = overwrite ? FileMode.Create : FileMode.CreateNew,
            Access = FileAccess.Write,
            Options = FileOptions.WriteThrough,
            BufferSize = 0,
            PreallocationSize = source.Length
        };
        await using var destination = new FileStream(destFileName, createForWriting);
        await source.CopyToAsync(destination);
    }
    
    public static void RunExe(string exePath, string[] args)
    {
        var startedProcess = Process.Start(new ProcessStartInfo(exePath, args)
        {
            CreateNoWindow = true,
#if DEBUG
            RedirectStandardOutput = true,
            RedirectStandardError = true
#endif
        });

        startedProcess?.WaitForExit();
    }

    public static bool DirectoryExistsWithFiles(string path)
    {
        return Directory.Exists(path) && Directory.GetFiles(path).Length > 0;
    }
    
    /// <summary>
    /// Gets the path to AppData/Roaming/.
    /// </summary>
    /// <returns>The path to AppData/Roaming/.</returns>
    private static string GetAppDataPath() => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    
    /// <summary>
    /// Maps the input value from the input range (inMin, inMax) to the output range (outMin, outMax).
    /// </summary>
    /// <param name="value">The value to map.</param>
    /// <param name="inMin">The minimum value of the input.</param>
    /// <param name="inMax">The maximum value of the input.</param>
    /// <param name="outMin">The minimum value of the output.</param>
    /// <param name="outMax">The maximum value of the output.</param>
    /// <returns>The input value mapped to the output range (outMin, outMax).</returns>
    public static double MapValues(double value, double inMin, double inMax, double outMin, double outMax)
    {
        value = Math.Clamp(value, inMin, inMax);
        
        double mappedValue = (value - inMin) / (inMax - inMin) * (outMax - outMin) + outMin;

        return mappedValue;
    }
    
    /// <summary>
    /// Initializes an <see cref="AssetsManager"/> with the necessary class database.
    /// </summary>
    /// <returns>An <see cref="AssetsManager"/> instance with a loaded class database.</returns>
    public static AssetsManager InitAssetManager(string obbPath)
    {
        // create an AssetsManager
        var am = new AssetsManager();
        am.LoadClassPackage(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "classdata.tpk"));
            
        // load globalgamemanagers so we can load a class database for the unity version
        var ggm = am.LoadAssetsFile(Path.Combine(obbPath, "globalgamemanagers"), false);
        am.LoadClassDatabaseFromPackage(ggm.file.Metadata.UnityVersion);

        am.UnloadAssetsFile(ggm);
        
        return am;
    }
}