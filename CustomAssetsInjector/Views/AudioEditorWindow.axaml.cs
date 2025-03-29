using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using CustomAssetsBackend.Audio;
using CustomAssetsBackend.Misc;
using CustomAssetsInjector.Services;
using CustomAssetsInjector.Utils;

namespace CustomAssetsInjector.Views;

public partial class AudioEditorWindow : Window
{
    private AudioInjector m_AudioInjector;
    
    private byte[]? m_AudioData;
    
    public AudioEditorWindow()
    {
        InitializeComponent();
        this.DataContext = this;
        
        LoadAudioButton.Click += LoadAudio;
        NameIdInput.TextChanged += CheckIfNameIdExists;
        
        Logger.LogAction += LogAction;
        Logger.ExceptionAction += ExceptionCallback;
        
        this.Closed += OnClosed;

        Task.Run(() =>
        {
            m_AudioInjector = new AudioInjector(AppBundleManager.Il2CppExtractFolderPath, AppBundleManager.ObbExtractFolderPath);
            m_AudioInjector.InitSoundNameCache();
        });
    }

    private void CheckIfNameIdExists(object? sender, TextChangedEventArgs e)
    {
        InjectButton.IsEnabled = !string.IsNullOrEmpty(NameIdInput.Text) && m_AudioData != null && m_AudioData.Length != 0;
        InjectButton.Content = m_AudioInjector.SoundAlreadyExists(NameIdInput.Text ?? string.Empty) ? "Replace" : "Inject";
    }

    private async void LoadAudio(object? sender, RoutedEventArgs e)
    {
        LoadAudioButton.IsEnabled = false;
        
        var file = await FileDialogUtils.PromptOpenFile(
            "Select an audio file", 
            this.StorageProvider,
            [FileDialogUtils.AudioFiles]);

        if (file == null || string.IsNullOrEmpty(file.Path.LocalPath))
        {
            Logger.Log("No audio file selected!");
            LoadAudioButton.IsEnabled = true;
            return;
        }

        LoadAudioButton.Click -= LoadAudio;
        LoadAudioButton.IsEnabled = false;
        LoadAudioButton.Content = $"Loaded file '{Path.GetFileName(file.Path.LocalPath)}'";

        m_AudioData = await File.ReadAllBytesAsync(file.Path.LocalPath);
        
        InjectButton.Click -= Inject;
        InjectButton.Click += Inject;

        InjectButton.IsEnabled = true;
    }

    private void OnClosed(object? sender, EventArgs eventArgs)
    {
        UtilExtensions.GetMainWindow()?.Show();
    }

    private void LogAction(string message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            Progress.IsEnabled = true;
            Progress.IsVisible = true;
            Progress.ProgressTextFormat = message;
        });
    }
    
    private void ExceptionCallback(string message, Exception err)
    {
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await MessageBox.ShowMessageBox(this, $"{message}\n\nPlease check CAIExceptionLog.txt in the application folder for more info.\n\nError: {err}", "An exception has occured.");
        });
    }

    private Sound CreateSoundFromUIOptions()
    {
        var sound = new Sound(NameIdInput.Text!);
        sound.ChannelId = MusicChannelRadioButton.IsChecked == true ? Channel.Music : Channel.Sound;
        sound.AssetPriority = AssetBundleIndex.A; // just always use A, there's not much point in actually letting the user decide since it doesn't change how the game behaves

        if (VolumeChangeCheckBox.IsChecked == true)
        {
            sound.Volume = (float)VolumeInput.Value!;
        }

        if (LoopingRadioButton.IsChecked == true)
        {
            sound.Params.Add(new SoundParameter(SoundParameterType.Looping, 0f));
        }
        else if (OneshotRadioButton.IsChecked == true)
        {
            sound.Params.Add(new SoundParameter(SoundParameterType.OneShot, 0f));
        }

        if (PrimaryMusicSourceRadioButton.IsChecked == true)
        {
            sound.Params.Add(new SoundParameter(SoundParameterType.CurrentPrimaryMusicSource, 0f));
        }
        else if (SecondaryMusicSourceRadioButton.IsChecked == true)
        {
            sound.Params.Add(new SoundParameter(SoundParameterType.CurrentSecondaryMusicSource, 0f));
        }
        // do nothing for default sound source, since the game uses the default if no param specifies the primary or secondary music source

        if (CrossfadeCheckBox.IsChecked == true) // todo: check why this isn't working
        { 
            sound.Params.Add(new SoundParameter(SoundParameterType.Crossfade, (float)CrossfadeLengthInput.Value!));
        }
        
        return sound;
    }

    private async void Inject(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(NameIdInput.Text))
        {
            Logger.Log("No NameID specified! Please input a NameID before injecting.");
            return;
        }
        
        if (m_AudioData == null || m_AudioData.Length == 0)
        {
            Logger.Log("Audio is null! Please select an audio file before injecting.");
            return;
        }

        if (m_AudioInjector.SoundAlreadyExists(NameIdInput.Text))
        {
            var choice = await MessageBox.ShowMessageBox(
                this,
                "ConfirmReplaceSound",
                "Are you sure you want to replace this sound? Injecting now will replace the sound in game with the new audio and it's corresponding settings.",
                "Replacing sound",
                ["Yes", "Cancel"]);
            
            if (choice == "Cancel" || choice == MessageBox.EXIT_STRING)
            {
                return;
            }
        }

        var sound = CreateSoundFromUIOptions();

        InjectButton.IsEnabled = false;
        
        // show the progress bar on the first injection, then keep it there
        Height = 650;
        Progress.Foreground = new SolidColorBrush(Colors.SeaGreen);
        Progress.IsVisible = true;
        Progress.IsEnabled = true;
        
        Progress.IsIndeterminate = true;
        
        var returnCode = CommonUtils.ReturnCode.UnknownError;
        try
        {
            returnCode = await Task.Run(() => m_AudioInjector.Inject(sound, m_AudioData));
        }
        catch (Exception err)
        {
            Logger.Log("Inject failed!", Logger.LogLevel.Exception, err);
            InjectButton.IsEnabled = true;
            return;
        }
        finally
        {
            Progress.IsIndeterminate = false;
        }

        if (returnCode != CommonUtils.ReturnCode.Success)
        {
            Logger.Log("Failed to inject audio! Error code: " + returnCode);
            Progress.Foreground = new SolidColorBrush(Colors.Firebrick);
            InjectButton.IsEnabled = true;
            return;
        }
        
        Logger.Log($"Successfully injected audio with name '{sound.NameId}'!");

        InjectButton.IsEnabled = true;
    }

    private void SetRecommendedSource(object? sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton musicRb || SoundSourceRadioButton == null)
            return;
        
        if (musicRb.IsChecked == true) // music channel
        {
            if (SoundSourceRadioButton.IsChecked == true)
                PrimaryMusicSourceRadioButton.IsChecked = true;
            SoundSourceRadioButton!.Content = "Sound source";
            PrimaryMusicSourceRadioButton.Content = "Primary source (recommended)";
        } 
        else if (musicRb.IsChecked == false) // sound channel
        {
            SoundSourceRadioButton!.Content = "Sound source (recommended)";
            PrimaryMusicSourceRadioButton!.Content = "Primary source";
        }
    }
}