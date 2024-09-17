using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;

namespace CustomAssetsInjector.Controls;

public enum HandleType
{
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left,
    Origin
}

public class Handle : Rectangle
{
    private Control m_RelativeObject;

    private double m_Size;
    
    public HandleType Type;
    
    public TransformControlRectangle ParentRectangle;
    
    public bool IsClicking;
    
    public double InitialWidth;
    public double InitialHeight;
    public double InitialMouseX;
    public double InitialMouseY;
    
    public Handle(TransformControlRectangle parentRectangle, int handleSize, HandleType type, Control relativeToObj)
    {
        var isOriginHandle = type == HandleType.Origin;
        
        this.Name = $"[{parentRectangle.Name ?? (parentRectangle as Sprite)?.SpriteName}]-[Handle:{type.ToString()}]";
        this.ZIndex = isOriginHandle ? 2 : 1; // prefer origin point
        
        ParentRectangle = parentRectangle;
        m_Size = handleSize;
        Type = type;
        m_RelativeObject = relativeToObj;

        Width = handleSize;
        Height = handleSize;

        StrokeThickness = 1;
        Stroke = new SolidColorBrush(isOriginHandle ? Colors.Black : Colors.White);
        Fill = new SolidColorBrush(isOriginHandle ? Colors.White : Colors.Black, 0.5);
        
        Reposition();
        
        this.PointerMoved += Handle_PointerMoved;
        this.PointerPressed += Handle_PointerPressed;
        this.PointerReleased += Handle_PointerReleased;
    }

    private void Handle_PointerMoved(object? sender, PointerEventArgs e)
    {
        switch (Type)
        {
            case HandleType.TopLeft:
                Cursor = new Cursor(StandardCursorType.TopLeftCorner);
                break;
            case HandleType.Top:
                Cursor = new Cursor(StandardCursorType.TopSide);
                break;
            case HandleType.TopRight:
                Cursor = new Cursor(StandardCursorType.TopRightCorner);
                break;
            case HandleType.Right:
                Cursor = new Cursor(StandardCursorType.RightSide);
                break;
            case HandleType.BottomRight:
                Cursor = new Cursor(StandardCursorType.BottomRightCorner);
                break;
            case HandleType.Bottom:
                Cursor = new Cursor(StandardCursorType.BottomSide);
                break;
            case HandleType.BottomLeft:
                Cursor = new Cursor(StandardCursorType.BottomLeftCorner);
                break;
            case HandleType.Left:
                Cursor = new Cursor(StandardCursorType.LeftSide);
                break;
        }
    }

    public void Reposition()
    {
        var parentLeft = Canvas.GetLeft(ParentRectangle);
        var parentRight = Canvas.GetRight(ParentRectangle);
        var parentTop = Canvas.GetTop(ParentRectangle);
        var parentBottom = Canvas.GetBottom(ParentRectangle);

        var leftPos = parentLeft;
        var topPos = parentTop;

        var handleSizeOffset = m_Size / 2;
        
        var leftX = parentLeft - handleSizeOffset;
        var middleX = (parentLeft + ParentRectangle.Width / 2) - handleSizeOffset;
        var rightX =  parentRight - handleSizeOffset;
        
        var topY = parentTop - handleSizeOffset;
        var middleY = (parentTop + ParentRectangle.Height / 2) - handleSizeOffset;
        var bottomY = parentBottom - handleSizeOffset;
        
        switch (Type)
        {
            case HandleType.TopLeft:
                leftPos = leftX;
                topPos = topY;
                break;
            case HandleType.Top:
                leftPos = middleX;
                topPos = topY;
                break;
            case HandleType.TopRight:
                leftPos = rightX;
                topPos = topY;
                break;
            case HandleType.Right:
                leftPos = rightX;
                topPos = middleY;
                break;
            case HandleType.BottomRight:
                leftPos = rightX;
                topPos = bottomY;
                break;
            case HandleType.Bottom:
                leftPos = middleX;
                topPos = bottomY;
                break;
            case HandleType.BottomLeft:
                leftPos = leftX;
                topPos = bottomY;
                break;
            case HandleType.Left:
                leftPos = leftX;
                topPos = middleY;
                break;
            case HandleType.Origin:
                leftPos = (parentLeft + (ParentRectangle.Width * ParentRectangle.OriginPoint.X)) - handleSizeOffset;
                topPos = (parentTop + (ParentRectangle.Height * ParentRectangle.OriginPoint.Y)) - handleSizeOffset;
                break;
        }
        
        Canvas.SetLeft(this, leftPos);
        Canvas.SetTop(this, topPos);
    }
    
    private void Handle_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(m_RelativeObject);
        if (!point.Properties.IsLeftButtonPressed)
            return;
        
        IsClicking = true;

        InitialWidth = ParentRectangle.Width;
        InitialHeight = ParentRectangle.Height;
        InitialMouseX = point.Position.X;
        InitialMouseY = point.Position.Y;
    }

    private void Handle_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left)
            return;

        IsClicking = false;
    }
}