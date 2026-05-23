using System;
using NAudio.Wave;

namespace WindowsSound.ChannelManager
{
    /// <summary>
    /// 声道测试音生成器
    /// 用于测试各声道分配是否正确
    /// </summary>
    public class ChannelToneGenerator : IDisposable
    {
        private readonly int _sampleRate = 48000;
        private readonly int _channels = 2;
        private IWavePlayer _wavePlayer;
        private ToneProvider _toneProvider;

        /// <summary>
        /// 测试音频率（Hz）
        /// </summary>
        public double Frequency { get; set; } = 440.0;

        /// <summary>
        /// 测试音音量 (0.0 - 1.0)
        /// </summary>
        public double Volume { get; set; } = 0.5;

        /// <summary>
        /// 播放指定声道的测试音
        /// </summary>
        public void PlayTone(ChannelType channel, int durationMs = 2000)
        {
            Stop();

            _toneProvider = new ToneProvider(_sampleRate, _channels, Frequency, Volume, channel);
            _wavePlayer = new DirectSoundOut(100);
            _wavePlayer.Init(_toneProvider);
            _wavePlayer.Play();

            // 自动停止
            if (durationMs > 0)
            {
                var timer = new System.Timers.Timer(durationMs);
                timer.Elapsed += (s, e) =>
                {
                    Stop();
                    timer.Dispose();
                };
                timer.AutoReset = false;
                timer.Start();
            }
        }

        /// <summary>
        /// 播放扫频测试音（用于延迟测量）
        /// </summary>
        public void PlaySweepTone(ChannelType channel, double startFreq = 200, double endFreq = 2000, int durationMs = 3000)
        {
            Stop();

            _toneProvider = new ToneProvider(_sampleRate, _channels, startFreq, Volume, channel, endFreq, durationMs);
            _wavePlayer = new DirectSoundOut(100);
            _wavePlayer.Init(_toneProvider);
            _wavePlayer.Play();

            if (durationMs > 0)
            {
                var timer = new System.Timers.Timer(durationMs);
                timer.Elapsed += (s, e) =>
                {
                    Stop();
                    timer.Dispose();
                };
                timer.AutoReset = false;
                timer.Start();
            }
        }

        /// <summary>
        /// 播放粉红噪声（用于频率响应测试）
        /// </summary>
        public void PlayPinkNoise(ChannelType channel, int durationMs = 2000)
        {
            Stop();

            _toneProvider = new ToneProvider(_sampleRate, _channels, Frequency, Volume, channel, 0, 0, true);
            _wavePlayer = new DirectSoundOut(100);
            _wavePlayer.Init(_toneProvider);
            _wavePlayer.Play();

            if (durationMs > 0)
            {
                var timer = new System.Timers.Timer(durationMs);
                timer.Elapsed += (s, e) =>
                {
                    Stop();
                    timer.Dispose();
                };
                timer.AutoReset = false;
                timer.Start();
            }
        }

        /// <summary>
        /// 播放脉冲信号（用于延迟校准）
        /// </summary>
        public void PlayPulse(ChannelType channel)
        {
            Stop();

            _toneProvider = new ToneProvider(_sampleRate, _channels, 1000, Volume, channel, 0, 0, false, true);
            _wavePlayer = new DirectSoundOut(100);
            _wavePlayer.Init(_toneProvider);
            _wavePlayer.Play();
        }

        /// <summary>
        /// 停止播放
        /// </summary>
        public void Stop()
        {
            _wavePlayer?.Stop();
            _wavePlayer?.Dispose();
            _wavePlayer = null;
            _toneProvider?.Dispose();
            _toneProvider = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }

    /// <summary>
    /// 测试音提供器
    /// </summary>
    internal class ToneProvider : ISampleProvider, IDisposable
    {
        private readonly int _sampleRate;
        private readonly int _channels;
        private readonly double _volume;
        private readonly ChannelType _channel;
        private readonly bool _isPinkNoise;
        private readonly bool _isPulse;

        private double _frequency;
        private double _endFrequency;
        private int _durationMs;
        private int _position = 0;
        private readonly Random _random = new Random();

        private double _phase;
        private double[] _pinkNoiseBuffer;

        public WaveFormat WaveFormat { get; }

        public ToneProvider(int sampleRate, int channels, double frequency, double volume,
            ChannelType channel, double endFrequency = 0, int durationMs = 0,
            bool isPinkNoise = false, bool isPulse = false)
        {
            _sampleRate = sampleRate;
            _channels = channels;
            _frequency = frequency;
            _endFrequency = endFrequency;
            _volume = volume;
            _channel = channel;
            _durationMs = durationMs;
            _isPinkNoise = isPinkNoise;
            _isPulse = isPulse;

            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);

            if (_isPinkNoise)
            {
                _pinkNoiseBuffer = new double[7];
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesToRead = count;
            int totalDurationSamples = _durationMs > 0 ? (_durationMs * _sampleRate / 1000) : int.MaxValue;

            for (int i = 0; i < samplesToRead; i++)
            {
                if (_position >= totalDurationSamples)
                {
                    // 填充剩余缓冲区为静音
                    for (int j = i; j < samplesToRead; j++)
                    {
                        for (int ch = 0; ch < _channels; ch++)
                        {
                            buffer[offset + j * _channels + ch] = 0;
                        }
                    }
                    return i;
                }

                double sample = 0;

                if (_isPulse)
                {
                    // 10ms 脉冲
                    sample = (_position < _sampleRate / 100) ? _volume : 0;
                }
                else if (_isPinkNoise)
                {
                    sample = GeneratePinkNoise() * _volume;
                }
                else
                {
                    // 扫频或固定频率
                    double currentFreq = _frequency;
                    if (_endFrequency > 0 && _durationMs > 0)
                    {
                        double t = (double)_position / totalDurationSamples;
                        currentFreq = _frequency * Math.Pow(_endFrequency / _frequency, t);
                    }

                    sample = Math.Sin(_phase) * _volume;
                    _phase += 2 * Math.PI * currentFreq / _sampleRate;
                }

                // 根据声道分配样本
                for (int ch = 0; ch < _channels; ch++)
                {
                    bool shouldPlay = GetChannelForIndex(ch) == _channel;
                    buffer[offset + i * _channels + ch] = shouldPlay ? (float)sample : 0;
                }

                _position++;
            }

            return samplesToRead;
        }

        private ChannelType GetChannelForIndex(int index)
        {
            return index switch
            {
                0 => ChannelType.Left,
                1 => ChannelType.Right,
                _ => ChannelType.Left
            };
        }

        private double GeneratePinkNoise()
        {
            // Paul Kellet's refined pink noise generator
            double white = _random.NextDouble() * 2 - 1;
            _pinkNoiseBuffer[0] = 0.99886 * _pinkNoiseBuffer[0] + white * 0.0555179;
            _pinkNoiseBuffer[1] = 0.99332 * _pinkNoiseBuffer[1] + white * 0.0750759;
            _pinkNoiseBuffer[2] = 0.96900 * _pinkNoiseBuffer[2] + white * 0.1538520;
            _pinkNoiseBuffer[3] = 0.86650 * _pinkNoiseBuffer[3] + white * 0.3104856;
            _pinkNoiseBuffer[4] = 0.55000 * _pinkNoiseBuffer[4] + white * 0.5329522;
            _pinkNoiseBuffer[5] = -0.7616 * _pinkNoiseBuffer[5] - white * 0.0168980;

            double pink = _pinkNoiseBuffer[0] + _pinkNoiseBuffer[1] + _pinkNoiseBuffer[2] +
                         _pinkNoiseBuffer[3] + _pinkNoiseBuffer[4] + _pinkNoiseBuffer[5] +
                         _pinkNoiseBuffer[6] + white * 0.5362;
            _pinkNoiseBuffer[6] = white * 0.115926;

            return pink * 0.11; // 归一化
        }

        public void Dispose()
        {
            _pinkNoiseBuffer = null;
        }
    }
}
