package com.kezry.soundplayer;

import android.app.Application;
import android.content.Context;

/**
 * SoundPlayer Application
 */
public class SoundPlayerApp extends Application {

    private static SoundPlayerApp instance;
    private static Context context;

    @Override
    public void onCreate() {
        super.onCreate();
        instance = this;
        context = getApplicationContext();

        // Initialize device identity
        DeviceIdentity.initialize(this);
    }

    public static SoundPlayerApp getInstance() {
        return instance;
    }

    public static Context getAppContext() {
        return context;
    }
}
