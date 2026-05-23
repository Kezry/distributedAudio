package com.kezry.soundplayer.decoder;

import android.util.Log;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;

/**
 * Opus 解码器
 * 解码 Opus 格式的音频数据为 PCM
 */
public class OpusDecoder {
    private static final String TAG = "OpusDecoder";
    private long nativeDecoderHandle;
    private int sampleRate;
    private int channels;
    private boolean initialized = false;

    static {
        try {
            System.loadLibrary("opus");
            Log.i(TAG, "Opus native library loaded");
        } catch (UnsatisfiedLinkError e) {
            Log.e(TAG, "Failed to load Opus library", e);
        }
    }

    public OpusDecoder(int sampleRate, int channels) {
        this.sampleRate = sampleRate;
        this.channels = channels;
    }

    public boolean init() {
        if (initialized) return true;

        try {
            nativeDecoderHandle = nativeCreate(sampleRate, channels);
            if (nativeDecoderHandle != 0) {
                initialized = true;
                Log.i(TAG, "Opus decoder initialized: " + sampleRate + "Hz, " + channels + " channels");
                return true;
            }
        } catch (Exception e) {
            Log.e(TAG, "Failed to initialize decoder", e);
        }

        return false;
    }

    public byte[] decode(byte[] opusData, int length) {
        if (!initialized) {
            Log.w(TAG, "Decoder not initialized");
            return new byte[0];
        }

        int frameSize = sampleRate / 50; // 20ms frame
        int pcmSize = frameSize * channels * 2; // 16-bit samples
        byte[] pcmData = new byte[pcmSize];

        int decodedSamples = nativeDecode(nativeDecoderHandle, opusData, length, pcmData, pcmSize, 0);

        if (decodedSamples > 0) {
            int actualSize = decodedSamples * channels * 2;
            if (actualSize < pcmSize) {
                byte[] trimmed = new byte[actualSize];
                System.arraycopy(pcmData, 0, trimmed, 0, actualSize);
                return trimmed;
            }
            return pcmData;
        }

        return new byte[0];
    }

    public void release() {
        if (nativeDecoderHandle != 0) {
            nativeDestroy(nativeDecoderHandle);
            nativeDecoderHandle = 0;
            initialized = false;
        }
    }

    private native long nativeCreate(int sampleRate, int channels);
    private native void nativeDestroy(long handle);
    private native int nativeDecode(long handle, byte[] data, int length, byte[] pcm, int pcmSize, int decodeFec);
}
