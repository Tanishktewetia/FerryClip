package com.clipsync.android

import android.app.Application
import com.clipsync.android.logging.CrashHandler
import com.clipsync.android.logging.FileLogger

class ClipSyncApplication : Application() {
    override fun onCreate() {
        super.onCreate()
        FileLogger.init(this)
        CrashHandler.init(this)
        com.clipsync.android.history.ClipboardHistory.get(this)
        FileLogger.info("FerryClip v${BuildConfig.VERSION_NAME} starting")
    }
}
