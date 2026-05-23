package com.kezry.soundplayer;

import android.content.Context;
import android.content.SharedPreferences;
import android.net.wifi.WifiInfo;
import android.net.wifi.WifiManager;
import android.provider.Settings;

import java.nio.ByteBuffer;
import java.util.UUID;

/**
 * 设备身份标识管理
 * 每个声音端有固定的UUID和可配置的别名
 */
public class DeviceIdentity {

    private static final String PREFS_NAME = "soundplayer_prefs";
    private static final String KEY_DEVICE_UUID = "device_uuid";
    private static final String KEY_DEVICE_ALIAS = "device_alias";
    private static final String KEY_CHANNEL_MODE = "channel_mode"; // stereo, left, right

    private static String deviceUUID;
    private static String deviceAlias;
    private static String macAddress;
    private static ChannelMode channelMode = ChannelMode.STEREO;

    public enum ChannelMode {
        STEREO("立体声"),
        LEFT("左声道"),
        RIGHT("右声道");

        private final String displayName;

        ChannelMode(String displayName) {
            this.displayName = displayName;
        }

        public String getDisplayName() {
            return displayName;
        }
    }

    public static void initialize(Context context) {
        SharedPreferences prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);

        // Generate or load UUID
        deviceUUID = prefs.getString(KEY_DEVICE_UUID, null);
        if (deviceUUID == null) {
            deviceUUID = generateUUID();
            prefs.edit().putString(KEY_DEVICE_UUID, deviceUUID).apply();
        }

        // Load alias
        deviceAlias = prefs.getString(KEY_DEVICE_ALIAS, "SoundPlayer-" + deviceUUID.substring(0, 4));

        // Load channel mode
        String modeStr = prefs.getString(KEY_CHANNEL_MODE, ChannelMode.STEREO.name());
        channelMode = ChannelMode.valueOf(modeStr);

        // Get MAC address
        macAddress = getMacAddress(context);
    }

    private static String generateUUID() {
        return UUID.randomUUID().toString();
    }

    private static String getMacAddress(Context context) {
        try {
            WifiManager wifi = (WifiManager) context.getApplicationContext()
                    .getSystemService(Context.WIFI_SERVICE);
            WifiInfo info = wifi.getConnectionInfo();
            return info.getMacAddress();
        } catch (Exception e) {
            return "unknown";
        }
    }

    public static String getDeviceUUID() {
        return deviceUUID;
    }

    public static String getDeviceAlias() {
        return deviceAlias;
    }

    public static void setDeviceAlias(String alias) {
        deviceAlias = alias;
        SharedPreferences prefs = getAppContext().getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
        prefs.edit().putString(KEY_DEVICE_ALIAS, alias).apply();
    }

    public static String getMacAddress() {
        return macAddress;
    }

    public static ChannelMode getChannelMode() {
        return channelMode;
    }

    public static void setChannelMode(ChannelMode mode) {
        channelMode = mode;
        SharedPreferences prefs = getAppContext().getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
        prefs.edit().putString(KEY_CHANNEL_MODE, mode.name()).apply();
    }

    private static Context getAppContext() {
        return SoundPlayerApp.getAppContext();
    }

    /**
     * 获取设备信息JSON
     */
    public static String getDeviceInfoJson() {
        return String.format(
                "{\"uuid\":\"%s\",\"alias\":\"%s\",\"mac\":\"%s\",\"mode\":\"%s\"}",
                deviceUUID, deviceAlias, macAddress, channelMode.name()
        );
    }
}
