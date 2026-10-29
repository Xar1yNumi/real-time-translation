using RealTimeTranslation.Core;
using RealTimeTranslation.Feasibility;

namespace RealTimeTranslation.IntegrationTests;

public sealed class FeasibilityOptionsTests
{
    [Theory]
    [InlineData("system", "zh-Hans", AudioSourceKind.SystemAudio, TargetLanguage.SimplifiedChinese)]
    [InlineData("microphone", "en", AudioSourceKind.Microphone, TargetLanguage.English)]
    [InlineData("microphone", "ru", AudioSourceKind.Microphone, TargetLanguage.Russian)]
    public void ParsesLiveSourceAndDuration(
        string source,
        string target,
        AudioSourceKind expectedSource,
        TargetLanguage expectedTarget)
    {
        var parsed = FeasibilityOptions.TryParse(
            ["--source", source, "--target", target, "--duration", "00:05:00"],
            out var options);

        Assert.True(parsed);
        Assert.Equal(expectedSource, options.AudioSource);
        Assert.Equal(expectedTarget, options.TargetLanguage);
        Assert.Equal(TimeSpan.FromMinutes(5), options.Duration);
        Assert.False(options.ShowText);
    }

    [Fact]
    public void ParsesWaveWithExplicitTextOptIn()
    {
        var parsed = FeasibilityOptions.TryParse(
            ["--wav", "sample.wav", "--target", "ru", "--show-text"],
            out var options);

        Assert.True(parsed);
        Assert.Equal(FeasibilitySource.Wave, options.Source);
        Assert.Equal("sample.wav", options.WavePath);
        Assert.True(options.ShowText);
    }

    [Theory]
    [InlineData("--source", "system", "--target", "zh-Hans")]
    [InlineData("--source", "system", "--target", "zh-Hans", "--duration", "00:00:00")]
    [InlineData("--source", "system", "--wav", "sample.wav", "--target", "zh-Hans", "--duration", "00:05:00")]
    [InlineData("--source", "system", "--target", "fr", "--duration", "00:05:00")]
    [InlineData("--wav", "sample.wav", "--target", "en", "--show-text", "--show-text")]
    public void RejectsInvalidOrMixedOptions(params string[] arguments) =>
        Assert.False(FeasibilityOptions.TryParse(arguments, out _));
}
