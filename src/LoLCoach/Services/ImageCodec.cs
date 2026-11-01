using System.IO;
using System.Windows.Media.Imaging;

namespace LoLCoach.Services;

public static class ImageCodec
{
    public static byte[] Encode(BitmapSource bitmap)
    {
        var encoder = new JpegBitmapEncoder { QualityLevel = 82 };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream(); encoder.Save(stream); return stream.ToArray();
    }
    public static BitmapSource Decode(byte[] jpeg)
    {
        using var stream = new MemoryStream(jpeg);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var bitmap = decoder.Frames[0]; bitmap.Freeze(); return bitmap;
    }
}