using System.Collections.Generic;
using System.Linq;
using CustomAssetsBackend.Classes;
using RectpackSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace CustomAssetsInjector.Utils;

public static class RectPacker
{
    public struct PackingSpriteData
    {
        public SpriteData SpriteData;
        public Image<Rgba32> ImageData;
    }
    
    public static List<PackingSpriteData> PackRects(List<PackingSpriteData> packingSpriteInfo, string outputImagePath, uint spaceBetweenSprites = 0)
    {
        var packRects = new PackingRectangle[packingSpriteInfo.Count];
        
        for (var i = 0; i < packingSpriteInfo.Count; i++)
        {
            var spriteInfo = packingSpriteInfo[i];
            var packRect = new PackingRectangle(0, 0, (uint)spriteInfo.SpriteData.Width + spaceBetweenSprites, (uint)spriteInfo.SpriteData.Height + spaceBetweenSprites, i);
            packRects[i] = packRect;
        }

        // pack sprites
        RectanglePacker.Pack(packRects, out var bounds, PackingHints.FindBest, 1D, 2);
        packRects = packRects.OrderBy(rect => rect.Id).ToArray();
        
        // export image
        using var spritesheet = new Image<Rgba32>((int)bounds.Width, (int)bounds.Height);
        for (var i = 0; i < packingSpriteInfo.Count; i++)
        {
            var info = packingSpriteInfo[i];
            var packRect = packRects[i];
            spritesheet.Mutate(ctx => ctx.DrawImage(info.ImageData, new Point((int)packRect.X, (int)packRect.Y), 1f));
        }

        spritesheet.SaveAsPng(outputImagePath);
        
        // update spritedata info
        for (var i = 0; i < packingSpriteInfo.Count; i++)
        {
            var info = packingSpriteInfo[i];
            var packRect = packRects[i];

            packRect.Width -= spaceBetweenSprites;
            packRect.Height -= spaceBetweenSprites;
            
            info.SpriteData.StartX = packRect.X;
            info.SpriteData.EndX = packRect.Right;
            
            info.SpriteData.StartY = packRect.Y;
            info.SpriteData.EndY = packRect.Bottom;

            info.SpriteData.Width = packRect.Width;
            info.SpriteData.Height = packRect.Height;
        }

        return packingSpriteInfo;
    }
}