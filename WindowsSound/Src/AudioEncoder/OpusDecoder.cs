using System;
using System.Runtime.InteropServices;

namespace DistributedAudio.AudioEncoder
{
    /// <summary>
    /// Opus audio decoder wrapper
    /// </summary>
    public class OpusDecoder : IDisposable
    {
        private IntPtr _decoder;
        private readonly int _sampleRate;
        private readonly int _channels;
        private readonly int _frameSize;
        private bool _disposed;

        public OpusDecoder(int sampleRate = 48000, int channels = 2)
        {
            _sampleRate = sampleRate;
            _channels = channels;
            _frameSize = sampleRate / 50; // 20ms frames

            _decoder = OpusNative.opus_decoder_create(
                sampleRate,
                channels,
                out int error
            );

            if (error != OpusNative.OPUS_OK)
            {
                throw new InvalidOperationException($"Failed to create Opus decoder: {error}");
            }
        }

        /// <summary>
        /// Decode Opus data to PCM
        /// </summary>
        public byte[] Decode(byte[] opusData, int length)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(OpusDecoder));

            int frameSize = opusData.Length > 0 ? _frameSize : 0;
            byte[] output = new byte[frameSize * _channels * 2]; // 16-bit samples

            int decodedSamples = OpusNative.opus_decode(
                _decoder,
                opusData,
                length,
                output,
                frameSize,
                0
            );

            if (decodedSamples < 0)
            {
                throw new InvalidOperationException($"Opus decoding failed: {decodedSamples}");
            }

            // Adjust actual output size
            byte[] result = new byte[decodedSamples * _channels * 2];
            Array.Copy(output, result, result.Length);
            return result;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_decoder != IntPtr.Zero)
                {
                    OpusNative.opus_decoder_destroy(_decoder);
                    _decoder = IntPtr.Zero;
                }
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Opus encoder extensions
    /// </summary>
    public static class OpusEncoderExtensions
    {
        /// <summary>
        /// Encode PCM to Opus with specified complexity
        /// </summary>
        public static void SetComplexity(this OpusEncoder encoder, int complexity)
        {
            if (complexity < 0 || complexity > 10)
                throw new ArgumentOutOfRangeException(nameof(complexity), "Complexity must be 0-10");

            // Force complexity setting
            // Note: This requires modifying the OpusEncoder class to expose the encoder handle
        }

        /// <summary>
        /// Set maximum bandwidth
        /// </summary>
        public static void SetMaxBandwidth(this OpusEncoder, OpusBandwidth bandwidth)
        {
            // Implementation depends on OpusNative constants
        }

        public enum OpusBandwidth
        {
            Narrowband = 1101,
            Mediumband = 1102,
            Wideband = 1103,
            SuperWideband = 1104,
            Fullband = 1105
        }
    }
}
