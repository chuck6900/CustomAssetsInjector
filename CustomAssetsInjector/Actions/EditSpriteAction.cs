using Avalonia.Controls;
using CustomAssetsBackend.Classes;
using CustomAssetsInjector.Controls;

namespace CustomAssetsInjector.Actions;

public class EditSpriteAction : IAction
{
    private TransformControlRectangle m_Sprite;
    private SpriteData? m_PreviousSpriteData;
    private SpriteData? m_CurrentSpriteData;
    
    public EditSpriteAction(TransformControlRectangle sprite)
    {
        m_Sprite = sprite;
    }
    
    public void SetPreviousSpriteData()
    {
        if (m_Sprite is Sprite sprite)
            m_PreviousSpriteData = sprite.AsSpriteData();
        else
            m_PreviousSpriteData = m_Sprite.AsSpriteData();
    }

    public void SetCurrentSpriteData()
    {
        if (m_Sprite is Sprite sprite)
            m_CurrentSpriteData = sprite.AsSpriteData();
        else
            m_CurrentSpriteData = m_Sprite.AsSpriteData();
    }
    
    public void Execute()
    {
        if (m_CurrentSpriteData == null)
            return;
        
        if (m_Sprite is Sprite sprite)
            sprite.SpriteName = m_CurrentSpriteData.Name;
        
        Canvas.SetLeft(m_Sprite, m_CurrentSpriteData.StartX);
        m_Sprite.XChanged?.Invoke(m_Sprite, m_CurrentSpriteData.StartX);
        
        Canvas.SetTop(m_Sprite, m_CurrentSpriteData.StartY);
        m_Sprite.YChanged?.Invoke(m_Sprite, m_CurrentSpriteData.StartY);
        
        Canvas.SetRight(m_Sprite, m_CurrentSpriteData.EndX);
        Canvas.SetBottom(m_Sprite, m_CurrentSpriteData.EndY);
        
        m_Sprite.Width = m_CurrentSpriteData.Width;
        m_Sprite.WidthChanged?.Invoke(m_Sprite, m_CurrentSpriteData.Width);
        
        m_Sprite.Height = m_CurrentSpriteData.Height;
        m_Sprite.HeightChanged?.Invoke(m_Sprite, m_CurrentSpriteData.Height);
        
        m_Sprite.OriginPoint = m_CurrentSpriteData.OriginPoint;
        m_Sprite.OriginXChanged?.Invoke(m_Sprite, m_CurrentSpriteData.OriginPoint.X);
        m_Sprite.OriginYChanged?.Invoke(m_Sprite, m_CurrentSpriteData.OriginPoint.Y);
        
        m_Sprite.RepositionHandles();
    }

    public void Revert()
    {
        if (m_PreviousSpriteData == null)
            return;
        
        if (m_Sprite is Sprite sprite)
            sprite.SpriteName = m_PreviousSpriteData.Name;
        
        Canvas.SetLeft(m_Sprite, m_CurrentSpriteData.StartX);
        m_Sprite.XChanged?.Invoke(m_Sprite, m_CurrentSpriteData.StartX);
        
        Canvas.SetTop(m_Sprite, m_CurrentSpriteData.StartY);
        m_Sprite.YChanged?.Invoke(m_Sprite, m_CurrentSpriteData.StartY);
        
        Canvas.SetRight(m_Sprite, m_CurrentSpriteData.EndX);
        Canvas.SetBottom(m_Sprite, m_CurrentSpriteData.EndY);
        
        m_Sprite.Width = m_CurrentSpriteData.Width;
        m_Sprite.WidthChanged?.Invoke(m_Sprite, m_CurrentSpriteData.Width);
        
        m_Sprite.Height = m_CurrentSpriteData.Height;
        m_Sprite.HeightChanged?.Invoke(m_Sprite, m_CurrentSpriteData.Height);
        
        m_Sprite.OriginPoint = m_CurrentSpriteData.OriginPoint;
        m_Sprite.OriginXChanged?.Invoke(m_Sprite, m_CurrentSpriteData.OriginPoint.X);
        m_Sprite.OriginYChanged?.Invoke(m_Sprite, m_CurrentSpriteData.OriginPoint.Y);
        
        m_Sprite.RepositionHandles();
    }
}