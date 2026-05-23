package com.kezry.soundplayer.dlna;

import android.app.Service;
import android.content.Intent;
import android.os.IBinder;
import android.util.Log;

import org.fourthline.cling.UpnpService;
import org.fourthline.cling.UpnpServiceImpl;
import org.fourthline.cling.model.Namespace;
import org.fourthline.cling.model.types.DeviceType;
import org.fourthline.cling.model.types.UDADeviceType;
import org.fourthline.cling.support.avtransport.AVTransportService;
import org.fourthline.cling.support.connectionmanager.ConnectionManagerService;
import org.fourthline.cling.support.renderingcontrol.RenderingControlService;
import org.fourthline.cling.support.model.ProtocolInfos;
import org.fourthline.cling.support.model.Protocol;

import java.util.concurrent.locks.ReentrantLock;

/**
 * DLNA DMR (Digital Media Renderer) 服务
 * 使Android设备作为DLNA播放器
 */
public class DlnaDmrService extends Service {

    private static final String TAG = "DlnaDmrService";

    public static final String ACTION_START = "com.kezry.soundplayer.dlna.START";
    public static final String ACTION_STOP = "com.kezry.soundplayer.dlna.STOP";

    private UpnpService upnpService;
    private DlnaDevice device;
    private final ReentrantLock lock = new ReentrantLock();

    @Override
    public void onCreate() {
        super.onCreate();
        Log.i(TAG, "DLNA DMR Service created");
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        if (intent != null) {
            String action = intent.getAction();
            if (ACTION_START.equals(action)) {
                startDmrService();
            } else if (ACTION_STOP.equals(action)) {
                stopDmrService();
            }
        }
        return START_STICKY;
    }

    /**
     * 启动DLNA DMR服务
     */
    private void startDmrService() {
        lock.lock();
        try {
            if (upnpService != null) {
                Log.w(TAG, "DLNA service already running");
                return;
            }

            Log.i(TAG, "Starting DLNA DMR service...");

            // 创建UPnP服务
            upnpService = new UpnpServiceImpl();

            // 创建DLNA设备
            device = createDlnaDevice();

            // 注册设备
            upnpService.getRegistry().addDevice(device);

            Log.i(TAG, "DLNA DMR service started successfully");
            Log.i(TAG, "Device name: " + device.getDetails().getFriendlyName());
            Log.i(TAG, "Device UDN: " + device.getIdentity().getUdn());

        } catch (Exception e) {
            Log.e(TAG, "Failed to start DLNA service", e);
            stopSelf();
        } finally {
            lock.unlock();
        }
    }

    /**
     * 停止DLNA DMR服务
     */
    private void stopDmrService() {
        lock.lock();
        try {
            if (upnpService == null) {
                Log.w(TAG, "DLNA service not running");
                return;
            }

            Log.i(TAG, "Stopping DLNA DMR service...");

            if (device != null) {
                upnpService.getRegistry().removeDevice(device);
                device = null;
            }

            upnpService.shutdown();
            upnpService = null;

            Log.i(TAG, "DLNA DMR service stopped");

        } catch (Exception e) {
            Log.e(TAG, "Error stopping DLNA service", e);
        } finally {
            lock.unlock();
        }
    }

    /**
     * 创建DLNA设备
     */
    private DlnaDevice createDlnaDevice() {
        // 创建媒体渲染器设备类型
        DeviceType deviceType = new UDADeviceType("MediaRenderer", 1);

        // 创建设备详情
        // 从设备身份管理器获取设备信息
        String deviceName = "DistributedAudio Player";
        String manufacturer = "Kezry";
        String modelName = "Android Sound Player";

        // 创建DLNA设备实例
        return new DlnaDevice(
            deviceType,
            deviceName,
            manufacturer,
            modelName,
            createProtocolInfos()
        );
    }

    /**
     * 创建支持的协议信息
     */
    private ProtocolInfos createProtocolInfos() {
        // 支持的音频协议
        ProtocolInfos protocols = new ProtocolInfos();

        // MP3
        protocols.add(new Protocol(
            "http-get:*:audio/mpeg:DLNA.ORG_PN=MP3",
            "http-get",
            "audio/mpeg",
            "DLNA.ORG_PN=MP3"
        ));

        // AAC
        protocols.add(new Protocol(
            "http-get:*:audio/mp4:DLNA.ORG_PN=AAC",
            "http-get",
            "audio/mp4",
            "DLNA.ORG_PN=AAC"
        ));

        // Opus
        protocols.add(new Protocol(
            "http-get:*:audio/opus:*",
            "http-get",
            "audio/opus",
            null
        ));

        // PCM
        protocols.add(new Protocol(
            "http-get:*:audio/L16:DLNA.ORG_PN=LPCM",
            "http-get",
            "audio/L16",
            "DLNA.ORG_PN=LPCM"
        ));

        return protocols;
    }

    /**
     * 获取DLNA服务状态
     */
    public boolean isRunning() {
        lock.lock();
        try {
            return upnpService != null;
        } finally {
            lock.unlock();
        }
    }

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }

    @Override
    public void onDestroy() {
        super.onDestroy();
        stopDmrService();
        Log.i(TAG, "DLNA DMR Service destroyed");
    }
}
