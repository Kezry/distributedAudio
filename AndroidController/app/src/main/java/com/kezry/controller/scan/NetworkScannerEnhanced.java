package com.kezry.controller.scan;

import android.content.Context;
import android.net.nsd.NsdManager;
import android.net.nsd.NsdServiceInfo;
import android.util.Log;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

/**
 * 增强的网络扫描器
 * 支持连续扫描和实时更新
 */
public class NetworkScannerEnhanced {

    private static final String TAG = "NetworkScanner";
    private static final String SERVICE_TYPE = "_soundplayer._tcp.local.";

    private Context context;
    private NsdManager nsdManager;
    private NsdManager.DiscoveryListener discoveryListener;
    private Map<String, SoundDevice> discoveredDevices = new ConcurrentHashMap<>();
    private boolean isScanning = false;
    private ScanCallback callback;

    public interface ScanCallback {
        void onDeviceFound(SoundDevice device);
        void onDeviceUpdated(SoundDevice device);
        void onDeviceLost(String deviceId);
        void onScanComplete(List<SoundDevice> devices);
        void onError(String error);
    }

    public NetworkScannerEnhanced(Context context) {
        this.context = context;
        this.nsdManager = (NsdManager) context.getSystemService(Context.NSD_SERVICE);
    }

    public void startScan(ScanCallback callback) {
        this.callback = callback;
        if (isScanning) {
            Log.w(TAG, "Scan already in progress");
            return;
        }

        discoveredDevices.clear();

        discoveryListener = new NsdManager.DiscoveryListener() {
            @Override
            public void onStartDiscoveryFailed(String serviceType, int errorCode) {
                Log.e(TAG, "Discovery start failed: " + errorCode);
                if (callback != null) {
                    callback.onError("Discovery start failed: " + errorCode);
                }
                isScanning = false;
            }

            @Override
            public void onStopDiscoveryFailed(String serviceType, int errorCode) {
                Log.e(TAG, "Discovery stop failed: " + errorCode);
            }

            @Override
            public void onDiscoveryStarted(String serviceType) {
                Log.i(TAG, "Discovery started");
                isScanning = true;
            }

            @Override
            public void onDiscoveryStopped(String serviceType) {
                Log.i(TAG, "Discovery stopped");
                isScanning = false;
                if (callback != null) {
                    callback.onScanComplete(new ArrayList<>(discoveredDevices.values()));
                }
            }

            @Override
            public void onServiceFound(NsdServiceInfo serviceInfo) {
                Log.d(TAG, "Service found: " + serviceInfo.getServiceName());

                if (serviceInfo.getServiceType().equals(SERVICE_TYPE)) {
                    nsdManager.resolveService(serviceInfo, new NsdManager.ResolveListener() {
                        @Override
                        public void onResolveFailed(NsdServiceInfo serviceInfo, int errorCode) {
                            Log.e(TAG, "Resolve failed: " + errorCode);
                        }

                        @Override
                        public void onServiceResolved(NsdServiceInfo serviceInfo) {
                            SoundDevice device = createDeviceFromServiceInfo(serviceInfo);
                            if (device != null) {
                                handleDeviceFound(device);
                            }
                        }
                    });
                }
            }

            @Override
            public void onServiceLost(NsdServiceInfo serviceInfo) {
                Log.d(TAG, "Service lost: " + serviceInfo.getServiceName());

                String deviceId = extractDeviceId(serviceInfo.getServiceName());
                if (discoveredDevices.containsKey(deviceId)) {
                    discoveredDevices.remove(deviceId);
                    if (callback != null) {
                        callback.onDeviceLost(deviceId);
                    }
                }
            }
        };

        nsdManager.discoverServices(SERVICE_TYPE, NsdManager.PROTOCOL_DNS_SD, discoveryListener);
    }

    public void stopScan() {
        if (nsdManager != null && discoveryListener != null) {
            nsdManager.stopServiceDiscovery(discoveryListener);
        }
        isScanning = false;
    }

    public List<SoundDevice> getDiscoveredDevices() {
        return new ArrayList<>(discoveredDevices.values());
    }

    private void handleDeviceFound(SoundDevice device) {
        boolean isNew = !discoveredDevices.containsKey(device.uuid);
        discoveredDevices.put(device.uuid, device);
        device.lastSeen = System.currentTimeMillis();

        if (callback != null) {
            if (isNew) {
                callback.onDeviceFound(device);
            } else {
                callback.onDeviceUpdated(device);
            }
        }
    }

    private SoundDevice createDeviceFromServiceInfo(NsdServiceInfo serviceInfo) {
        try {
            Map<String, byte[]> attributes = serviceInfo.getAttributes();

            String uuid = getStringAttribute(attributes, "uuid");
            String alias = getStringAttribute(attributes, "alias");
            String mac = getStringAttribute(attributes, "mac");

            if (uuid == null) {
                uuid = extractDeviceId(serviceInfo.getServiceName());
            }

            if (alias == null) {
                alias = "SoundPlayer-" + uuid.substring(0, 4);
            }

            java.net.InetAddress host = serviceInfo.getHost();
            int port = serviceInfo.getPort();

            SoundDevice device = new SoundDevice(uuid, alias, host.getHostAddress(), port);
            device.macAddress = mac;
            device.lastSeen = System.currentTimeMillis();

            return device;
        } catch (Exception e) {
            Log.e(TAG, "Error creating device from service info", e);
            return null;
        }
    }

    private String getStringAttribute(Map<String, byte[]> attributes, String key) {
        if (attributes.containsKey(key)) {
            return new String(attributes.get(key));
        }
        return null;
    }

    private String extractDeviceId(String serviceName) {
        if (serviceName != null && serviceName.startsWith("SoundPlayer-")) {
            return serviceName.substring("SoundPlayer-".length());
        }
        return serviceName;
    }

    public boolean isScanning() {
        return isScanning;
    }
}
