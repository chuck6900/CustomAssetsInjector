using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using CustomAssetsBackend.Misc;

namespace CustomAssetsInjector.Services;

public static class AppBundleManager
{
    public static readonly string Il2CppExtractFolderPath = Path.Combine(CommonUtils.HomeAppDataPath, "il2cpp/");
    
    public static readonly string ObbExtractFolderPath = Path.Combine(CommonUtils.HomeAppDataPath, "obb/");

    public static bool IsAndroid(this ZipArchive archive)
    {
        // check if the bundle contains classes.dex
        // if it does, its android, otherwise its ios
        return archive.GetEntry("classes.dex") != null;
    }

    public static bool CreateBundle(string appBundlePath, string outBundlePath)
    {
        // open bundle
        if (!File.Exists(appBundlePath))
        {
            Logger.Log("App bundle does not exist!");
            return false;
        }

        try
        {
            File.Copy(appBundlePath, outBundlePath, true);
            
            using var archive = ZipFile.Open(outBundlePath, ZipArchiveMode.Update);

            var isAndroid = archive.IsAndroid();

            // copy obb over
            var obbPath = isAndroid ? "assets\\bin\\Data" : "Payload\\gold.app\\Data";
            
            // delete all entries in the data dir
            var obbEntries = archive.Entries.Where(asset => Path.GetDirectoryName(asset.FullName) == obbPath);
            obbEntries.ToList().ForEach(asset => asset.Delete());
            
            var files = Directory.GetFiles(ObbExtractFolderPath);
            for (var i = 0; i < files.Length; i++)
            {
                var assetPath = files[i];
                var fullPathInBundle = Path.Combine(obbPath, Path.GetFileName(assetPath));

                archive.CreateEntryFromFile(assetPath, fullPathInBundle);
                
                ProgressService.UpdateProgress(
                    ProgressService.CreateBundleProgressId,
                    i,
                    false,
                    0,
                    files.Length - 1,
                    "Importing modified obb, {0}/{3} files imported ({1:0}%)");
            }

            // set the text to compressing bundle, because after this line it will execute archive.Dispose
            // because it's the end of the scope of the using statement
            ProgressService.UpdateProgress(
                ProgressService.CreateBundleProgressId,
                1,
                true,
                0,
                1,
                "Compressing bundle, do not close the app.");
        }
        catch (Exception err)
        {
            Logger.Log("Failed to create app bundle!", Logger.LogLevel.Exception, err);
            return false;
        }
        
        // all files were imported, with no exceptions
        return true;
    }
    
    public static bool CheckData()
    {
        return CommonUtils.DirectoryExistsWithFiles(ObbExtractFolderPath) && CommonUtils.DirectoryExistsWithFiles(Il2CppExtractFolderPath);
    }
    
    /// <summary>
    /// Extracts only the obb assets inside a zip file to the specified path asynchronously.
    /// </summary>
    /// <param name="zipPath">The path of the zip to extract.</param>
    public static async Task ExtractObb(string zipPath)
    {
        Directory.CreateDirectory(ObbExtractFolderPath);
        
        await Task.Run(() => ExtractObb(zipPath, ObbExtractFolderPath));
    }
    
    public static void ExtractObb(string zipPath, string extractPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        
        // use backslashes since that's what Path.GetDirectoryName() uses
        var dataDirName = archive.GetEntry("classes.dex") != null ? "assets\\bin\\Data" : "Payload\\gold.app\\Data";
        
        // get all files in the data directory only
        var obbAssets = archive.Entries
            .Where(entry => Path.GetDirectoryName(entry.FullName) == dataDirName)
            .ToList();
        
        // get all .split files so we can reconstruct any missing .assets files later
        var splitFiles = obbAssets
            .Where(entry => Path.GetExtension(entry.FullName).Contains(".split"))
            .ToList();

        // remove the split files from the obbAssets list so we dont extract them
        obbAssets.RemoveAll(asset => splitFiles.Contains(asset));

        Directory.CreateDirectory(extractPath);
        
        var failedCount = 0;
        for (var i = 0; i < obbAssets.Count; i++)
        {
            try
            {
                var entry = obbAssets[i];
                var filePath = Path.Combine(extractPath, entry.Name);

                entry.ExtractToFile(filePath, true);
                                
                ProgressService.UpdateProgress(
                    ProgressService.ApkLoadingProgressId, 
                    i, 
                    false, 
                    0, 
                    obbAssets.Count - 1, 
                    "Extracting obb data: {0}/{3} files extracted ({1:0}%), " + failedCount + "/{3} failed");
            }
            catch (DirectoryNotFoundException)
            {
                // attempt to extract a directory, ignore
            }
            catch (FileNotFoundException)
            {
                // discard
            }
            catch (Exception err)
            {
                failedCount++;
                Logger.Log("Exception occured while extracting the obb!", Logger.LogLevel.Exception, err);
            }
        }

        if (splitFiles.Count <= 0)
            return;

        // get the base .asset file that the split files are from
        var baseFilesAndCorrespondingSplitFiles = splitFiles
            .GroupBy(file => Path.GetFileNameWithoutExtension(file.FullName))
            .ToDictionary(group => group.Key, group => group.ToList());

        var missingBaseFilesAndCorrespondingSplitFiles = baseFilesAndCorrespondingSplitFiles
            .Where(baseFilePair => 
                !obbAssets
                    .Select(asset => Path.GetFileName(asset.FullName))
                    .Contains(baseFilePair.Key))
            .ToDictionary();

        foreach (var baseFilePair in missingBaseFilesAndCorrespondingSplitFiles)
        {
            ReconstructAssetFileFromSplitFiles(baseFilePair.Value, Path.Combine(ObbExtractFolderPath, baseFilePair.Key));
        }
    }

    private static int GetSplitFileIndex(string splitFilePath)
    {
        var ext = ".split";
        var startOfIndex = splitFilePath.LastIndexOf(ext, StringComparison.Ordinal) + ext.Length;
        if (startOfIndex >= 0)
        {
            if (!int.TryParse(splitFilePath.AsSpan(startOfIndex), out var splitFileIndex))
                return -1;

            return splitFileIndex;
        }
        return -1;
    }
    
    /// <summary>
    /// Extracts the IL2CPP binary and the global metadata inside of a zip file to the specified.
    /// </summary>
    /// <param name="zipPath">The path of the zip to extract.</param>
    public static async Task<(string, string)> ExtractIl2CppData(string zipPath)
    {
        Directory.CreateDirectory(Il2CppExtractFolderPath);
        
        var (il2CppBinaryPath, metadataPath) = await Task.Run(() => ExtractIl2CppDataByPlatform(zipPath, Il2CppExtractFolderPath));

        return (il2CppBinaryPath, metadataPath);
    }
    
    public static (string, string) ExtractIl2CppDataByPlatform(string zipPath, string extractPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);

        var isAndroid = archive.GetEntry("classes.dex") != null;

        var il2CppBinary = archive.GetEntry(isAndroid ? "lib/armeabi-v7a/libil2cpp.so" : "Payload/gold.app/gold");
        
        var globalMetadata = archive.GetEntry(
            (isAndroid ? "assets/bin/" : "Payload/gold.app/") + "Data/Managed/Metadata/global-metadata.dat");

        var il2CppPath = Path.Combine(extractPath, "il2cpp.binary");
        il2CppBinary?.ExtractToFile(il2CppPath, true);

        var metadataPath = Path.Combine(extractPath, "global-metadata.dat");
        globalMetadata?.ExtractToFile(metadataPath, true);

        return (il2CppPath, metadataPath);
    }
    
    /// <summary>
    /// Creates a .assets file from a list of .assets.split files.
    /// </summary>
    /// <param name="splitFiles">A list of all the .split zip file entries.</param>
    /// <param name="outputFilePath">The file path of the resulting .assets file.</param>
    private static void ReconstructAssetFileFromSplitFiles(List<ZipArchiveEntry> splitFiles, string outputFilePath)
    {
        splitFiles = splitFiles
            .OrderBy(file => GetSplitFileIndex(file.FullName))
            .ToList();
            
        splitFiles.RemoveAll(file => GetSplitFileIndex(file.FullName) == -1);
        
        try
        {
            using var newAssetFile = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write);
            foreach (var splitFile in splitFiles)
            {
                using var splitFileData = splitFile.Open();
                splitFileData.CopyTo(newAssetFile);
            }
        }
        catch (Exception err)
        {
            Logger.Log("Failed to reconstruct asset file!", Logger.LogLevel.Exception, err);
        }
    }
}