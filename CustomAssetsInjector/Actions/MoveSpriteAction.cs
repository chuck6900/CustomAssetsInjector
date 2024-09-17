using Avalonia;
using Avalonia.Controls;
using CustomAssetsInjector.Controls;

namespace CustomAssetsInjector.Actions;

public class MoveSpriteAction : IAction
{
    private Sprite m_Sprite;
    private Point m_StartPoint;
    private Point m_EndPoint;
    private SpriteSheetPreviewBox m_SpriteSheetPreviewBox;

    public MoveSpriteAction(Sprite movedSprite, Point startingPoint, SpriteSheetPreviewBox spritePreviewBox)
    {
        m_Sprite = movedSprite;
        m_StartPoint = startingPoint;
        m_EndPoint = new Point(Canvas.GetLeft(m_Sprite), Canvas.GetTop(m_Sprite));
        m_SpriteSheetPreviewBox = spritePreviewBox;
    }
    
    public void Execute()
    {
        Canvas.SetLeft(m_Sprite, m_EndPoint.X);
        Canvas.SetTop(m_Sprite, m_EndPoint.Y);
        Canvas.SetRight(m_Sprite, m_EndPoint.X + m_Sprite.Width);
        Canvas.SetBottom(m_Sprite, m_EndPoint.Y + m_Sprite.Height);
           
        // invoke the x and y changed events
        m_Sprite.XChanged?.Invoke(m_Sprite, m_EndPoint.X);
        m_Sprite.YChanged?.Invoke(m_Sprite, m_EndPoint.Y);

        m_SpriteSheetPreviewBox.SelectedSprite = m_Sprite;
    }

    public void Revert()
    {
        Canvas.SetLeft(m_Sprite, m_StartPoint.X);
        Canvas.SetTop(m_Sprite, m_StartPoint.Y);
        Canvas.SetRight(m_Sprite, m_StartPoint.X + m_Sprite.Width);
        Canvas.SetBottom(m_Sprite, m_StartPoint.Y + m_Sprite.Height);
        
        // invoke the x and y changed events
        m_Sprite.XChanged?.Invoke(m_Sprite, m_StartPoint.X);
        m_Sprite.YChanged?.Invoke(m_Sprite, m_StartPoint.Y);

        m_SpriteSheetPreviewBox.SelectedSprite = m_Sprite;
    }
}