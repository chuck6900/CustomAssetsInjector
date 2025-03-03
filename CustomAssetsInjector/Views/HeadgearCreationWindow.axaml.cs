using System;
using System.Collections.Generic;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CustomAssetsBackend.Classes;
using CustomAssetsBackend.SpriteSheet.SmoothMoves;
using CustomAssetsInjector.Utils;

namespace CustomAssetsInjector.Views;

public partial class HeadgearCreationWindow : Window
{
    public const string PrefabPreviewButtonPath = "avares://CustomAssetsInjector/Assets/prefabPreviewButton.png";
    
    public HeadgearCreationWindow()
    {
        InitializeComponent();

        HeadgearNameInput.TextChanged += UpdateFinalPreviewImage;
        
        FrontSpritePreview.SpriteSelectionComboBox.SelectionChanged += UpdateFinalPreviewImage;
        BackSpritePreview.SpriteSelectionComboBox.SelectionChanged += UpdateFinalPreviewImage;

        XPositionFrontSprite.ValueChanged += UpdateFinalPreviewImage;
        YPositionFrontSprite.ValueChanged += UpdateFinalPreviewImage;
        XPositionBackSprite.ValueChanged += UpdateFinalPreviewImage;
        YPositionBackSprite.ValueChanged += UpdateFinalPreviewImage;
        
        XScaleFrontSprite.ValueChanged += UpdateFinalPreviewImage;
        YScaleFrontSprite.ValueChanged += UpdateFinalPreviewImage;
        XScaleBackSprite.ValueChanged += UpdateFinalPreviewImage;
        YScaleBackSprite.ValueChanged += UpdateFinalPreviewImage;
        
        CreateHeadgearButton.Click += CreateHeadgear;
    }

    private void CreateHeadgear(object? sender, RoutedEventArgs e)
    {
        if (this.Owner is not SpriteSheetEditorWindow editor ||
            string.IsNullOrEmpty(HeadgearNameInput.Text) ||
            FrontSpritePreview.GetImageSource() == null ||
            BackSpritePreview.GetImageSource() == null)
        {
            return;
        }

        var headgear = new SmoothMovesSpriteSheetManager.Headgear
        {
            Name = HeadgearNameInput.Text
        };

        var (frontSpritePos, backSpritePos) = GetSpritePositions();
        var (frontSpriteScale, backSpriteScale) = GetSpriteScales();
        
        var frontSprite = new SmoothMovesSpriteSheetManager.HeadgearSprite
        {
            Data = (FrontSpritePreview.SpriteSelectionComboBox.SelectionBoxItem as SpriteData)!,
            Position = frontSpritePos.FixNegativeZero(),
            Scale = frontSpriteScale.FixNegativeZero()
        };

        var backSprite = new SmoothMovesSpriteSheetManager.HeadgearSprite
        {
            Data = (BackSpritePreview.SpriteSelectionComboBox.SelectionBoxItem as SpriteData)!,
            Position = backSpritePos.FixNegativeZero(),
            Scale = backSpriteScale.FixNegativeZero()
        };

        headgear.FrontSprite = frontSprite;
        headgear.BackSprite = backSprite;

        editor.CreateHeadgear(headgear);
    }

    private void UpdateFinalPreviewImage(object? sender, RoutedEventArgs e)
    {
        FinalSpritePreview.SetImageSource(GenerateFinalPreviewImage());
        FinalSpritePreviewWithButton.SetImageSource(GenerateFinalPreviewImageWithButton());
    }

    private static IImage CombineImages(IImage frontImage, Vector2 frontImagePos, Vector2 frontImageScale, IImage backImage, Vector2 backImagePos, Vector2 backImageScale)
    {
        var (frontPosX, frontPosY) = (frontImagePos.X, frontImagePos.Y);
        var (backPosX, backPosY) = (backImagePos.X, backImagePos.Y);

        var frontWidth = frontImage.Size.Width * frontImageScale.X;
        var frontHeight = frontImage.Size.Height * frontImageScale.Y;

        var backWidth = backImage.Size.Width * backImageScale.X;
        var backHeight = backImage.Size.Height * backImageScale.Y;
        
        var newImageWidth = (int)Math.Max(Math.Abs(frontPosX) + frontWidth / 2, Math.Abs(backPosX) + backWidth / 2) * 2;
        var newImageHeight = (int)Math.Max(Math.Abs(frontPosY) + frontHeight / 2, Math.Abs(backPosY) + backHeight / 2) * 2;

        var newImage = new RenderTargetBitmap(new PixelSize(newImageWidth, newImageHeight));
        
        using var ctx = newImage.CreateDrawingContext();
        
        var centerX = newImageWidth / 2;
        var centerY = newImageHeight / 2;
        
        // draw the back sprite FIRST so it's below the front sprite
        var backImageX = centerX + backPosX - backWidth / 2;
        var backImageY = centerY + backPosY - backHeight / 2;
        
        var backImageRect = new Rect(backImageX, backImageY, backWidth, backHeight);
        ctx.DrawImage(backImage, backImageRect);
        
        var frontImageX = centerX + frontPosX - frontWidth / 2;
        var frontImageY = centerY + frontPosY - frontHeight / 2;
        
        var frontImageRect = new Rect(frontImageX, frontImageY, frontWidth, frontHeight);
        ctx.DrawImage(frontImage, frontImageRect);
        
        return newImage;
    }

    public IImage? GenerateFinalPreviewImage()
    {
        var (frontVector2, backVector2) = GetSpritePositions();
        var frontPreviewImage = FrontSpritePreview.GetImageSource();
        var backPreviewImage = BackSpritePreview.GetImageSource();

        if (frontPreviewImage == null || backPreviewImage == null)
        {
            (FinalSpritePreview.Parent as GroupBox.Avalonia.Controls.GroupBox)!.SetActive(false);
            CreateHeadgearButton.SetActive(false);
            return null;
        }
        (FinalSpritePreview.Parent as GroupBox.Avalonia.Controls.GroupBox)!.SetActive(true);
        CreateHeadgearButton.SetActive(!string.IsNullOrEmpty(HeadgearNameInput.Text));

        var (frontSpriteScale, backSpriteScale) = GetSpriteScales();

        frontVector2 = new Vector2(frontVector2.X / frontSpriteScale.X, frontVector2.Y / frontSpriteScale.Y);
        backVector2 = new Vector2(backVector2.X / backSpriteScale.X, backVector2.Y / backSpriteScale.Y);

        return CombineImages(frontPreviewImage, frontVector2, new Vector2(1, 1), backPreviewImage, backVector2, new Vector2(1, 1));
    }
    
    public IImage? GenerateFinalPreviewImageWithButton()
    {
        var (frontVector2, backVector2) = GetSpritePositions();
        var frontPreviewImage = FrontSpritePreview.GetImageSource();
        var backPreviewImage = BackSpritePreview.GetImageSource();
        var buttonImage = new Bitmap(AssetLoader.Open(new Uri(PrefabPreviewButtonPath)));

        if (frontPreviewImage == null || backPreviewImage == null)
        {
            (FinalSpritePreviewWithButton.Parent as GroupBox.Avalonia.Controls.GroupBox)!.SetActive(false);
            CreateHeadgearButton.SetActive(false);
            return null;
        }
        (FinalSpritePreviewWithButton.Parent as GroupBox.Avalonia.Controls.GroupBox)!.SetActive(true);
        CreateHeadgearButton.SetActive(!string.IsNullOrEmpty(HeadgearNameInput.Text));

        var (frontSpriteScale, backSpriteScale) = GetSpriteScales();

        var headgearPreviewImage = CombineImages(frontPreviewImage, frontVector2, frontSpriteScale,backPreviewImage, backVector2, backSpriteScale);
        var previewImageWithButton = CombineImages(headgearPreviewImage, new Vector2(0, 0), new Vector2(1, 1), buttonImage, new Vector2(0, 0), new Vector2(1, 1));
        return previewImageWithButton;
    }
    
    private (Vector2, Vector2) GetSpriteScales()
    {
        var xScaleFrontSprite = (float)(XScaleFrontSprite.Value ?? 1);
        var yScaleFrontSprite = (float)(YScaleFrontSprite.Value ?? 1);
        var xScaleBackSprite = (float)(XScaleBackSprite.Value ?? 1);
        var yScaleBackSprite = (float)(YScaleBackSprite.Value ?? 1);

        return (new Vector2(xScaleFrontSprite, yScaleFrontSprite), new Vector2(xScaleBackSprite, yScaleBackSprite));
    }
    
    private (Vector2, Vector2) GetSpritePositions()
    {
        // invert the value so positive = up and negative = down
        var xPosFrontSprite = -(float)(XPositionFrontSprite.Value ?? 0);
        var yPosFrontSprite = -(float)(YPositionFrontSprite.Value ?? 0);
        var xPosBackSprite = -(float)(XPositionBackSprite.Value ?? 0);
        var yPosBackSprite = -(float)(YPositionBackSprite.Value ?? 0);

        return (new Vector2(xPosFrontSprite, yPosFrontSprite), new Vector2(xPosBackSprite, yPosBackSprite));
    }

    public void SetupSpritePreviews(List<SpriteData> sprites, string spriteSheetPath)
    {
        FrontSpritePreview.SetSpriteList(sprites);
        BackSpritePreview.SetSpriteList(sprites);
        
        FrontSpritePreview.SetSpriteSheet(spriteSheetPath);
        BackSpritePreview.SetSpriteSheet(spriteSheetPath);
        
        FinalSpritePreview.DisableSpriteSelection();
        FinalSpritePreview.SetImageSource(null);
        
        FinalSpritePreviewWithButton.DisableSpriteSelection();
        FinalSpritePreviewWithButton.SetImageSource(null);

        GenerateFinalPreviewImage();
        GenerateFinalPreviewImageWithButton();
    }
}