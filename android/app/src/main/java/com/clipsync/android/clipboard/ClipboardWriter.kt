package com.clipsync.android.clipboard

import android.content.ClipData
import android.content.ClipDescription
import android.content.ClipboardManager
import android.content.Context
import android.os.PersistableBundle
import android.os.Build

class ClipboardWriter(context: Context) {
    private val history = com.clipsync.android.history.ClipboardHistory.get(context)
    private val clipboard = context.getSystemService(ClipboardManager::class.java)
    fun write(text: String) {
        val clip = ClipData.newPlainText("FerryClip", text)
        clip.description.extras = PersistableBundle().apply {
            // Redacts the OS preview. It cannot promise to hide Android's system overlay.
            putBoolean(if (Build.VERSION.SDK_INT >= 33) ClipDescription.EXTRA_IS_SENSITIVE else "android.content.extra.IS_SENSITIVE", true)
        }
        clipboard.setPrimaryClip(clip)
        // The locally added sensitive flag only redacts the OS overlay; it is not source metadata.
        history.record(text, "From PC")
        // No toast, snackbar, notification, or clipboard listener here.
    }
}
