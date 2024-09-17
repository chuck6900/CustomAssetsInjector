using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CustomAssetsInjector.Controls;

public class ZoomCanvas : Canvas
{
    private ScaleTransform m_ScaleTransform = new(1, 1);
    
    private double m_ZoomFactor = 1.0;
    
    private const double ZoomIncrement = 0.5;
    
    public const double MaxZoom = 5.0;
    
    public const double MinZoom = 0.5;

    private double m_StartingWidth;
    private double m_StartingHeight;

    public Action<double>? BeforeZoomChanged;
    public Action<double>? ZoomChanged;

    public ZoomCanvas()
    {
        this.RenderTransformOrigin = new RelativePoint(0D, 0D, RelativeUnit.Relative);
    }
    
    public void Init()
    {
        m_StartingWidth = this.Width;
        m_StartingHeight = this.Height;
        
        m_ScaleTransform = new ScaleTransform(m_ZoomFactor, m_ZoomFactor);

        this.RenderTransform = m_ScaleTransform;
    }
    
    public void ZoomIn()
    {
        if (m_ZoomFactor < MaxZoom)
        {
            m_ZoomFactor += ZoomIncrement;
            ApplyZoom();
        }
    }

    public void ZoomOut()
    {
        if (m_ZoomFactor > MinZoom)
        {
            m_ZoomFactor -= ZoomIncrement;
            ApplyZoom();
        }
    }

    public void ResetZoom()
    {
        m_StartingWidth = this.Width / m_ZoomFactor;
        m_StartingHeight = this.Height / m_ZoomFactor;

        m_ZoomFactor = 1;
        
        ApplyZoom();
    }
    
    private void ApplyZoom()
    {
        BeforeZoomChanged?.Invoke(m_ZoomFactor);
        
        m_ScaleTransform.ScaleX = m_ZoomFactor;
        m_ScaleTransform.ScaleY = m_ZoomFactor;
        
        this.Width = m_StartingWidth * m_ZoomFactor;
        this.Height = m_StartingHeight * m_ZoomFactor;
        
        ZoomChanged?.Invoke(m_ZoomFactor);
    }
}