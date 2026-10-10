package com.clipsync.android.service

import android.app.*
import android.content.*
import android.net.*
import android.net.wifi.WifiManager
import android.os.Build
import android.os.IBinder
import androidx.core.app.NotificationCompat
import androidx.core.content.ContextCompat
import com.clipsync.android.clipboard.ClipboardReadStore
import com.clipsync.android.clipboard.ClipboardWriter
import com.clipsync.android.logging.FileLogger
import com.clipsync.android.store.*
import com.clipsync.android.transport.*
import com.clipsync.android.ui.ClipboardReadActivity
import com.clipsync.android.ui.MainActivity
import com.clipsync.core.ManualSendResult
import com.clipsync.core.ReconnectJournal
import com.clipsync.core.hash
import kotlinx.coroutines.*
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.map

/** Foreground owner for independent, pinned PC sessions. */
class ClipboardWatchService : Service() {
    private data class PeerSession(
        var device: PairedDevice,
        val journal: ReconnectJournal,
        val receiver: com.clipsync.core.ReceiveSession,
        val sender: com.clipsync.core.ManualSendSession,
        var client: TlsClipboardClient? = null,
        var connectionJob: Job? = null,
        var retryJob: Job? = null,
        var generation: Long = 0,
        var retryAttempt: Int = 0,
        var recoveryBlocked: Boolean = false,
    )

    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)
    private lateinit var notifications: NotificationManager
    private lateinit var settings: SyncSettings
    private lateinit var networks: ConnectivityManager
    private lateinit var keyguard: KeyguardManager
    private var wifiLock: WifiManager.WifiLock? = null
    private lateinit var pairingController: PairingController
    private val sessions = linkedMapOf<String, PeerSession>()
    private var sharedReceiverConnected = false
    private var destroyed = false
    private var networkRegistered = false
    private var unlockRegistered = false
    private var reachabilityJob: Job? = null
    private val sendSignal = Channel<String>(Channel.CONFLATED)

    private val networkCallback = object : ConnectivityManager.NetworkCallback() {
        override fun onAvailable(network: Network) { scope.launch { refreshReachability(); recover("Local route available") } }
        override fun onLost(network: Network) { scope.launch {
            sessions.values.toList().forEach { peer ->
                if (peer.client?.network == network || !hasLocalNetwork()) {
                    peer.client?.close()
                    updateDevice(peer.device.id) { it.copy(connectionState = DeviceState.Unreachable, connectedSince = null) }
                    peer.receiver.disconnect(); peer.sender.clear()
                }
            }
            updateAggregate()
            if (!hasLocalNetwork()) SyncRuntime.update { it.copy(networkLabel = "No reachable local network") }
        } }
    }
    private val unlockReceiver = object : BroadcastReceiver() {
        override fun onReceive(context: Context, intent: Intent?) {
            if (keyguard.isKeyguardLocked) return
            if (intent?.action == Intent.ACTION_USER_PRESENT || intent?.action == Intent.ACTION_USER_UNLOCKED || intent?.action == Intent.ACTION_SCREEN_ON) {
                applyDeferred(); recover("Phone unlocked")
            }
        }
    }

    override fun onCreate() {
        super.onCreate()
        settings = SyncSettings(this)
        ClipboardReadStore.initialize(this)
        SyncRuntime.initialize(this)
        networks = getSystemService(ConnectivityManager::class.java)
        keyguard = getSystemService(KeyguardManager::class.java)
        pairingController = PairingController(applicationContext, settings, scope, ::saveDevices, ::connectPairedDevice)
        notifications = getSystemService(NotificationManager::class.java)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            notifications.createNotificationChannel(NotificationChannel(CHANNEL, "FerryClip connection", NotificationManager.IMPORTANCE_LOW).apply {
                description = "Quiet FerryClip connection status and manual clipboard sending"; setSound(null, null); enableVibration(false); setShowBadge(false)
            })
        }
        startForeground(NOTIFICATION_ID, notification(SyncRuntime.state.value))
        ClipboardReadStore.setServiceRunning(true)
        SyncRuntime.update { it.copy(running = true) }
        SyncRuntime.onManualSend = ::offerManualSend
        scope.launch { SyncRuntime.state.map { it.copy(received = 0, sent = 0) }.distinctUntilChanged().collect { notifications.notify(NOTIFICATION_ID, notification(it)) } }
        scope.launch { for (id in sendSignal) drainSends(id) }
        try {
            ContextCompat.registerReceiver(this, unlockReceiver, IntentFilter().apply {
                addAction(Intent.ACTION_USER_PRESENT); addAction(Intent.ACTION_USER_UNLOCKED); addAction(Intent.ACTION_SCREEN_ON)
            }, ContextCompat.RECEIVER_NOT_EXPORTED)
            unlockRegistered = true
            networks.registerNetworkCallback(NetworkRequest.Builder().addCapability(NetworkCapabilities.NET_CAPABILITY_NOT_RESTRICTED).build(), networkCallback)
            networkRegistered = true
        } catch (e: Exception) { FileLogger.warn("Lifecycle callback registration failed: " + e.javaClass.simpleName) }
        scope.launch { var previousRoutes = ""; while (isActive) { val routes = LocalNetwork.routesForLocalPeer(networks).joinToString { "${it.network}:${it.sourceAddress?.hostAddress}/${it.prefixLength}" }; if(routes != previousRoutes) { previousRoutes = routes;
                val available = LocalNetwork.routesForLocalPeer(networks)
                sessions.values.toList().forEach { peer -> if(peer.client?.route != null && peer.client?.route !in available) { peer.client?.close(); updateDevice(peer.device.id) { it.copy(connectionState = DeviceState.Unreachable, connectedSince = null) } } }
                updateAggregate(); refreshReachability(); recover("Local interface changed") }; delay(1000) } }
        FileLogger.info("FerryClip foreground service started; independent PC sessions enabled")
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        if (!ClipboardReadStore.isServiceEnabled(this)) { stopSelf(); return START_NOT_STICKY }
        when (intent?.action) {
            ACTION_PAIR -> pairingController.start(intent.getStringExtra("pairingCode") ?: "", intent.getStringExtra("deviceId"))
            ACTION_CANCEL_PAIR -> pairingController.cancel()
            ACTION_CONNECT -> intent.getStringExtra("deviceId")?.let { id ->
                val target = SyncRuntime.state.value.devices.firstOrNull { it.id == id }
                if (target != null) { saveDevices(DevicePolicy.activate(SyncRuntime.state.value.devices, id)); sessionFor(target.copy(isActive = true)).apply { recoveryBlocked = false; retryAttempt = 0 }; connect(id) }
            }
            ACTION_DISCONNECT -> intent.getStringExtra("deviceId")?.let { stopSession(it, deactivate = true) }
            ACTION_PAUSE -> intent.getStringExtra("deviceId")?.let { id -> setDevicePaused(id, intent.getBooleanExtra("paused", false)) }
            ACTION_FORGET -> intent.getStringExtra("deviceId")?.let { id ->
                stopSession(id, deactivate = false)
                saveDevices(SyncRuntime.state.value.devices.filterNot { it.id == id }); sessions.remove(id)
                if (SyncRuntime.state.value.devices.isEmpty()) { ClipboardReadStore.setServiceEnabled(false); stopSelf() }
            }
            ACTION_RECOVER, null -> recover("Service resume")
            else -> recover("Service resume")
        }
        return START_STICKY
    }

    private fun sessionFor(device: PairedDevice): PeerSession = sessions.getOrPut(device.id) {
        val receiver = com.clipsync.core.ReceiveSession(ClipboardWriter(applicationContext)::write)
        PeerSession(device, ReconnectJournal(settings.deviceId + ":" + device.id), receiver, com.clipsync.core.ManualSendSession(receiver))
    }.also { it.device = device }

    private fun connectPairedDevice(id: String) {
        val current = SyncRuntime.state.value.devices.firstOrNull { it.id == id } ?: return
        if (current.paused || current.connectionState == DeviceState.Connected) return
        if (!current.isActive) saveDevices(DevicePolicy.activate(SyncRuntime.state.value.devices, id))
        sessions[id]?.apply { recoveryBlocked = false; retryAttempt = 0 }
        connect(id)
    }
    private fun connect(id: String) {
        val selected = SyncRuntime.state.value.devices.firstOrNull { it.id == id } ?: return
        val peer = sessionFor(selected)
        if (!selected.isActive || selected.paused) { updateAggregate(); return }
        if (!hasLocalNetwork()) { updateDevice(id) { it.copy(connectionState = DeviceState.Unreachable) }; updateAggregate(); return }
        peer.generation++
        val attempt = peer.generation
        peer.retryJob?.cancel(); peer.retryJob = null
        val previous = peer.connectionJob
        peer.client?.close(); previous?.cancel(); peer.client = null
        peer.sender.clear(); peer.receiver.disconnect()
        updateDevice(id) { it.copy(connectionState = DeviceState.Connecting, connectedSince = null) }
        SyncRuntime.update { it.copy(error = null, sendFeedback = null) }
        acquireWifiLock()
        peer.connectionJob = scope.launch {
            previous?.join()
            var shouldRetry = false
            var identityChanged = false
            try {
                val endpoint = withContext(Dispatchers.IO) {
                    com.clipsync.android.security.KeyStoreIdentity()
                    PcDiscovery(applicationContext, settings).race(false) { hint ->
                        val identity = try { ControlClient(applicationContext, selected.certFingerprint).probe(hint) }
                        catch (e: Exception) {
                            if (generateSequence<Throwable>(e) { it.cause }.any { it is com.clipsync.android.security.PeerPinMismatchException } && formatAddress(hint) == selected.lastKnownAddress) {
                                identityChanged = true
                                withContext(Dispatchers.Main) { updateDevice(id) { it.copy(connectionState = DeviceState.IdentityChanged, connectedSince = null) } }
                            }
                            throw e
                        }
                        if (!identity.fingerprint.equals(selected.certFingerprint, true)) {
                            if (formatAddress(hint) == selected.lastKnownAddress) withContext(Dispatchers.Main) { updateDevice(id) { it.copy(connectionState = DeviceState.IdentityChanged, connectedSince = null) } }
                            throw java.io.IOException("Candidate identity did not match the saved PC")
                        }
                        hint
                    }
                }
                val transport = TlsClipboardClient(applicationContext, settings, peer.journal, selected.certFingerprint) { peer.device.paused }
                peer.client = transport
                withContext(Dispatchers.IO) {
                    transport.run(endpoint.address, endpoint.port, routeOverride = endpoint.route,
                        connected = { withContext(Dispatchers.Main) {
                            check(attempt == peer.generation); check(hasLocalNetwork())
                            peer.retryAttempt = 0
                            val now = System.currentTimeMillis()
                            updateDevice(id) { it.copy(lastKnownAddress = formatAddress(endpoint), lastConnectedAt = now, connectedSince = now,
                                connectionState = if (peer.device.paused) DeviceState.Paused else DeviceState.Connected) }
                            peer.receiver.connected(peer.device.paused)
                            SyncRuntime.update { it.copy(address = formatAddress(endpoint), error = null) }
                            updateAggregate(); FileLogger.info("Connected to PC " + selected.displayName + ": TLS 1.3, pinned identity"); applyDeferred()
                        } },
                        receive = { text -> withContext(Dispatchers.Main) {
                            check(attempt == peer.generation)
                            if (!peer.device.paused && sharedReceiverConnected) {
                                val applied = SyncRuntime.receiver.receive(text, keyguard.isKeyguardLocked)
                                sessions.values.forEach { it.receiver.engine.hashGuard.observeLocal(text) }
                                SyncRuntime.update { it.copy(received = SyncRuntime.receiver.received, pendingUnlock = SyncRuntime.receiver.hasDeferred) }
                                if (applied) FileLogger.info("Clipboard received from " + peer.device.displayName + ": text length=" + text.length + " hash=" + hash(text.toByteArray()).take(6))
                                else FileLogger.info(if (SyncRuntime.receiver.hasDeferred) "PC clipboard deferred until unlock (latest only)" else "Clipboard not applied: paused or duplicate")
                            }
                        } })
                }
            } catch (e: TimeoutCancellationException) {
                if (attempt == peer.generation) { shouldRetry = true; failPeer(id, "Connection timed out. Reconnecting…", e) }
            } catch (e: CancellationException) { throw e
            } catch (e: Exception) {
                if (attempt == peer.generation) {
                    val stage = peer.client?.stage ?: ConnectionStage.IDLE
                    shouldRetry = !identityChanged && ((peer.client == null && e is java.io.IOException) || RecoveryPolicy.retry(stage, e, false))
                    val message = if (e is ConnectionProblem) e.message ?: "Connection failed." else ConnectionDiagnostics.userMessage(stage, e)
                    failPeer(id, if (shouldRetry) "Connection interrupted. Reconnecting on the local network…" else message, e)
                }
            } finally {
                if (attempt == peer.generation) {
                    peer.client?.close(); peer.client = null; peer.sender.clear(); peer.receiver.disconnect()
                    if (identityChanged) updateDevice(id) { it.copy(connectionState = DeviceState.IdentityChanged, connectedSince = null) }
                    else if (peer.device.isActive && peer.device.paused) updateDevice(id) { it.copy(connectionState = DeviceState.Paused, connectedSince = null) }
                    else updateDevice(id) { it.copy(connectionState = DeviceState.Unreachable, connectedSince = null) }
                    updateAggregate(); releaseWifiLockIfIdle()
                    if (shouldRetry && canRecover(peer)) peer.retryJob = scope.launch { delay(RecoveryPolicy.delayMillis(peer.retryAttempt++)); recoverPeer(peer, "Connection retry") }
                }
            }
        }
    }

    private fun canRecover(peer: PeerSession) = !destroyed && !peer.recoveryBlocked && ClipboardReadStore.isServiceEnabled(this) && peer.device.isActive && !peer.device.paused && hasLocalNetwork()
    private fun recover(reason: String, replaceConnected: Boolean = false) {
        if (destroyed || !ClipboardReadStore.isServiceEnabled(this) || !hasLocalNetwork()) return
        sessions.values.toList().forEach { peer ->
            if (!peer.device.isActive || peer.device.paused) return@forEach
            if (peer.connectionJob?.isActive == true && !(replaceConnected && peer.device.connectionState == DeviceState.Connected)) return@forEach
            recoverPeer(peer, reason)
        }
    }
    private fun recoverPeer(peer: PeerSession, reason: String) {
        if (!canRecover(peer)) return
        FileLogger.info("Restoring " + peer.device.displayName + " connection: " + reason)
        peer.retryJob?.cancel(); peer.retryJob = null; connect(peer.device.id)
    }
    private fun stopSession(id: String, deactivate: Boolean) {
        val peer = sessions[id]
        if (peer != null) {
            peer.generation++; peer.client?.close(); peer.client = null; peer.connectionJob?.cancel(); peer.connectionJob = null
            peer.retryJob?.cancel(); peer.retryJob = null; peer.sender.clear(); peer.receiver.disconnect(); peer.journal.clear()
        }
        if (deactivate) saveDevices(DevicePolicy.deactivate(SyncRuntime.state.value.devices, id))
        updateAggregate(); releaseWifiLockIfIdle()
    }
    private fun setDevicePaused(id: String, paused: Boolean) {
        if (SyncRuntime.state.value.devices.none { it.id == id }) return
        saveDevices(SyncRuntime.state.value.devices.map { if (it.id == id) it.copy(paused = paused) else it })
        val peer = sessions[id]
        if (peer != null) {
            peer.receiver.setPaused(paused)
            if (paused) { peer.sender.clear(); peer.journal.clear(); if (peer.client != null) updateDevice(id) { it.copy(connectionState = DeviceState.Paused) } }
            else if (peer.device.isActive && peer.client == null) { peer.recoveryBlocked = false; connect(id) }
            else if (peer.client != null) updateDevice(id) { it.copy(connectionState = DeviceState.Connected) }
        }
        updateAggregate()
        val name = SyncRuntime.state.value.devices.first { it.id == id }.displayName
        FileLogger.info("PC session " + (if (paused) "paused: " else "resumed: ") + name)
    }

    private fun hasLocalNetwork() = LocalNetwork.routeForLocalPeer(networks) != null
    private fun formatAddress(endpoint: PcEndpoint) = if (endpoint.address.contains(":") && !endpoint.address.startsWith("[")) "[" + endpoint.address + "]:" + endpoint.port else endpoint.address + ":" + endpoint.port
    private fun acquireWifiLock() {
        if (wifiLock?.isHeld == true) return
        val wifi = applicationContext.getSystemService(Context.WIFI_SERVICE) as WifiManager
        wifiLock = wifi.createWifiLock(WifiManager.WIFI_MODE_FULL_HIGH_PERF, "FerryClip:connections").apply { setReferenceCounted(false); acquire() }
    }
    private fun releaseWifiLockIfIdle() {
        if (sessions.values.any { it.client != null || it.connectionJob?.isActive == true }) return
        wifiLock?.let { if (it.isHeld) it.release() }; wifiLock = null
    }
    private fun updateDevice(id: String, transform: (PairedDevice) -> PairedDevice) {
        saveDevices(SyncRuntime.state.value.devices.map { if (it.id == id) transform(it) else it })
    }
    private fun saveDevices(devices: List<PairedDevice>) {
        settings.saveDevices(devices); devices.forEach { sessions[it.id]?.device = it }
        SyncRuntime.update { it.copy(devices = DevicePolicy.ordered(devices), paired = devices.isNotEmpty()) }; updateAggregate()
    }
    private fun updateAggregate() {
        val devices = SyncRuntime.state.value.devices
        val live = devices.filter { it.connectionState in setOf(DeviceState.Connected, DeviceState.Paused) }
        val unpausedLive = live.any { !it.paused }
        if (unpausedLive && !sharedReceiverConnected) { SyncRuntime.receiver.connected(false); sharedReceiverConnected = true }
        else if (!unpausedLive && sharedReceiverConnected) { SyncRuntime.receiver.disconnect(); sharedReceiverConnected = false }
        val active = devices.filter { it.isActive }
        SyncRuntime.update { old -> old.copy(devices = DevicePolicy.ordered(devices), paired = devices.isNotEmpty(), connected = live.isNotEmpty(),
            connecting = devices.any { it.connectionState == DeviceState.Connecting }, paused = active.isNotEmpty() && active.all { it.paused },
            pendingUnlock = SyncRuntime.receiver.hasDeferred) }
    }

    private fun refreshReachability() {
        if (!hasLocalNetwork()) { SyncRuntime.update { it.copy(networkLabel = "No reachable local network") }; return }
        @Suppress("DEPRECATION")
        val hotspot = networks.allNetworks.any { networks.getLinkProperties(it)?.routes?.any { route -> route.gateway?.hostAddress == "192.168.137.1" } == true }
        val phoneHotspot = LocalNetwork.routesForLocalPeer(networks).any { it.sourceAddress != null }
        SyncRuntime.update { it.copy(networkLabel = if (phoneHotspot) "Phone hotspot · local connection" else if (hotspot) "PC hotspot · local connection" else "Local network · internet not required") }
        if (SyncRuntime.state.value.devices.isEmpty() || reachabilityJob?.isActive == true) return
        val scanned = SyncRuntime.state.value.devices.filterNot { it.isActive }.map { it.id }.toSet()
        if (scanned.isEmpty()) return
        reachabilityJob = scope.launch {
            val seen = mutableSetOf<String>()
            try { PcDiscovery(applicationContext, settings).scan(5000) { endpoint ->
                val identity = try { ControlClient(applicationContext).probe(endpoint) } catch (e: CancellationException) { throw e } catch (_: Exception) { return@scan }
                withContext(Dispatchers.Main) {
                    val address = formatAddress(endpoint)
                    val match = SyncRuntime.state.value.devices.firstOrNull { it.certFingerprint.equals(identity.fingerprint, true) }
                    if (match != null) {
                        seen += match.id
                        if (!match.isActive) updateDevice(match.id) { it.copy(lastKnownAddress = address, connectionState = if (it.paused) DeviceState.Paused else DeviceState.Available) }
                    } else SyncRuntime.update { old -> old.copy(devices = old.devices.map { if (!it.isActive && it.lastKnownAddress == address) it.copy(connectionState = DeviceState.IdentityChanged) else it }) }
                }
            } } catch (e: CancellationException) { throw e } catch (_: Exception) { }
            SyncRuntime.update { old -> old.copy(devices = old.devices.map { if (it.id in scanned && it.id !in seen && !it.isActive && !it.paused && it.connectionState != DeviceState.IdentityChanged) it.copy(connectionState = DeviceState.Unreachable) else it }) }
        }
    }

    private fun offerManualSend(text: String, sensitive: Boolean): ManualSendResult {
        if (text.isEmpty()) return ManualSendResult.EMPTY
        if (text.toByteArray().size > com.clipsync.core.SessionProtocol.MAX_TEXT) return ManualSendResult.TOO_LARGE
        if (sensitive) return ManualSendResult.SENSITIVE
        val targets = sessions.values.filter { it.device.isActive && it.client != null }
        if (targets.isEmpty()) return ManualSendResult.DISCONNECTED
        val results = targets.map { it to it.sender.offer(text, false) }
        val queued = results.filter { it.second == ManualSendResult.QUEUED }
        queued.forEach { sendSignal.trySend(it.first.device.id) }
        val result = when {
            queued.isNotEmpty() -> ManualSendResult.QUEUED
            results.any { it.second == ManualSendResult.ECHO } -> ManualSendResult.ECHO
            results.any { it.second == ManualSendResult.DUPLICATE } -> ManualSendResult.DUPLICATE
            results.all { it.first.device.paused } -> ManualSendResult.PAUSED
            else -> results.first().second
        }
        SyncRuntime.update { it.copy(sendFeedback = when (result) {
            ManualSendResult.QUEUED -> "Sending to " + queued.size + " connected PC(s)…"
            ManualSendResult.ECHO -> "Already received from a PC; not sent back."
            ManualSendResult.DUPLICATE -> "Already sent; no duplicate needed."
            ManualSendResult.SENSITIVE -> "Sensitive clipboard skipped."
            ManualSendResult.PAUSED -> "All connected PCs are paused."
            ManualSendResult.DISCONNECTED -> "Connect a PC before sending."
            ManualSendResult.EMPTY -> "No text to send."
            ManualSendResult.TOO_LARGE -> "Not sent: text exceeds the 1 MiB limit."
        }) }
        return result
    }
    private suspend fun drainSends(id: String) {
        val peer = sessions[id] ?: return
        while (true) {
            val clip = peer.sender.take() ?: return
            val transport = peer.client ?: return
            val attempt = peer.generation
            val watchdog = scope.launch { delay(5000); if (attempt == peer.generation) { FileLogger.warn("Clipboard send deadline exceeded for " + peer.device.displayName); transport.close() } }
            try {
                withContext(Dispatchers.IO) { transport.sendText(clip.message.text ?: "") }
                if (attempt == peer.generation && !peer.device.paused) {
                    peer.sender.completed(clip); SyncRuntime.update { it.copy(sent = it.sent + 1, sendFeedback = "Clipboard sent to " + peer.device.displayName + ".") }
                    FileLogger.info("Clipboard sent to " + peer.device.displayName + ": text length=" + (clip.message.text?.length ?: 0) + " hash=" + clip.hash.take(6))
                }
            } catch (e: CancellationException) { throw e
            } catch (e: Exception) {
                if (attempt == peer.generation) { peer.sender.clear(); SyncRuntime.update { it.copy(sendFeedback = "Send interrupted. Reconnect to " + peer.device.displayName + ".") }; transport.close() }
                return
            } finally { watchdog.cancel() }
        }
    }

    private fun applyDeferred() {
        if (keyguard.isKeyguardLocked || !SyncRuntime.receiver.hasDeferred) return
        try {
            if (SyncRuntime.receiver.flushAfterUnlock()) FileLogger.info("Deferred PC clipboard applied after unlock")
            SyncRuntime.update { it.copy(received = SyncRuntime.receiver.received, pendingUnlock = SyncRuntime.receiver.hasDeferred) }
        } catch (e: Exception) { FileLogger.warn("Deferred clipboard write failed: " + e.javaClass.simpleName) }
    }
    private fun failPeer(id: String, message: String, exception: Exception) {
        SyncRuntime.update { it.copy(error = message) }
        val peer = sessions[id]
        FileLogger.warn("Connection to " + (peer?.device?.displayName ?: id) + " ended: " + ConnectionDiagnostics.summary(peer?.client?.stage ?: ConnectionStage.IDLE, exception))
    }
    override fun onTaskRemoved(rootIntent: Intent?) { FileLogger.info("UI task removed; foreground sync remains enabled"); super.onTaskRemoved(rootIntent) }
    override fun onDestroy() {
        pairingController.close(); destroyed = true
        sessions.values.forEach { peer -> peer.generation++; peer.client?.close(); peer.connectionJob?.cancel(); peer.retryJob?.cancel(); peer.sender.clear(); peer.receiver.disconnect() }
        sessions.clear(); scope.cancel(); sendSignal.close()
        if (networkRegistered) runCatching { networks.unregisterNetworkCallback(networkCallback) }
        if (unlockRegistered) runCatching { unregisterReceiver(unlockReceiver) }
        SyncRuntime.onManualSend = null; SyncRuntime.cancelPairing(); SyncRuntime.sender.clear()
        SyncRuntime.receiver.disconnect(); SyncRuntime.receiver.clearDeferred(); sharedReceiverConnected = false
        wifiLock?.let { if (it.isHeld) it.release() }; wifiLock = null
        ClipboardReadStore.setServiceRunning(false)
        SyncRuntime.update { it.copy(running = false, connecting = false, connected = false, pendingUnlock = false, error = null, devices = DevicePolicy.restore(it.devices)) }
        FileLogger.info("FerryClip service stopped; saved PC connections remain available"); super.onDestroy()
    }
    override fun onBind(intent: Intent?): IBinder? = null
    private fun notification(state: SyncUiState): Notification {
        val open = PendingIntent.getActivity(this, 0, Intent(this, MainActivity::class.java), PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE)
        val send = PendingIntent.getActivity(this, 2205, ClipboardReadActivity.createIntent(this, "notification"), PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE)
        val activeCount = state.devices.count { it.connectionState == DeviceState.Connected }
        val connected = state.connected && activeCount > 0
        val status = when {
            state.pairing != null -> "Enter the PC code to pair"
            state.paused -> "Sync paused"
            state.pendingUnlock -> "PC copy waiting for unlock"
            connected -> "Connected to $activeCount ${if (activeCount == 1) "PC" else "PCs"}"
            state.sendFeedback != null -> state.sendFeedback
            else -> "Waiting for a PC"
        }
        val builder = NotificationCompat.Builder(this, CHANNEL)
            .setSmallIcon(com.clipsync.android.R.drawable.ic_clipsync_status)
            .setColor(if (connected) 0xFF14C8D2.toInt() else 0xFF607D8B.toInt())
            .setContentTitle("FerryClip")
            .setContentText(status)
            .setContentIntent(open)
            .setOngoing(true)
            .setSilent(true)
            .setOnlyAlertOnce(true)
            .setPriority(NotificationCompat.PRIORITY_LOW)
            .setCategory(NotificationCompat.CATEGORY_SERVICE)
        if (connected && !state.paused) {
            val compact = android.widget.RemoteViews(packageName, com.clipsync.android.R.layout.notification_sync)
            compact.setTextViewText(com.clipsync.android.R.id.notification_status, status)
            compact.setOnClickPendingIntent(com.clipsync.android.R.id.notification_send, send)
            compact.setContentDescription(com.clipsync.android.R.id.notification_send,
                "Send clipboard to $activeCount ${if (activeCount == 1) "PC" else "PCs"}")
            builder.setCustomContentView(compact)
                .setStyle(NotificationCompat.DecoratedCustomViewStyle())
            builder.addAction(NotificationCompat.Action.Builder(
                com.clipsync.android.R.drawable.ic_notification_send,
                "Send clipboard",
                send,
            ).setAuthenticationRequired(true).build())
        }
        return builder.build()
    }
    companion object {
        const val ACTION_FORGET = "com.clipsync.android.FORGET"
        const val ACTION_CONNECT = "com.clipsync.android.CONNECT"
        const val ACTION_DISCONNECT = "com.clipsync.android.DISCONNECT"
        const val ACTION_CANCEL_PAIR = "com.clipsync.android.CANCEL_PAIR"
        const val ACTION_PAIR = "com.clipsync.android.PAIR"
        const val ACTION_PAUSE = "com.clipsync.android.PAUSE"
        const val ACTION_RECOVER = "com.clipsync.android.RECOVER"
        private const val CHANNEL = "ferryclip_status_v3"
        private const val NOTIFICATION_ID = 2201
    }
}
