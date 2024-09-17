using CustomAssetsInjector.Controls;

namespace CustomAssetsInjector.Actions;

public class CreateSpriteAction : IAction
{
    private Sprite m_Sprite;
    private SpriteSheetPreviewBox m_SpritePreviewBox;

    public CreateSpriteAction(Sprite createdSprite, SpriteSheetPreviewBox spritePreviewBox)
    {
        m_Sprite = createdSprite;
        m_SpritePreviewBox = spritePreviewBox;
    }
    
    public void Execute()
    {
        if (!m_SpritePreviewBox.SelectionCanvas.Children.Contains(m_Sprite))
            m_SpritePreviewBox.SelectionCanvas.Children.Add(m_Sprite);
        
        if (!m_SpritePreviewBox.SpriteDatabase.Sprites.Contains(m_Sprite))
            m_SpritePreviewBox.SpriteDatabase.Sprites.Add(m_Sprite);

        m_SpritePreviewBox.SelectedSprite = m_Sprite;
    }

    public void Revert()
    {
        m_Sprite.SetHandlesVisible(false);
        m_SpritePreviewBox.SelectionCanvas.Children.Remove(m_Sprite);
        m_SpritePreviewBox.SpriteDatabase.Sprites.Remove(m_Sprite);
        m_SpritePreviewBox.SelectedSprite = null;
    }
}