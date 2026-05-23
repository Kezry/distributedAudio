package com.kezry.soundplayer.service;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.Intent;
import android.os.IBinder;
import android.util.Log;
import com.kezry.soundplayer.DeviceIdentity;
import com.kezry.soundplayer.R;
import com.kezry.soundplayer.decoder.OpusDecoder;
import com.kezry.soundplayer.dlna.DlnaReceiver;
import com.kezry.soundplayer.network.AudioReceiver;
import com.kezry.soundplayer.player.AudioPlayer;
import com.kezry.soundplayer.sync.PtpSync;

/**
 * 音频接收服务
 * 后台运行，接收并播放音频
 */
public class AudioReceiverService extends Service {

    private static final String TAG = "AudioReceiverService";
    private static final String CHANNEL_ID = "AudioReceiverChannel";
    private static final int NOTIFICATION_ID = 1;

    private AudioReceiver audioReceiver;
    private AudioPlayer audioPlayer;
    private DlnaReceiver dlnaReceiver;
    private PtpSync ptpSync;

    private boolean isRunning = false;

    @Override
    public void onCreate() {
        super.onCreate();
        createNotificationChannel();
        Log.i(TAG, "AudioReceiverService created");
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        if (isRunning) {
            return START_STICKY;
        }

        startForeground(NOTIFICATION_ID, createNotification());
        startAudioService();

        return START_STICKY;
    }

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }

    @Override
    public void onDestroy() {
        super.onDestroy();
        stopAudioService();
        Log.i(TAG, "AudioReceiverService destroyed");
    }

    private void startAudioService() {
        isRunning = true;

        // Initialize components
        audioPlayer = new AudioPlayer();
        audioReceiver = new AudioReceiver();
        dlnaReceiver = new DlnaReceiver();
        ptpSync = new PtpSync();

        // Set up callbacks
        audioReceiver.setCallback(new AudioReceiver.AudioDataCallback() {
            @Override
            public void onAudioDataReceived(byte[] data, int length) {
                audioPlayer.writeAudioData(data);
            }

            @Override
            public void onReceiveError(String error) {
                Log.e(TAG, "Audio receiver error: " + error);
            }
        });

        // Start components
        audioPlayer.start();
        audioReceiver.start();
        dlnaReceiver.start();
        ptpSync.start();

        updateNotificationText("正在接收音频 - " + DeviceIdentity.getDeviceAlias());
        Log.i(TAG, "Audio service started");
    }

    private void stopAudioService() {
        isRunning = false;

        if (audioPlayer != null) {
            audioPlayer.stop();
        }
        if (audioReceiver != null) {
            audioReceiver.stop();
        }
        if (dlnaReceiver != null) {
            dlnaReceiver.stop();
        }
        if (ptpSync != null) {
            ptpSync.stop();
        }

        Log.i(TAG, "Audio service stopped");
    }

    private void createNotificationChannel() {
        NotificationChannel channel = new NotificationChannel(
            CHANNEL_ID,
            "Audio Receiver",
            NotificationManager.IMPORTANCE_LOW
        );
        channel.setDescription("Audio playback notification");

        NotificationManager manager = getSystemService(NotificationManager.class);
        manager.createNotificationChannel(channel);
    }

    private Notification createNotification() {
        Intent intent = new Intent(this, MainActivity.class);
        PendingIntent pendingIntent = PendingIntent.getActivity(
            this, 0, intent, PendingIntent.FLAG_IMMUTABLE
        );

        Notification.Builder builder = new Notification.Builder(this, CHANNEL_ID)
            .setContentTitle("分布式音频")
            .setContentText("正在接收音频 - " + DeviceIdentity.getDeviceAlias())
            .setSmallIcon(R.drawable.ic_launcher_foreground)
            .setContentIntent(pendingIntent)
            .setOngoing(true);

        return builder.build();
    }

    private void updateNotificationText(String text) {
        Notification notification = createNotification();
        NotificationManager manager = getSystemService(NotificationManager.class);
        manager.notify(NOTIFICATION_ID, notification);
    }
}
