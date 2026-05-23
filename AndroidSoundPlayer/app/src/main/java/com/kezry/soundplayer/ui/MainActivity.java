package com.kezry.soundplayer.ui;

import android.Manifest;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.os.Build;
import android.os.Bundle;
import android.widget.Button;
import android.widget.TextView;
import androidx.annotation.NonNull;
import androidx.appcompat.app.AppCompatActivity;
import androidx.core.app.ActivityCompat;
import androidx.core.content.ContextCompat;
import com.kezry.soundplayer.DeviceIdentity;
import com.kezry.soundplayer.R;
import com.kezry.soundplayer.service.AudioReceiverService;

/**
 * 主界面
 */
public class MainActivity extends AppCompatActivity {

    private static final String TAG = "MainActivity";
    private static final int REQUEST_PERMISSIONS = 100;

    private TextView statusText;
    private TextView deviceInfoText;
    private Button startButton;
    private Button stopButton;

    private boolean isRunning = false;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        initViews();
        checkPermissions();
        updateDeviceInfo();
    }

    private void initViews() {
        statusText = findViewById(R.id.statusText);
        deviceInfoText = findViewById(R.id.deviceInfoText);
        startButton = findViewById(R.id.startButton);
        stopButton = findViewById(R.id.stopButton);

        startButton.setOnClickListener(v -> startService());
        stopButton.setOnClickListener(v -> stopService());

        updateStatus("就绪");
    }

    private void checkPermissions() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
            String[] permissions = {
                Manifest.permission.RECORD_AUDIO,
                Manifest.permission.ACCESS_WIFI_STATE,
                Manifest.permission.CHANGE_WIFI_MULTICAST_STATE,
                Manifest.permission.ACCESS_NETWORK_STATE
            };

            boolean needsPermission = false;
            for (String permission : permissions) {
                if (ContextCompat.checkSelfPermission(this, permission) != PackageManager.PERMISSION_GRANTED) {
                    needsPermission = true;
                    break;
                }
            }

            if (needsPermission) {
                ActivityCompat.requestPermissions(this, permissions, REQUEST_PERMISSIONS);
            }
        }
    }

    @Override
    public void onRequestPermissionsResult(int requestCode, @NonNull String[] permissions, @NonNull int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);

        if (requestCode == REQUEST_PERMISSIONS) {
            boolean allGranted = true;
            for (int result : grantResults) {
                if (result != PackageManager.PERMISSION_GRANTED) {
                    allGranted = false;
                    break;
                }
            }

            if (!allGranted) {
                updateStatus("需要权限才能运行");
            }
        }
    }

    private void startService() {
        if (isRunning) return;

        Intent intent = new Intent(this, AudioReceiverService.class);
        startForegroundService(intent);

        isRunning = true;
        updateStatus("正在接收音频...");
        startButton.setEnabled(false);
        stopButton.setEnabled(true);
    }

    private void stopService() {
        if (!isRunning) return;

        Intent intent = new Intent(this, AudioReceiverService.class);
        stopService(intent);

        isRunning = false;
        updateStatus("已停止");
        startButton.setEnabled(true);
        stopButton.setEnabled(false);
    }

    private void updateDeviceInfo() {
        String info = "设备信息:\n" +
            "名称: " + DeviceIdentity.getDeviceAlias() + "\n" +
            "UUID: " + DeviceIdentity.getDeviceUUID().substring(0, 8) + "...\n" +
            "声道: " + DeviceIdentity.getChannelMode().getDisplayName() + "\n" +
            "MAC: " + DeviceIdentity.getMacAddress();

        deviceInfoText.setText(info);
    }

    private void updateStatus(String status) {
        statusText.setText("状态: " + status);
    }

    @Override
    protected void onDestroy() {
        super.onDestroy();
        if (isRunning) {
            stopService();
        }
    }
}
