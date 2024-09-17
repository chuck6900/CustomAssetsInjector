using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using CustomAssetsInjector.Utils;
using Color = Avalonia.Media.Color;

namespace CustomAssetsInjector.Services;

public static class ProgressService
{
    private static Dictionary<string, ProgressBar> m_CurrentProgressBars = new();
    
    public static readonly string ApkLoadingProgressId = "APK_Loading_Progress";
    
    public static readonly string ApkResetProgressId = "APK_Reset_Progress";
    
    public static readonly string SpriteSheetLoadingProgressId = "SpriteSheet_Loading_Progress";
    
    public static readonly string CreateBundleProgressId = "Bundle_Create_Progress";

    public static void Reset(bool hide)
    {
        foreach (var progressId in m_CurrentProgressBars.Keys)
        {
            ProgressService.DeRegisterProgress(progressId, hide);
        }
    }
    
    public static bool RegisterProgress(string progressId, ProgressBar progressBar)
    {
        if (!m_CurrentProgressBars.TryAdd(progressId, progressBar))
            return false;

        Dispatcher.UIThread.Invoke(() =>
        {
            progressBar.SetActive(true);
        });

        return true;
    }

    public static void DeRegisterProgress(string progressId, bool hide)
    {
        m_CurrentProgressBars.Remove(progressId, out var progressBar);

        if (progressBar == null)
            return;

        Dispatcher.UIThread.Invoke(() =>
        {
            progressBar.SetActive(!hide);
        });
    }
    
    public static ProgressBar? UpdateProgress(
        string progressId,
        double progress,
        bool? indeterminate = null,
        double? min = null,
        double? max = null,
        string? progressString = null,
        Color? foregroundColor = null)
    {
        if (!m_CurrentProgressBars.TryGetValue(progressId, out var progressBar))
            return null;

        Dispatcher.UIThread.Invoke(() =>
        {
            progressBar.Value = progress;

            if (indeterminate != null)
                progressBar.IsIndeterminate = indeterminate.Value;

            if (min != null)
                progressBar.Minimum = min.Value;

            if (max != null)
                progressBar.Maximum = max.Value;

            if (progressString != null)
                progressBar.ProgressTextFormat = progressString;

            if (foregroundColor != null)
                progressBar.Foreground = new SolidColorBrush(foregroundColor.Value);
        });

        return progressBar;
    }
}