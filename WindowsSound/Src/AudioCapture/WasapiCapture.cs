using NAudio.Wasapi;
using NAudio.CoreAudioApi;
using System;
using System.Threading.Tasks;

namespace DistributedAudio.AudioCapture
{
    /// <summary>
    /// WASAPI Loopback Audio Capture
    /// Captures system audio output
    /// </summary>
    public class WasapiCapture : IDisposable
    {
        private WasapiLoopbackCapture? _capture;
        private bool _isRecording;
        private readonly object _lock = new();

        public event EventHandler<byte[]>? AudioDataCaptured;
        public event EventHandler<Exception>? CaptureError;

        public bool IsRecording => _isRecording;

        public int SampleRate { get; private set; } = 48000;
        public int Channels { get; private set; } = 2;
        public int BitsPerSample { get; private set; } = 16;

        /// <summary>
        /// Start capturing system audio
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                if (_isRecording) return;

                try
                {
                    _capture = new WasapiLoopbackCapture();
                    _capture.RecordingStopped += OnRecordingStopped;
                    _capture.DataAvailable += OnDataAvailable;

                    // Configure audio format
                    _capture.WaveFormat = new NAudio.Wave.WaveFormat(
                        SampleRate,
                        BitsPerSample,
                        Channels
                    );

                    _capture.StartRecording();
                    _isRecording = true;
                }
                catch (Exception ex)
                {
                    CaptureError?.Invoke(this, ex);
                }
            }
        }

        /// <summary>
        /// Stop capturing
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                if (!_isRecording) return;

                _capture?.StopRecording();
            }
        }

        private void OnDataAvailable(object? sender, NAudio.Wave.WaveInEventArgs e)
        {
            if (e.Buffer != null && e.BytesRecorded > 0)
            {
                byte[] data = new byte[e.BytesRecorded];
                Array.Copy(e.Buffer, data, e.BytesRecorded);
                AudioDataCaptured?.Invoke(this, data);
            }
        }

        private void OnRecordingStopped(object? sender, NAudio.Wave.StoppedEventArgs e)
        {
            _isRecording = false;
            _capture?.Dispose();
            _capture = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
