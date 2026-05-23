package com.kezry.controller.ui;

import android.os.Bundle;
import android.view.MenuItem;
import android.view.View;
import android.widget.ArrayAdapter;
import android.widget.Button;
import android.widget.Spinner;
import android.widget.Switch;
import android.widget.TextView;

import androidx.appcompat.app.AppCompatActivity;

import com.kezry.controller.R;
import com.kezry.controller.model.AudioDevice;
import com.kezry.controller.network.DeviceConfigClient;

/**
 * 设备详情页面
 * 显示设备详细信息并支持配置
 */
public class DeviceDetailActivity extends AppCompatActivity {

    private static final String TAG = "DeviceDetailActivity";

    public static final String EXTRA_DEVICE = "device";

    private AudioDevice device;
    private DeviceConfigClient configClient;

    // UI 组件
    private TextView deviceIdText;
    private TextView deviceAliasText;
    private TextView deviceIpText;
    private TextView deviceMacText;
    private TextView latencyText;
    private TextView rssiText;
    private TextView packetLossText;
    private TextView bufferLevelText;

    private Spinner modeSpinner;
    private Spinner channelSpinner;
    private Switch dlnaGroupSwitch;
    private TextView delayOffsetText;

    private Button applyButton;
    private Button testSoundButton;
    private Button syncTestButton;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_device_detail);

        // 获取设备数据
        device = (AudioDevice) getIntent().getSerializableExtra(EXTRA_DEVICE);
        if (device == null) {
            finish();
            return;
        }

        configClient = new DeviceConfigClient(this);

        setupToolbar();
        setupUI();
        loadDeviceInfo();
    }

    private void setupToolbar() {
        if (getSupportActionBar() != null) {
            getSupportActionBar().setDisplayHomeAsUpEnabled(true);
            getSupportActionBar().setTitle(device.getAlias());
        }
    }

    private void setupUI() {
        // 设备信息
        deviceIdText = findViewById(R.id.device_id);
        deviceAliasText = findViewById(R.id.device_alias);
        deviceIpText = findViewById(R.id.device_ip);
        deviceMacText = findViewById(R.id.device_mac);

        // 状态信息
        latencyText = findViewById(R.id.latency_value);
        rssiText = findViewById(R.id.rssi_value);
        packetLossText = findViewById(R.id.packet_loss_value);
        bufferLevelText = findViewById(R.id.buffer_level_value);

        // 配置控件
        modeSpinner = findViewById(R.id.mode_spinner);
        channelSpinner = findViewById(R.id.channel_spinner);
        dlnaGroupSwitch = findViewById(R.id.dlna_group_switch);
        delayOffsetText = findViewById(R.id.delay_offset_text);

        // 按钮
        applyButton = findViewById(R.id.apply_button);
        testSoundButton = findViewById(R.id.test_sound_button);
        syncTestButton = findViewById(R.id.sync_test_button);

        // 设置模式下拉框
        String[] modes = {"声卡模式", "DLNA单机", "DLNA多机"};
        ArrayAdapter<String> modeAdapter = new ArrayAdapter<>(this, android.R.layout.simple_spinner_item, modes);
        modeAdapter.setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item);
        modeSpinner.setAdapter(modeAdapter);

        // 设置声道上拉框
        String[] channels = {"立体声", "左声道", "右声道"};
        ArrayAdapter<String> channelAdapter = new ArrayAdapter<>(this, android.R.layout.simple_spinner_item, channels);
        channelAdapter.setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item);
        channelSpinner.setAdapter(channelAdapter);

        // 按钮事件
        applyButton.setOnClickListener(this::onApplyConfig);
        testSoundButton.setOnClickListener(this::onTestSound);
        syncTestButton.setOnClickListener(this::onSyncTest);

        // 延迟调整
        findViewById(R.id.delay_decrease_button).setOnClickListener(v -> adjustDelay(-10));
        findViewById(R.id.delay_increase_button).setOnClickListener(v -> adjustDelay(10));
    }

    private void loadDeviceInfo() {
        deviceIdText.setText(device.getDeviceId());
        deviceAliasText.setText(device.getAlias());
        deviceIpText.setText(device.getIpAddress());
        deviceMacText.setText(device.getMacAddress());

        // 从设备状态获取实时信息
        updateDeviceStatus();
    }

    private void updateDeviceStatus() {
        latencyText.setText(device.getLatencyMs() + " ms");
        rssiText.setText(device.getRssi() + " dBm");
        packetLossText.setText(String.format("%.1f%%", device.getPacketLossRate() * 100));
        bufferLevelText.setText(device.getBufferLevel() + "%");

        // 更新配置
        int modeIndex = getModeIndex(device.getMode());
        modeSpinner.setSelection(modeIndex);

        int channelIndex = getChannelIndex(device.getChannel());
        channelSpinner.setSelection(channelIndex);

        dlnaGroupSwitch.setChecked(device.isDlnaGroupEnabled());
        delayOffsetText.setText(String.valueOf(device.getDlnaDelayMs()));
    }

    private int getModeIndex(String mode) {
        switch (mode) {
            case "soundcard": return 0;
            case "dlna_single": return 1;
            case "dlna_multi": return 2;
            default: return 0;
        }
    }

    private int getChannelIndex(String channel) {
        switch (channel) {
            case "stereo": return 0;
            case "left": return 1;
            case "right": return 2;
            default: return 0;
        }
    }

    private String getSelectedMode() {
        int pos = modeSpinner.getSelectedItemPosition();
        switch (pos) {
            case 0: return "soundcard";
            case 1: return "dlna_single";
            case 2: return "dlna_multi";
            default: return "soundcard";
        }
    }

    private String getSelectedChannel() {
        int pos = channelSpinner.getSelectedItemPosition();
        switch (pos) {
            case 0: return "stereo";
            case 1: return "left";
            case 2: return "right";
            default: return "stereo";
        }
    }

    private void adjustDelay(int delta) {
        try {
            int currentDelay = Integer.parseInt(delayOffsetText.getText().toString());
            int newDelay = Math.max(-200, Math.min(200, currentDelay + delta));
            delayOffsetText.setText(String.valueOf(newDelay));
        } catch (NumberFormatException e) {
            delayOffsetText.setText("0");
        }
    }

    private void onApplyConfig(View v) {
        applyButton.setEnabled(false);
        applyButton.setText("应用中...");

        String newMode = getSelectedMode();
        String newChannel = getSelectedChannel();
        boolean dlnaGroup = dlnaGroupSwitch.isChecked();
        int delayMs = Integer.parseInt(delayOffsetText.getText().toString());

        configClient.setConfig(device.getIpAddress(), newMode, newChannel, device.getAlias(),
            dlnaGroup, delayMs, new DeviceConfigClient.ConfigCallback() {
                @Override
                public void onSuccess() {
                    runOnUiThread(() -> {
                        applyButton.setEnabled(true);
                        applyButton.setText("应用");
                        device.setMode(newMode);
                        device.setChannel(newChannel);
                        device.setDlnaGroupEnabled(dlnaGroup);
                        device.setDlnaDelayMs(delayMs);
                        updateDeviceStatus();
                    });
                }

                @Override
                public void onError(String error) {
                    runOnUiThread(() -> {
                        applyButton.setEnabled(true);
                        applyButton.setText("应用");
                    });
                }
            });
    }

    private void onTestSound(View v) {
        configClient.sendTestCommand(device.getIpAddress(), new DeviceConfigClient.ConfigCallback() {
            @Override
            public void onSuccess() {
                // 测试命令已发送
            }

            @Override
            public void onError(String error) {
                // 处理错误
            }
        });
    }

    private void onSyncTest(View v) {
        configClient.startCalibration(device.getIpAddress(), new DeviceConfigClient.ConfigCallback() {
            @Override
            public void onSuccess() {
                // 校准已启动
            }

            @Override
            public void onError(String error) {
                // 处理错误
            }
        });
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
        if (configClient != null) {
            configClient.release();
        }
    }
}
