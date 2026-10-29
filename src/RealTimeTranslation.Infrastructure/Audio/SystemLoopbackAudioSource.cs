using NAudio.Wave;

namespace RealTimeTranslation.Infrastructure.Audio;

public sealed class SystemLoopbackAudioSource : WindowsAudioCaptureSource
{
    public SystemLoopbackAudioSource()
#pragma warning disable CS0618 // The approved baseline specifies WasapiLoopbackCapture.
        : base(new WasapiLoopbackCapture())
#pragma warning restore CS0618
    {
    }

    internal SystemLoopbackAudioSource(IWaveIn capture)
        : base(capture)
    {
    }
}
