package com.kezry.soundplayer.network;

import android.util.Log;
import com.kezry.soundplayer.decoder.OpusDecoder;
import com.kezry.soundplayer.player.AudioPlayer;

import java.io.IOException;
import java.net.DatagramPacket;
import java.net.DatagramSocket;
import java.net.SocketException;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicInteger;

/**
 * 增强的音频接收器
 * 支持RTP解析和Opus解码
 */
public class AudioReceiver {

    private static final String TAG = "AudioReceiver";
    private static final int BUFFER_SIZE = 4096;
    private static final int DEFAULT_PORT = 5004;

    private DatagramSocket socket;
    private AtomicBoolean isRunning = new AtomicBoolean(false);
    private ReceiveThread receiveThread;
    private AudioDataCallback callback;

    // Decoder
    private OpusDecoder opusDecoder;
    private boolean useOpus = true;

    // Statistics
    private AtomicInteger packetsReceived = new AtomicInteger(0);
    private AtomicInteger packetsLost = new AtomicInteger(0);
    private int lastSequenceNumber = -1;

    public interface AudioDataCallback {
        void onAudioDataReceived(byte[] data, int length);
        void onReceiveError(String error);
    }

    public AudioReceiver() {
        opusDecoder = new OpusDecoder(48000, 2);
        opusDecoder.init();
    }

    public void setCallback(AudioDataCallback callback) {
        this.callback = callback;
    }

    public void setUseOpus(boolean use) {
        this.useOpus = use;
    }

    public void start(int port) {
        if (isRunning.get()) {
            Log.w(TAG, "AudioReceiver already running");
            return;
        }

        try {
            socket = new DatagramSocket(port);
            socket.setReuseAddress(true);
            socket.setSoTimeout(5000);
            socket.setReceiveBufferSize(256 * 1024); // 256KB buffer

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

        if (opusDecoder != null) {
            opusDecoder.release();
        }

        Log.i(TAG, "AudioReceiver stopped");
    }

    public boolean isRunning() {
        return isRunning.get();
    }

    public int getPacketsReceived() {
        return packetsReceived.get();
    }

    public int getPacketsLost() {
        return packetsLost.get();
    }

    private class ReceiveThread extends Thread {
        @Override
        public void run() {
            byte[] buffer = new byte[BUFFER_SIZE];
            DatagramPacket packet = new DatagramPacket(buffer, buffer.length);

            while (isRunning.get()) {
                try {
                    socket.receive(packet);
                    packetsReceived.incrementAndGet();

                    // Parse RTP packet
                    RtpPacket rtpPacket = RtpPacket.parse(packet.getData());
                    if (rtpPacket != null) {
                        // Check for packet loss
                        int seqNum = rtpPacket.getSequenceNumber();
                        if (lastSequenceNumber >= 0) {
                            int expectedSeq = (lastSequenceNumber + 1) & 0xFFFF;
                            if (seqNum != expectedSeq) {
                                int lost = (seqNum - expectedSeq + 0x10000) % 0x10000;
                                packetsLost.addAndGet(lost);
                                Log.w(TAG, "Packet loss detected: " + lost + " packets");
                            }
                        }
                        lastSequenceNumber = seqNum;

                        // Process payload
                        byte[] payload = rtpPacket.getPayload();
                        byte[] audioData;

                        if (useOpus) {
                            // Decode Opus
                            audioData = opusDecoder.decode(payload, payload.length);
                        } else {
                            // Raw PCM
                            audioData = payload;
                        }

                        // Send to callback
                        if (callback != null && audioData.length > 0) {
                            callback.onAudioDataReceived(audioData, audioData.length);
                        }
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
