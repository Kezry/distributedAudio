package com.kezry.controller.scan;

import android.content.Context;
import android.net.nsd.NsdManager;
import android.net.nsd.NsdServiceInfo;
import android.util.Log;

import java.net.InetAddress;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;

/**
 * 网络扫描器
 * 扫描局域网内的声音端设备
 */
public class NetworkScanner {

    private static final String TAG = "NetworkScanner";
    private static final String SERVICE_TYPE = "_soundplayer._tcp.local.";

    private Context context;
    private NsdManager nsdManager;
    private NsdManager.DiscoveryListener discoveryListener;
    private List<SoundDevice> discoveredDevices = new ArrayList<>();
    private ScanCallback callback;

    public interface ScanCallback {
        void onDeviceFound(SoundDevice device);
        void onDeviceLost(String deviceId);
        void onScanComplete(List<SoundDevice> devices);
        void onError(String error);
    }

    public static class SoundDevice {
        public String uuid;
        public String alias;
        public String macAddress;
        public String host;
        public int port;
        public int signalStrength;
        public long lastSeen;

        public SoundDevice(String uuid, String alias, String host, int port) {
            this.uuid = uuid;
            this.alias = alias;
            this.host = host;
            this.port = port;
            this.lastSeen = System.currentTimeMillis();
        }

        public String getDisplayName() {
            return alias + " (" + uuid.substring(0, 4) + ")";
        }
    }

    public NetworkScanner(Context context) {
        this.context = context;
        this.nsdManager = (NsdManager) context.getSystemService(Context.NSD_SERVICE);
    }

    /**
     * 开始扫描
     */
    public void startScan(ScanCallback callback) {
        this.callback = callback;
        discoveredDevices.clear();

        discoveryListener = new NsdManager.DiscoveryListener() {
            @Override
            public void onStartDiscoveryFailed(String serviceType, int errorCode) {
                Log.e(TAG, "Discovery start failed: " + errorCode);
                if (callback != null) {
                    callback.onError("Discovery start failed: " + errorCode);
                }
            }

            @Override
            public void onStopDiscoveryFailed(String serviceType, int errorCode) {
                Log.e(TAG, "Discovery stop failed: " + errorCode);
            }

            @Override
            public void onDiscoveryStarted(String serviceType) {
                Log.i(TAG, "Discovery started");
            }

            @Override
            public void onDiscoveryStopped(String serviceType) {
                Log.i(TAG, "Discovery stopped");
                if (callback != null) {
                    callback.onScanComplete(new ArrayList<>(discoveredDevices));
                }
            }

            @Override
            public void onServiceFound(NsdServiceInfo serviceInfo) {
                Log.d(TAG, "Service found: " + serviceInfo.getServiceName());

                // Only process SoundPlayer services
                if (serviceInfo.getServiceType().equals(SERVICE_TYPE)) {
                    nsdManager.resolveService(serviceInfo, new NsdManager.ResolveListener() {
                        @Override
                        public void onResolveFailed(NsdServiceInfo serviceInfo, int errorCode) {
                            Log.e(TAG, "Resolve failed: " + errorCode);
                        }

                        @Override
                        public void onServiceResolved(NsdServiceInfo serviceInfo) {
                            SoundDevice device = createDeviceFromServiceInfo(serviceInfo);
                            if (device != null && callback != null) {
                                discoveredDevices.add(device);
                                callback.onDeviceFound(device);
                            }
                        }
                    });
                }
            }

            @Override
            public void onServiceLost(NsdServiceInfo serviceInfo) {
                Log.d(TAG, "Service lost: " + serviceInfo.getServiceName());

                if (callback != null) {
                    String deviceId = extractDeviceId(serviceInfo.getServiceName());
                    callback.onDeviceLost(deviceId);
                }
            }
        };

        nsdManager.discoverServices(SERVICE_TYPE, NsdManager.PROTOCOL_DNS_SD, discoveryListener);
    }

    /**
     * 停止扫描
     */
    public void stopScan() {
        if (nsdManager != null && discoveryListener != null) {
            nsdManager.stopServiceDiscovery(discoveryListener);
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

            InetAddress host = serviceInfo.getHost();
            int port = serviceInfo.getPort();

            SoundDevice device = new SoundDevice(uuid, alias, host.getHostAddress(), port);
            device.macAddress = mac;

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
        // Extract UUID from "SoundPlayer-XXXX" format
        if (serviceName != null && serviceName.startsWith("SoundPlayer-")) {
            return serviceName.substring("SoundPlayer-".length());
        }
        return serviceName;
    }
}
