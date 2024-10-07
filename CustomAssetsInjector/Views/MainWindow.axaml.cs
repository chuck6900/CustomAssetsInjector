using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CustomAssetsBackend.Misc;
using CustomAssetsInjector.Services;
using CustomAssetsInjector.Utils;

namespace CustomAssetsInjector.Views;

public partial class MainWindow : Window
{
    private const string SelectApkHintText = "Select an APK or IPA.";

    private const string SelectEditorHintText = "Select an editor.";

    public MainWindow()
    {
        InitializeComponent();

        // menu buttons
        ResetApkButton.Click += ResetApk;
        ResetPrefsButton.Click += ResetPrefs;
        
        AboutButton.Click += About_OnClick;
        
        ExportBundleButton.Click += ExportBundle;
        
        // main buttons
        SelectApkButton.Click += SelectApk;
        SpritesheetEditorButton.Click += SpriteSheetEditorButton_OnClick;

        this.Loaded += Initialize;
        this.Closing += delegate { PreferenceService.SavePrefs(); };
    }

    private async void ExportBundle(object? sender, RoutedEventArgs e)
    {
        if (!AppBundleManager.CheckExtractedData())
            return;
        
        var selectedBundleFile = await FileDialogUtils.PromptOpenFile(
            "Select the input file", 
            this.StorageProvider, 
            [FileDialogUtils.ApkFile, FileDialogUtils.IpaFile]);

        if (selectedBundleFile == null || !File.Exists(selectedBundleFile.Path.LocalPath))
            return;

        var selectedBundlePath = selectedBundleFile.Path.LocalPath;
        
        string ext;
        try
        {
            using var zip = ZipFile.OpenRead(selectedBundlePath);
            ext = zip.IsAndroid() ? "APK" : "IPA";
        }
        catch (InvalidDataException err)
        {
            Logger.Log("Zip file is corrupt! Please select a different zip file.", Logger.LogLevel.Exception, err);
            return;
        }

        var exportIsValidForIos = 
            ext == "IPA" &&
            Directory.GetFiles(AppBundleManager.ObbExtractFolderPath)
                .Contains(Path.Combine(AppBundleManager.ObbExtractFolderPath, "resources.assets"));
        
        var exportIsValidForAndroid = 
            ext == "APK" &&
            !Directory.GetFiles(AppBundleManager.ObbExtractFolderPath)
                .Contains(Path.Combine(AppBundleManager.ObbExtractFolderPath, "resources.assets"));

        if ((ext == "IPA" && !exportIsValidForIos) || (ext == "APK" && !exportIsValidForAndroid))
        {
            var result = await MessageBox.ShowMessageBox(
                this,
                $"You have selected to export an {ext}, while the assets you have loaded do NOT come from an {ext}.\n" +
                $"The exported {ext} will likely not work. Are you sure you want to continue?",
                $"Invalid {ext}",
                ["Yes", "Cancel"]);

            if (result == "Cancel")
                return;
        }

        var file = await FileDialogUtils.PromptSaveFile(
            $"Save modified {ext}", 
            this.StorageProvider, 
            null,
            ext.ToLowerInvariant(),
            [ext == "APK" ? FileDialogUtils.ApkFile : FileDialogUtils.IpaFile]);

        if (file == null)
            return;

        ProgressService.RegisterProgress(ProgressService.CreateBundleProgressId, Progress);
        
        Progress.IsIndeterminate = true;
        Logger.Log($"Creating {ext}..");
        
        var success = await Task.Run(() => AppBundleManager.CreateBundle(selectedBundlePath, file.Path.LocalPath));
        if (!success)
        {
            Logger.Log($"Failed to export {ext}!");
            Progress.IsIndeterminate = false;
            return;
        }
        
        ProgressService.DeRegisterProgress(ProgressService.CreateBundleProgressId, false);

        Progress.IsIndeterminate = false;
        Progress.Minimum = 0; Progress.Maximum = 1; Progress.Value = 1;
        Logger.Log($"Successfully exported the modified {ext}!");
    }

    private void ResetPrefs(object? sender, RoutedEventArgs e)
    {
        // not worth using ProgressService here, just mess with the progress bar manually
        Progress.SetActive(true);
        Progress.IsIndeterminate = true;
        Progress.ProgressTextFormat = "Resetting preferences..";
        
        PreferenceService.SetPrefs(new PreferenceService.Preferences());
        PreferenceService.SavePrefs();

        Progress.Minimum = 0; Progress.Maximum = 1; Progress.Value = 1;
        Progress.IsIndeterminate = false;
        Progress.ProgressTextFormat = "Resetting preferences.. Done!";
        
        // re-init
        Initialize(sender, e);
    }

    private async void ResetApk(object? sender, RoutedEventArgs e)
    {
        EditorPanel.SetActive(false);
        SelectApkButton.IsEnabled = false;

        // reset apk
        ProgressService.RegisterProgress(ProgressService.ApkResetProgressId, Progress);
        await ResetData();
        ProgressService.DeRegisterProgress(ProgressService.ApkResetProgressId, false);
        
        // delete atlas
        try
        {
            File.Delete(CommonUtils.AtlasImagePath);
        }
        catch (Exception)
        {
            // log a debug message because this doesn't really affect the end user
            Logger.Log("Failed to delete atlas.png.", Logger.LogLevel.Debug);
        }
        
        // reset cached assets in prefs
        var prefs = PreferenceService.GetPrefs();
        prefs.AssetCache.Clear();
        PreferenceService.SetPrefs(prefs);
        PreferenceService.SavePrefs();
        
        // re-init
        Initialize(sender, e);

        SelectApkButton.IsEnabled = true;
    }

    private async Task ResetData()
    {
        // wipe old data
        try
        {
            ProgressService.UpdateProgress(ProgressService.ApkResetProgressId, 0, true, null, null, "Deleting obb..");

            Directory.CreateDirectory(AppBundleManager.ObbExtractFolderPath);
            await Task.Run(() => Directory.Delete(AppBundleManager.ObbExtractFolderPath, true));

            ProgressService.UpdateProgress(ProgressService.ApkResetProgressId, 0, true, null, null, "Deleting IL2CPP data..");

            Directory.CreateDirectory(AppBundleManager.Il2CppExtractFolderPath);
            await Task.Run(() => Directory.Delete(AppBundleManager.Il2CppExtractFolderPath, true));

            ProgressService.UpdateProgress(ProgressService.ApkResetProgressId, 1, false, 0, 1, "Reset done!");
        }
        catch (Exception err)
        {
            Logger.Log("Failed to reset data!", Logger.LogLevel.Exception, err);
        }
    }

    private void ExceptionCallback(string message, Exception err)
    {
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await MessageBox.ShowMessageBox(this, $"{message}\n\nPlease check CAIExceptionLog.txt in the application folder for more info.\n\nError: {err}", $"An exception has occured.");
        });
    }
    
    private void LogAction(string logMessage)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            Progress.ProgressTextFormat = logMessage;
        });
    }
    
    private void Initialize(object? sender, RoutedEventArgs e)
    {
        // initialize events
        Logger.LogAction -= LogAction;
        Logger.LogAction += LogAction;
        
        Logger.ExceptionAction -= ExceptionCallback;
        Logger.ExceptionAction += ExceptionCallback;
        
        // initialize prefs
        PreferenceService.Initialize();
        
        // check obb and il2cpp data
        if (AppBundleManager.CheckExtractedData())
        {
            ApkPanel.SetActive(false);
            EditorPanel.SetActive(true);

            HintText.Text = SelectEditorHintText;
            
            SpriteSheetEditorButton_OnClick(sender, e);

            return;
        }
        
        ApkPanel.SetActive(true);
        SelectApkButton.SetActive(true);
        EditorPanel.SetActive(false);
        
        HintText.Text = SelectApkHintText;
    }

    private void SpriteSheetEditorButton_OnClick(object? sender, RoutedEventArgs e)
    { 
        new SpriteSheetEditorWindow().Show();
        
        ProgressService.DeRegisterProgress(ProgressService.ApkLoadingProgressId, true);
        ProgressService.DeRegisterProgress(ProgressService.CreateBundleProgressId, true);
        
        Logger.LogAction -= LogAction;
        Logger.ExceptionAction -= ExceptionCallback;
        
        this.Close();
    }
    
    private async void SelectApk(object? sender, RoutedEventArgs e)
    {
        SelectApkButton.IsEnabled = false;
        
        var file = await FileDialogUtils.PromptOpenFile(
            "Select APK or IPA File", 
            this.StorageProvider, 
            [FileDialogUtils.ApkFile, FileDialogUtils.IpaFile]);

        if (file == null)
        {
            SelectApkButton.IsEnabled = true;
            return;
        }

        // reset any existing data
        ProgressService.RegisterProgress(ProgressService.ApkResetProgressId, Progress);
        await ResetData();
        ProgressService.DeRegisterProgress(ProgressService.ApkResetProgressId, false);

        var bundlePath = file.Path.LocalPath;

        Progress.IsIndeterminate = true;
        Logger.Log("Extracting obb data...");

        // extract obb
        ProgressService.RegisterProgress(ProgressService.ApkLoadingProgressId, Progress);
        await AppBundleManager.ExtractObb(bundlePath);
        ProgressService.DeRegisterProgress(ProgressService.ApkLoadingProgressId, false);
        
        // extract il2cpp binary and metadata
        await AppBundleManager.ExtractIl2CppData(bundlePath);

        ApkPanel.SetActive(false);
        EditorPanel.SetActive(true);

        HintText.Text = SelectEditorHintText;
    }

    private async void About_OnClick(object? sender, RoutedEventArgs e)
    {
        const string version = "0.2.0";

        await MessageBox.ShowMessageBox(
            this,
            $"""
                Angry Birds Epic - CustomAssetsInjector (v{version})
                By Heroic (@heroic2 on Discord)
                
                A tool to edit various types of Angry Birds Epic's assets.
                
                Special thanks to:
                Chimera Entertainment
                AssetsTools.NET
                Cpp2IL
                Angry Birds Modding Hub
                """,
            "About"
        );
    }
}