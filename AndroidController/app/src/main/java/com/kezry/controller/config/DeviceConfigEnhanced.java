package com.kezry.controller.config;

import android.util.Log;
import com.google.gson.Gson;
import com.google.gson.JsonObject;
import com.kezry.controller.scan.NetworkScanner;
import java.io.IOException;
import java.util.concurrent.TimeUnit;
import okhttp3.Call;
import okhttp3.Callback;
import okhttp3.MediaType;
import okhttp3.OkHttpClient;
import okhttp3.Request;
import okhttp3.RequestBody;
import okhttp3.Response;

/**
 * 增强的设备配置管理
 */
public class DeviceConfigEnhanced {

    private static final String TAG = "DeviceConfigEnhanced";
    private static final MediaType JSON = MediaType.get("application/json; charset=utf-8");

    private OkHttpClient httpClient;
    private Gson gson;

    public DeviceConfigEnhanced() {
        // Configure HTTP client with timeout
        this.httpClient = new OkHttpClient.Builder()
            .connectTimeout(10, TimeUnit.SECONDS)
            .readTimeout(10, TimeUnit.SECONDS)
            .writeTimeout(10, TimeUnit.SECONDS)
            .build();

        this.gson = new Gson();
    }

    /**
     * 配置设备工作模式
     */
    public void configureDevice(NetworkScanner.SoundDevice device, Configuration config, ConfigCallback callback) {
        String url = String.format("http://%s:%d/api/config", device.host, device.port);

        JsonObject jsonConfig = new JsonObject();
        jsonConfig.addProperty("workMode", config.workMode.name());
        jsonConfig.addProperty("channelMode", config.channelMode.name());
        jsonConfig.addProperty("alias", config.alias);
        jsonConfig.addProperty("bufferSize", config.bufferSize);
        jsonConfig.addProperty("latency", config.latency);

        RequestBody body = RequestBody.create(jsonConfig.toString(), JSON);
        Request request = new Request.Builder()
            .url(url)
            .post(body)
            .build();

        httpClient.newCall(request).enqueue(new Callback() {
            @Override
            public void onFailure(Call call, IOException e) {
                Log.e(TAG, "Configuration failed", e);
                if (callback != null) {
                    callback.onError("Configuration failed: " + e.getMessage());
                }
            }

            @Override
            public void onResponse(Call call, Response response) {
                if (response.isSuccessful()) {
                    Log.i(TAG, "Device configured successfully");
                    if (callback != null) {
                        callback.onSuccess();
                    }
                } else {
                    Log.e(TAG, "Configuration failed: " + response.code());
                    if (callback != null) {
                        callback.onError("Configuration failed: " + response.code());
                    }
                }
                response.close();
            }
        });
    }

    /**
     * 获取设备当前配置
     */
    public void getDeviceConfig(NetworkScanner.SoundDevice device, ConfigFetchCallback callback) {
        String url = String.format("http://%s:%d/api/config", device.host, device.port);

        Request request = new Request.Builder()
            .url(url)
            .get()
            .build();

        httpClient.newCall(request).enqueue(new Callback() {
            @Override
            public void onFailure(Call call, IOException e) {
                Log.e(TAG, "Failed to fetch config", e);
                if (callback != null) {
                    callback.onError("Failed to fetch config: " + e.getMessage());
                }
            }

            @Override
            public void onResponse(Call call, Response response) {
                if (response.isSuccessful() && response.body() != null) {
                    try {
                        String json = response.body().string();
                        Configuration config = gson.fromJson(json, Configuration.class);
                        if (callback != null) {
                            callback.onConfigFetched(config);
                        }
                    } catch (Exception e) {
                        Log.e(TAG, "Failed to parse config", e);
                        if (callback != null) {
                            callback.onError("Failed to parse config");
                        }
                    }
                }
                response.close();
            }
        });
    }

    /**
     * 测试设备连接
     */
    public void testConnection(NetworkScanner.SoundDevice device, ConnectionTestCallback callback) {
        long startTime = System.currentTimeMillis();

        Request request = new Request.Builder()
            .url(String.format("http://%s:%d/api/ping", device.host, device.port))
            .get()
            .build();

        httpClient.newCall(request).enqueue(new Callback() {
            @Override
            public void onFailure(Call call, IOException e) {
                if (callback != null) {
                    callback.onConnectionResult(false, -1);
                }
            }

            @Override
            public void onResponse(Call call, Response response) {
                long latency = System.currentTimeMillis() - startTime;
                response.close();
                if (callback != null) {
                    callback.onConnectionResult(response.isSuccessful(), (int)latency);
                }
            }
        });
    }

    public interface ConfigCallback {
        void onSuccess();
        void onError(String error);
    }

    public interface ConfigFetchCallback {
        void onConfigFetched(Configuration config);
        void onError(String error);
    }

    public interface ConnectionTestCallback {
        void onConnectionResult(boolean success, int latencyMs);
    }
}
