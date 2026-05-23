package com.kezry.soundplayer.network;

import android.util.Log;

import java.io.IOException;
import java.net.DatagramPacket;
import java.net.DatagramSocket;
import java.net.SocketException;
import java.util.concurrent.atomic.AtomicBoolean;

/**
 * 自定义协议音频接收器
 * 使用UDP接收音频流，支持Opus/PCM格式
 */
public class AudioReceiver {

    private static final String TAG = "AudioReceiver";
    private static final int BUFFER_SIZE = 4096;
    private static final int DEFAULT_PORT = 5004;

    private DatagramSocket socket;
    private AtomicBoolean isRunning = new AtomicBoolean(false);
    private ReceiveThread receiveThread;
    private AudioDataCallback callback;

    public interface AudioDataCallback {
        void onAudioDataReceived(byte[] data, int length);
        void onReceiveError(String error);
    }

    public void setCallback(AudioDataCallback callback) {
        this.callback = callback;
    }

    public void start(int port) {
        if (isRunning.get()) {
            Log.w(TAG, "AudioReceiver already running");
            return;
        }

        try {
            socket = new DatagramSocket(port);
            socket.setReuseAddress(true);
            socket.setSoTimeout(5000); // 5 second timeout

            isRunning.set(true);
            receiveThread = new ReceiveThread();
            receiveThread.start();

            Log.i(TAG, "AudioReceiver started on port " + port);
        } catch (SocketException e) {
            Log.e(TAG, "Failed to start AudioReceiver", e);
            if (callback != null) {
                callback.onReceiveError("Failed to start: " + e.getMessage());
            }
        }
    }

    public void start() {
        start(DEFAULT_PORT);
    }

    public void stop() {
        if (!isRunning.get()) {
            return;
        }

        isRunning.set(false);

        if (socket != null && !socket.isClosed()) {
            socket.close();
        }

        if (receiveThread != null) {
            try {
                receiveThread.join(1000);
            } catch (InterruptedException e) {
                Log.e(TAG, "Error stopping receive thread", e);
            }
        }

        Log.i(TAG, "AudioReceiver stopped");
    }

    public boolean isRunning() {
        return isRunning.get();
    }

    private class ReceiveThread extends Thread {
        @Override
        public void run() {
            byte[] buffer = new byte[BUFFER_SIZE];
            DatagramPacket packet = new DatagramPacket(buffer, buffer.length);

            while (isRunning.get()) {
                try {
                    socket.receive(packet);

                    if (callback != null) {
                        byte[] data = new byte[packet.getLength()];
                        System.arraycopy(packet.getData(), packet.getOffset(), data, 0, packet.getLength());
                        callback.onAudioDataReceived(data, packet.getLength());
                    }
                } catch (IOException e) {
                    if (isRunning.get()) {
                        Log.e(TAG, "Receive error", e);
                        if (callback != null) {
                            callback.onReceiveError("Receive error: " + e.getMessage());
                        }
                    }
                }
            }
        }
    }
}
