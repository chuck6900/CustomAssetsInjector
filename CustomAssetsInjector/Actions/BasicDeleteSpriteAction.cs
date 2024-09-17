using Avalonia.Threading;
using CustomAssetsInjector.Controls;

namespace CustomAssetsInjector.Actions;

public class BasicDeleteSpriteAction : IAction
{
    private Sprite m_Sprite;
    private SpriteSheetPreviewBox m_SpritePreviewBox;

    public BasicDeleteSpriteAction(Sprite deletedSprite, SpriteSheetPreviewBox spritePreviewBox)
    {
        m_Sprite = deletedSprite;
        m_SpritePreviewBox = spritePreviewBox;
    }
    
    public void Execute()
    {
        m_Sprite.SetHandlesVisible(false);
        m_SpritePreviewBox.SelectionCanvas.Children.Remove(m_Sprite);
        m_SpritePreviewBox.SpriteDatabase.Sprites.Remove(m_Sprite);
        
        if (m_SpritePreviewBox.SelectedSprite == m_Sprite)
            m_SpritePreviewBox.SelectedSprite = null;
    }

    public void Revert()
    {
        m_Sprite.SetHandlesVisible(true);
        
        if (!m_SpritePreviewBox.SelectionCanvas.Children.Contains(m_Sprite))
            m_SpritePreviewBox.SelectionCanvas.Children.Add(m_Sprite);
        
        if (!m_SpritePreviewBox.SpriteDatabase.Sprites.Contains(m_Sprite))
            m_SpritePreviewBox.SpriteDatabase.Sprites.Add(m_Sprite);
        
        m_SpritePreviewBox.SelectedSprite = m_Sprite;
    }
    
    // delete handles whenever the garbage collector cleans stuff up
    ~BasicDeleteSpriteAction()
    {
        // destructor doesn't get called from the ui thread
        Dispatcher.UIThread.Invoke(() =>
        {
            // destroy handles BEFORE removing from the visual tree in Execute()
            m_Sprite.DestroyHandles();
            Execute();
        });
    }
}