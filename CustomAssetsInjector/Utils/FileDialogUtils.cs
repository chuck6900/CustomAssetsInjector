using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace CustomAssetsInjector.Utils;

public static class FileDialogUtils
{
    /// <summary>
    /// Opens a file picker where the user can select a single file.
    /// </summary>
    /// <param name="title">The name of the file picker window.</param>
    /// <param name="storageProvider">The <see cref="IStorageProvider"/> instance to use.</param>
    /// <param name="fileTypes">An array of allowed file types for the user to select.</param>
    /// <returns>The selected <see cref="IStorageFile"/>.</returns>
    public static async Task<IStorageFile?> PromptOpenFile(
        string title,
        IStorageProvider storageProvider,
        FilePickerFileType[]? fileTypes = null)
    {
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            FileTypeFilter = fileTypes,
            AllowMultiple = false
        });

        return files.Count > 0 ? files[0] : null;
    }

    /// <summary>
    /// Opens a file picker where the user can select multiple files.
    /// </summary>
    /// <param name="title">The name of the file picker window.</param>
    /// <param name="storageProvider">The <see cref="IStorageProvider"/> instance to use.</param>
    /// <param name="fileTypes">An array of allowed file types for the user to select.</param>
    /// <returns>An <see cref="IStorageFile"/> list of selected files.</returns>
    public static async Task<IReadOnlyList<IStorageFile>?> PromptOpenFiles(
        string title,
        IStorageProvider storageProvider,
        FilePickerFileType[] fileTypes)
    {
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            FileTypeFilter = fileTypes,
            AllowMultiple = true
        });

        return files.Count > 0 ? files : null;
    }

    /// <summary>
    /// Opens a save file dialog.
    /// </summary>
    /// <param name="title">The name of the file dialog window.</param>
    /// <param name="storageProvider">The <see cref="IStorageProvider"/> instance to use.</param>
    /// <param name="suggestedFileName">An optional default file name.</param>
    /// <param name="defaultExtension">An optional default extension.</param>
    /// <param name="fileTypes">The file types the user can pick from.</param>
    /// <returns>An <see cref="IStorageFile"/> representing the saved file.</returns>
    public static async Task<IStorageFile?> PromptSaveFile(
        string title,
        IStorageProvider storageProvider,
        string? suggestedFileName = null,
        string? defaultExtension = null,
        IReadOnlyList<FilePickerFileType>? fileTypes = null)
    {
        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = defaultExtension,
            FileTypeChoices = fileTypes
        });
        
        return file;
    }
    
    public static async Task<IStorageFolder?> PromptSelectFolder(
        string title,
        IStorageProvider storageProvider,
        string? suggestedStartLocation = null)
    {
        var startLocation = suggestedStartLocation != null
            ? await storageProvider.TryGetFolderFromPathAsync(suggestedStartLocation)
            : null;
        
        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            SuggestedStartLocation = startLocation,
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0] : null;
    }
    
    public static FilePickerFileType ApkFile => new("APK") { Patterns = ["*.apk"] };
    
    public static FilePickerFileType IpaFile => new("IPA") { Patterns = ["*.ipa"] };
    
    public static FilePickerFileType PngFile => new("PNG") { Patterns = ["*.png"] };
    
    public static FilePickerFileType JsonFile => new("JSON") { Patterns = ["*.json"] };
}
