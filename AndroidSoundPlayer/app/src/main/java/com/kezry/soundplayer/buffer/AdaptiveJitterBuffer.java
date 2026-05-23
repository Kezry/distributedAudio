package com.kezry.soundplayer.buffer;

import android.util.Log;
import java.util.concurrent.atomic.AtomicInteger;

/**
 * 自适应抖动缓冲区
 * 根据网络状况动态调整缓冲大小
 */
public class AdaptiveJitterBuffer {

    private static final String TAG = "AdaptiveJitterBuffer";
    private static final int MIN_BUFFER_MS = 40;
    private static final int MAX_BUFFER_MS = 300;
    private static final int TARGET_BUFFER_MS = 100;

    // 缓冲区
    private byte[] ringBuffer;
    private int capacity;
    private int writePos = 0;
    private int readPos = 0;
    private final Object lock = new Object();

    // 网络状况监控
    private NetworkConditionMonitor networkMonitor;

    public AdaptiveJitterBuffer(int initialSize) {
        this.capacity = initialSize * 2;
        this.ringBuffer = new byte[this.capacity];
        this.networkMonitor = new NetworkConditionMonitor();
    }

    /**
     * 写入数据
     */
    public void write(byte[] data) {
        synchronized (lock) {
            int required = data.length;
            int available = capacity - getAvailable();

            if (available < required) {
                // 需要扩展缓冲区
                expandCapacity(required + capacity / 2);
            }

            for (byte b : data) {
                ringBuffer[writePos] = b;
                writePos = (writePos + 1) % capacity;

                // 覆盖旧数据
                if (writePos == readPos) {
                    readPos = (readPos + 1) % capacity;
                }
            }

            // 更新网络监控
            networkMonitor.onPacketReceived(getAvailable());
        }
    }

    /**
     * 读取数据
     */
    public int read(byte[] output) {
        synchronized (lock) {
            int available = getAvailable();

            // 自适应读取大小
            int targetSize = networkMonitor.getOptimalReadSize(available, output.length);

            int toRead = Math.min(targetSize, output.length);
            for (int i = 0; i < toRead; i++) {
                output[i] = ringBuffer[readPos];
                readPos = (readPos + 1) % capacity;
            }

            // 更新网络监控
            networkMonitor.onPacketRead(getAvailable());

            return toRead;
        }
    }

    /**
     * 获取可用数据量
     */
    public int getAvailable() {
        synchronized (lock) {
            return (writePos - readPos + capacity) % capacity;
        }
    }

    /**
     * 扩展缓冲区容量
     */
    private void expandCapacity(int minRequired) {
        int newCapacity = Math.max(capacity * 2, capacity + minRequired);
        byte[] newBuffer = new byte[newCapacity];

        // 复制数据
        int available = getAvailable();
        for (int i = 0; i < available; i++) {
            newBuffer[i] = ringBuffer[(readPos + i) % capacity];
        }

        ringBuffer = newBuffer;
        capacity = newCapacity;
        writePos = available;
        readPos = 0;

        Log.i(TAG, "Buffer expanded: " + capacity + " -> " + newCapacity);
    }

    /**
     * 获取当前缓冲延迟（毫秒）
     */
    public int getBufferLatencyMs() {
        int available = getAvailable();
        return (available * 1000) / (SAMPLE_RATE * 2); // 假设 16-bit 立体声
    }

    /**
     * 获取缓冲区健康状态
     */
    public BufferHealth getBufferHealth() {
        int available = getAvailable();
        int bufferPercent = (available * 100) / capacity;

        if (bufferPercent < 20) {
            return BufferHealth.UNDERFLOW;
        } else if (bufferPercent > 80) {
            return BufferHealth.OVERFLOW;
        } else {
            return BufferHealth.HEALTHY;
        }
    }

    public enum BufferHealth {
        UNDERFLOW,  // 缓冲区接近空，可能产生音频断续
        HEALTHY,    // 缓冲区健康
        OVERFLOW    // 缓冲区接近满，延迟增加
    }

    /**
     * 网络状况监控
     */
    private static class NetworkConditionMonitor {
        private static final int SAMPLE_RATE = 48000;
        private static final int BYTES_PER_MS = SAMPLE_RATE * 2 / 1000; // 立体声

        private long lastPacketTime = System.currentTimeMillis();
        private int packetsLost = 0;
        private int packetsReceived = 0;

        // 抖动计算
        private final long[] jitterHistory = new long[10];
        private int jitterIndex = 0;

        public void onPacketReceived(int available) {
            lastPacketTime = System.currentTimeMillis();
            packetsReceived++;

            // 记录抖动
            long now = System.nanoTime();
            jitterHistory[jitterIndex] = now - lastPacketTime;
            jitterIndex = (jitterIndex + 1) % jitterHistory.length;
        }

        public void onPacketRead(int available) {
            // 检测丢包
            if (available == 0 && (System.currentTimeMillis() - lastPacketTime > 100)) {
                packetsLost++;
            }
        }

        public int getOptimalReadSize(int available, int requested) {
            // 根据网络状况调整读取大小
            double jitter = calculateJitter();
            double lossRate = (double) packetsLost / (packetsReceived + packetsLost + 1);

            // 高抖动或高丢包：减少读取，保持更多缓冲
            if (jitter > 20 || lossRate > 0.05) {
                return Math.max(requested / 2, available / 4);
            }

            return requested;
        }

        private double calculateJitter() {
            double sum = 0;
            int count = 0;

            for (long j : jitterHistory) {
                if (j > 0) {
                    sum += j;
                    count++;
                }
            }

            return count > 0 ? sum / count : 0;
        }

        public double getLossRate() {
            return (double) packetsLost / (packetsReceived + packetsLost + 1);
        }

        public boolean isNetworkHealthy() {
            return getLossRate() < 0.01 && calculateJitter() < 30;
        }
    }
}
