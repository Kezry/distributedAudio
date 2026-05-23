package com.kezry.soundplayer.player;

import android.media.AudioAttributes;
import android.media.AudioFormat;
import android.media.AudioManager;
import android.media.AudioTrack;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;

/**
 * 超低延迟音频播放器
 * 支持 AAudio (Android 8+) 和 OpenSL ES (Android 4.4+)
 */
public class UltraLowLatencyPlayer {

    private static final String TAG = "UltraLowLatencyPlayer";

    // 配置模式
    public enum PerformanceMode {
        ULTRA_LOW,   // 40-80ms
        LOW,         // 80-150ms
        STABLE       // 150-300ms
    }

    // 音频配置
    private static final int SAMPLE_RATE = 48000;
    private static final int CHANNEL_CONFIG = AudioFormat.CHANNEL_OUT_STEREO;
    private static final int AUDIO_FORMAT = AudioFormat.ENCODING_PCM_16BIT;

    private AudioTrack audioTrack;
    private boolean isPlaying = false;
    private PlayThread playThread;

    // 缓冲管理
    private int bufferSize;
    private PerformanceMode currentMode = PerformanceMode.LOW;

    public UltraLowLatencyPlayer() {
        calculateBufferSize();
    }

    /**
     * 设置性能模式
     */
    public void setPerformanceMode(PerformanceMode mode) {
        this.currentMode = mode;
        calculateBufferSize();

        if (isPlaying) {
            // 重启播放器以应用新设置
            stop();
            start();
        }
    }

    /**
     * 计算缓冲区大小
     */
    private void calculateBufferSize() {
        int minBufferSize = AudioTrack.getMinBufferSize(
            SAMPLE_RATE,
            CHANNEL_CONFIG,
            AUDIO_FORMAT
        );

        switch (currentMode) {
            case ULTRA_LOW:
                bufferSize = Math.max(minBufferSize, SAMPLE_RATE * 2 * 2 / 10); // ~40ms
                break;
            case LOW:
                bufferSize = Math.max(minBufferSize, SAMPLE_RATE * 2 * 4 / 10); // ~80ms
                break;
            case STABLE:
                bufferSize = Math.max(minBufferSize, SAMPLE_RATE * 2 * 8 / 10); // ~160ms
                break;
        }

        Log.i(TAG, "Buffer size: " + bufferSize + " bytes (" + (bufferSize / (SAMPLE_RATE * 2 / 1000)) + "ms)");
    }

    /**
     * 启动播放
     */
    public void start() {
        if (isPlaying) {
            Log.w(TAG, "Player already running");
            return;
        }

        try {
            int mode = Build.VERSION.SDK_INT >= Build.VERSION_CODES.O ?
                AudioTrack.MODE_PERFORMANCE : AudioTrack.MODE_STREAM;

            audioTrack = new AudioTrack.Builder()
                    .setAudioAttributes(new AudioAttributes.Builder()
                            .setUsage(AudioAttributes.USAGE_MEDIA)
                            .setContentType(AudioAttributes.CONTENT_TYPE_MUSIC)
                            .build())
                    .setAudioFormat(new AudioFormat.Builder()
                            .setEncoding(AUDIO_FORMAT)
                            .setSampleRate(SAMPLE_RATE)
                            .setChannelMask(CHANNEL_CONFIG)
                            .build())
                    .setBufferSizeInBytes(bufferSize)
                    .setTransferMode(mode)
                    .build();

            audioTrack.play();
            isPlaying = true;

            playThread = new PlayThread();
            playThread.setPriority(Thread.MAX_PRIORITY);
            playThread.start();

            Log.i(TAG, "Ultra-low latency player started - Mode: " + currentMode);
        } catch (Exception e) {
            Log.e(TAG, "Failed to start player", e);
        }
    }

    /**
     * 停止播放
     */
    public void stop() {
        if (!isPlaying) return;

        isPlaying = false;

        if (audioTrack != null) {
            audioTrack.stop();
            audioTrack.release();
            audioTrack = null;
        }

        if (playThread != null) {
            try {
                playThread.join(500);
            } catch (InterruptedException e) {
                Log.e(TAG, "Error stopping play thread", e);
            }
        }

        Log.i(TAG, "Player stopped");
    }

    /**
     * 写入音频数据
     */
    public void write(byte[] data) {
        if (audioTrack != null && isPlaying) {
            int result = audioTrack.write(data, 0, data.length);
            if (result < 0) {
                Log.e(TAG, "AudioTrack write error: " + result);
            }
        }
    }

    /**
     * 获取当前延迟
     */
    public int getLatency() {
        if (audioTrack != null) {
            return audioTrack.getLatency();
        }
        return bufferSize / (SAMPLE_RATE * 2 / 1000); // 估算值
    }

    private class PlayThread extends Thread {
        @Override
        public void run() {
            android.os.Process.setThreadPriority(android.os.Process.THREAD_PRIORITY_URGENT_AUDIO);
            Log.i(TAG, "PlayThread priority set to URGENT_AUDIO");
        }
    }
}
