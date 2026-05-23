package com.kezry.soundplayer.sync;

import android.util.Log;
import java.net.DatagramPacket;
import java.net.DatagramSocket;
import java.net.InetAddress;
import java.util.concurrent.atomic.AtomicBoolean;

/**
 * PTP 时间戳同步
 * 实现多设备音频同步
 */
public class PtpSync {

    private static final String TAG = "PtpSync";
    private static final int SYNC_PORT = 5005;

    private DatagramSocket syncSocket;
    private AtomicBoolean isRunning = new AtomicBoolean(false);
    private SyncThread syncThread;

    private long clockOffset = 0;
    private long networkLatency = 0;

    public interface SyncCallback {
        void onSyncComplete(long offset, long latency);
        void onSyncError(String error);
    }

    private SyncCallback callback;

    public void setCallback(SyncCallback callback) {
        this.callback = callback;
    }

    public void start() {
        if (isRunning.get()) {
            Log.w(TAG, "PTP sync already running");
            return;
        }

        try {
            syncSocket = new DatagramSocket(SYNC_PORT);
            isRunning.set(true);
            syncThread = new SyncThread();
            syncThread.start();

            Log.i(TAG, "PTP sync started on port " + SYNC_PORT);
        } catch (Exception e) {
            Log.e(TAG, "Failed to start PTP sync", e);
            if (callback != null) {
                callback.onSyncError("Failed to start: " + e.getMessage());
            }
        }
    }

    public void stop() {
        if (!isRunning.get()) {
            return;
        }

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

        Log.i(TAG, "PTP sync stopped");
    }

    public boolean isRunning() {
        return isRunning.get();
    }

    public long getClockOffset() {
        return clockOffset;
    }

    public long getNetworkLatency() {
        return networkLatency;
    }

    public long getPreciseTime() {
        return System.nanoTime() / 1000 + clockOffset; // microseconds
    }

    private class SyncThread extends Thread {
        @Override
        public void run() {
            byte[] buffer = new byte[1024];
            DatagramPacket packet = new DatagramPacket(buffer, buffer.length);

            while (isRunning.get()) {
                try {
                    syncSocket.receive(packet);
                    processSyncPacket(packet);
                } catch (Exception e) {
                    if (isRunning.get()) {
                        Log.e(TAG, "Error receiving sync packet", e);
                    }
                }
            }
        }

        private void processSyncPacket(DatagramPacket packet) {
            try {
                byte[] data = packet.getData();
                int length = packet.getLength();

                // Parse sync request (simplified)
                long t1 = getPreciseTime();

                // Send sync response
                byte[] response = new byte[16];
                // t2 (timestamp of response)
                long t2 = getPreciseTime();
                writeLong(response, 0, t2);

                DatagramPacket responsePacket = new DatagramPacket(
                    response,
                    response.length,
                    packet.getAddress(),
                    packet.getPort()
                );
                syncSocket.send(responsePacket);

            } catch (Exception e) {
                Log.e(TAG, "Error processing sync packet", e);
            }
        }

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

        private long readLong(byte[] buffer, int offset) {
            return ((long)buffer[offset] << 56) |
                   ((long)(buffer[offset + 1] & 0xFF) << 48) |
                   ((long)(buffer[offset + 2] & 0xFF) << 40) |
                   ((long)(buffer[offset + 3] & 0xFF) << 32) |
                   ((long)(buffer[offset + 4] & 0xFF) << 24) |
                   ((long)(buffer[offset + 5] & 0xFF) << 16) |
                   ((long)(buffer[offset + 6] & 0xFF) << 8) |
                   (long)(buffer[offset + 7] & 0xFF);
        }
    }
}
