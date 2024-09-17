namespace CustomAssetsBackend.Classes;

public class UnityAsset
{
    public override int GetHashCode()
    {
        return HashCode.Combine((int)ObjectType, Name, Path, PathId);
    }

    public enum UnityObjectType
    {
        None = -1,
        UIAtlas,
        MonoBehaviour,
        Texture2D,
        Material
    }
    
    public UnityObjectType ObjectType { get; init; } = UnityObjectType.None;
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public long PathId { get; init; } = -1;

    public static UnityAsset Empty => new()
    {
        ObjectType = UnityObjectType.None,
        Name = string.Empty,
        Path = string.Empty,
        PathId = -1
    };

    public override string ToString()
    {
        return $"{ObjectType.ToString()}: Name: {Name}, Asset path: {Path}, PathID: {PathId}";
    }
    
    public static bool operator !=(UnityAsset asset, UnityAsset? obj)
    {
        return !asset.Equals(obj);
    }
    
    public static bool operator ==(UnityAsset asset, UnityAsset? obj)
    {
        return asset.Equals(obj);
    }
    
    private bool Equals(UnityAsset? obj)
    {
        return obj is not null && 
               obj.ObjectType == this.ObjectType && 
               obj.Name == this.Name && 
               obj.Path == this.Path && 
               obj.PathId == this.PathId;
    }
}