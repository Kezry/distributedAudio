package com.kezry.soundplayer.dlna;

import android.content.Context;
import android.net.wifi.WifiManager;
import android.util.Log;

import org.fourthline.cling.model.MetaData;
import org.fourthline.cling.model.ModelUtil;
import org.fourthline.cling.model.types.DeviceType;
import org.fourthline.cling.model.types.UDN;
import org.fourthline.cling.model.types.Datatype;
import org.fourthline.cling.model.types.InvalidValueException;
import org.fourthline.cling.support.model.ProtocolInfos;
import org.fourthline.cling.support.avtransport.AVTransportService;
import org.fourthline.cling.support.connectionmanager.ConnectionManagerService;
import org.fourthline.cling.support.renderingcontrol.RenderingControlService;
import org.fourthline.cling.support.model.Device;
import org.fourthline.cling.support.model.Service;

import java.util.UUID;

/**
 * DLNA媒体渲染器设备
 */
public class DlnaDevice extends Device {

    private static final String TAG = "DlnaDevice";

    public DlnaDevice(DeviceType deviceType, String friendlyName,
                      String manufacturer, String modelName,
                      ProtocolInfos protocolInfos) throws InvalidValueException {

        super(createUdn(), new Version(1, 0), new DeviceDetails(friendlyName,
                new ManufacturerDetails(manufacturer),
                new ModelDetails(modelName),
                new DLNADoc[]{new DLNADoc("DMS", "1.00"), new DLNADoc("DMR", "1.00")},
                new DLNACaps("av-upload", "image-upload", "audio-upload")),
                createDeviceType(deviceType));

        // 添加服务
        addService(new ConnectionManagerService(protocolInfos, null));
        addService(new AVTransportService());
        addService(new RenderingControlService());
    }

    /**
     * 创建设备UDN
     */
    private static UDN createUdn() {
        try {
            WifiManager wm = (WifiManager) Context.getSystemService(Context.WIFI_SERVICE);
            String macAddress = wm.getConnectionInfo().getMacAddress();
            return new UDN(UUID.nameUUIDFromBytes(macAddress.getBytes()));
        } catch (Exception e) {
            Log.w(TAG, "Failed to get MAC address, using random UUID");
            return new UDN(UUID.randomUUID());
        }
    }

    /**
     * 创建设备类型
     */
    private static DeviceType createDeviceType(DeviceType type) {
        return new UDADeviceType("MediaRenderer", 1);
    }

    /**
     * 获取设备标识符
     */
    public String getDeviceId() {
        return getIdentity().getUdn().getIdentifierString();
    }
}
