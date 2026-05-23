package com.kezry.soundplayer.dlna;

import android.content.Context;
import android.content.SharedPreferences;
import android.util.Log;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * DLNA 多机组管理
 * 支持统一播放、进度同步、延迟补偿
 */
public class DlnaGroupManager {

    private static final String TAG = "DlnaGroupManager";
    private static final String PREFS_NAME = "dlna_group_prefs";
    private static final String KEY_GROUP_ID = "dlnaGroupId";
    private static final String KEY_DELAY_MS = "dlnaDelayMs";
    private static final String KEY_SYNC_TOGETHER = "syncTogether";

    private Context context;
    private SharedPreferences prefs;

    private String groupId;
    private int delayMs;
    private boolean syncTogether;

    private DlnaGroupPlaybackCallback callback;

    public interface DlnaGroupPlaybackCallback {
        void onPlayAt(long timestampMs);
        void onStop();
        void onSeek(long positionMs);
        void onSyncStatus(int syncOffsetMs);
    }

    public DlnaGroupManager(Context context) {
        this.context = context;
        this.prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
        loadConfig();
    }

    /**
     * 加载组配置
     */
    private void loadConfig() {
        groupId = prefs.getString(KEY_GROUP_ID, "");
        delayMs = prefs.getInt(KEY_DELAY_MS, 0);
        syncTogether = prefs.getBoolean(KEY_SYNC_TOGETHER, true);
    }

    /**
     * 设置DLNA多机组信息
     */
    public void setGroup(String groupId, int delayMs, boolean syncTogether) {
        this.groupId = groupId;
        this.delayMs = delayMs;
        this.syncTogether = syncTogether;

        saveConfig();
        Log.i(TAG, String.format("DLNA group set: %s, delay: %dms, sync: %b",
            groupId, delayMs, syncTogether));
    }

    /**
     * 保存配置
     */
    private void saveConfig() {
        prefs.edit()
            .putString(KEY_GROUP_ID, groupId)
            .putInt(KEY_DELAY_MS, delayMs)
            .putBoolean(KEY_SYNC_TOGETHER, syncTogether)
            .apply();
    }

    /**
     * 获取组ID
     */
    public String getGroupId() {
        return groupId;
    }

    /**
     * 获取延迟补偿
     */
    public int getDelayMs() {
        return delayMs;
    }

    /**
     * 是否统一起播
     */
    public boolean isSyncTogether() {
        return syncTogether;
    }

    /**
     * 是否在组中
     */
    public boolean isInGroup() {
        return groupId != null && !groupId.isEmpty();
    }

    /**
     * 退出组
     */
    public void leaveGroup() {
        groupId = "";
        delayMs = 0;
        syncTogether = true;
        saveConfig();
        Log.i(TAG, "Left DLNA group");
    }

    /**
     * 统一起播 (PLAY_AT)
     * @param timestampMs 播放时间戳 (毫秒)
     */
    public void playAt(long timestampMs) {
        if (!isInGroup() || !syncTogether) {
            Log.w(TAG, "Not in a group or sync disabled");
            return;
        }

        // 应用延迟补偿
        long adjustedTimestamp = timestampMs + delayMs;

        Log.i(TAG, String.format("PLAY_AT: %d (adjusted: %d, delay: %d)",
            timestampMs, adjustedTimestamp, delayMs));

        if (callback != null) {
            callback.onPlayAt(adjustedTimestamp);
        }
    }

    /**
     * 停止播放
     */
    public void stop() {
        if (!isInGroup()) {
            return;
        }

        Log.i(TAG, "Group STOP");

        if (callback != null) {
            callback.onStop();
        }
    }

    /**
     * 跳转播放位置
     */
    public void seek(long positionMs) {
        if (!isInGroup() || !syncTogether) {
            return;
        }

        Log.i(TAG, String.format("Group SEEK: %dms", positionMs));

        if (callback != null) {
            callback.onSeek(positionMs);
        }
    }

    /**
     * 同步状态报告
     */
    public void reportSyncStatus(int syncOffsetMs) {
        if (!isInGroup()) {
            return;
        }

        Log.d(TAG, String.format("Sync offset: %dms", syncOffsetMs));

        if (callback != null) {
            callback.onSyncStatus(syncOffsetMs);
        }
    }

    /**
     * 自动延迟测量
     */
    public void autoMeasureDelay() {
        if (!isInGroup()) {
            Log.w(TAG, "Not in a group, cannot measure delay");
            return;
        }

        Log.i(TAG, "Starting automatic delay measurement...");

        // 实现延迟测量算法
        // 1. 发送测试信号
        // 2. 接收响应
        // 3. 计算往返延迟
        // 4. 更新延迟补偿值
    }

    /**
     * 手动调整延迟
     */
    public void manualAdjustDelay(int deltaMs) {
        delayMs += deltaMs;

        // 限制范围
        delayMs = Math.max(-200, Math.min(200, delayMs));

        saveConfig();

        Log.i(TAG, String.format("Delay adjusted by %dms, new value: %dms", deltaMs, delayMs));
    }

    /**
     * 设置回放回调
     */
    public void setCallback(DlnaGroupPlaybackCallback callback) {
        this.callback = callback;
    }

    /**
     * 获取组配置摘要
     */
    public String getSummary() {
        if (!isInGroup()) {
            return "未加入DLNA组";
        }

        return String.format("组ID: %s\n延迟: %dms\n同步模式: %s",
            groupId, delayMs, syncTogether ? "统一起播" : "自由播放");
    }

    /**
     * 同步状态监控
     */
    private SyncMonitor syncMonitor;

    public void startSyncMonitoring() {
        if (syncMonitor == null) {
            syncMonitor = new SyncMonitor();
            syncMonitor.start();
        }
    }

    public void stopSyncMonitoring() {
        if (syncMonitor != null) {
            syncMonitor.stop();
            syncMonitor = null;
        }
    }

    /**
     * 同步监控线程
     */
    private class SyncMonitor {
        private Thread monitorThread;
        private volatile boolean running = false;

        public void start() {
            running = true;
            monitorThread = new Thread(() -> {
                while (running) {
                    try {
                        Thread.sleep(1000);

                        // 检查同步状态
                        // 报告偏移量
                        if (callback != null) {
                            // 获取当前播放位置的偏移量
                            int offset = calculateSyncOffset();
                            callback.onSyncStatus(offset);
                        }

                    } catch (InterruptedException e) {
                        Thread.currentThread().interrupt();
                        break;
                    }
                }
            });
            monitorThread.setName("DLNA-SyncMonitor");
            monitorThread.start();
        }

        public void stop() {
            running = false;
            if (monitorThread != null) {
                monitorThread.interrupt();
                try {
                    monitorThread.join(1000);
                } catch (InterruptedException e) {
                    Thread.currentThread().interrupt();
                }
            }
        }

        private int calculateSyncOffset() {
            // 计算当前播放位置与期望位置的偏移
            // 这里需要与播放器交互
            return 0;
        }
    }

    /**
     * 组配置数据模型
     */
    public static class GroupConfig {
        public String groupId;
        public int delayMs;
        public boolean syncTogether;

        public GroupConfig(String groupId, int delayMs, boolean syncTogether) {
            this.groupId = groupId;
            this.delayMs = delayMs;
            this.syncTogether = syncTogether;
        }

        public JSONObject toJson() {
            try {
                JSONObject json = new JSONObject();
                json.put("groupId", groupId);
                json.put("delayMs", delayMs);
                json.put("syncTogether", syncTogether);
                return json;
            } catch (JSONException e) {
                return null;
            }
        }

        public static GroupConfig fromJson(JSONObject json) {
            try {
                return new GroupConfig(
                    json.getString("groupId"),
                    json.getInt("delayMs"),
                    json.getBoolean("syncTogether")
                );
            } catch (JSONException e) {
                return null;
            }
        }
    }
}
