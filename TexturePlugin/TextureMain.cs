using System.Globalization;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace TexturePlugin;

#nullable disable
public class TextureMain
{
    private static int IndexToTextureFormat(int format)
    {
        if (format >= 37)
            return format + 41 - 37;
        else
            return format + 1;
    }

    private static int TextureFormatToIndex(int format)
    {
        if (format >= 41)
            return format - 41 + 37;
        else
            return format - 1;
    }

    public static AssetTypeValueField ReplaceTexture(AssetsFileInstance fileInst, AssetTypeValueField baseField, string imagePath)
    {
        uint platform = fileInst.file.Metadata.TargetPlatform;
        byte[] platformBlob = TextureHelper.GetPlatformBlob(baseField);

        var tex = TextureFile.ReadTextureFile(baseField);

        Image<Rgba32> imgToImport;
        if (imagePath == null)
        {
            byte[] data = TextureHelper.GetRawTextureBytes(tex, fileInst);
            imgToImport = TextureImportExport.Export(data, tex.m_Width, tex.m_Height,
                (TextureFormat)tex.m_TextureFormat, platform, platformBlob);
        }
        else
        {
            imgToImport = Image.Load<Rgba32>(imagePath);
        }

        TextureFormat fmt = (TextureFormat)IndexToTextureFormat(TextureFormatToIndex(tex.m_TextureFormat));

        var chkHasMipMaps = tex.m_MipMap;
        var chkIsReadable = tex.m_IsReadable;

        int mips = 1;
        if (chkHasMipMaps)
        {
            if (imgToImport.Width == tex.m_Width && imgToImport.Height == tex.m_Height)
            {
                mips = tex.m_MipCount;
            }
            else if (TextureHelper.IsPo2(imgToImport.Width) && TextureHelper.IsPo2(imgToImport.Height))
            {
                mips = TextureHelper.GetMaxMipCount(imgToImport.Width, imgToImport.Height);
            }
        }

        int width = 0, height = 0;
        byte[] encImageBytes = null;
        string exceptionMessage = string.Empty;
        try
        {
            encImageBytes = TextureImportExport.Import(imgToImport, fmt, out width, out height, ref mips, platform, platformBlob);
        }
        catch (Exception ex)
        {
            throw;
            Console.WriteLine($"[TextureMain] Fatal error occured trying to encode texture! Error: {ex}");
        }

        if (encImageBytes == null)
        {
            string dialogText = $"Failed to encode texture format {fmt}!";
            if (exceptionMessage != null)
            {
                dialogText += "\n" + exceptionMessage;
            }

            return null;
        }

        AssetTypeValueField m_StreamData = baseField["m_StreamData"];
        m_StreamData["offset"].AsInt = 0;
        m_StreamData["size"].AsInt = 0;
        m_StreamData["path"].AsString = "";

        baseField["m_Name"].AsString = baseField["m_Name"].AsString;

        if (!baseField["m_MipMap"].IsDummy)
            baseField["m_MipMap"].AsBool = chkHasMipMaps;

        if (!baseField["m_MipCount"].IsDummy)
            baseField["m_MipCount"].AsInt = mips;

        if (!baseField["m_ReadAllowed"].IsDummy)
            baseField["m_ReadAllowed"].AsBool = chkIsReadable;

        AssetTypeValueField m_TextureSettings = baseField["m_TextureSettings"];

        m_TextureSettings["m_FilterMode"].AsInt = tex.m_TextureSettings.m_FilterMode;
        m_TextureSettings["m_Aniso"].AsInt = tex.m_TextureSettings.m_Aniso;
        m_TextureSettings["m_MipBias"].AsInt = (int)tex.m_TextureSettings.m_MipBias;

        if (!m_TextureSettings["m_WrapU"].IsDummy)
            m_TextureSettings["m_WrapU"].AsInt = tex.m_TextureSettings.m_WrapU;

        if (!m_TextureSettings["m_WrapV"].IsDummy)
            m_TextureSettings["m_WrapV"].AsInt = tex.m_TextureSettings.m_WrapV;

        var boxLightMapFormat = "0x" + tex.m_LightmapFormat.ToString("X2");

        if (boxLightMapFormat.StartsWith("0x"))
        {
            if (int.TryParse(boxLightMapFormat, NumberStyles.HexNumber, CultureInfo.CurrentCulture,
                    out int lightFmt))
                baseField["m_LightmapFormat"].AsInt = lightFmt;
        }
        else
        {
            if (int.TryParse(boxLightMapFormat, out int lightFmt))
                baseField["m_LightmapFormat"].AsInt = lightFmt;
        }

        if (!baseField["m_ColorSpace"].IsDummy)
            baseField["m_ColorSpace"].AsInt = tex.m_ColorSpace;

        baseField["m_TextureFormat"].AsInt = (int)fmt;

        if (!baseField["m_CompleteImageSize"].IsDummy)
            baseField["m_CompleteImageSize"].AsInt = encImageBytes.Length;

        baseField["m_Width"].AsInt = width;
        baseField["m_Height"].AsInt = height;

        AssetTypeValueField image_data = baseField["image data"];
        image_data.Value.ValueType = AssetValueType.ByteArray;
        image_data.TemplateField.ValueType = AssetValueType.ByteArray;
        image_data.AsByteArray = encImageBytes;
        
        return baseField;
    }
}