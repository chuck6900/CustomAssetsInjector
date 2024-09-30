using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CustomAssetsBackend.Classes;
using CustomAssetsBackend.Misc;
using CustomAssetsBackend.SpriteSheet;
using CustomAssetsBackend.SpriteSheet.SmoothMoves;
using CustomAssetsInjector.Actions;
using CustomAssetsInjector.Controls;
using CustomAssetsInjector.Services;
using CustomAssetsInjector.Utils;
using LibCpp2IL.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;

namespace CustomAssetsInjector.Views;

public partial class SpriteSheetEditorWindow : Window
{
    private int IndexOfSpriteSettingsTab => SpriteSettingsTabControl.IndexFromContainer(SettingsTab);
    private int IndexOfLogsTab => SpriteSettingsTabControl.IndexFromContainer(LogsTab);

    private StateManager m_StateManager = new();
    
    private string m_AtlasImagePath;

    private string m_LoadedAtlasName;

    private Sprite? m_SelectedSprite;
    private Sprite? SelectedSprite
    {
        get
        {
            // if you draw a sprite, once you finish drawing, that sprite gets selected automatically
            // so make sure that we are never de-synced
            m_SelectedSprite = PreviewGroupBox.SelectedSprite;
            return m_SelectedSprite;
        }
        set
        {
            PreviewGroupBox.SelectedSprite = value;
            m_SelectedSprite = value;
            
            if (m_SelectedSprite != null)
            {
                SetMaxSizeControlValues();
                UpdateSizeControlValues(m_SelectedSprite);
                RegisterRectEventHandlers(m_SelectedSprite);
            }
        }
    }

    private SpriteSheetManager? m_SpriteSheetManager;

    public SpriteSheetEditorWindow()
    {
        InitializeComponent();

        PreviewGroupBox.StateManager = m_StateManager;
        
        LoadAndSaveAtlasButton.Content = "Load atlas";
        
        PreviewGroupBox.SpriteCreated -= PreviewGroupBox_SpriteCreated;
        PreviewGroupBox.SpriteCreated += PreviewGroupBox_SpriteCreated;
        
        // local log callbacks
        Logger.LogAction -= LogCallback;
        Logger.LogAction += LogCallback;
        Logger.ExceptionAction -= ExceptionLogCallback;
        Logger.ExceptionAction += ExceptionLogCallback;
        
        // LibCpp2IL log callback
        var callbackWriter = new CallbackWriter();
        callbackWriter.LogCallback -= LibCpp2IlLogCallback;
        callbackWriter.LogCallback += LibCpp2IlLogCallback;
        LibLogger.Writer = callbackWriter;
        
        // base buttons
        ResetButton.Click += Reset;
        LoadAndSaveAtlasButton.Click += LoadAtlas;
        
        // edit tab buttons
        ImportAtlasImageButton.Click += ImportAtlasPng;
        ImportSpritesButton.Click += ImportSprites;
        ExportAtlasImageButton.Click += ExportAtlasPng;
        ExportAllSpritesButton.Click += ExportAllAtlasSprites;
        ImportAtlasDataButton.Click += ImportAtlasData;
        ExportAtlasDataButton.Click += ExportAtlasData;
        
        // delete buttons
        DeleteRectKeepImageButton.Click += DeleteRectKeepImage;
        DeleteRectDeleteImageButton.Click += DeleteRectDeleteImage;
        
        // input fields
        AtlasNameInput.TextChanged += OnAtlasNameInputUpdated;
        AtlasNameInput.KeyDown += OnAtlasNameInputKeyDown;
        SpriteNameInput.TextChanged += SpriteNameInputTextChanged;
        
        // size controls
        XPositionInput.ValueChanged += XPositionInput_ValueChanged;
        YPositionInput.ValueChanged += YPositionInput_ValueChanged;
        WidthInput.ValueChanged += WidthInput_ValueChanged;
        HeightInput.ValueChanged += HeightInput_ValueChanged;
        OriginXInput.ValueChanged += OriginXInput_ValueChanged;
        OriginYInput.ValueChanged += OriginYInput_ValueChanged;

        // zoom controls
        ZoomInButton.Click += delegate { PreviewGroupBox.SelectionCanvas.ZoomIn(); };
        PreviewGroupBox.SelectionCanvas.ZoomChanged += OnSelectionCanvasZoomChanged;
        ZoomOutButton.Click += delegate { PreviewGroupBox.SelectionCanvas.ZoomOut(); };
        ResetZoomButton.Click += delegate { PreviewGroupBox.SelectionCanvas.ResetZoom(); };
        
        // keybind handling
        this.KeyDown += OnKeyDown;
        
        // confirmation popup on close
        this.Closing += OnClosing;
    }

    #region Window events

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.Control)
        {
            switch (e.Key)
            {
                // zoom in on ctrl +
                case Key.Add:
                case Key.OemPlus:
                    PreviewGroupBox.SelectionCanvas.ZoomIn();
                    break;
                // zoom out on ctrl -
                case Key.Subtract:
                case Key.OemMinus:
                    PreviewGroupBox.SelectionCanvas.ZoomOut();
                    break;
                // advanced delete (delete rect + image) on ctrl + delete
                case Key.Delete:
                    DeleteRectDeleteImage(sender, e);
                    break;
                // undo on ctrl + z
                case Key.Z:
                    m_StateManager.Undo();
                    break;
                // redo on ctrl + y
                case Key.Y:
                    m_StateManager.Redo();
                    break;
            }
        }
        else if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            // redo on ctrl + shift + z
            if (e.Key == Key.Z)
                m_StateManager.Redo();
        } 
        // basic delete on delete
        else if (e.Key == Key.Delete)
            DeleteRectKeepImage(sender, e);
        else
            return;
        
        e.Handled = true;
    }
    
    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (m_SpriteSheetManager == null)
            return;
        
        e.Cancel = true;
        
        var currentSpriteDatas = PreviewGroupBox.SpriteDatabase.Sprites
            .Select(sprite => sprite.AsSpriteData())
            .ToList();

        // if lists are equal, no changes have been made
        if (currentSpriteDatas.IsEqualTo(m_SpriteSheetManager.Sprites))
        {
            e.Cancel = false;
            this.Closing -= OnClosing;
            this.Close();
            return;
        }

        // changes have been made
        var result = await MessageBox.ShowMessageBox(
            this,
            "You have unsaved changes! Are you sure you want to quit?",
            "Confirm",
            ["Yes", "Cancel"]);

        if (result == "Yes")
        {
            e.Cancel = false;
            this.Closing -= OnClosing;
            this.Close();
            return;
        }

        // cancel shutdown
        e.Cancel = true;
    }
    
    #endregion
    
    #region Logging

    private void LibCpp2IlLogCallback(string logMessage, CallbackWriter.LogLevel logLevel)
    {
        // block verbose and info because there are lots of logs
        if (logLevel == CallbackWriter.LogLevel.Verbose || logLevel == CallbackWriter.LogLevel.Info)
            return;

        var localLoggerLevel = logLevel switch
        {
            //CallbackWriter.LogLevel.Verbose => Logger.LogLevel.Debug,
            //CallbackWriter.LogLevel.Info => Logger.LogLevel.Generic,
            CallbackWriter.LogLevel.Warn => Logger.LogLevel.Generic,
            CallbackWriter.LogLevel.Error => Logger.LogLevel.Exception,
            _ => throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null)
        };

        logMessage = logMessage.ReplaceLineEndings(" ");
        
        Logger.Log(logMessage, localLoggerLevel);
    }
    
    private void LogCallback(string logMessage)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            LogWindow.Text += logMessage + "\n";
        });
    }

    private void ExceptionLogCallback(string logMessage, Exception err)
    {
        LogCallback(logMessage);
        
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await MessageBox.ShowMessageBox(this, $"{logMessage}\n\nPlease check CAIExceptionLog.txt in the application folder for more info.\n\nError: {err}", "An exception has occured.");
        });
    }
    
    #endregion
    
    #region Deleting
    
    private void DeleteRectKeepImage(object? sender, RoutedEventArgs e)
    {
        if (SelectedSprite == null)
            return;

        var basicDeleteAction = new BasicDeleteSpriteAction(SelectedSprite, PreviewGroupBox);
        m_StateManager.ExecuteAction(basicDeleteAction);
    }
    
    private async void DeleteRectDeleteImage(object? sender, RoutedEventArgs e)
    {
        if (SelectedSprite == null)
            return;

        var result = await MessageBox.ShowMessageBox(
            this,
            $"""
            Are you sure you want to delete '{SelectedSprite.SpriteName}' from the spritesheet? 
            This action cannot be undone.
            """,
            "Confirm", 
            ["Yes", "Cancel"]);

        if (result == "Cancel")
            return;
        
        // visually indicate that its loading
        PreviewGroupBox.SelectionCanvas.Opacity = 0.5;
        PreviewGroupBox.SelectionCanvas.IsEnabled = false;
        
        // delete rect
        // create the delete action and execute the action itself
        // don't execute it via the state manager otherwise you can undo JUST the rect delete
        new BasicDeleteSpriteAction(SelectedSprite, PreviewGroupBox).Execute();
        
        // pack current sprites
        // packing sprites after deleting the rect means that the image will not be packed into the spritesheet and will get removed
        await Task.Run(() => RePackAndParseSprites());
        
        PreviewGroupBox.SelectionCanvas.IsEnabled = true;
        PreviewGroupBox.SelectionCanvas.Opacity = 1;
    }
    
    private void RePackAndParseSprites(List<RectPacker.PackingSpriteData>? sprites = null)
    {
        var atlasImage = Image.Load<Rgba32>(m_AtlasImagePath);
        
        // create images of existing sprites
        var spriteInfoList = new List<RectPacker.PackingSpriteData>();
        if (sprites != null)
            spriteInfoList.AddRange(sprites);
            
        foreach (var sprite in PreviewGroupBox.SpriteDatabase.Sprites)
        {
            var xPos = 0;
            var yPos = 0;
            var spriteWidth = 0;
            var spriteHeight = 0;
            Dispatcher.UIThread.Invoke(() =>
            {
                xPos = (int)Canvas.GetLeft(sprite);
                yPos = (int)Canvas.GetTop(sprite);
                spriteWidth = (int)sprite.Width;
                spriteHeight = (int)sprite.Height;
            });

            if (spriteWidth < 10 || spriteHeight < 10)
                continue;

            var cropRect = new Rectangle(xPos, yPos, spriteWidth, spriteHeight);
                    
            var croppedImage = atlasImage.Clone();
            croppedImage.Mutate(ctx => ctx.Crop(cropRect));

            SpriteData data = new();
            Dispatcher.UIThread.Invoke(() =>
            {
                data = sprite.AsSpriteData();
            });
            
            var spriteInfo = new RectPacker.PackingSpriteData
            {
                SpriteData = data,
                ImageData = croppedImage
            };
            spriteInfoList.Add(spriteInfo);
        }

        var newSprites = RectPacker.PackRects(spriteInfoList, m_AtlasImagePath, 2);
        
        m_SpriteSheetManager?.Sprites.Clear();
        m_SpriteSheetManager?.Sprites.AddRange(newSprites.Select(info => info.SpriteData));

        Dispatcher.UIThread.Invoke(() =>
        {
            // parse again
            LoadSprites();

            // reload image
            LoadImage();
        });
    }
    
    #endregion

    #region Sprite event handlers
    
    private void Sprite_RightClicked(object? sender, EventArgs e)
    {
        var rightClickedSprite = (Sprite)sender!;
        
        if (SelectedSprite != null && SelectedSprite == rightClickedSprite)
        {
            SettingsTab.IsEnabled = false;
            if (SpriteSettingsTabControl.SelectedIndex == IndexOfSpriteSettingsTab)
                SpriteSettingsTabControl.SelectedIndex = IndexOfLogsTab;
            SelectedSprite = null;
            return;
        }

        SettingsTab.IsEnabled = true;
        SpriteSettingsTabControl.SelectedIndex = IndexOfSpriteSettingsTab;
        SelectedSprite = rightClickedSprite;
        SpriteNameInput.Text = rightClickedSprite.SpriteName;
    }
    
    private void PreviewGroupBox_SpriteCreated(Sprite createdSprite)
    {
        RegisterRectEventHandlers(createdSprite);
        createdSprite.RightClicked -= Sprite_RightClicked;
        createdSprite.RightClicked += Sprite_RightClicked;
    }
    
    private void OnEditActionCreated(object? sender, EditSpriteAction action) => m_StateManager.ExecuteAction(action);

    private void XChanged(object? sender, double newValue) => XPositionInput.Value = (decimal)newValue;
    private void YChanged(object? sender, double newValue) => YPositionInput.Value = (decimal)newValue;
    private void WidthChanged(object? sender, double newValue) => WidthInput.Value = (decimal)newValue;
    private void HeightChanged(object? sender, double newValue) => HeightInput.Value = (decimal)newValue;
    private void OriginXChanged(object? sender, double newValue) => OriginXInput.Value = (decimal)newValue;
    private void OriginYChanged(object? sender, double newValue) => OriginYInput.Value = (decimal)newValue;
    
    private void DeRegisterRectEventHandlers(TransformControlRectangle rect)
    {
        rect.XChanged -= XChanged;
        rect.YChanged -= YChanged;
        rect.WidthChanged -= WidthChanged;
        rect.HeightChanged -= HeightChanged;
        rect.OriginXChanged -= OriginXChanged;
        rect.OriginYChanged -= OriginYChanged;
        
        rect.OnEditActionCreated -= OnEditActionCreated;
    }
    
    private void RegisterRectEventHandlers(TransformControlRectangle rect)
    {
        DeRegisterRectEventHandlers(rect);
        
        rect.XChanged += XChanged;
        rect.YChanged += YChanged;
        rect.WidthChanged += WidthChanged;
        rect.HeightChanged += HeightChanged;
        rect.OriginXChanged += OriginXChanged;
        rect.OriginYChanged += OriginYChanged;
        
        rect.OnEditActionCreated += OnEditActionCreated;
    }

    private void UpdateSizeControlValues(TransformControlRectangle sprite)
    {
        XPositionInput.Value = (decimal)Canvas.GetLeft(sprite);
        YPositionInput.Value = (decimal)Canvas.GetTop(sprite);

        WidthInput.Value = (decimal)sprite.Width;
        HeightInput.Value = (decimal)sprite.Height;

        OriginXInput.Value = (decimal)sprite.OriginPoint.X;
        OriginYInput.Value = (decimal)sprite.OriginPoint.Y;
        
        sprite.RepositionHandles();
    }

    private void SetMaxSizeControlValues()
    {
        var (resWidth, resHeight) = CommonUtils.GetImageResolution(m_AtlasImagePath);
        XPositionInput.Maximum = resWidth;
        YPositionInput.Maximum = resHeight;

        WidthInput.Maximum = resWidth;
        HeightInput.Maximum = resHeight;
        
        // origin point maximums are already set in axaml
    }
    
    private bool GuardSelectedSpriteNull()
    {
        if (SelectedSprite == null)
        {
            SettingsTab.IsEnabled = false;
            if (SpriteSettingsTabControl.SelectedIndex == IndexOfSpriteSettingsTab) 
                SpriteSettingsTabControl.SelectedIndex = IndexOfLogsTab;
            
            return true;
        }

        return false;
    }
    
    private void SpriteNameInputTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (GuardSelectedSpriteNull())
            return;

        SelectedSprite!.SpriteName = SpriteNameInput.Text ?? string.Empty;
    }
    
    private void XPositionInput_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (GuardSelectedSpriteNull())
            return;
        
        // shut the damn compiler up with its "nullable blah blah blah"
        var selectedSprite = SelectedSprite!;
        
        double tempTop = 0;
        var newXPos = (double?)e.NewValue ?? Canvas.GetLeft(selectedSprite);
        var newRightPos = newXPos + selectedSprite.Width;
        
        Sprite.MakePointsInsideControl(PreviewGroupBox.AtlasImage, ref newXPos, ref newRightPos, ref tempTop, ref tempTop, selectedSprite.Width, 0);
        
        Canvas.SetLeft(selectedSprite, newXPos);
        Canvas.SetRight(selectedSprite, newRightPos);
        
        UpdateSizeControlValues(selectedSprite);
    }
    
    private void YPositionInput_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (GuardSelectedSpriteNull())
            return;
        
        var selectedSprite = SelectedSprite!;
        
        double tempLeft = 0;
        var newYPos = (double?)e.NewValue ?? Canvas.GetTop(selectedSprite);
        var newBottomPos = newYPos + selectedSprite.Height;
        
        Sprite.MakePointsInsideControl(PreviewGroupBox.AtlasImage, ref tempLeft, ref tempLeft, ref newYPos, ref newBottomPos, 0, selectedSprite.Height);
        
        Canvas.SetTop(selectedSprite, newYPos);
        Canvas.SetBottom(selectedSprite, newBottomPos);
        
        UpdateSizeControlValues(selectedSprite);
    }
    
    private void WidthInput_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (GuardSelectedSpriteNull())
            return;
        
        var selectedSprite = SelectedSprite!;

        double tempTop = 0;
        var newWidth = (double?)e.NewValue ?? selectedSprite.Width;
        var newLeft = Canvas.GetLeft(selectedSprite);
        var newRight = newLeft + selectedSprite.Width;
        
        Sprite.MakePointsInsideControl(PreviewGroupBox.AtlasImage, ref newLeft, ref newRight, ref tempTop, ref tempTop, selectedSprite.Width, 0);
        
        selectedSprite.Width = newWidth;
        
        Canvas.SetLeft(selectedSprite, newLeft);
        Canvas.SetRight(selectedSprite, newLeft + newWidth);
        
        UpdateSizeControlValues(selectedSprite);
    }
    
    private void HeightInput_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (GuardSelectedSpriteNull())
            return;
        
        var selectedSprite = SelectedSprite!;

        double tempLeft = 0;
        var newHeight = (double?)e.NewValue ?? selectedSprite.Height;
        var newYPos = Canvas.GetTop(selectedSprite);
        var newBottomPos = newYPos + selectedSprite.Height;
        
        Sprite.MakePointsInsideControl(PreviewGroupBox.AtlasImage, ref tempLeft, ref tempLeft, ref newYPos, ref newBottomPos, 0, selectedSprite.Height);
        
        selectedSprite.Height = newHeight;
        
        Canvas.SetTop(selectedSprite, newYPos);
        Canvas.SetBottom(selectedSprite, newYPos + newHeight);
        
        UpdateSizeControlValues(selectedSprite);
    }
    
    private void OriginXInput_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (GuardSelectedSpriteNull())
            return;
        
        var selectedSprite = SelectedSprite!;
        
        var newOriginPointX = (float?)e.NewValue ?? selectedSprite.OriginPoint.X;
        selectedSprite.OriginPoint.X = newOriginPointX;
        selectedSprite.RepositionHandles();
    }
    
    private void OriginYInput_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (GuardSelectedSpriteNull())
            return;
        
        var selectedSprite = SelectedSprite!;
        
        var newOriginPointY = (float?)e.NewValue ?? selectedSprite.OriginPoint.Y;
        selectedSprite.OriginPoint.Y = newOriginPointY;
        selectedSprite.RepositionHandles();
    }
    
    #endregion
    
    #region Event handlers for the buttons in the Edit tab
    
    private void OnSelectionCanvasZoomChanged(double newValue)
    {
        // set current zoom text
        var newValueInPercent = newValue * 100;
        CurrentZoomText.Text = newValueInPercent.ToString(CultureInfo.InvariantCulture) + "%";
    }
    
    private async void ImportAtlasPng(object? sender, RoutedEventArgs e)
    {
        try
        {
            var file = await FileDialogUtils.PromptOpenFile(
                "Select atlas png", 
                this.StorageProvider, 
                [FileDialogUtils.PngFile]);

            if (file == null)
            {
                Logger.Log("No file selected.");
                return;
            }

            // overwrite old image
            File.Copy(file.Path.LocalPath, m_AtlasImagePath, true);
            LoadImage();
        }
        catch (Exception err)
        {
            Logger.Log("An exception has occured while trying to import the png!", Logger.LogLevel.Exception, err);
        }
    }
    
    private async void ImportSprites(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await FileDialogUtils.PromptOpenFiles(
                "Select the sprites to add", 
                this.StorageProvider, 
                [FileDialogUtils.PngFile]);
        
            if (files == null)
            {
                Logger.Log("No files selected.");
                return;
            }
            
            // visually indicate that its loading
            PreviewGroupBox.SelectionCanvas.Opacity = 0.5;
            PreviewGroupBox.SelectionCanvas.IsEnabled = false;
            
            var spriteInfoList = new List<RectPacker.PackingSpriteData>();
            
            // create images of existing sprites and add them to the list
            foreach (var file in files)
            {
                var path = file.Path.LocalPath;
                
                var imageData = Image.Load<Rgba32>(path);
                var (width, height) = CommonUtils.GetImageResolution(path);
                
                // we only really need to set the width and height
                var spriteName = Path.GetFileNameWithoutExtension(path);
                
                var spriteData = new SpriteData
                {
                    Name = spriteName,
                    
                    Width = width,
                    Height = height
                };

                var spriteInfo = new RectPacker.PackingSpriteData
                {
                    SpriteData = spriteData,
                    ImageData = imageData
                };
                spriteInfoList.Add(spriteInfo);
            }
            
            // pack all current sprites + the new ones in the list
            await Task.Run(() => RePackAndParseSprites(spriteInfoList));
            
            // select the last sprite so the user gets an idea of where the new sprites are
            var lastNewSpriteName = spriteInfoList.LastOrDefault().SpriteData.Name;
            SelectedSprite = PreviewGroupBox.SpriteDatabase.Sprites.LastOrDefault(sprite => sprite.SpriteName == lastNewSpriteName);
        }
        catch (Exception err)
        {
            Logger.Log("An exception has occured while trying to import the sprites!", Logger.LogLevel.Exception, err);
        }
        
        PreviewGroupBox.SelectionCanvas.IsEnabled = true;
        PreviewGroupBox.SelectionCanvas.Opacity = 1;
    }

    private async void ExportAtlasPng(object? sender, RoutedEventArgs e)
    {
        try
        {
            var file = await FileDialogUtils.PromptSaveFile(
                "Save atlas png",
                this.StorageProvider,
                m_LoadedAtlasName,
                "png");

            if (file == null)
                return;

            File.Copy(m_AtlasImagePath, file.Path.LocalPath, true);
        }
        catch (Exception err)
        {
            Logger.Log("An exception has occured while trying to export the png!", Logger.LogLevel.Exception, err);
        }
    }
    
    private async void ExportAllAtlasSprites(object? sender, RoutedEventArgs e)
    {
        try
        {
            var folder = await FileDialogUtils.PromptSelectFolder("Select a folder to export sprites to.", this.StorageProvider);

            if (folder == null)
                return;

            var exportingSpritesProgressId = "Sprites_Export_Progress";

            ProgressService.RegisterProgress(exportingSpritesProgressId, LogsProgressBar);

            var atlasImage = Image.Load<Rgba32>(m_AtlasImagePath);

            var sprites = PreviewGroupBox.SpriteDatabase.Sprites;
            for (var i = 0; i < sprites.Count; i++)
            {
                var sprite = sprites[i];
                
                var xPos = Canvas.GetLeft(sprite);
                var yPos = Canvas.GetTop(sprite);

                var cropRect = new Rectangle((int)xPos, (int)yPos, (int)sprite.Width, (int)sprite.Height);

                var croppedImage = atlasImage.Clone();
                croppedImage.Mutate(ctx => ctx.Crop(cropRect));

                var outputPath = Path.Combine(folder.Path.LocalPath, sprite.SpriteName + ".png");
                
                await croppedImage.SaveAsPngAsync(outputPath);

                ProgressService.UpdateProgress(
                    exportingSpritesProgressId,
                    i,
                    false,
                    0,
                    sprites.Count - 1,
                    "Exporting sprites: {0}/{3} sprites exported ({1:0}%)");
            }
            
            ProgressService.DeRegisterProgress(exportingSpritesProgressId, false);
        }
        catch (Exception err)
        {
            Logger.Log("An exception has occured while trying to export the sprites!", Logger.LogLevel.Exception, err);
        }
    }
    
    private async void ImportAtlasData(object? sender, RoutedEventArgs e)
    {
        try
        {
            var result = await MessageBox.ShowMessageBox(
                this,
                "Importing new spritesheet data will overwrite any unsaved changes. Are you sure you want to continue?",
                "Confirm",
                ["Yes", "Cancel"]);

            if (result != "Yes")
                return;
            
            var file = await FileDialogUtils.PromptOpenFile(
                "Import spritesheet data from JSON file", 
                this.StorageProvider, 
                [FileDialogUtils.JsonFile]);

            if (file == null)
            {
                Logger.Log("No file selected.");
                return;
            }
            
            PreviewGroupBox.SelectionCanvas.Opacity = 0.5;
            PreviewGroupBox.SelectionCanvas.IsEnabled = false;
            LoadAndSaveAtlasButton.IsEnabled = false;
            ResetButton.IsEnabled = false;
            
            var returnCode = await Task.Run(() => m_SpriteSheetManager?.Import(file.Path.LocalPath));
            
            PreviewGroupBox.SelectionCanvas.Opacity = 1;
            PreviewGroupBox.SelectionCanvas.IsEnabled = true;
            LoadAndSaveAtlasButton.IsEnabled = true;
            ResetButton.IsEnabled = true;

            if (returnCode != CommonUtils.ReturnCode.Success)
            {
                // if returnCode == null, then m_SpriteSheetManager was null, meaning no atlas was loaded
                Logger.Log($"Import failed with error code: {returnCode ?? CommonUtils.ReturnCode.NoAtlasLoaded}");
            }
            
            // clear undo/redo stuff
            m_StateManager.Reset();

            // reload the atlas (don't call LoadAtlas because that will do the whole obb search process again)
            LoadSprites();
            LoadImage();
        }
        catch (Exception err)
        {
            Logger.Log("An exception has occured while trying to import the new spritesheet data!", Logger.LogLevel.Exception, err);
        }
    }

    private async void ExportAtlasData(object? sender, RoutedEventArgs e)
    {
        try
        {
            var file = await FileDialogUtils.PromptSaveFile(
                "Import spritesheet data from JSON file", 
                this.StorageProvider,
                "dump.json",
                "json",
                [FileDialogUtils.JsonFile]);

            if (file == null)
                return;
            
            PreviewGroupBox.SelectionCanvas.Opacity = 0.5;
            PreviewGroupBox.SelectionCanvas.IsEnabled = false;
            LoadAndSaveAtlasButton.IsEnabled = false;
            ResetButton.IsEnabled = false;
            
            var returnCode = await Task.Run(() => m_SpriteSheetManager?.Export(file.Path.LocalPath));
            
            PreviewGroupBox.SelectionCanvas.Opacity = 1;
            PreviewGroupBox.SelectionCanvas.IsEnabled = true;
            LoadAndSaveAtlasButton.IsEnabled = true;
            ResetButton.IsEnabled = true;

            if (returnCode != CommonUtils.ReturnCode.Success)
            {
                // if returnCode == null, then m_SpriteSheetManager was null, meaning no atlas was loaded
                Logger.Log($"Export failed with error code: {returnCode ?? CommonUtils.ReturnCode.NoAtlasLoaded}");
            }
        }
        catch (Exception err)
        {
            Logger.Log("An exception has occured while trying to export the spritesheet data!", Logger.LogLevel.Exception, err);
        }
    }

    #endregion
    
    #region Loading and saving
    
    private async void LoadAtlas(object? sender, RoutedEventArgs e)
    {
        var obbSearchToken = new CancellationTokenSource();
        void CancelLoad(object? _, RoutedEventArgs __) => obbSearchToken.Cancel();
        
        LoadAndSaveAtlasButton.IsEnabled = false;
        AtlasNameInput.IsEnabled = false;
        
        ResetButton.Content = "Cancel";
        ResetButton.Click -= Reset;
        ResetButton.Click += CancelLoad;
        ResetButton.IsEnabled = true;
        
        var atlasName = AtlasNameInput.Text ?? string.Empty;
        var useLowResAtlas = LowResToggle.IsChecked ?? false;

        if (useLowResAtlas)
            Logger.Log("Finding the low res atlas! You probably want to turn this off unless you know what you're doing.");

        ProgressService.RegisterProgress(ProgressService.SpriteSheetLoadingProgressId, LogsProgressBar);
        ProgressService.UpdateProgress(
            ProgressService.SpriteSheetLoadingProgressId,
            0,
            true,
            0,
            1,
            "Initializing..", 
            useLowResAtlas ? Colors.Goldenrod : Colors.SeaGreen);
        
        var (returnCode, spriteSheetManager) = await Task.Run(
            () => SpriteSheetManagerFactory.CreateSpriteSheetManager(atlasName, useLowResAtlas, obbSearchToken.Token), 
            obbSearchToken.Token);

        ResetButton.Content = "Reset";
        ResetButton.Click -= CancelLoad;
        ResetButton.Click += Reset;
        ResetButton.IsEnabled = false;
        
        if (returnCode == CommonUtils.ReturnCode.Cancelled || obbSearchToken.Token.IsCancellationRequested)
        {
            Logger.Log("Loading was cancelled.");
            ProgressService.UpdateProgress(
                ProgressService.SpriteSheetLoadingProgressId, 
                1, 
                false, 
                0, 
                1, 
                "Cancelled.");
            
            LoadAndSaveAtlasButton.IsEnabled = true;
            AtlasNameInput.IsEnabled = true;
            
            return;
        }
        
        if (returnCode != CommonUtils.ReturnCode.Success)
        {
            Logger.Log($"Unable to resolve spritesheet manager. Error code: {returnCode.ToString()}");
            LoadAndSaveAtlasButton.IsEnabled = true;
            AtlasNameInput.IsEnabled = true;
            return;
        }

        m_SpriteSheetManager = spriteSheetManager!;
        
        // Logger.Log($"Spritesheet manager successfully resolved. Manager: {m_SpriteSheetManager.GetType()}", Logger.LogLevel.Debug);
            
        var spriteSheetMgrReturnCode = await Task.Run(() => m_SpriteSheetManager.Load());
        
        if (spriteSheetMgrReturnCode != CommonUtils.ReturnCode.Success)
        {
            Logger.Log($"Failed to load the atlas! Error: {spriteSheetMgrReturnCode.ToString()}");
            LoadAndSaveAtlasButton.IsEnabled = true;
            AtlasNameInput.IsEnabled = true;
            return;
        }

        // AssetCache will always contain the necessary assets if ReturnCode == Success
        m_LoadedAtlasName = m_SpriteSheetManager.AssetCache.First(asset => asset.ObjectType == UnityAsset.UnityObjectType.Texture2D).Name;

        // if the atlas image doesn't exist, then the load failed
        if (!File.Exists(CommonUtils.AtlasImagePath)) 
            return;
        
        m_AtlasImagePath = CommonUtils.AtlasImagePath;

        LoadSprites();
        
        LoadImage();

        ResetButton.IsEnabled = true;
        
        LoadAndSaveAtlasButton.Content = "Save atlas";
        LoadAndSaveAtlasButton.Click -= LoadAtlas;
        LoadAndSaveAtlasButton.Click += SaveAtlas;

        AtlasNameBox.Text = m_LoadedAtlasName;
        
        PreviewGroupBox.IsEnabled = true;
        LoadAndSaveAtlasButton.IsEnabled = true;
    }

    private async void SaveAtlas(object? sender, RoutedEventArgs e)
    {
        LoadAndSaveAtlasButton.IsEnabled = false;
        ResetButton.IsEnabled = false;
        
        SaveSprites();

        var returnCode = await Task.Run(() => m_SpriteSheetManager?.Save());

        LoadAndSaveAtlasButton.IsEnabled = true;
        ResetButton.IsEnabled = true;

        if (returnCode != CommonUtils.ReturnCode.Success)
        {
            Logger.Log($"Failed to save the atlas! Error: {returnCode.ToString()}");
            return;
        }
        
        Logger.Log("Successfully saved the atlas.");

        // todo: headgear stuff
    }

    private void SaveSprites()
    {
        // add new sprites to sprite list
        m_SpriteSheetManager?.Sprites.Clear();
        
        foreach (var sprite in PreviewGroupBox.SpriteDatabase.Sprites)
        {
            var startX = Canvas.GetLeft(sprite);
            var endX = Canvas.GetRight(sprite);
            var width = endX - startX;

            var startY = Canvas.GetTop(sprite);
            var endY = Canvas.GetBottom(sprite);
            var height = endY - startY;
                
            m_SpriteSheetManager?.Sprites.Add(new SpriteData
            {
                Name = sprite.SpriteName,
                
                StartX = startX,
                EndX = endX,
                
                StartY = startY,
                EndY = endY,
                
                Width = width,
                Height = height,
                
                OriginPoint = sprite.OriginPoint
            });
        }
    }
    
    private void LoadSprites()
    {
        PreviewGroupBox.Reset();

        // should never be null in this case but yeah
        if (m_SpriteSheetManager == null)
            return;
        
        var sprites = m_SpriteSheetManager.Sprites;

        var isSmoothMoves = m_SpriteSheetManager is SmoothMovesSpriteSheetManager;
        PreviewGroupBox.SpriteDatabase.IsSmoothMoves = isSmoothMoves;
        
        foreach (var sprite in sprites)
        {
            var width = sprite.Width;
            var height = sprite.Height;
            
            var rect = new Sprite(sprite.Name)
            {
                Width = width,
                Height = height,
                
                OriginPoint = sprite.OriginPoint
            };
            
            Canvas.SetLeft(rect, sprite.StartX);
            Canvas.SetTop(rect, sprite.StartY);
            Canvas.SetRight(rect, sprite.EndX);
            Canvas.SetBottom(rect, sprite.EndY);

            rect.RightClicked -= Sprite_RightClicked;
            rect.RightClicked += Sprite_RightClicked;
            
            rect.InitHandles(PreviewGroupBox.SelectionCanvas, PreviewGroupBox.AtlasImage, isSmoothMoves);
            rect.SetHandlesVisible(false);
            
            PreviewGroupBox.SelectionCanvas.Children.Add(rect);
            PreviewGroupBox.SpriteDatabase.Sprites.Add(rect);
        }

        OriginXInput.IsEnabled = isSmoothMoves;
        OriginYInput.IsEnabled = isSmoothMoves;
    }

    private void LoadImage()
    {
        PreviewGroupBox.AtlasImage.Source = new Bitmap(m_AtlasImagePath);

        // set the width and height properties of the canvas so the scrollbars show up
        var (width, height) = CommonUtils.GetImageResolution(m_AtlasImagePath);
        
        PreviewGroupBox.SelectionCanvas.Width = width;
        PreviewGroupBox.SelectionCanvas.Height = height;

        PreviewGroupBox.Acrylic.Width = width;
        PreviewGroupBox.Acrylic.Height = height;

        PreviewGroupBox.SelectionCanvas.Init();

        PreviewGroupBox.Acrylic.SetActive(true);
        
        EditTab.IsEnabled = true;
        
        SetMaxSizeControlValues();
    }
    
    private void TrySetLoadButtonEnabled(bool enable)
        => LoadAndSaveAtlasButton.IsEnabled = enable && !string.IsNullOrEmpty(AtlasNameInput.Text);
    
    private void OnAtlasNameInputUpdated(object? sender, TextChangedEventArgs e) 
        => TrySetLoadButtonEnabled(!string.IsNullOrEmpty(AtlasNameInput.Text));
    
    private void OnAtlasNameInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            LoadAtlas(sender, e);
    }
    
    #endregion
    
    #region Reset

    private async void Reset(object? sender, RoutedEventArgs e)
    {
        var msgBoxResult = await MessageBox.ShowMessageBox(
            this,
            "Are you sure you want to reset? You will lose any changes you have made.", 
            "Confirm", 
            ["Yes", "Cancel"]);

        if (msgBoxResult != "Yes")
            return;
        
        // remove event handlers
        LoadAndSaveAtlasButton.Click -= LoadAtlas;
        LoadAndSaveAtlasButton.Click -= SaveAtlas;
        
        PreviewGroupBox.SpriteDatabase.Sprites.ForEach(DeRegisterRectEventHandlers);
        
        // reset fields
        m_AtlasImagePath = default;
        m_LoadedAtlasName = default;
        SelectedSprite = default;
        m_SpriteSheetManager = default;
        
        // reset ui
        PreviewGroupBox.Reset();
        m_StateManager.Reset();

        ProgressService.Reset(true);

        LowResToggle.IsChecked = false;
        ResetButton.IsEnabled = false;
        
        LoadAndSaveAtlasButton.Content = "Load atlas";
        LoadAndSaveAtlasButton.Click += LoadAtlas;
        
        AtlasNameBox.Text = "No atlas is loaded";
        AtlasNameInput.IsEnabled = true;
        
        EditTab.IsEnabled = false;

        SpriteNameInput.Text = null;
        SettingsTab.IsEnabled = false;
        SpriteSettingsTabControl.SelectedIndex = IndexOfLogsTab;
        
        LogWindow.Clear();
        
        PreferenceService.SavePrefs();
        
        Logger.Log("Reset complete!");
    }
    
    #endregion
}