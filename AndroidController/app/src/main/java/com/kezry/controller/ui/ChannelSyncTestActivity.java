package com.kezry.controller.ui;

import android.media.AudioAttributes;
import android.media.AudioFormat;
import android.media.AudioTrack;
import android.os.Bundle;
import android.view.MenuItem;
import android.view.View;
import android.widget.Button;
import android.widget.TextView;

import androidx.appcompat.app.AppCompatActivity;

import com.kezry.controller.R;
import com.kezry.controller.model.AudioDevice;

import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.util.ArrayList;
import java.util.List;

/**
 * 左右声道同步测试工具
 * 用于测试两个设备的声道同步精度
 */
public class ChannelSyncTestActivity extends AppCompatActivity {

    private static final String TAG = "ChannelSyncTest";
    private static final int SAMPLE_RATE = 48000;
    private static final int TEST_DURATION_MS = 3000;

    private AudioDevice leftDevice;
    private AudioDevice rightDevice;

    private AudioTrack audioTrack;
    private boolean isPlaying = false;

    // UI 组件
    private TextView leftDeviceInfo;
    private TextView rightDeviceInfo;
    private Button leftTestButton;
    private Button rightTestButton;
    private Button bothTestButton;
    private Button stopButton;
    private TextView statusText;
    private TextView resultText;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_channel_sync_test);

        // 获取设备数据（假设从Intent传递）
        // leftDevice = (AudioDevice) getIntent().getSerializableExtra("left_device");
        // rightDevice = (AudioDevice) getIntent().getSerializableExtra("right_device");

        setupToolbar();
        setupUI();
    }

    private void setupToolbar() {
        if (getSupportActionBar() != null) {
            getSupportActionBar().setDisplayHomeAsUpEnabled(true);
            getSupportActionBar().setTitle("声道同步测试");
        }
    }

    private void setupUI() {
        leftDeviceInfo = findViewById(R.id.left_device_info);
        rightDeviceInfo = findViewById(R.id.right_device_info);
        leftTestButton = findViewById(R.id.left_test_button);
        rightTestButton = findViewById(R.id.right_test_button);
        bothTestButton = findViewById(R.id.both_test_button);
        stopButton = findViewById(R.id.stop_button);
        statusText = findViewById(R.id.status_text);
        resultText = findViewById(R.id.result_text);

        // 更新设备信息显示
        updateDeviceInfo();

        leftTestButton.setOnClickListener(v -> playTestTone(ChannelType.LEFT));
        rightTestButton.setOnClickListener(v -> playTestTone(ChannelType.RIGHT));
        bothTestButton.setOnClickListener(v -> playTestTone(ChannelType.BOTH));
        stopButton.setOnClickListener(v -> stopPlayback());

        updateButtonState(false);
    }

    private void updateDeviceInfo() {
        if (leftDevice != null) {
            leftDeviceInfo.setText(String.format("左声道设备:\n%s\n延迟: %dms",
                leftDevice.getAlias(), leftDevice.getLatencyMs()));
        }

        if (rightDevice != null) {
            rightDeviceInfo.setText(String.format("右声道设备:\n%s\n延迟: %dms",
                rightDevice.getAlias(), rightDevice.getLatencyMs()));
        }
    }

    private void playTestTone(ChannelType channel) {
        if (isPlaying) {
            stopPlayback();
        }

        statusText.setText("正在播放测试音...");
        resultText.setText("");

        // 生成测试音频数据
        byte[] audioData = generateTestTone(channel, 440, TEST_DURATION_MS);

        // 播放本地测试音
        playLocalAudio(audioData);

        // 发送测试命令到远程设备
        sendTestToDevices(channel);

        updateButtonState(true);
        isPlaying = true;

        // 自动停止
        stopButton.postDelayed(() -> {
            stopPlayback();
            statusText.setText("测试完成");
            resultText.setText("请在设备上检查左右声道是否同时听到声音");
        }, TEST_DURATION_MS + 500);
    }

    private byte[] generateTestTone(ChannelType channel, int frequency, int durationMs) {
        int numSamples = (SAMPLE_RATE * durationMs) / 1000;
        byte[] buffer = new byte[numSamples * 4]; // 16-bit stereo

        for (int i = 0; i < numSamples; i++) {
            double t = (double) i / SAMPLE_RATE;
            double sample = Math.sin(2 * Math.PI * frequency * t) * 0.5; // 50% 音量

            short shortSample = (short) (sample * Short.MAX_VALUE);

            int idx = i * 4;
            if (channel == ChannelType.LEFT || channel == ChannelType.BOTH) {
                buffer[idx] = (byte) (shortSample & 0xFF);
                buffer[idx + 1] = (byte) ((shortSample >> 8) & 0xFF);
            } else {
                buffer[idx] = 0;
                buffer[idx + 1] = 0;
            }

            if (channel == ChannelType.RIGHT || channel == ChannelType.BOTH) {
                buffer[idx + 2] = (byte) (shortSample & 0xFF);
                buffer[idx + 3] = (byte) ((shortSample >> 8) & 0xFF);
            } else {
                buffer[idx + 2] = 0;
                buffer[idx + 3] = 0;
            }
        }

        return buffer;
    }

    private void playLocalAudio(byte[] audioData) {
        int bufferSize = AudioTrack.getMinBufferSize(
            SAMPLE_RATE,
            AudioFormat.CHANNEL_OUT_STEREO,
            AudioFormat.ENCODING_PCM_16BIT
        );

        audioTrack = new AudioTrack.Builder()
            .setAudioAttributes(new AudioAttributes.Builder()
                .setUsage(android.media.AudioAttributes.USAGE_MEDIA)
                .setContentType(android.media.AudioAttributes.CONTENT_TYPE_MUSIC)
                .build())
            .setAudioFormat(new AudioFormat.Builder()
                .setEncoding(AudioFormat.ENCODING_PCM_16BIT)
                .setSampleRate(SAMPLE_RATE)
                .setChannelMask(AudioFormat.CHANNEL_OUT_STEREO)
                .build())
            .setBufferSizeInBytes(bufferSize)
            .setTransferMode(AudioTrack.MODE_STREAM)
            .build();

        audioTrack.write(audioData, 0, audioData.length);
        audioTrack.play();
    }

    private void sendTestToDevices(ChannelType channel) {
        // 发送测试命令到远程设备
        // 使用 HTTP API 或 UDP 命令

        // 左声道设备
        if (leftDevice != null && (channel == ChannelType.LEFT || channel == ChannelType.BOTH)) {
            sendTestCommandToDevice(leftDevice, "left");
        }

        // 右声道设备
        if (rightDevice != null && (channel == ChannelType.RIGHT || channel == ChannelType.BOTH)) {
            sendTestCommandToDevice(rightDevice, "right");
        }
    }

    private void sendTestCommandToDevice(AudioDevice device, String channel) {
        // 使用 DeviceConfigClient 发送测试命令
        // TODO: 实现 HTTP API 调用
    }

    private void stopPlayback() {
        if (audioTrack != null) {
            audioTrack.stop();
            audioTrack.release();
            audioTrack = null;
        }

        isPlaying = false;
        updateButtonState(false);
    }

    private void updateButtonState(boolean playing) {
        leftTestButton.setEnabled(!playing);
        rightTestButton.setEnabled(!playing);
        bothTestButton.setEnabled(!playing);
        stopButton.setEnabled(playing);
    }

    @Override
    public boolean onOptionsItemSelected(MenuItem item) {
        if (item.getItemId() == android.R.id.home) {
            finish();
            return true;
        }
        return super.onOptionsItemSelected(item);
    }

    @Override
    protected void onDestroy() {
        super.onDestroy();
        stopPlayback();
    }

    private enum ChannelType {
        LEFT, RIGHT, BOTH
    }
}
