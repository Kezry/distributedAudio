package com.kezry.controller.ui;

import android.app.AlertDialog;
import android.content.Context;
import android.view.LayoutInflater;
import android.view.View;
import android.widget.ArrayAdapter;
import android.widget.Button;
import android.widget.Spinner;
import android.widget.TextView;
import android.widget.Toast;

import com.kezry.controller.R;
import com.kezry.controller.model.AudioDevice;
import com.kezry.controller.network.DeviceConfigClient;

import java.util.List;

/**
 * 批量配置对话框
 * 支持对多个设备统一配置
 */
public class BatchConfigDialog {

    private static final String TAG = "BatchConfigDialog";

    private Context context;
    private List<AudioDevice> devices;
    private DeviceConfigClient configClient;

    private AlertDialog dialog;
    private Spinner modeSpinner;
    private Spinner channelSpinner;
    private Button applyButton;
    private Button cancelButton;
    private TextView statusText;

    private int appliedCount = 0;
    private int totalCount = 0;

    public BatchConfigDialog(Context context, List<AudioDevice> devices) {
        this.context = context;
        this.devices = devices;
        this.configClient = new DeviceConfigClient(context);
    }

    public void show() {
        AlertDialog.Builder builder = new AlertDialog.Builder(context);
        LayoutInflater inflater = LayoutInflater.from(context);
        View view = inflater.inflate(R.layout.dialog_batch_config, null);

        setupUI(view);
        builder.setView(view);
        builder.setCancelable(false);

        dialog = builder.create();
        dialog.show();
    }

    private void setupUI(View view) {
        TextView titleText = view.findViewById(R.id.title_text);
        titleText.setText(String.format("批量配置 (%d 台设备)", devices.size()));

        modeSpinner = view.findViewById(R.id.mode_spinner);
        channelSpinner = view.findViewById(R.id.channel_spinner);
        applyButton = view.findViewById(R.id.apply_button);
        cancelButton = view.findViewById(R.id.cancel_button);
        statusText = view.findViewById(R.id.status_text);

        // 模式选项
        String[] modes = {"保持不变", "声卡模式", "DLNA单机", "DLNA多机"};
        ArrayAdapter<String> modeAdapter = new ArrayAdapter<>(context, android.R.layout.simple_spinner_item, modes);
        modeAdapter.setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item);
        modeSpinner.setAdapter(modeAdapter);

        // 声道选项
        String[] channels = {"保持不变", "立体声", "左声道", "右声道"};
        ArrayAdapter<String> channelAdapter = new ArrayAdapter<>(context, android.R.layout.simple_spinner_item, channels);
        channelAdapter.setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item);
        channelSpinner.setAdapter(channelAdapter);

        applyButton.setOnClickListener(v -> applyBatchConfig());
        cancelButton.setOnClickListener(v -> dialog.dismiss());
    }

    private void applyBatchConfig() {
        applyButton.setEnabled(false);
        cancelButton.setEnabled(false);
        statusText.setText("正在应用配置...");

        totalCount = devices.size();
        appliedCount = 0;

        int modePos = modeSpinner.getSelectedItemPosition();
        int channelPos = channelSpinner.getSelectedItemPosition();

        String mode = null;
        String channel = null;

        if (modePos > 0) {
            mode = getModeString(modePos - 1);
        }
        if (channelPos > 0) {
            channel = getChannelString(channelPos - 1);
        }

        for (AudioDevice device : devices) {
            String targetMode = (mode != null) ? mode : device.getMode();
            String targetChannel = (channel != null) ? channel : device.getChannel();

            configClient.setConfig(device.getIpAddress(), targetMode, targetChannel,
                device.getAlias(), false, 0, new DeviceConfigClient.ConfigCallback() {
                    @Override
                    public void onSuccess() {
                        appliedCount++;
                        updateProgress();
                    }

                    @Override
                    public void onError(String error) {
                        appliedCount++; // 即使失败也继续
                        updateProgress();
                    }
                });
        }
    }

    private String getModeString(int index) {
        switch (index) {
            case 0: return "soundcard";
            case 1: return "dlna_single";
            case 2: return "dlna_multi";
            default: return "soundcard";
        }
    }

    private String getChannelString(int index) {
        switch (index) {
            case 0: return "stereo";
            case 1: return "left";
            case 2: return "right";
            default: return "stereo";
        }
    }

    private void updateProgress() {
        if (totalCount == 0) return;

        final int progress = (appliedCount * 100) / totalCount;

        context.runOnUiThread(() -> {
            statusText.setText(String.format("应用中... %d/%d (%d%%)", appliedCount, totalCount, progress));

            if (appliedCount >= totalCount) {
                applyButton.setEnabled(true);
                cancelButton.setEnabled(true);
                statusText.setText(String.format("完成！已配置 %d 台设备", totalCount));

                new android.os.Handler().postDelayed(() -> {
                    dialog.dismiss();
                }, 1500);
            }
        });
    }

    public void release() {
        if (configClient != null) {
            configClient.release();
        }
    }
}
