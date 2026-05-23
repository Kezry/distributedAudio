package com.kezry.soundplayer.player;

import android.media.AudioAttributes;
import android.media.AudioFormat;
import android.media.AudioManager;
import android.media.AudioTrack;
import android.util.Log;

import java.nio.ByteBuffer;

/**
 * 低延迟音频播放器
 * 使用AudioTrack实现，支持PCM格式
 */
public class AudioPlayer {

    private static final String TAG = "AudioPlayer";

    // Audio configuration
    private static final int SAMPLE_RATE = 48000;
    private static final int CHANNEL_CONFIG = AudioFormat.CHANNEL_OUT_STEREO;
    private static final int AUDIO_FORMAT = AudioFormat.ENCODING_PCM_16BIT;
    private static final int BUFFER_SIZE_FACTOR = 2;

    private AudioTrack audioTrack;
    private int bufferSize;
    private boolean isPlaying = false;
    private PlayThread playThread;

    // Jitter buffer
    private JitterBuffer jitterBuffer;

    public AudioPlayer() {
        bufferSize = AudioTrack.getMinBufferSize(
                SAMPLE_RATE,
                CHANNEL_CONFIG,
                AUDIO_FORMAT
        ) * BUFFER_SIZE_FACTOR;

        jitterBuffer = new JitterBuffer(bufferSize);
    }

    public void start() {
        if (isPlaying) {
            Log.w(TAG, "AudioPlayer already playing");
            return;
        }

        try {
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
                    .setTransferMode(AudioTrack.MODE_STREAM)
                    .build();

            audioTrack.play();
            isPlaying = true;

            playThread = new PlayThread();
            playThread.start();

            Log.i(TAG, "AudioPlayer started");
        } catch (Exception e) {
            Log.e(TAG, "Failed to start AudioPlayer", e);
        }
    }

    public void stop() {
        if (!isPlaying) {
            return;
        }

        isPlaying = false;

        if (audioTrack != null) {
            audioTrack.stop();
            audioTrack.release();
            audioTrack = null;
        }

        if (playThread != null) {
            try {
                playThread.join(1000);
            } catch (InterruptedException e) {
                Log.e(TAG, "Error stopping play thread", e);
            }
        }

        Log.i(TAG, "AudioPlayer stopped");
    }

    public void writeAudioData(byte[] data) {
        jitterBuffer.write(data);
    }

    public int getLatency() {
        if (audioTrack != null) {
            return audioTrack.getLatency();
        }
        return 0;
    }

    private class PlayThread extends Thread {
        @Override
        public void run() {
            byte[] buffer = new byte[bufferSize];

            while (isPlaying) {
                int bytesRead = jitterBuffer.read(buffer);
                if (bytesRead > 0 && audioTrack != null) {
                    int result = audioTrack.write(buffer, 0, bytesRead);
                    if (result < 0) {
                        Log.e(TAG, "AudioTrack write error: " + result);
                    }
                } else {
                    try {
                        Thread.sleep(10);
                    } catch (InterruptedException e) {
                        break;
                    }
                }
            }
        }
    }

    /**
     * 抖动缓冲区
     * 平滑网络延迟波动
     */
    private static class JitterBuffer {
        private final ByteBuffer buffer;
        private final int capacity;
        private int writePos = 0;
        private int readPos = 0;
        private final Object lock = new Object();

        public JitterBuffer(int size) {
            this.capacity = size;
            this.buffer = ByteBuffer.allocate(size);
        }

        public void write(byte[] data) {
            synchronized (lock) {
                for (byte b : data) {
                    buffer.put(writePos, b);
                    writePos = (writePos + 1) % capacity;

                    // Overwrite if buffer full
                    if (writePos == readPos) {
                        readPos = (readPos + 1) % capacity;
                    }
                }
            }
        }

        public int read(byte[] output) {
            synchronized (lock) {
                int available = (writePos - readPos + capacity) % capacity;
                if (available == 0) {
                    return 0;
                }

                int toRead = Math.min(available, output.length);
                for (int i = 0; i < toRead; i++) {
                    output[i] = buffer.get(readPos);
                    readPos = (readPos + 1) % capacity;
                }

                return toRead;
            }
        }

        public int getAvailable() {
            synchronized (lock) {
                return (writePos - readPos + capacity) % capacity;
            }
        }
    }
}
