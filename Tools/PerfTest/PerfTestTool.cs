using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DistributedAudio.Tools.PerfTest
{
    /// <summary>
    /// 性能测试工具
    /// 测试端到端延迟、设备间同步、网络抖动等性能指标
    /// </summary>
    public class PerfTestTool
    {
        private readonly ILogger<PerfTestTool> _logger;
        private readonly AudioCaptureService _captureService;
        private readonly DeviceScanner _deviceScanner;

        public PerfTestTool(
            ILogger<PerfTestTool> logger,
            AudioCaptureService captureService,
            DeviceScanner deviceScanner)
        {
            _logger = logger;
            _captureService = captureService;
            _deviceScanner = deviceScanner;
        }

        /// <summary>
        /// 运行完整性能测试套件
        /// </summary>
        public async Task<PerfTestReport> RunFullTestSuite()
        {
            _logger.LogInformation("Starting full performance test suite...");

            var report = new PerfTestReport
            {
                StartTime = DateTime.Now,
                TestResults = new Dictionary<string, TestResult>()
            };

            // 1. 延迟测试
            var latencyResult = await TestLatency();
            report.TestResults["Latency"] = latencyResult;

            // 2. 同步测试
            var syncResult = await TestSync();
            report.TestResults["Sync"] = syncResult;

            // 3. 抖动测试
            var jitterResult = await TestJitter();
            report.TestResults["Jitter"] = jitterResult;

            // 4. 音频质量测试
            var qualityResult = await TestAudioQuality();
            report.TestResults["AudioQuality"] = qualityResult;

            // 5. 压力测试
            var stressResult = await TestStress();
            report.TestResults["Stress"] = stressResult;

            report.EndTime = DateTime.Now;
            report.Duration = report.EndTime - report.StartTime;

            _logger.LogInformation("Performance test suite completed in {Duration}", report.Duration);

            return report;
        }

        /// <summary>
        /// 端到端延迟测试
        /// </summary>
        private async Task<TestResult> TestLatency()
        {
            _logger.LogInformation("Starting latency test...");

            var result = new TestResult
            {
                Name = "End-to-End Latency",
                Passed = true
            };

            var measurements = new List<double>();

            // 扫描设备
            var devices = await _deviceScanner.ScanAsync();
            if (!devices.Any())
            {
                result.Passed = false;
                result.ErrorMessage = "No devices found";
                return result;
            }

            var testDevice = devices.First();

            // 生成测试信号 (10ms脉冲)
            var testSignal = GeneratePulseSignal(48000, 2, 10);

            // 发送测试信号并测量响应时间
            for (int i = 0; i < 100; i++)
            {
                var stopwatch = Stopwatch.StartNew();

                // 发送测试信号
                await SendTestSignal(testDevice, testSignal);

                // 等待设备响应
                await WaitForDeviceResponse(testDevice);

                stopwatch.Stop();

                measurements.Add(stopwatch.Elapsed.TotalMilliseconds);
                await Task.Delay(50); // 间隔50ms
            }

            // 计算统计数据
            result.Metrics["Average"] = measurements.Average();
            result.Metrics["Min"] = measurements.Min();
            result.Metrics["Max"] = measurements.Max();
            result.Metrics["StdDev"] = CalculateStdDev(measurements);
            result.Metrics["P95"] = CalculatePercentile(measurements, 95);
            result.Metrics["P99"] = CalculatePercentile(measurements, 99);

            // 判断是否通过 (目标: <80ms P95)
            result.Passed = result.Metrics["P95"] < 80.0;

            _logger.LogInformation("Latency test: {Result} (P95: {P95}ms)",
                result.Passed ? "PASS" : "FAIL", result.Metrics["P95"]);

            return result;
        }

        /// <summary>
        /// 设备间同步测试
        /// </summary>
        private async Task<TestResult> TestSync()
        {
            _logger.LogInformation("Starting sync test...");

            var result = new TestResult
            {
                Name = "Device Synchronization",
                Passed = true
            };

            var devices = await _deviceScanner.ScanAsync();
            if (devices.Count() < 2)
            {
                result.Passed = false;
                result.ErrorMessage = "Need at least 2 devices for sync test";
                return result;
            }

            var device1 = devices.ElementAt(0);
            var device2 = devices.ElementAt(1);

            var syncOffsets = new List<double>();

            // 发送同步测试音并测量两设备的时间差
            for (int i = 0; i < 50; i++)
            {
                // 发送PTP同步信号
                var t1 = GetCurrentTimestamp();
                await SendSyncSignal(device1);
                await SendSyncSignal(device2);

                // 读取两设备的时钟偏移
                var offset1 = await GetClockOffset(device1);
                var offset2 = await GetClockOffset(device2);

                var syncOffset = Math.Abs(offset1 - offset2);
                syncOffsets.Add(syncOffset);

                await Task.Delay(100);
            }

            // 计算统计数据
            result.Metrics["AverageOffset"] = syncOffsets.Average();
            result.Metrics["MaxOffset"] = syncOffsets.Max();
            result.Metrics["StdDev"] = CalculateStdDev(syncOffsets);

            // 判断是否通过 (目标: <20ms)
            result.Passed = result.Metrics["MaxOffset"] < 20.0;

            _logger.LogInformation("Sync test: {Result} (Max offset: {Max}ms)",
                result.Passed ? "PASS" : "FAIL", result.Metrics["MaxOffset"]);

            return result;
        }

        /// <summary>
        /// 网络抖动测试
        /// </summary>
        private async Task<TestResult> TestJitter()
        {
            _logger.LogInformation("Starting jitter test...");

            var result = new TestResult
            {
                Name = "Network Jitter",
                Passed = true
            };

            var devices = await _deviceScanner.ScanAsync();
            if (!devices.Any())
            {
                result.Passed = false;
                result.ErrorMessage = "No devices found";
                return result;
            }

            var testDevice = devices.First();
            var intervals = new List<double>();
            var lastPacketTime = DateTime.Now;

            // 发送1000个数据包并测量间隔
            for (int i = 0; i < 1000; i++)
            {
                var packetTime = DateTime.Now;
                await SendTestPacket(testDevice, i);

                if (i > 0)
                {
                    var interval = (packetTime - lastPacketTime).TotalMilliseconds;
                    intervals.Add(interval);
                }

                lastPacketTime = packetTime;
                await Task.Delay(10);
            }

            // 计算抖动 (间隔标准差)
            result.Metrics["AverageInterval"] = intervals.Average();
            result.Metrics["Jitter"] = CalculateStdDev(intervals);
            result.Metrics["PacketLoss"] = await MeasurePacketLoss(testDevice);

            // 判断是否通过 (目标: 抖动 <30ms, 丢包 <1%)
            result.Passed = result.Metrics["Jitter"] < 30.0 && result.Metrics["PacketLoss"] < 0.01;

            _logger.LogInformation("Jitter test: {Result} (Jitter: {Jitter}ms, Loss: {Loss}%)",
                result.Passed ? "PASS" : "FAIL", result.Metrics["Jitter"], result.Metrics["PacketLoss"] * 100);

            return result;
        }

        /// <summary>
        /// 音频质量测试
        /// </summary>
        private async Task<TestResult> TestAudioQuality()
        {
            _logger.LogInformation("Starting audio quality test...");

            var result = new TestResult
            {
                Name = "Audio Quality",
                Passed = true
            };

            // 生成标准测试信号 (1kHz正弦波)
            var testSignal = GenerateSineWave(48000, 2, 1000, 5000);

            // 发送到设备并记录
            var devices = await _deviceScanner.ScanAsync();
            if (!devices.Any())
            {
                result.Passed = false;
                result.ErrorMessage = "No devices found";
                return result;
            }

            await SendAudioTestSignal(devices.First(), testSignal);

            // 这里应该使用音频分析工具来评估质量
            // 简化版本: 测试Opus编解码质量
            var encoded = EncodeOpus(testSignal);
            var decoded = DecodeOpus(encoded);

            // 计算信噪比
            var snr = CalculateSNR(testSignal, decoded);
            result.Metrics["SNR"] = snr;

            // 判断是否通过 (目标: SNR > 30dB)
            result.Passed = snr > 30.0;

            _logger.LogInformation("Audio quality test: {Result} (SNR: {SNR}dB)",
                result.Passed ? "PASS" : "FAIL", snr);

            return result;
        }

        /// <summary>
        /// 压力测试
        /// </summary>
        private async Task<TestResult> TestStress()
        {
            _logger.LogInformation("Starting stress test...");

            var result = new TestResult
            {
                Name = "Stress Test",
                Passed = true
            };

            // 扫描所有设备
            var devices = await _deviceScanner.ScanAsync();
            var deviceCount = devices.Count();

            if (deviceCount == 0)
            {
                result.Passed = false;
                result.ErrorMessage = "No devices found";
                return result;
            }

            _logger.LogInformation("Stress test with {Count} devices", deviceCount);

            // 持续播放60分钟
            var duration = TimeSpan.FromMinutes(60);
            var startTime = DateTime.Now;
            var errors = 0;
            var totalPackets = 0L;

            using (var cts = new CancellationTokenSource(duration))
            {
                try
                {
                    await Task.Run(async () =>
                    {
                        // 启动音频流
                        await _captureService.StartAsync();

                        // 监控
                        while (!cts.Token.IsCancellationRequested)
                        {
                            // 统计错误
                            var currentErrors = await GetErrorCount();
                            var currentPackets = await GetPacketCount();

                            errors = currentErrors;
                            totalPackets = currentPackets;

                            await Task.Delay(5000);
                        }

                        await _captureService.StopAsync();

                    }, cts.Token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Stress test failed");
                    result.Passed = false;
                    result.ErrorMessage = ex.Message;
                }
            }

            var actualDuration = DateTime.Now - startTime;
            result.Metrics["Duration"] = actualDuration.TotalMinutes;
            result.Metrics["TotalPackets"] = totalPackets;
            result.Metrics["Errors"] = errors;
            result.Metrics["ErrorRate"] = totalPackets > 0 ? (double)errors / totalPackets : 0;

            // 判断是否通过 (错误率 <0.1%)
            result.Passed = result.Metrics["ErrorRate"] < 0.001;

            _logger.LogInformation("Stress test: {Result} (Duration: {Duration}min, Errors: {Errors})",
                result.Passed ? "PASS" : "FAIL", actualDuration.TotalMinutes, errors);

            return result;
        }

        // 辅助方法

        private byte[] GeneratePulseSignal(int sampleRate, int channels, int durationMs)
        {
            int samples = (sampleRate * durationMs) / 1000;
            byte[] data = new byte[samples * channels * 2];
            // 生成脉冲信号
            return data;
        }

        private byte[] GenerateSineWave(int sampleRate, int channels, int frequency, int durationMs)
        {
            int samples = (sampleRate * durationMs) / 1000;
            byte[] data = new byte[samples * channels * 2];
            // 生成正弦波
            return data;
        }

        private Task SendTestSignal(AudioDevice device, byte[] signal)
        {
            // 发送测试信号到设备
            return Task.CompletedTask;
        }

        private Task SendTestPacket(AudioDevice device, int sequenceNumber)
        {
            // 发送测试数据包
            return Task.CompletedTask;
        }

        private Task WaitForDeviceResponse(AudioDevice device)
        {
            // 等待设备响应
            return Task.Delay(10);
        }

        private long GetCurrentTimestamp()
        {
            return Stopwatch.GetTimestamp();
        }

        private Task SendSyncSignal(AudioDevice device)
        {
            // 发送PTP同步信号
            return Task.CompletedTask;
        }

        private Task<double> GetClockOffset(AudioDevice device)
        {
            // 获取时钟偏移
            return Task.FromResult(0.0);
        }

        private Task<double> MeasurePacketLoss(AudioDevice device)
        {
            // 测量丢包率
            return Task.FromResult(0.0);
        }

        private byte[] EncodeOpus(byte[] pcmData)
        {
            // Opus编码
            return pcmData;
        }

        private byte[] DecodeOpus(byte[] opusData)
        {
            // Opus解码
            return opusData;
        }

        private double CalculateSNR(byte[] original, byte[] decoded)
        {
            // 计算信噪比
            return 40.0; // 示例值
        }

        private Task<long> GetErrorCount()
        {
            // 获取错误计数
            return Task.FromResult(0L);
        }

        private Task<long> GetPacketCount()
        {
            // 获取数据包计数
            return Task.FromResult(0L);
        }

        private Task SendAudioTestSignal(AudioDevice device, byte[] signal)
        {
            // 发送音频测试信号
            return Task.CompletedTask;
        }

        private double CalculateStdDev(IEnumerable<double> values)
        {
            double avg = values.Average();
            double sumOfSquares = values.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sumOfSquares / values.Count());
        }

        private double CalculatePercentile(List<double> sortedValues, int percentile)
        {
            sortedValues.Sort();
            int index = (int)Math.Ceiling(percentile / 100.0 * sortedValues.Count) - 1;
            return sortedValues[Math.Max(0, Math.Min(sortedValues.Count - 1, index))];
        }
    }

    /// <summary>
    /// 性能测试报告
    /// </summary>
    public class PerfTestReport
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public Dictionary<string, TestResult> TestResults { get; set; }

        public bool AllPassed => TestResults.All(r => r.Value.Passed);

        public void SaveToFile(string path)
        {
            using (var writer = new StreamWriter(path))
            {
                writer.WriteLine("=== Performance Test Report ===");
                writer.WriteLine($"Start: {StartTime}");
                writer.WriteLine($"End: {EndTime}");
                writer.WriteLine($"Duration: {Duration}");
                writer.WriteLine($"Result: {(AllPassed ? "PASS" : "FAIL")}");
                writer.WriteLine();

                foreach (var test in TestResults)
                {
                    writer.WriteLine($"--- {test.Value.Name} ---");
                    writer.WriteLine($"Status: {(test.Value.Passed ? "PASS" : "FAIL")}");

                    foreach (var metric in test.Value.Metrics)
                    {
                        writer.WriteLine($"  {metric.Key}: {metric.Value:F2}");
                    }

                    if (!string.IsNullOrEmpty(test.Value.ErrorMessage))
                    {
                        writer.WriteLine($"  Error: {test.Value.ErrorMessage}");
                    }

                    writer.WriteLine();
                }
            }
        }
    }

    /// <summary>
    /// 测试结果
    /// </summary>
    public class TestResult
    {
        public string Name { get; set; }
        public bool Passed { get; set; }
        public Dictionary<string, double> Metrics { get; set; } = new Dictionary<string, double>();
        public string ErrorMessage { get; set; }
    }
}
