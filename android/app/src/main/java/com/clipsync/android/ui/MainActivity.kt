package com.clipsync.android.ui

import android.Manifest
import android.content.ClipData
import android.content.ClipboardManager
import android.content.ContentValues
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.os.Environment
import android.os.PersistableBundle
import android.os.PowerManager
import android.provider.MediaStore
import android.provider.Settings
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.runtime.*
import androidx.core.content.ContextCompat
import com.clipsync.android.clipboard.ClipboardReadStore
import com.clipsync.android.logging.CrashHandler
import com.clipsync.android.logging.FileLogger
import com.clipsync.android.security.ManualAddress
import com.clipsync.android.service.ClipboardWatchService
import com.clipsync.android.service.SyncRuntime
import com.clipsync.android.store.SyncSettings
import com.clipsync.android.ui.theme.FerryClipTheme

class MainActivity : ComponentActivity() {
    private lateinit var settings: SyncSettings
    private var setupRevision by mutableIntStateOf(0)
    private val notificationPermission = registerForActivityResult(ActivityResultContracts.RequestPermission()) { setupRevision++ }
    private val saveDocument = registerForActivityResult(ActivityResultContracts.CreateDocument("text/plain")) { uri ->
        if (uri != null) exportLogs(uri)
    }
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        settings = SyncSettings(this)
        ClipboardReadStore.initialize(this)
        SyncRuntime.initialize(this)
        val lastCrash = CrashHandler.consumeLastCrash()
        FileLogger.info("FerryClip dashboard/setup opened")
        setContent {
            var step by remember { mutableIntStateOf(settings.onboardingStep.coerceIn(0, 3)) }
            var done by androidx.compose.runtime.saveable.rememberSaveable { mutableStateOf(settings.onboardingDone) }
            var replayingSetup by androidx.compose.runtime.saveable.rememberSaveable { mutableStateOf(false) }
            val pageState = androidx.compose.runtime.saveable.rememberSaveableStateHolder()
            val revision = setupRevision
            val notifications = remember(revision) { androidx.core.app.NotificationManagerCompat.from(this).areNotificationsEnabled() }
            val battery = remember(revision) { getSystemService(PowerManager::class.java).isIgnoringBatteryOptimizations(packageName) }
            val tile = remember(revision) { settings.tileAdded }
            val tileDismissed = remember(revision) { settings.tileDismissed }
            FerryClipTheme {
                pageState.SaveableStateProvider(if(done) "dashboard" else "onboarding") {
                if (!done) OnboardingScreen(step, notifications, battery, settings.tileAdded,
                    advance = { step = it; settings.onboardingStep = it },
                    finish = { done = true; settings.onboardingDone = true; replayingSetup = false },
                    exitReplay = if(replayingSetup) ({ done = true; settings.onboardingDone = true; replayingSetup = false }) else null,
                    requestNotifications = ::requestNotifications, requestBattery = ::requestBattery, addTile = ::addSendTile)
                else MainScreen(state = SyncRuntime.state.collectAsState().value, lastCrash = lastCrash,
                    notifications = notifications, battery = battery, tile = tile, tileDismissed = tileDismissed,
                    onNotifications = ::requestNotifications, onBattery = ::requestBattery, onTile = ::addSendTile,
                    onDismissTile = { settings.tileDismissed = true; setupRevision++ },
                    onRedo = { replayingSetup = true; step = 0; settings.onboardingStep = 0; done = false; settings.onboardingDone = false },
                    onConnect = { command(ClipboardWatchService.ACTION_CONNECT, it) },
                    onDisconnect = { command(ClipboardWatchService.ACTION_DISCONNECT, it) },
                    onPair = ::pair, onCancelPair = { command(ClipboardWatchService.ACTION_CANCEL_PAIR) }, onPause = ::pause,
                    onForget = { command(ClipboardWatchService.ACTION_FORGET, it) },
                    onRename = { id, name ->
                        val devices = SyncRuntime.state.value.devices.map { if (it.id == id) it.copy(displayName = name) else it }
                        settings.saveDevices(devices); SyncRuntime.update { it.copy(devices = devices) }
                    },
                    onPairCode = { id, code -> SyncRuntime.onPairCode?.invoke(id, code) },
                    onCopyLogs = ::copyLogs, onShareLogs = ::shareLogs, onSaveLogs = ::saveLogs,
                    )
                }
            }
        }
        // Restarts only an already enabled, paired discovery session.
        if (ClipboardReadStore.isServiceEnabled(this) && !SyncRuntime.state.value.running && settings.devices().isNotEmpty()) {
            runCatching { ContextCompat.startForegroundService(this, Intent(this, ClipboardWatchService::class.java)) }
                .onFailure { SyncRuntime.update { state -> state.copy(error = "Tap Reconnect to restart the background connection.") } }
        }
    }
    override fun onResume() {
        super.onResume()
        setupRevision++
        if (::settings.isInitialized && SyncRuntime.state.value.running) {
            startService(Intent(this, ClipboardWatchService::class.java).setAction(ClipboardWatchService.ACTION_RECOVER))
        }
    }
    private fun addSendTile() {
        if (Build.VERSION.SDK_INT < 33) {
            Toast.makeText(this, "Open Quick Settings → Edit, then add Send to PC.", Toast.LENGTH_LONG).show()
            return
        }
        try {
            val manager = getSystemService(android.app.StatusBarManager::class.java)
                ?: error("Quick Settings tile service unavailable")
            manager.requestAddTileService(
                android.content.ComponentName(this, SendClipboardTileService::class.java), "Send to PC",
                android.graphics.drawable.Icon.createWithResource(this, com.clipsync.android.R.drawable.ic_clipsync_status), mainExecutor,
            ) { result ->
                when (result) {
                    android.app.StatusBarManager.TILE_ADD_REQUEST_RESULT_TILE_ADDED,
                    android.app.StatusBarManager.TILE_ADD_REQUEST_RESULT_TILE_ALREADY_ADDED -> {
                        settings.tileAdded = true
                        Toast.makeText(this, "Send to PC is ready in Quick Settings.", Toast.LENGTH_SHORT).show()
                    }
                    android.app.StatusBarManager.TILE_ADD_REQUEST_RESULT_TILE_NOT_ADDED ->
                        Toast.makeText(this, "Tile not added. You can add it from Quick Settings → Edit.", Toast.LENGTH_LONG).show()
                    else -> Toast.makeText(this, "Quick Settings did not add the tile.", Toast.LENGTH_LONG).show()
                }
                setupRevision++
            }
        } catch (e: Exception) {
            FileLogger.warn("Tile add request unavailable: " + e.javaClass.simpleName)
            Toast.makeText(this, "Open Quick Settings → Edit, then add Send to PC.", Toast.LENGTH_LONG).show()
        }
    }    private fun requestNotifications() {
        if (Build.VERSION.SDK_INT >= 33 && ContextCompat.checkSelfPermission(this, Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED && !settings.notificationRequested) {
            settings.notificationRequested = true
            notificationPermission.launch(Manifest.permission.POST_NOTIFICATIONS)
        } else runCatching {
            val intent = if (Build.VERSION.SDK_INT >= 26) Intent(Settings.ACTION_APP_NOTIFICATION_SETTINGS).putExtra(Settings.EXTRA_APP_PACKAGE, packageName)
                else Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS, Uri.parse("package:$packageName"))
            startActivity(intent)
        }
    }
    private fun requestBattery() {
        val power = getSystemService(PowerManager::class.java)
        val destination = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M && power.isIgnoringBatteryOptimizations(packageName)) {
            Intent(Settings.ACTION_IGNORE_BATTERY_OPTIMIZATION_SETTINGS)
        } else {
            Intent(Settings.ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS, Uri.parse("package:$packageName"))
        }
        runCatching { startActivity(destination) }
            .onFailure { runCatching { startActivity(Intent(Settings.ACTION_IGNORE_BATTERY_OPTIMIZATION_SETTINGS)) }
                .onFailure { Toast.makeText(this, "Open FerryClip app settings to review background battery use.", Toast.LENGTH_LONG).show() } }
    }    private fun pair(code: String, replacement: String?) { command(ClipboardWatchService.ACTION_PAIR, replacement, code) }
    private fun command(action: String, deviceId: String? = null, code: String = "") {
        ClipboardReadStore.setServiceEnabled(true)
        try {
            ContextCompat.startForegroundService(this, Intent(this, ClipboardWatchService::class.java).setAction(action)
                .putExtra("deviceId", deviceId).putExtra("pairingCode", code))
        } catch (e: Exception) {
            FileLogger.warn("Service start failed: " + e.javaClass.simpleName)
            SyncRuntime.update { it.copy(error = "Could not start background sync. Try again from the dashboard.") }
        }
    }
    private fun pause(id: String, paused: Boolean) {
        runCatching { startService(Intent(this, ClipboardWatchService::class.java).setAction(ClipboardWatchService.ACTION_PAUSE)
            .putExtra("deviceId", id).putExtra("paused", paused)) }
    }
    private fun copyLogs() {
        val text = FileLogger.getRecentLogs()
        SyncRuntime.receiver.engine.hashGuard.observeLocal(text)
        val payload = com.clipsync.android.logging.DiagnosticsShare.clipboardPayload(text)
        val clip = ClipData.newPlainText(payload.label, payload.text)
        if (payload.isSensitive) clip.description.extras = PersistableBundle().apply {
            putBoolean(if (Build.VERSION.SDK_INT >= 33) "android.content.extra.IS_SENSITIVE" else "android.content.extra.IS_SENSITIVE", true)
        }
        getSystemService(ClipboardManager::class.java).setPrimaryClip(clip)
        FileLogger.info("Diagnostics copied by user")
    }
    private fun shareLogs() {
        startActivity(Intent.createChooser(Intent(Intent.ACTION_SEND).apply {
            type = "text/plain"; putExtra(Intent.EXTRA_SUBJECT, "FerryClip Diagnostics")
            putExtra(Intent.EXTRA_TEXT, FileLogger.getRecentLogs())
        }, "Share FerryClip diagnostics"))
    }
    private fun saveLogs() {
        if (Build.VERSION.SDK_INT < 29) { saveDocument.launch("clipsync-logs.txt"); return }
        var uri: Uri? = null
        try {
            val values = ContentValues().apply {
                put(MediaStore.Downloads.DISPLAY_NAME, "clipsync-logs-${System.currentTimeMillis()}.txt")
                put(MediaStore.Downloads.MIME_TYPE, "text/plain")
                put(MediaStore.Downloads.RELATIVE_PATH, Environment.DIRECTORY_DOWNLOADS)
                put(MediaStore.Downloads.IS_PENDING, 1)
            }
            uri = contentResolver.insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI, values) ?: error("No Downloads destination")
            contentResolver.openOutputStream(uri)?.bufferedWriter()?.use { it.write(FileLogger.getRecentLogs()) } ?: error("No output stream")
            contentResolver.update(uri, ContentValues().apply { put(MediaStore.Downloads.IS_PENDING, 0) }, null, null)
            Toast.makeText(this, "Diagnostics saved to Downloads", Toast.LENGTH_SHORT).show()
        } catch (e: Exception) {
            uri?.let { runCatching { contentResolver.delete(it, null, null) } }
            FileLogger.warn("Diagnostics save failed: ${e.javaClass.simpleName}")
            Toast.makeText(this, "Could not save the diagnostics file.", Toast.LENGTH_LONG).show()
        }
    }
    private fun exportLogs(uri: Uri) {
        try {
            contentResolver.openOutputStream(uri)?.bufferedWriter()?.use { it.write(FileLogger.getRecentLogs()) } ?: error("No output stream")
            Toast.makeText(this, "Diagnostics saved", Toast.LENGTH_SHORT).show()
        } catch (e: Exception) { FileLogger.warn("Diagnostics export failed: ${e.javaClass.simpleName}") }
    }
}
