using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using CustomAssetsBackend.Classes;
using CustomAssetsBackend.Misc;
using CustomAssetsInjector.Actions;
using CustomAssetsInjector.Utils;

namespace CustomAssetsInjector.Controls;

public class TransformControlRectangle : Rectangle
{
    public EventHandler<double>? XChanged;
    public EventHandler<double>? YChanged;
    public EventHandler<double>? WidthChanged;
    public EventHandler<double>? HeightChanged;
    public EventHandler<double>? OriginXChanged;
    public EventHandler<double>? OriginYChanged;

    public EventHandler<EditSpriteAction>? OnEditActionCreated;
    
    private readonly List<Handle> m_Handles = new();
    private bool m_DidUpdateLastTime;
    private EditSpriteAction? m_Action;

    // origin point is stored with values 0-1, where 0 is far left / top and 1 is far right / bottom
    public Vector2 OriginPoint = new(0.5f, 0.5f);

    public SpriteData AsSpriteData()
    {
        return new SpriteData
        {
            Name = "TransformControlRectangle-NoName",

            StartX = Canvas.GetLeft(this),
            EndX = Canvas.GetRight(this),

            StartY = Canvas.GetTop(this),
            EndY = Canvas.GetBottom(this),

            Width = this.Width,
            Height = this.Height,

            OriginPoint = OriginPoint
        };
    }
    
    public void UpdateHandles(Point mousePos, Control limitControl)
    {
        var clickedHandle = GetClickedHandle();
        if (clickedHandle == null)
        {
            if (m_DidUpdateLastTime)
            {
                // last time we updated stuff, but this time the handle is null meaning we've finished editing
                m_Action!.SetCurrentSpriteData();
                OnEditActionCreated?.Invoke(this, m_Action);
                m_Action = null;
                
                // invoke events
                XChanged?.Invoke(this, Canvas.GetLeft(this));
                YChanged?.Invoke(this, Canvas.GetTop(this));
                WidthChanged?.Invoke(this, this.Width);
                HeightChanged?.Invoke(this, this.Height);
                OriginXChanged?.Invoke(this, OriginPoint.X);
                OriginYChanged?.Invoke(this, OriginPoint.Y);
            }
            m_DidUpdateLastTime = false;
            return;
        }
        else if (!m_DidUpdateLastTime)
        {
            // last time we didnt update stuff, but now a handle has been clicked meaning we've just started editing
            m_Action = new EditSpriteAction(this);
            m_Action.SetPreviousSpriteData();
        }

        m_DidUpdateLastTime = true;
        
        var left = Canvas.GetLeft(this);
        var top = Canvas.GetTop(this);
        var right = Canvas.GetRight(this);
        var bottom = Canvas.GetBottom(this);
        
        switch (clickedHandle.Type)
        {
            case HandleType.TopLeft:
                // left
                this.Width = clickedHandle.InitialWidth - (mousePos.X - clickedHandle.InitialMouseX);
                left = right - this.Width;
                
                // top
                this.Height = clickedHandle.InitialHeight - (mousePos.Y - clickedHandle.InitialMouseY);
                top = bottom - this.Height;
                break;
            case HandleType.Top:
                // top
                this.Height = clickedHandle.InitialHeight - (mousePos.Y - clickedHandle.InitialMouseY);
                top = bottom - this.Height;
                break;
            case HandleType.TopRight:
                // right
                this.Width = clickedHandle.InitialWidth + (mousePos.X - clickedHandle.InitialMouseX);
                right = left + this.Width;
                
                // top
                this.Height = clickedHandle.InitialHeight - (mousePos.Y - clickedHandle.InitialMouseY);
                top = bottom - this.Height;
                break;
            case HandleType.Right:
                // right
                this.Width = clickedHandle.InitialWidth + (mousePos.X - clickedHandle.InitialMouseX);
                right = left + this.Width;
                break;
            case HandleType.BottomRight:
                // right
                this.Width = clickedHandle.InitialWidth + (mousePos.X - clickedHandle.InitialMouseX);
                right = left + this.Width;
                
                // bottom
                this.Height = clickedHandle.InitialHeight + (mousePos.Y - clickedHandle.InitialMouseY);
                bottom = top + this.Height;
                break;
            case HandleType.Bottom:
                // bottom
                this.Height = clickedHandle.InitialHeight + (mousePos.Y - clickedHandle.InitialMouseY);
                bottom = top + this.Height;
                break;
            case HandleType.BottomLeft:
                // left
                this.Width = clickedHandle.InitialWidth - (mousePos.X - clickedHandle.InitialMouseX);
                left = right - this.Width;
                
                // bottom
                this.Height = clickedHandle.InitialHeight + (mousePos.Y - clickedHandle.InitialMouseY);
                bottom = top + this.Height;
                break;
            case HandleType.Left:
                // left
                this.Width = clickedHandle.InitialWidth - (mousePos.X - clickedHandle.InitialMouseX);
                left = right - this.Width;
                break;
            case HandleType.Origin:
                var newX = (float)CommonUtils.MapValues(mousePos.X, left, right, 0, 1);
                var newY = (float)CommonUtils.MapValues(mousePos.Y, top, bottom, 0, 1);
                this.OriginPoint = new Vector2(newX, newY);
                break;
        }

        Canvas.SetLeft(this, left);
        Canvas.SetTop(this, top);
        Canvas.SetRight(this, right);
        Canvas.SetBottom(this, bottom);
        
        this.MakePointsInsideControl(limitControl);
        this.RepositionHandles();
    }

    public void DestroyHandles()
    {
        if (Parent is not Panel parentPanel)
            return;
        
        foreach (var handle in m_Handles)
        {
            parentPanel.Children.Remove(handle);
        }
    }
    
    public void SetHandlesVisible(bool visible)
    {
        foreach (var handle in m_Handles)
        {
            if (visible)
                handle.Reposition();
            
            handle.SetActive(visible);
        }
    }

    public void RepositionHandles()
    {
        m_Handles.ForEach(handle => handle.Reposition());
    }

    public void InitHandles(Canvas canvas, Control relativeObj, bool useOriginPoint = false)
    {
        foreach (var handleType in Enum.GetValues<HandleType>())
        {
            if (handleType == HandleType.Origin && !useOriginPoint)
                continue;
            
            var newHandle = new Handle(this, 6, handleType, relativeObj);
            
            m_Handles.Add(newHandle);
            canvas.Children.Add(newHandle);
        }
    }
    
    private void MakePointsInsideControl(Control control)
    {
        var left = Canvas.GetLeft(this);
        var right = Canvas.GetRight(this);
        var top = Canvas.GetTop(this);
        var bottom = Canvas.GetBottom(this);
        
        Sprite.MakePointsInsideControl(control, ref left, ref right, ref top, ref bottom);
        
        var spriteWidth = right - left;
        var spriteHeight = bottom - top;

        var handle = GetClickedHandle();

        // ensure sprite is at least 10x10
        if (handle != null)
        {
            if (spriteHeight < 10) 
            {
                spriteHeight = 10;
                switch (handle.Type)
                {
                    case HandleType.TopLeft:
                    case HandleType.Top:
                    case HandleType.TopRight:
                        top = bottom - 10;
                        break;
                    case HandleType.BottomLeft:
                    case HandleType.Bottom:
                    case HandleType.BottomRight:
                        bottom = top + 10;
                        break;
                }
            }

            if (spriteWidth < 10)
            {
                spriteWidth = 10;

                switch (handle.Type)
                {
                    case HandleType.TopLeft:
                    case HandleType.Left:
                    case HandleType.BottomLeft:
                        left = right - 10;
                        break;
                    case HandleType.TopRight:
                    case HandleType.Right:
                    case HandleType.BottomRight:
                        right = left + 10;
                        break;
                }
            }
        }

        Canvas.SetLeft(this, left);
        Canvas.SetRight(this, right);
        Canvas.SetTop(this, top);
        Canvas.SetBottom(this, bottom);
        
        this.Width = spriteWidth;
        this.Height = spriteHeight;
    }
    
    public Handle? GetClickedHandle() => m_Handles.FirstOrDefault(handle => handle.IsClicking);
}