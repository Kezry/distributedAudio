package com.kezry.soundplayer.discovery;

import android.content.Context;
import android.net.nsd.NsdManager;
import android.net.nsd.NsdServiceInfo;
import android.util.Log;

import javax.jmdns.JmDNS;
import javax.jmdns.ServiceInfo;
import java.io.IOException;
import java.net.InetAddress;
import java.util.HashMap;
import java.util.Map;

/**
 * 设备发现服务
 * 使用mDNS/Bonjour协议发布和发现设备
 */
public class DeviceDiscovery {

    private static final String TAG = "DeviceDiscovery";
    private static final String SERVICE_TYPE = "_soundplayer._tcp.local.";
    private static final String SERVICE_NAME = "SoundPlayer-";

    private Context context;
    private NsdManager nsdManager;
    private JmDNS jmdns;
    private NsdManager.RegistrationListener registrationListener;
    private String serviceName;

    public DeviceDiscovery(Context context) {
        this.context = context;
        this.nsdManager = (NsdManager) context.getSystemService(Context.NSD_SERVICE);
    }

    /**
     * 注册服务，让发送端能够发现
     */
    public void registerService(int port, String deviceInfo) {
        serviceName = SERVICE_NAME + com.kezry.soundplayer.DeviceIdentity.getDeviceUUID().substring(0, 4);

        registrationListener = new NsdManager.RegistrationListener() {
            @Override
            public void onRegistrationFailed(NsdServiceInfo serviceInfo, int errorCode) {
                Log.e(TAG, "Registration failed: " + errorCode);
            }

            @Override
            public void onUnregistrationFailed(NsdServiceInfo serviceInfo, int errorCode) {
                Log.e(TAG, "Unregistration failed: " + errorCode);
            }

            @Override
            public void onServiceRegistered(NsdServiceInfo serviceInfo) {
                Log.i(TAG, "Service registered: " + serviceInfo.getServiceName());
            }

            @Override
            public void onServiceUnregistered(NsdServiceInfo serviceInfo) {
                Log.i(TAG, "Service unregistered: " + serviceInfo.getServiceName());
            }
        };

        NsdServiceInfo serviceInfo = new NsdServiceInfo();
        serviceInfo.setServiceName(serviceName);
        serviceInfo.setServiceType(SERVICE_TYPE);
        serviceInfo.setPort(port);

        // Add device info as attributes
        Map<String, String> attributes = new HashMap<>();
        attributes.put("uuid", com.kezry.soundplayer.DeviceIdentity.getDeviceUUID());
        attributes.put("alias", com.kezry.soundplayer.DeviceIdentity.getDeviceAlias());
        attributes.put("mac", com.kezry.soundplayer.DeviceIdentity.getMacAddress());
        serviceInfo.setAttributes(attributes);

        nsdManager.registerService(serviceInfo, NsdManager.PROTOCOL_DNS_SD, registrationListener);

        Log.i(TAG, "Registering mDNS service: " + serviceName);
    }

    /**
     * 注销服务
     */
    public void unregisterService() {
        if (nsdManager != null && registrationListener != null) {
            nsdManager.unregisterService(registrationListener);
            Log.i(TAG, "Unregistered mDNS service");
        }
    }

    /**
     * 初始化JmDNS用于发现其他设备
     */
    public void startDiscovery(DiscoveryListener listener) {
        new Thread(() -> {
            try {
                InetAddress inetAddress = getLocalIpAddress();
                jmdns = JmDNS.create(inetAddress);

                // Browse for SoundPlayer services
                jmdns.addServiceListener(SERVICE_TYPE, new javax.jmdns.ServiceListener() {
                    @Override
                    public void serviceAdded(ServiceEvent event) {
                        Log.i(TAG, "Service added: " + event.getName());
                        ServiceInfo info = jmdns.getServiceInfo(SERVICE_TYPE, event.getName());
                        if (info != null && listener != null) {
                            listener.onDeviceDiscovered(info);
                        }
                    }

                    @Override
                    public void serviceRemoved(ServiceEvent event) {
                        Log.i(TAG, "Service removed: " + event.getName());
                        if (listener != null) {
                            listener.onDeviceRemoved(event.getName());
                        }
                    }

                    @Override
                    public void serviceResolved(ServiceEvent event) {
                        Log.i(TAG, "Service resolved: " + event.getName());
                    }
                });

                Log.i(TAG, "JmDNS discovery started");
            } catch (IOException e) {
                Log.e(TAG, "Failed to start JmDNS discovery", e);
            }
        }).start();
    }

    /**
     * 停止发现
     */
    public void stopDiscovery() {
        if (jmdns != null) {
            try {
                jmdns.close();
            } catch (IOException e) {
                Log.e(TAG, "Error closing JmDNS", e);
            }
        }
    }

    private InetAddress getLocalIpAddress() {
        try {
            return InetAddress.getByName("localhost");
        } catch (Exception e) {
            return null;
        }
    }

    public interface DiscoveryListener {
        void onDeviceDiscovered(ServiceInfo deviceInfo);
        void onDeviceRemoved(String deviceName);
    }
}
