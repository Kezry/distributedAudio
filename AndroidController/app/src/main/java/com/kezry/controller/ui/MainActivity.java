package com.kezry.controller.ui;

import android.Manifest;
import android.content.pm.PackageManager;
import android.os.Build;
import android.os.Bundle;
import android.widget.Button;
import androidx.annotation.NonNull;
import androidx.appcompat.app.AppCompatActivity;
import androidx.core.app.ActivityCompat;
import androidx.core.content.ContextCompat;
import androidx.recyclerview.widget.LinearLayoutManager;
import androidx.recyclerview.widget.RecyclerView;
import com.kezry.controller.R;
import com.kezry.controller.scan.NetworkScanner;
import com.kezry.controller.config.DeviceConfigEnhanced;
import com.kezry.controller.ui.adapter.DeviceListAdapter;
import java.util.ArrayList;
import java.util.List;

/**
 * 主界面
 */
public class MainActivity extends AppCompatActivity {

    private static final String TAG = "MainActivity";
    private static final int REQUEST_PERMISSIONS = 100;

    private RecyclerView deviceList;
    private Button scanButton;
    private Button stopButton;

    private DeviceListAdapter adapter;
    private NetworkScannerEnhanced scanner;
    private DeviceConfigEnhanced configManager;

    private List<NetworkScanner.SoundDevice> devices = new ArrayList<>();

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        initViews();
        checkPermissions();
        initComponents();
    }

    private void initViews() {
        deviceList = findViewById(R.id.deviceList);
        scanButton = findViewById(R.id.scanButton);
        stopButton = findViewById(R.id.stopButton);

        deviceList.setLayoutManager(new LinearLayoutManager(this));
        adapter = new DeviceListAdapter(devices);
        deviceList.setAdapter(adapter);

        scanButton.setOnClickListener(v -> startScan());
        stopButton.setOnClickListener(v -> stopScan());

        stopButton.setEnabled(false);
    }

    private void checkPermissions() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
            String[] permissions = {
                Manifest.permission.ACCESS_WIFI_STATE,
                Manifest.permission.CHANGE_WIFI_MULTICAST_STATE,
                Manifest.permission.ACCESS_NETWORK_STATE,
                Manifest.permission.INTERNET,
                Manifest.permission.ACCESS_FINE_LOCATION,
                Manifest.permission.ACCESS_COARSE_LOCATION
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

    private void initComponents() {
        scanner = new NetworkScannerEnhanced(this);
        configManager = new DeviceConfigEnhanced();
    }

    private void startScan() {
        devices.clear();
        adapter.notifyDataSetChanged();

        scanButton.setEnabled(false);
        stopButton.setEnabled(true);

        scanner.startScan(new NetworkScanner.ScanCallback() {
            @Override
            public void onDeviceFound(NetworkScanner.SoundDevice device) {
                devices.add(device);
                adapter.notifyItemInserted(devices.size() - 1);
            }

            @Override
            public void onDeviceUpdated(NetworkScanner.SoundDevice device) {
                int position = devices.indexOf(device);
                if (position >= 0) {
                    devices.set(position, device);
                    adapter.notifyItemChanged(position);
                }
            }

            @Override
            public void onDeviceLost(String deviceId) {
                for (int i = 0; i < devices.size(); i++) {
                    if (devices.get(i).uuid.equals(deviceId)) {
                        devices.remove(i);
                        adapter.notifyItemRemoved(i);
                        break;
                    }
                }
            }

            @Override
            public void onScanComplete(List<NetworkScanner.SoundDevice> foundDevices) {
                scanButton.setEnabled(true);
                stopButton.setEnabled(false);
            }

            @Override
            public void onError(String error) {
                scanButton.setEnabled(true);
                stopButton.setEnabled(false);
            }
        });
    }

    private void stopScan() {
        scanner.stopScan();
        scanButton.setEnabled(true);
        stopButton.setEnabled(false);
    }

    @Override
    public void onRequestPermissionsResult(int requestCode, @NonNull String[] permissions, @NonNull int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
        // Handle permission results
    }

    @Override
    protected void onDestroy() {
        super.onDestroy();
        scanner.stopScan();
    }
}
