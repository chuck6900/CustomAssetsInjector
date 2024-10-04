using System;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CustomAssetsBackend.Misc;
using CustomAssetsInjector.Utils;

namespace CustomAssetsInjector;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }
    
    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var err = (e.ExceptionObject as Exception)!;
        
        Logger.Log("FATAL EXCEPTION! Please open a github issue or ping/message @heroic2 on Discord with this file.", Logger.LogLevel.Exception, err);
        _ = UtilExtensions.GetClipboard()?.SetTextAsync(err.ToString());
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            string mshtaArgs = "vbscript:Execute(\"CreateObject(\"\"WScript.Shell\"\").Popup \"\"An unhandled exception has occured. The exception has been copied to the clipboard. Please open the CAIExceptionLog.txt file located by the exe for more info.\"\",,\"\"CustomAssetsInjector crash\"\" :close\")";
            Process.Start(new ProcessStartInfo("mshta", mshtaArgs));
        }
        else
        {
            Console.WriteLine("CustomAssetsInjector crash! The exception has been copied to the clipboard.");
            Console.WriteLine(err.ToString());
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Views.MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}