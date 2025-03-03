using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CustomAssetsBackend.Classes;
using CustomAssetsInjector.Utils;

namespace CustomAssetsInjector.Controls;

public partial class SpritePreviewer : UserControl
{
    private List<SpriteData> m_Sprites = new();

    private Bitmap? m_SpriteSheet;
    
    public SpritePreviewer()
    {
        InitializeComponent();
        
        SpriteSelectionComboBox.SelectionChanged += SpriteSelectionChanged;
    }

    public void DisableSpriteSelection()
    {
        SpriteSelectionText.SetActive(false);
        SpriteSelectionComboBox.SetActive(false);
        PreviewText.SetActive(false);
    }
    
    public void SetImageSource(IImage? source) => PreviewImage.Source = source;

    public IImage? GetImageSource() => PreviewImage.Source;

    private void SpriteSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0 || e.AddedItems[0] is not SpriteData newItem || m_SpriteSheet == null)
            return;

        var cropRect = new PixelRect((int)newItem.StartX, (int)newItem.StartY, (int)newItem.Width, (int)newItem.Height);
        SetImageSource(new CroppedBitmap(m_SpriteSheet, cropRect));
    }

    private void CreateComboBoxEntries()
    {
        SpriteSelectionComboBox.Items.Clear();
        SpriteSelectionComboBox.ItemsSource = m_Sprites;
    }

    public void SetSpriteList(List<SpriteData> newSprites)
    {
        m_Sprites.Clear();
        m_Sprites.AddRange(newSprites);
        
        CreateComboBoxEntries();
    }

    public void SetSpriteSheet(string spriteSheetPath)
    {
        m_SpriteSheet = new Bitmap(spriteSheetPath);
    }
}