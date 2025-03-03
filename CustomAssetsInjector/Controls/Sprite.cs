using System;
using System.Numerics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using CustomAssetsBackend.Classes;

namespace CustomAssetsInjector.Controls;

public class Sprite : TransformControlRectangle
{
    private string m_SpriteName;
    public string SpriteName
    {
        get => m_SpriteName;
        set
        {
            m_SpriteName = value;
            m_ToolTip.Content = value;
            SpriteData.Name = value;
        }
    }

    public new double Width
    {
        get => base.Width;
        set
        {
            SpriteData.Width = value;
            base.Width = value;
        }
    }
    
    public new Vector2 OriginPoint
    {
        get => base.OriginPoint;
        set
        {
            SpriteData.OriginPoint = value;
            base.OriginPoint = value;
        }
    }

    private SpriteData m_SpriteData;

    public SpriteData SpriteData
    {
        get
        {
            m_SpriteData.StartX = Canvas.GetLeft(this);
            m_SpriteData.EndX = Canvas.GetRight(this);
            
            m_SpriteData.StartY = Canvas.GetTop(this);
            m_SpriteData.EndY = Canvas.GetBottom(this);

            return m_SpriteData;
        }
        set => m_SpriteData = value;
    }

    public EventHandler? RightClicked;

    private bool m_IsClicking;

    private bool m_IsPointerOver;
    
    private ToolTip m_ToolTip = new();

    public Sprite(SpriteData data)
    {
        Canvas.SetLeft(this, data.StartX);
        Canvas.SetTop(this, data.StartY);
        Canvas.SetRight(this, data.EndX);
        Canvas.SetBottom(this, data.EndY);
        
        SpriteData = data;
        
        SpriteName = SpriteData.Name;
        Width = SpriteData.Width;
        Height = SpriteData.Height;
        
        Fill = new SolidColorBrush(Colors.White, 0.12);
        Stroke = new SolidColorBrush(Colors.White, 0.5);
        StrokeThickness = 1; // 1px thick
        
        ToolTip.SetTip(this, m_ToolTip);
        
        PointerPressed += delegate { m_IsClicking = true; };
        PointerReleased += Sprite_PointerReleased;
        PointerEntered += Sprite_PointerEntered;
        PointerExited += Sprite_PointerExited;
    }

    public override SpriteData AsSpriteData() => SpriteData;

    private async void Sprite_PointerEntered(object? sender, PointerEventArgs e)
    {
        if (m_IsClicking)
            return;

        m_IsPointerOver = true;
        Fill = new SolidColorBrush(Colors.White, 0.31);
        
        // spawn tooltip
        await Task.Run((Func<Task>)(async () =>
        {
            await Task.Delay(1000); // 1 second wait
            if (m_IsPointerOver && !m_IsClicking)
            {
               Dispatcher.UIThread.Post(() => m_ToolTip.IsEnabled = true);
            }
        }));
    }
    
    private void Sprite_PointerExited(object? sender, PointerEventArgs e)
    {
        if (m_IsClicking)
            return;

        m_IsPointerOver = false;
        m_ToolTip.IsEnabled = false;
        Fill = new SolidColorBrush(Colors.White, 0.12);
    }

    private void Sprite_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        m_IsClicking = false;
        m_ToolTip.IsEnabled = false;
        if (e.InitialPressMouseButton == MouseButton.Right)
        {
            RightClicked?.Invoke(this, EventArgs.Empty);
        }
    }
    
    public static void MakePointsInsideControl(Control control, ref double left, ref double right, ref double top, ref double bottom, double width = 0, double height = 0)
    {
        // cant use Math.Clamp() because we need to deal with some specific conditions...
        var boundsLeft = control.Bounds.Left;
        var boundsRight = control.Bounds.Right;
        var boundsTop = control.Bounds.Top;
        var boundsBottom = control.Bounds.Bottom;
        
        // left is on the right of the control
        if (left > boundsRight)
            left = boundsRight;

        // left is on the left of the control (overflow)
        if (left < boundsLeft)
        {
            left = boundsLeft;
            right = width != 0 ? left + width : right;
        }
        
        // right is on the right of the control (overflow)
        if (right > boundsRight)
        {
            right = boundsRight;
            left = width != 0 ? right - width : left;
        }

        // right is on the left of the control
        if (right < boundsLeft)
            right = boundsLeft;

        // top is on top of the control (overflow)
        if (top < boundsTop)
        {
            top = boundsTop;
            bottom = height != 0 ? top + height : bottom;
        }

        // top is under the control
        if (top > boundsBottom)
            top = boundsBottom;

        // bottom is under the bottom of the control (overflow)
        if (bottom > boundsBottom)
        {
            bottom = boundsBottom;
            top = height != 0 ? bottom - height : top;
        }

        // bottom is on top of the control
        if (bottom < boundsTop)
            bottom = boundsTop;
    }
}