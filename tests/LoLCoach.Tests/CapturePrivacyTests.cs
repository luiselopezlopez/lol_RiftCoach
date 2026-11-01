using LoLCoach.Services;
using Xunit;

namespace LoLCoach.Tests;

public class CapturePrivacyTests
{
    [Fact]
    public void CapturePreservesEveryPixelWithoutMaskingOrChangingInput()
    {
        var pixels = new byte[400 * 240 * 4];
        new Random(42).NextBytes(pixels);
        var original = pixels.ToArray();
        var bitmap = WindowCaptureService.CreateFrame(400, 240, pixels, 1600);
        var output = new byte[pixels.Length];
        bitmap.CopyPixels(output, 1600, 0);
        Assert.Equal(original, output);
        Assert.Equal(original, pixels);
        Assert.True(bitmap.IsFrozen);
    }

    [Fact]
    public void CaptureFailsClosedForInvalidDimensions()
        => Assert.Throws<ArgumentException>(() => WindowCaptureService.CreateFrame(100, 100, new byte[40000], 400));
}