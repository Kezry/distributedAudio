using System;
using System.Runtime.InteropServices;

namespace DistributedAudio.AudioEncoder
{
    /// <summary>
    /// Opus audio encoder wrapper
    /// Low latency audio codec
    /// </summary>
    public class OpusEncoder : IDisposable
    {
        private IntPtr _encoder;
        private readonly int _sampleRate;
        private readonly int _channels;
        private readonly int _frameSize;
        private readonly int _bitrate;
        private bool _disposed;

        public OpusEncoder(int sampleRate = 48000, int channels = 2, int bitrate = 64000)
        {
            _sampleRate = sampleRate;
            _channels = channels;
            _frameSize = sampleRate / 50; // 20ms frames
            _bitrate = bitrate;

            _encoder = OpusNative.opus_encoder_create(
                sampleRate,
                channels,
                OpusNative.OPUS_APPLICATION_AUDIO,
                out int error
            );

            if (error != OpusNative.OPUS_OK)
            {
                throw new InvalidOperationException($"Failed to create Opus encoder: {error}");
            }

            // Configure encoder
            OpusNative.opus_encoder_ctl(_encoder, OpusNative.OPUS_SET_BITRATE_REQUEST, bitrate);
            OpusNative.opus_encoder_ctl(_encoder, OpusNative.OPUS_SET_COMPLEXITY_REQUEST, 10);
            OpusNative.opus_encoder_ctl(_encoder, OpusNative.OPUS_SET_SIGNAL_REQUEST, OpusNative.OPUS_SIGNAL_MUSIC);
        }

        /// <summary>
        /// Encode PCM data to Opus
        /// </summary>
        public byte[] Encode(byte[] pcmData)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(OpusEncoder));

            int maxOutputSize = pcmData.Length / 4; // Conservative estimate
            byte[] output = new byte[maxOutputSize];

            int encodedBytes = OpusNative.opus_encode(
                _encoder,
                pcmData,
                _frameSize,
                output,
                maxOutputSize
            );

            if (encodedBytes < 0)
            {
                throw new InvalidOperationException($"Opus encoding failed: {encodedBytes}");
            }

            byte[] result = new byte[encodedBytes];
            Array.Copy(output, result, encodedBytes);
            return result;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_encoder != IntPtr.Zero)
                {
                    OpusNative.opus_encoder_destroy(_encoder);
                    _encoder = IntPtr.Zero;
                }
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Opus codec native bindings
    /// </summary>
    internal static class OpusNative
    {
        private const string OpusDll = "opus";

        public const int OPUS_OK = 0;
        public const int OPUS_APPLICATION_AUDIO = 2049;
        public const int OPUS_SIGNAL_MUSIC = 1804;
        public const int OPUS_SET_BITRATE_REQUEST = 4002;
        public const int OPUS_SET_COMPLEXITY_REQUEST = 4010;
        public const int OPUS_SET_SIGNAL_REQUEST = 4024;

        [DllImport(OpusDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr opus_encoder_create(
            int Fs,
            int channels,
            int application,
            out int error
        );

        [DllImport(OpusDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void opus_encoder_destroy(IntPtr encoder);

        [DllImport(OpusDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int opus_encode(
            IntPtr st,
            byte[] pcm,
            int frame_size,
            byte[] data,
            int max_data_bytes
        );

        [DllImport(OpusDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int opus_encoder_ctl(
            IntPtr st,
            int request,
            int value
        );
    }
}
