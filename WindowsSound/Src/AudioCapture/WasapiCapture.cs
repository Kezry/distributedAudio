using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Threading.Tasks;

namespace DistributedAudio.AudioCapture
{
    /// <summary>
    /// WASAPI Loopback Audio Capture
    /// Captures system audio output with low latency
    /// </summary>
    public class WasapiCapture : IDisposable
    {
        private WasapiLoopbackCapture? _capture;
        private bool _isRecording;
        private readonly object _lock = new();
        private AudioFormatConfig _format = AudioFormatConfig.CreateStereo();

        public event EventHandler<byte[]>? AudioDataCaptured;
        public event EventHandler<Exception>? CaptureError;
        public event EventHandler? RecordingStarted;
        public event EventHandler? RecordingStopped;

        public bool IsRecording => _isRecording;
        public AudioFormatConfig Format => _format;

        /// <summary>
        /// 配置音频格式
        /// </summary>
        public void SetFormat(AudioFormatConfig format)
        {
            if (_isRecording)
                throw new InvalidOperationException("Cannot change format while recording");

            _format = format;
        }

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

                    // Use device's native format for best quality
                    // The capture will provide data in its native format
                    // We'll convert if needed

                    _capture.StartRecording();
                    _isRecording = true;

                    RecordingStarted?.Invoke(this, EventArgs.Empty);
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

            if (e.Exception != null)
            {
                CaptureError?.Invoke(this, e.Exception);
            }

            _capture?.Dispose();
            _capture = null;

            RecordingStopped?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 获取当前延迟（毫秒）
        /// </summary>
        public int GetLatency()
        {
            if (_capture != null)
            {
                try
                {
                    return _capture.EngineLatencyInMilliseconds;
                }
                catch
                {
                    return 0;
                }
            }
            return 0;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
