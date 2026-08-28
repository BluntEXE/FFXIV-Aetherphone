using Aetherphone.Core.Notifications;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Xunit;

namespace Aetherphone.Tests;

public sealed class UiSoundDecodeTests
{
    private const int SampleRate = 48000;

    [Fact]
    public void EveryBundledClipDecodesToAudibleAudio()
    {
        var root = Path.Combine(FindProjectRoot(), "src", "Aetherphone", "Sounds");
        var files = UiSoundCatalog.Files();
        for (var index = 0; index < files.Count; index++)
        {
            var path = Path.Combine(root, files[index]);
            var peak = DecodePeak(path);
            Assert.True(peak > 0.01f, $"{files[index]} decoded to a peak of {peak}");
        }
    }

    private static float DecodePeak(string path)
    {
        using var reader = SoundEffectPlayer.OpenReader(path);
        var samples = reader.ToSampleProvider();
        if (samples.WaveFormat.SampleRate != SampleRate)
        {
            samples = new WdlResamplingSampleProvider(samples, SampleRate);
        }

        if (samples.WaveFormat.Channels == 1)
        {
            samples = new MonoToStereoSampleProvider(samples);
        }

        var buffer = new float[4096];
        var peak = 0f;
        while (true)
        {
            var read = samples.Read(buffer, 0, buffer.Length);
            if (read == 0)
            {
                break;
            }

            for (var sampleIndex = 0; sampleIndex < read; sampleIndex++)
            {
                peak = Math.Max(peak, Math.Abs(buffer[sampleIndex]));
            }
        }

        return peak;
    }

    private static string FindProjectRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Aetherphone.sln")))
        {
            current = current.Parent;
        }

        Assert.NotNull(current);
        return current.FullName;
    }
}
