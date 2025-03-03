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
        m_PreviousSpriteData = m_Sprite.AsSpriteData();
    }

    public void SetCurrentSpriteData()
    {
        m_CurrentSpriteData = m_Sprite.AsSpriteData();
    }
    
    public void Execute()
    {
        if (m_CurrentSpriteData == null)
            return;
        
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
        
        Canvas.SetLeft(m_Sprite, m_PreviousSpriteData.StartX);
        m_Sprite.XChanged?.Invoke(m_Sprite, m_PreviousSpriteData.StartX);
        
        Canvas.SetTop(m_Sprite, m_PreviousSpriteData.StartY);
        m_Sprite.YChanged?.Invoke(m_Sprite, m_PreviousSpriteData.StartY);
        
        Canvas.SetRight(m_Sprite, m_PreviousSpriteData.EndX);
        Canvas.SetBottom(m_Sprite, m_PreviousSpriteData.EndY);
        
        m_Sprite.Width = m_PreviousSpriteData.Width;
        m_Sprite.WidthChanged?.Invoke(m_Sprite, m_PreviousSpriteData.Width);
        
        m_Sprite.Height = m_PreviousSpriteData.Height;
        m_Sprite.HeightChanged?.Invoke(m_Sprite, m_PreviousSpriteData.Height);
        
        m_Sprite.OriginPoint = m_PreviousSpriteData.OriginPoint;
        m_Sprite.OriginXChanged?.Invoke(m_Sprite, m_PreviousSpriteData.OriginPoint.X);
        m_Sprite.OriginYChanged?.Invoke(m_Sprite, m_PreviousSpriteData.OriginPoint.Y);
        
        m_Sprite.RepositionHandles();
    }
}