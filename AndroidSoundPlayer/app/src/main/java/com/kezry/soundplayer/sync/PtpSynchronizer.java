package com.kezry.soundplayer.sync;

import android.util.Log;
import java.io.IOException;
import java.net.DatagramPacket;
import java.net.DatagramSocket;
import java.net.InetAddress;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.util.concurrent.atomic.AtomicBoolean;

/**
 * PTP 时间戳同步协议实现
 * 实现高精度设备间同步（目标 5-20ms 偏差）
 */
public class PtpSynchronizer {

    private static final String TAG = "PtpSynchronizer";
    private static final int SYNC_PORT = 5005;
    private static final int MASTER_TIMEOUT_MS = 5000;

    // PTP 消息类型
    private static final byte SYNC_REQUEST = 0x01;
    private static final byte SYNC_RESPONSE = 0x02;
    private static final byte FOLLOW_UP = 0x03;
    private static final byte DELAY_REQUEST = 0x04;
    private static final byte DELAY_RESPONSE = 0x05;

    private DatagramSocket syncSocket;
    private AtomicBoolean isRunning = new AtomicBoolean(false);
    private PtpThread syncThread;

    // 时钟同步
    private volatile long clockOffset = 0;      // 微秒
    private volatile long networkDelay = 0;      // 微秒
    private volatile long masterClockId = 0;

    // 统计
    private int syncCount = 0;
    private long totalOffset = 0;

    public interface SyncCallback {
        void onSyncUpdate(long offset, long delay);
        void onSyncError(String error);
    }

    private SyncCallback callback;

    public PtpSynchronizer() {
        // 创建时间戳生成器
        masterClockId = System.nanoTime() / 1000; // 微秒
    }

    public void setCallback(SyncCallback callback) {
        this.callback = callback;
    }

    /**
     * 启动 PTP 同步服务
     */
    public void start() {
        if (isRunning.get()) {
            Log.w(TAG, "PTP already running");
            return;
        }

        try {
            syncSocket = new DatagramSocket(SYNC_PORT);
            syncSocket.setSoTimeout(MASTER_TIMEOUT_MS);
            isRunning.set(true);
            syncThread = new PtpThread();
            syncThread.start();

            Log.i(TAG, "PTP synchronizer started on port " + SYNC_PORT);
        } catch (IOException e) {
            Log.e(TAG, "Failed to start PTP synchronizer", e);
            if (callback != null) {
                callback.onSyncError("Failed to start: " + e.getMessage());
            }
        }
    }

    /**
     * 停止 PTP 同步服务
     */
    public void stop() {
        if (!isRunning.get()) return;

        isRunning.set(false);

        if (syncSocket != null && !syncSocket.isClosed()) {
            syncSocket.close();
        }

        if (syncThread != null) {
            try {
                syncThread.join(1000);
            } catch (InterruptedException e) {
                Log.e(TAG, "Error stopping sync thread", e);
            }
        }

        Log.i(TAG, "PTP synchronizer stopped");
    }

    /**
     * 获取精确时间戳（已补偿偏移）
     */
    public long getPreciseTime() {
        return System.nanoTime() / 1000 + clockOffset;
    }

    /**
     * 获取时钟偏移
     */
    public long getClockOffset() {
        return clockOffset;
    }

    /**
     * 获取网络延迟
     */
    public long getNetworkDelay() {
        return networkDelay;
    }

    /**
     * PTP 同步线程
     */
    private class PtpThread extends Thread {
        @Override
        public void run() {
            byte[] buffer = new byte[1024];
            DatagramPacket packet = new DatagramPacket(buffer, buffer.length);

            while (isRunning.get()) {
                try {
                    syncSocket.receive(packet);
                    processPtpMessage(packet);
                } catch (IOException e) {
                    if (isRunning.get()) {
                        Log.e(TAG, "Error receiving PTP message", e);
                    }
                }
            }
        }
    }

    /**
     * 处理 PTP 消息
     */
    private void processPtpMessage(DatagramPacket packet) {
        try {
            byte[] data = packet.getData();
            int length = packet.getLength();

            if (length < 9) return;

            byte messageType = data[0];

            switch (messageType) {
                case SYNC_REQUEST:
                    handleSyncRequest(packet);
                    break;

                case DELAY_REQUEST:
                    handleDelayRequest(packet);
                    break;

                default:
                    Log.w(TAG, "Unknown PTP message type: " + messageType);
            }
        } catch (Exception e) {
            Log.e(TAG, "Error processing PTP message", e);
        }
    }

    /**
     * 处理同步请求
     */
    private void handleSyncRequest(DatagramPacket requestPacket) {
        try {
            byte[] data = requestPacket.getData();
            int length = requestPacket.getLength();

            // 解析 T1
            long t1 = readLong(data, 1);

            // T2 - 接收时间
            long t2 = getPreciseTime();

            // 发送 Sync Response (T2, T1)
            byte[] response = new byte[17];
            response[0] = SYNC_RESPONSE;
            writeLong(response, 1, t2);      // T2
            writeLong(response, 9, t1);      // T1

            DatagramPacket responsePacket = new DatagramPacket(
                response,
                response.length,
                requestPacket.getAddress(),
                requestPacket.getPort()
            );
            syncSocket.send(responsePacket);

        } catch (Exception e) {
            Log.e(TAG, "Error handling sync request", e);
        }
    }

    /**
     * 处理延迟请求
     */
    private void handleDelayRequest(DatagramPacket requestPacket) {
        try {
            // T4 - 接收时间
            long t4 = getPreciseTime();

            // 发送 Delay Response (T4)
            byte[] response = new byte[9];
            response[0] = DELAY_RESPONSE;
            writeLong(response, 1, t4);

            DatagramPacket responsePacket = new DatagramPacket(
                response,
                response.length,
                requestPacket.getAddress(),
                requestPacket.getPort()
            );
            syncSocket.send(responsePacket);

        } catch (Exception e) {
            Log.e(TAG, "Error handling delay request", e);
        }
    }

    /**
     * 执行时钟偏移测量（从客户端角度）
     */
    public void measureClockOffset(String serverIp, int serverPort) {
        new Thread(() -> {
            try {
                DatagramSocket clientSocket = new DatagramSocket();
                clientSocket.setSoTimeout(5000);

                // T1
                long t1 = getPreciseTime();

                // 发送 Sync Request
                byte[] request = new byte[9];
                request[0] = SYNC_REQUEST;
                writeLong(request, 1, t1);

                DatagramPacket requestPacket = new DatagramPacket(
                    request,
                    request.length,
                    InetAddress.getByName(serverIp),
                    serverPort
                );

                long startTime = System.nanoTime();
                clientSocket.send(requestPacket);

                // 等待 Sync Response
                byte[] buffer = new byte[1024];
                DatagramPacket responsePacket = new DatagramPacket(buffer, buffer.length);
                clientSocket.receive(responsePacket);

                long rtt = System.nanoTime() - startTime;

                if (responsePacket.getLength() >= 17) {
                    byte[] data = responsePacket.getData();

                    // T2
                    long t2 = readLong(data, 1);
                    // T1 (echo back)
                    long t1Echo = readLong(data, 9);

                    // T4
                    long t4 = getPreciseTime();

                    // PTP 计算: offset = ((t2 - t1) - (t4 - t3)) / 2
                    // 这里简化为往返延迟模型: offset = ((t2 - t1) - (t4 - t2)) / 2
                    long oneWayDelay = (t4 - t1) / 2;
                    long currentOffset = t2 - t1 - oneWayDelay;
                    long currentDelay = t4 - t1;

                    // 更新统计
                    syncCount++;
                    totalOffset += currentOffset;

                    // 移动平均
                    clockOffset = totalOffset / syncCount;
                    networkDelay = currentDelay;

                    Log.i(TAG, String.format("Sync: offset=%dms, delay=%dms, rtt=%dms",
                        clockOffset / 1000, networkDelay / 1000, rtt / 1000000));

                    if (callback != null) {
                        callback.onSyncUpdate(clockOffset, networkDelay);
                    }
                }

                clientSocket.close();
            } catch (Exception e) {
                Log.e(TAG, "Clock offset measurement failed", e);
                if (callback != null) {
                    callback.onSyncError("Measurement failed: " + e.getMessage());
                }
            }
        }).start();
    }

    /**
     * 写入 long 到字节数组 (大端序)
     */
    private void writeLong(byte[] buffer, int offset, long value) {
        buffer[offset] = (byte)(value >> 56);
        buffer[offset + 1] = (byte)(value >> 48);
        buffer[offset + 2] = (byte)(value >> 40);
        buffer[offset + 3] = (byte)(value >> 32);
        buffer[offset + 4] = (byte)(value >> 24);
        buffer[offset + 5] = (byte)(value >> 16);
        buffer[offset + 6] = (byte)(value >> 8);
        buffer[offset + 7] = (byte)value;
    }

    /**
     * 从字节数组读取 long (大端序)
     */
    private long readLong(byte[] buffer, int offset) {
        return ((long)buffer[offset] & 0xff) << 56 |
               ((long)buffer[offset + 1] & 0xff) << 48 |
               ((long)buffer[offset + 2] & 0xff) << 40 |
               ((long)buffer[offset + 3] & 0xff) << 32 |
               ((long)buffer[offset + 4] & 0xff) << 24 |
               ((long)buffer[offset + 5] & 0xff) << 16 |
               ((long)buffer[offset + 6] & 0xff) << 8 |
               (long)(buffer[offset + 7] & 0xff);
    }
}
