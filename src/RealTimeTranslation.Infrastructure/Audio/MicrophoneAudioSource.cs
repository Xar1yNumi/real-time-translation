using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace RealTimeTranslation.Infrastructure.Audio;

public sealed class MicrophoneAudioSource : WindowsAudioCaptureSource
{
    public MicrophoneAudioSource()
#pragma warning disable CS0618 // The approved baseline specifies WasapiCapture.
        : base(new WasapiCapture())
#pragma warning restore CS0618
    {
    }

    internal MicrophoneAudioSource(IWaveIn capture)
        : base(capture)
    {
    }
}
