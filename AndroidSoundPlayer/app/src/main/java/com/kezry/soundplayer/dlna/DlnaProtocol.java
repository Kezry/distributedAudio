package com.kezry.soundplayer.dlna;

import android.util.Log;

import org.json.JSONException;
import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/**
 * DLNA 协议处理器
 * 处理DLNA控制命令和状态同步
 */
public class DlnaProtocol {

    private static final String TAG = "DlnaProtocol";
    private static final int TIMEOUT_MS = 5000;

    private ExecutorService executor;

    public DlnaProtocol() {
        executor = Executors.newSingleThreadExecutor();
    }

    /**
     * DLNA 命令类型
     */
    public enum DlnaCommand {
        PLAY_AT,           // 统一起播
        STOP,             // 停止
        PAUSE,            // 暂停
        SEEK,             // 跳转
        SET_VOLUME,       // 设置音量
        GET_STATUS,       // 获取状态
        SYNC_REPORT       // 同步报告
    }

    /**
     * 发送DLNA命令
     */
    public void sendCommand(String deviceIp, int port, DlnaCommand command,
                           JSONObject params, DlnaCallback callback) {

        executor.execute(() -> {
            try {
                String urlString = String.format("http://%s:%d/api/dlna/control", deviceIp, port);
                URL url = new URL(urlString);
                HttpURLConnection conn = (HttpURLConnection) url.openConnection();

                try {
                    conn.setRequestMethod("POST");
                    conn.setRequestProperty("Content-Type", "application/json");
                    conn.setConnectTimeout(TIMEOUT_MS);
                    conn.setReadTimeout(TIMEOUT_MS);
                    conn.setDoOutput(true);

                    // 构建命令JSON
                    JSONObject commandJson = new JSONObject();
                    commandJson.put("command", command.name());
                    if (params != null) {
                        commandJson.put("params", params);
                    }

                    // 发送请求
                    OutputStream os = conn.getOutputStream();
                    os.write(commandJson.toString().getBytes());
                    os.flush();
                    os.close();

                    // 读取响应
                    int responseCode = conn.getResponseCode();
                    if (responseCode == HttpURLConnection.HTTP_OK) {
                        BufferedReader br = new BufferedReader(
                            new InputStreamReader(conn.getInputStream()));
                        StringBuilder response = new StringBuilder();
                        String line;
                        while ((line = br.readLine()) != null) {
                            response.append(line);
                        }
                        br.close();

                        if (callback != null) {
                            JSONObject responseJson = new JSONObject(response.toString());
                            callback.onSuccess(responseJson);
                        }
                    } else {
                        if (callback != null) {
                            callback.onError("HTTP error: " + responseCode);
                        }
                    }

                } finally {
                    conn.disconnect();
                }

            } catch (Exception e) {
                Log.e(TAG, "Error sending DLNA command", e);
                if (callback != null) {
                    callback.onError(e.getMessage());
                }
            }
        });
    }

    /**
     * 发送PLAY_AT命令 (统一起播)
     */
    public void sendPlayAt(String deviceIp, int port, long timestampMs, String mediaUrl, DlnaCallback callback) {
        try {
            JSONObject params = new JSONObject();
            params.put("timestamp", timestampMs);
            params.put("mediaUrl", mediaUrl);

            sendCommand(deviceIp, port, DlnaCommand.PLAY_AT, params, callback);

        } catch (JSONException e) {
            if (callback != null) {
                callback.onError("Invalid parameters: " + e.getMessage());
            }
        }
    }

    /**
     * 发送STOP命令
     */
    public void sendStop(String deviceIp, int port, DlnaCallback callback) {
        sendCommand(deviceIp, port, DlnaCommand.STOP, null, callback);
    }

    /**
     * 发送同步报告
     */
    public void sendSyncReport(String deviceIp, int port, int offsetMs, long positionMs, DlnaCallback callback) {
        try {
            JSONObject params = new JSONObject();
            params.put("offset", offsetMs);
            params.put("position", positionMs);

            sendCommand(deviceIp, port, DlnaCommand.SYNC_REPORT, params, callback);

        } catch (JSONException e) {
            if (callback != null) {
                callback.onError("Invalid parameters: " + e.getMessage());
            }
        }
    }

    /**
     * DLNA 回调接口
     */
    public interface DlnaCallback {
        void onSuccess(JSONObject response);
        void onError(String error);
    }

    /**
     * DLNA 控制服务器
     * 接收来自其他设备的DLNA命令
     */
    public static class DlnaControlServer {

        private static final String TAG = "DlnaControlServer";
        private static final int DEFAULT_PORT = 5006;

        private android.server.http.HttpServer server;
        private DlnaCommandHandler commandHandler;

        public DlnaControlServer(DlnaCommandHandler handler) {
            this.commandHandler = handler;
        }

        public void start() throws IOException {
            server = new android.server.http.HttpServer(DEFAULT_PORT);
            server.createContext("/api/dlna/control", new HttpHandler() {
                @Override
                public void handle(HttpExchange exchange) throws IOException {
                    try {
                        // 读取请求体
                        BufferedReader br = new BufferedReader(
                            new InputStreamReader(exchange.getRequestBody()));
                        StringBuilder requestBody = new StringBuilder();
                        String line;
                        while ((line = br.readLine()) != null) {
                            requestBody.append(line);
                        }

                        // 解析JSON
                        JSONObject json = new JSONObject(requestBody.toString());
                        String commandStr = json.getString("command");
                        DlnaCommand command = DlnaCommand.valueOf(commandStr);
                        JSONObject params = json.optJSONObject("params");

                        // 处理命令
                        JSONObject response = commandHandler.handleCommand(command, params);

                        // 发送响应
                        String responseStr = response.toString();
                        exchange.sendResponseHeaders(200, responseStr.length());
                        OutputStream os = exchange.getResponseBody();
                        os.write(responseStr.getBytes());
                        os.close();

                    } catch (Exception e) {
                        Log.e(TAG, "Error handling DLNA command", e);

                        // 发送错误响应
                        JSONObject errorResponse = new JSONObject();
                        try {
                            errorResponse.put("success", false);
                            errorResponse.put("error", e.getMessage());
                        } catch (JSONException ex) {}

                        String responseStr = errorResponse.toString();
                        exchange.sendResponseHeaders(500, responseStr.length());
                        OutputStream os = exchange.getResponseBody();
                        os.write(responseStr.getBytes());
                        os.close();
                    }
                }
            });

            server.start();
            Log.i(TAG, "DLNA control server started on port " + DEFAULT_PORT);
        }

        public void stop() {
            if (server != null) {
                server.stop(0);
                Log.i(TAG, "DLNA control server stopped");
            }
        }
    }

    /**
     * DLNA 命令处理器接口
     */
    public interface DlnaCommandHandler {
        JSONObject handleCommand(DlnaCommand command, JSONObject params);
    }

    public void shutdown() {
        if (executor != null) {
            executor.shutdownNow();
        }
    }
}
