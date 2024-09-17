using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using CustomAssetsBackend.Misc;
using CustomAssetsInjector.Actions;
using CustomAssetsInjector.Services;
using CustomAssetsInjector.Utils;

namespace CustomAssetsInjector.Controls;

public class SpriteSheetPreviewBox : GroupBox.Avalonia.Controls.GroupBox
{
    private struct ScrollViewerInfo
    {
        public Vector ScrollBarMax;
        public Vector ScrollBarPos;
    }
    
    public StateManager StateManager;
    
    /// <summary>
    /// The left (X) and top (Y) coordinates of the sprite when the sprite began moving.
    /// </summary>
    private Point m_MovedSpriteStartPoint;

    private ScrollViewerInfo? m_ScrollerInfoBeforeZoom;
    
    private bool m_IsClicking;

    private Point m_StartPoint;

    private double m_StartingLeft;

    private double m_StartingTop;

    private bool m_ShouldMove;

    private Sprite? m_CurrentSprite;

    private Sprite? m_SelectedSprite;
    public Sprite? SelectedSprite
    {
        get => m_SelectedSprite;
        set
        {
            // clean up the currently selected sprite
            if (m_SelectedSprite != null)
            {
                m_SelectedSprite.SetHandlesVisible(false);
                m_SelectedSprite.Stroke = new SolidColorBrush(Colors.White, 0.5);
            }
            
            // set the new selected sprite
            m_SelectedSprite = value;
            
            if (m_SelectedSprite == null)
                return;
            
            // initialize the new sprite
            m_SelectedSprite.SetHandlesVisible(true);
            m_SelectedSprite.Stroke = new SolidColorBrush(Colors.White, 1);
        } 
    }

    public Action<Sprite>? SpriteCreated;

    private readonly ScrollViewer m_CanvasScroller;
    
    public readonly ZoomCanvas SelectionCanvas;

    public readonly Image AtlasImage;
    
    public ExperimentalAcrylicBorder Acrylic;

    public SpriteDatabase SpriteDatabase = new();
    
    public SpriteSheetPreviewBox()
    {
        // initialize ui
        m_CanvasScroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        SelectionCanvas = new ZoomCanvas();
        
        m_CanvasScroller.Content = SelectionCanvas;
        
        AtlasImage = new Image
        {
            Stretch = Stretch.None
        };

        Application.Current!.TryFindResource("BlackAcrylicMaterial", out var acrylicMaterial);
        
        Acrylic = new ExperimentalAcrylicBorder
        {
            IsHitTestVisible = false,
            Material = acrylicMaterial as ExperimentalAcrylicMaterial
        };
        
        Acrylic.SetActive(false);
        
        SelectionCanvas.Children.Add(Acrylic);
        SelectionCanvas.Children.Add(AtlasImage);

        this.Content = m_CanvasScroller;
        
        this.PointerPressed += SpriteSheetPreviewBox_PointerPressed;
        this.PointerReleased += SpriteSheetPreviewBox_PointerReleased;
        this.PointerMoved += SpriteSheetPreviewBox_PointerMoved;
        
        SelectionCanvas.BeforeZoomChanged += BeforeSelectionCanvasZoomChanged;
        SelectionCanvas.ZoomChanged += OnSelectionCanvasZoomChanged;
    }

    private void BeforeSelectionCanvasZoomChanged(double newValue)
    {
        var width = SelectionCanvas.Width;
        var height = SelectionCanvas.Height;
        
        if (double.IsNaN(width) || double.IsNaN(height))
            return;

        m_ScrollerInfoBeforeZoom = new ScrollViewerInfo
        {
            ScrollBarMax = m_CanvasScroller.ScrollBarMaximum,
            ScrollBarPos = m_CanvasScroller.Offset
        };
    }

    private void OnSelectionCanvasZoomChanged(double newValue)
    {
        // set scrollbar pos so we keep the current scroll position
        if (m_ScrollerInfoBeforeZoom == null)
            return;

        var info = m_ScrollerInfoBeforeZoom.Value;

        // trigger a re-render to update the scroller's Extent value, which then updates ScrollBarMaximum
        this.UpdateLayout();
        
        var newXScrollPos = CommonUtils.MapValues(info.ScrollBarPos.X, // val
            0, info.ScrollBarMax.X, // in
            0, m_CanvasScroller.ScrollBarMaximum.X); // out
        
        var newYScrollPos = CommonUtils.MapValues(info.ScrollBarPos.Y, // val
            0, info.ScrollBarMax.Y, // in
            0, m_CanvasScroller.ScrollBarMaximum.Y); // out

        // zoom into the middle by default
        if (double.IsNaN(newXScrollPos))
            newXScrollPos = m_CanvasScroller.ScrollBarMaximum.X / 2;
        if (double.IsNaN(newYScrollPos))
            newYScrollPos = m_CanvasScroller.ScrollBarMaximum.Y / 2;
        
        m_CanvasScroller.Offset = new Vector(newXScrollPos, newYScrollPos);
    }

    public void Reset()
    {
        SpriteDatabase = new SpriteDatabase();
        
        AtlasImage.Source = default;
        
        Acrylic.SetActive(false);
        
        SelectionCanvas.Children.Clear();
        SelectionCanvas.Children.Add(Acrylic);
        SelectionCanvas.Children.Add(AtlasImage);
        SelectionCanvas.ResetZoom();
    }
    
    private void SpriteSheetPreviewBox_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(AtlasImage); // get the point clicked in relation to the atlas image
        
        if (!point.Properties.IsLeftButtonPressed)
            return;
        
        m_StartPoint = point.Position;
        m_StartingLeft = Canvas.GetLeft(SelectedSprite ?? new AvaloniaObject());
        m_StartingTop = Canvas.GetTop(SelectedSprite ?? new AvaloniaObject());

        m_IsClicking = true;
    }
    
    private void SpriteSheetPreviewBox_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // finalize move state
        if (m_ShouldMove)
            HandleSpriteMove(e);
        
        SelectedSprite?.UpdateHandles(e.GetCurrentPoint(AtlasImage).Position, AtlasImage);
        
        m_IsClicking = false;
        m_ShouldMove = false;
        
        if (e.InitialPressMouseButton != MouseButton.Left || e.Source?.GetType() == typeof(Handle) || m_CurrentSprite == null)
            return;

        if (m_CurrentSprite.Width <= 10 || m_CurrentSprite.Height <= 10)
        {
            SelectionCanvas.Children.Remove(m_CurrentSprite);
            m_CurrentSprite = null;
            return;
        }

        SelectedSprite = m_CurrentSprite;
        
        m_CurrentSprite.SetHandlesVisible(true);
        m_CurrentSprite = null;

        var createSpriteAction = new CreateSpriteAction(SelectedSprite, this);
        StateManager.ExecuteAction(createSpriteAction);
        
        SpriteCreated?.Invoke(SelectedSprite);
    }

    private bool CanMove(PointerEventArgs e)
    {
        var currentPoint = e.GetCurrentPoint(AtlasImage);

        if (!currentPoint.Properties.IsLeftButtonPressed)
        {
            if (m_ShouldMove)
            {
                // left mouse button has been released, but last move event the requirements were satisfied
                var moveAction = new MoveSpriteAction(SelectedSprite!, m_MovedSpriteStartPoint, this);
                StateManager.ExecuteAction(moveAction);
            }

            m_ShouldMove = false;
        } 
        else if (e.KeyModifiers == KeyModifiers.Control && (bool)SelectedSprite?.Bounds.Contains(currentPoint.Position))
        {
            if (!m_ShouldMove)
            {
                // m_ShouldMove was false last move event, meaning that this is the first move event
                m_MovedSpriteStartPoint = new Point(Canvas.GetLeft(SelectedSprite), Canvas.GetTop(SelectedSprite));
            }
            // ctrl is pressed, and mouse is inside bounds
            m_ShouldMove = true;
        } 
        else if (e.KeyModifiers == KeyModifiers.Control && m_ShouldMove)
        {
            // ctrl is pressed, and mouse WAS inside bounds (m_ShouldMove was true)
            m_ShouldMove = true;
        }
        else
        {
            
            if (m_ShouldMove)
            {
                // ctrl has been released, but last move event it was pressed (m_ShouldMove was true)
                var moveAction = new MoveSpriteAction(SelectedSprite!, m_MovedSpriteStartPoint, this);
                StateManager.ExecuteAction(moveAction);
            }
            m_ShouldMove = false;
        }

        return m_ShouldMove;
    }

    private bool HandleSpriteMove(PointerEventArgs e)
    {
        var currentPoint = e.GetCurrentPoint(AtlasImage);

        if (SelectedSprite == null || !CanMove(e))
            return false;

        // make sure that there is no sprite being drawn
        m_CurrentSprite = null;
        
        // move the sprite
        Cursor = new Cursor(StandardCursorType.SizeAll);

        var newLeft = m_StartingLeft + (currentPoint.Position.X - m_StartPoint.X);
        var newTop = m_StartingTop + (currentPoint.Position.Y - m_StartPoint.Y);
        var newRight = newLeft + SelectedSprite.Width;
        var newBottom = newTop + SelectedSprite.Height;
                
        Sprite.MakePointsInsideControl(AtlasImage, ref newLeft, ref newRight, ref newTop, ref newBottom, SelectedSprite.Width, SelectedSprite.Height);
                
        Canvas.SetLeft(SelectedSprite, newLeft);
        Canvas.SetTop(SelectedSprite, newTop);
        Canvas.SetRight(SelectedSprite, newRight);
        Canvas.SetBottom(SelectedSprite, newBottom);
        
        return true;
    }

    private void UpdateDrawnSprite(Point currentPoint)
    {
        if (m_CurrentSprite == null || m_StartPoint == default(Point))
            return;
        
        var widthIsNegative = currentPoint.X - m_StartPoint.X < 0;
        var heightIsNegative = currentPoint.Y - m_StartPoint.Y < 0;
        
        var left = widthIsNegative ? currentPoint.X : m_StartPoint.X;
        var top = heightIsNegative ? currentPoint.Y : m_StartPoint.Y;
        var right = widthIsNegative ? m_StartPoint.X : currentPoint.X;
        var bottom = heightIsNegative ? m_StartPoint.Y : currentPoint.Y;
        
        Sprite.MakePointsInsideControl(AtlasImage, ref left, ref right, ref top, ref bottom);
        
        var spriteWidth = Math.Abs(left - right);
        var spriteHeight = Math.Abs(top - bottom);
        
        m_CurrentSprite!.Width = spriteWidth;
        m_CurrentSprite.Height = spriteHeight;
        
        Canvas.SetLeft(m_CurrentSprite, left);
        Canvas.SetTop(m_CurrentSprite, top);
        Canvas.SetRight(m_CurrentSprite, right);
        Canvas.SetBottom(m_CurrentSprite, bottom);
    }
    
    private void SpriteSheetPreviewBox_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!m_IsClicking)
        {
            Cursor = Cursor.Default;
            return;
        }
        var currentPoint = e.GetCurrentPoint(AtlasImage);
        
        SelectedSprite?.UpdateHandles(currentPoint.Position, AtlasImage);
        SelectedSprite?.RepositionHandles();

        if (SelectedSprite?.GetClickedHandle() != null || HandleSpriteMove(e))
            return;

        Cursor = Cursor.Default;
        
        if (m_CurrentSprite == null && SelectedSprite == null)
            CreateNewSprite();
        
        UpdateDrawnSprite(currentPoint.Position);
    }

    private void CreateNewSprite()
    {
        SelectedSprite?.SetHandlesVisible(false);
        
        m_CurrentSprite = new Sprite("Sprite" + (SpriteDatabase.Sprites.Count + 1));
            
        m_CurrentSprite.InitHandles(SelectionCanvas, AtlasImage, SpriteDatabase.IsSmoothMoves);
        m_CurrentSprite.SetHandlesVisible(false);
            
        SelectionCanvas.Children.Add(m_CurrentSprite);
        SpriteDatabase.Sprites.Add(m_CurrentSprite);
    }
}

public class SpriteDatabase
{
    public bool IsSmoothMoves { get; set; }
    public List<Sprite> Sprites { get; } = new();
}