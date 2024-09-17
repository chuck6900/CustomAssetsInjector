namespace CustomAssetsBackend.Classes;

public class SpriteData : IEquatable<SpriteData>
{
    public string Name { get; set; }
    public double StartX { get; set; }
    public double StartY { get; set; }
    public double EndX { get; set; }
    public double EndY { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public System.Numerics.Vector2 OriginPoint { get; set; } = new(0.5f, 0.5f);

    public bool Equals(SpriteData? other)
    {
        if (other is null) 
            return false;
        if (ReferenceEquals(this, other)) 
            return true;
        
        return Name == other.Name && 
               StartX.Equals(other.StartX) && 
               StartY.Equals(other.StartY) && 
               EndX.Equals(other.EndX) && 
               EndY.Equals(other.EndY) && 
               Width.Equals(other.Width) && 
               Height.Equals(other.Height) && 
               OriginPoint.Equals(other.OriginPoint);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) 
            return false;
        if (ReferenceEquals(this, obj)) 
            return true;
        if (obj.GetType() != GetType()) 
            return false;
        
        return Equals((SpriteData)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, StartX, StartY, EndX, EndY, Width, Height, OriginPoint);
    }
}