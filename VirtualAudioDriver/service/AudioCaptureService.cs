using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WindowsSound.AudioEncoder;
using WindowsSound.ChannelRouter;
using WindowsSound.Discovery;

namespace WindowsSound.AudioCapture
{
    /// <summary>
    /// 虚拟声卡音频捕获服务
    /// 从驱动共享内存读取音频数据并发送到网络
    /// </summary>
    public class AudioCaptureService
    {
        private const string SHARED_MEMORY_NAME = "DistributedAudio_SharedMemory";
        private const string EVENT_NAME = "DistributedAudio_DataReady";
        private const int BUFFER_SIZE = 1024 * 1024; // 1MB

        private readonly ILogger<AudioCaptureService> _logger;
        private readonly AudioStreamer _streamer;
        private readonly ChannelRouter _channelRouter;

        private MemoryMappedFile _mmf;
        private MemoryMappedViewAccessor _accessor;
        private EventWaitHandle _dataReadyEvent;

        private SharedMemoryHeader _header;
        private CancellationTokenSource _cts;
        private Task _captureTask;

        public AudioCaptureService(
            ILogger<AudioCaptureService> logger,
            AudioStreamer streamer,
            ChannelRouter channelRouter)
        {
            _logger = logger;
            _streamer = streamer;
            _channelRouter = channelRouter;
        }

        /// <summary>
        /// 启动音频捕获服务
        /// </summary>
        public async Task StartAsync()
        {
            try
            {
                _logger.LogInformation("Starting audio capture service...");

                // 打开共享内存
                _mmf = MemoryMappedFile.OpenExisting(SHARED_MEMORY_NAME);
                _accessor = _mmf.CreateViewAccessor(0, BUFFER_SIZE);

                // 读取头部
                _header = new SharedMemoryHeader();
                ReadHeader();

                // 打开数据就绪事件
                _dataReadyEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EVENT_NAME);

                // 启动捕获任务
                _cts = new CancellationTokenSource();
                _captureTask = Task.Run(() => CaptureLoop(_cts.Token));

                _logger.LogInformation("Audio capture service started");
                _logger.LogInformation("Format: {SampleRate}Hz, {Channels}ch, {Bits}bit",
                    _header.SampleRate, _header.Channels, _header.BitsPerSample);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start audio capture service");
                throw;
            }
        }

        /// <summary>
        /// 停止音频捕获服务
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                _logger.LogInformation("Stopping audio capture service...");

                _cts?.Cancel();

                if (_captureTask != null)
                {
                    await _captureTask;
                }

                _dataReadyEvent?.Dispose();
                _accessor?.Dispose();
                _mmf?.Dispose();

                _logger.LogInformation("Audio capture service stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping audio capture service");
            }
        }

        /// <summary>
        /// 捕获循环
        /// </summary>
        private void CaptureLoop(CancellationToken cancellationToken)
        {
            byte[] readBuffer = new byte[4096]; // 4KB读缓冲区
            long lastReadOffset = 0;

            _logger.LogDebug("Capture loop started");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // 等待数据就绪事件
                    bool dataReady = _dataReadyEvent.WaitOne(100);

                    if (!dataReady)
                    {
                        continue;
                    }

                    // 读取当前头部状态
                    ReadHeader();

                    if (!_header.Active)
                    {
                        // 驱动未激活
                        Thread.Sleep(100);
                        continue;
                    }

                    // 计算可读取的数据量
                    long availableData = CalculateAvailableData(lastReadOffset);

                    if (availableData < readBuffer.Length)
                    {
                        // 数据不足，等待更多数据
                        continue;
                    }

                    // 读取数据
                    long toRead = Math.Min(availableData, readBuffer.Length);
                    ReadData(lastReadOffset, readBuffer, (int)toRead);

                    // 更新读指针
                    lastReadOffset = (lastReadOffset + toRead) % _header.BufferSize;

                    // 更新共享内存的读指针
                    _accessor.Write(8, _header.ReadOffset = (int)lastReadOffset);

                    // 处理音频数据
                    ProcessAudioData(readBuffer, (int)toRead);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in capture loop");
                    Thread.Sleep(1000);
                }
            }

            _logger.LogDebug("Capture loop stopped");
        }

        /// <summary>
        /// 读取共享内存头部
        /// </summary>
        private void ReadHeader()
        {
            _accessor.Read(0, out _header.WriteOffset);
            _accessor.Read(4, out _header.ReadOffset);
            _accessor.Read(8, out _header.BufferSize);
            _accessor.Read(12, out _header.SampleRate);
            _accessor.Read(16, out _header.Channels);
            _accessor.Read(20, out _header.BitsPerSample);
            _accessor.Read(24, out _header.Active);
        }

        /// <summary>
        /// 计算可读取的数据量
        /// </summary>
        private long CalculateAvailableData(long lastReadOffset)
        {
            long writeOffset = _header.WriteOffset;

            if (writeOffset >= lastReadOffset)
            {
                return writeOffset - lastReadOffset;
            }
            else
            {
                return _header.BufferSize - lastReadOffset + writeOffset;
            }
        }

        /// <summary>
        /// 读取数据
        /// </summary>
        private void ReadData(long offset, byte[] buffer, int count)
        {
            long dataStartOffset = Marshal.SizeOf<SharedMemoryHeader>();

            // 处理环形缓冲区读取
            long firstChunk = Math.Min(count, _header.BufferSize - offset);
            _accessor.ReadArray(dataStartOffset + offset, buffer, 0, (int)firstChunk);

            if (firstChunk < count)
            {
                int secondChunk = count - (int)firstChunk;
                _accessor.ReadArray(dataStartOffset, buffer, (int)firstChunk, secondChunk);
            }
        }

        /// <summary>
        /// 处理音频数据
        /// </summary>
        private void ProcessAudioData(byte[] data, int length)
        {
            try
            {
                // 根据声道配置路由音频
                var routedData = _channelRouter.RouteAudio(data, length);

                // 编码音频
                var encodedData = EncodeAudio(routedData);

                // 发送到网络
                _streamer.SendToAllDevices(encodedData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing audio data");
            }
        }

        /// <summary>
        /// 编码音频数据
        /// </summary>
        private byte[] EncodeAudio(byte[] pcmData)
        {
            // 使用Opus编码器
            // 这里简化为返回PCM数据
            // 实际实现需要集成OpusEncoder
            return pcmData;
        }

        public void Dispose()
        {
            StopAsync().Wait();
        }
    }

    /// <summary>
    /// 共享内存头部结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SharedMemoryHeader
    {
        public int WriteOffset;
        public int ReadOffset;
        public int BufferSize;
        public int SampleRate;
        public int Channels;
        public int BitsPerSample;
        public int Active;
        private int _reserved1;
        private int _reserved2;
        private int _reserved3;
        private int _reserved4;
    }
}
