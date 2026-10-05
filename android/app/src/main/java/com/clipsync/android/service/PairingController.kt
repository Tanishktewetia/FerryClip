package com.clipsync.android.service

import android.content.Context
import com.clipsync.android.logging.FileLogger
import com.clipsync.android.store.*
import com.clipsync.android.transport.*
import kotlinx.coroutines.*
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicReference

/** Pairs only the PC whose short-lived code was typed by the user. Discovery stays automatic. */
class PairingController(
    private val context: Context,
    private val settings: SyncSettings,
    private val scope: CoroutineScope,
    private val save: (List<PairedDevice>) -> Unit,
    private val onPaired: (String) -> Unit = {},
) {
    private var search: Job? = null
    private val attemptLock = Mutex()

    fun start(code: String, replacementId: String? = null) {
        cancel()
        if (!code.matches(Regex("[0-9]{6}"))) {
            SyncRuntime.update { it.copy(pairingMessage = "Enter the six-digit code shown on your PC.", pairingFailed = true) }
            return
        }
        SyncRuntime.clearPairingFeedback()
        val revision = SyncRuntime.feedbackRevision
        search = scope.launch {
            SyncRuntime.update { it.copy(searching = true, pairingMessage = null, pairingFailed = false) }
            val sawOpenPc = AtomicBoolean(false)
            val wrongCode = AtomicBoolean(false)
            val paired = AtomicBoolean(false)
            val failure = AtomicReference<String?>(null)
            try {
                suspend fun tryEndpoint(endpoint: PcEndpoint) {
                    if (paired.get() || wrongCode.get()) return
                    val probe = try { ControlClient(context).probe(endpoint) }
                    catch (e: CancellationException) { throw e }
                    catch (e: Exception) { failure.set((e as? ConnectionProblem)?.message); return }
                    if (!probe.pairingOpen || paired.get()) return
                    sawOpenPc.set(true)
                    attemptLock.withLock {
                        if (paired.get() || wrongCode.get()) return
                        try {
                            val identity = ControlClient(context).pair(endpoint, code)
                            withContext(Dispatchers.Main) {
                                val before = SyncRuntime.state.value.devices
                                val existing = before.firstOrNull { it.certFingerprint.equals(identity.fingerprint, true) }
                                val incoming = PairedDevice(
                                    id = existing?.id ?: java.util.UUID.randomUUID().toString(),
                                    displayName = existing?.displayName ?: identity.host,
                                    hostLabel = identity.host,
                                    certFingerprint = identity.fingerprint,
                                    lastKnownAddress = formatEndpoint(endpoint),
                                )
                                save(DevicePolicy.merge(before, incoming, replacementId))
                                if (existing == null || (!existing.paused && existing.connectionState != DeviceState.Connected)) onPaired(incoming.id)
                                SyncRuntime.updatePairingFeedback(revision) {
                                    it.copy(pairingMessage = null, pairingFailed = false)
                                }
                                FileLogger.info("Paired PC after typed one-time code: ${endpoint.address}:${endpoint.port}")
                            }
                            paired.set(true)
                        } catch (e: CancellationException) { throw e }
                        catch (_: WrongPairingCodeException) { wrongCode.set(true) }
                        catch (e: Exception) { failure.set((e as? ConnectionProblem)?.message ?: ControlFailure.message("pairing", e)) }
                    }
                }
                PcDiscovery(context, settings).scan(12000, shouldStop = { paired.get() || wrongCode.get() }) { endpoint -> tryEndpoint(endpoint) }
            } catch (e: CancellationException) { throw e }
            catch (_: Exception) { failure.set("Keep both devices on the same Wi-Fi or hotspot and allow FerryClip through Windows Firewall.") }
            finally {
                if (currentCoroutineContext().isActive && !paired.get()) {
                    val message = when {
                        wrongCode.get() -> "That code didn't match. Check the six digits on the PC and try again."
                        !sawOpenPc.get() -> failure.get() ?: "No FerryClip pairing window was found. Choose Generate code to connect on the PC and keep both devices on a reachable local network."
                        else -> failure.get() ?: "Pairing could not finish. Open a fresh code on the PC and try again."
                    }
                    SyncRuntime.updatePairingFeedback(revision) { it.copy(pairingMessage = message, pairingFailed = true) }
                }
                SyncRuntime.updatePairingFeedback(revision) { it.copy(searching = false) }
            }
        }
    }

    private fun formatEndpoint(endpoint: PcEndpoint): String = if (endpoint.address.contains(":") && !endpoint.address.startsWith("[")) "[${endpoint.address}]:${endpoint.port}" else "${endpoint.address}:${endpoint.port}"

    fun cancel() { SyncRuntime.clearPairingFeedback(); search?.cancel(); search = null; SyncRuntime.update { it.copy(searching = false) } }
    fun close() { cancel(); SyncRuntime.onPairCode = null }
}
