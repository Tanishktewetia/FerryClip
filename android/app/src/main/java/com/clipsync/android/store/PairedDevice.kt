package com.clipsync.android.store

enum class DeviceState { Connected, Connecting, Available, Paused, Unreachable, IdentityChanged }
data class PairedDevice(
    val id: String,
    val displayName: String,
    val hostLabel: String,
    val certFingerprint: String,
    val lastKnownAddress: String,
    val lastConnectedAt: Long? = null,
    val connectionState: DeviceState = DeviceState.Available,
    val isActive: Boolean = false,
    val connectedSince: Long? = null,
    val paused: Boolean = false,
)

/** Pure state transitions: discovery can change reachability, never select a peer. */
object DevicePolicy {
    fun ordered(devices: List<PairedDevice>) = devices.sortedWith(compareByDescending<PairedDevice> { it.isActive }
        .thenByDescending { it.lastConnectedAt ?: 0L }.thenBy { it.id })
    fun restore(devices: List<PairedDevice>) = devices.map { it.copy(connectionState = when { it.paused -> DeviceState.Paused; it.isActive -> DeviceState.Connecting; else -> DeviceState.Available }, connectedSince = null) }
    fun wifiLost(devices: List<PairedDevice>) = devices.map { it.copy(connectionState = DeviceState.Unreachable, connectedSince = null) }
    fun activate(devices: List<PairedDevice>, id: String): List<PairedDevice> {
        require(devices.any { it.id == id })
        return devices.map { if (it.id == id) it.copy(isActive = true, connectedSince = null, connectionState = DeviceState.Connecting) else it }
    }
    fun deactivate(devices: List<PairedDevice>, id: String): List<PairedDevice> = devices.map {
        if (it.id == id) it.copy(isActive = false, connectedSince = null, connectionState = if (it.paused) DeviceState.Paused else DeviceState.Available) else it
    }
    fun merge(devices: List<PairedDevice>, incoming: PairedDevice, replacementId: String? = null): List<PairedDevice> {
        val existing = devices.firstOrNull { it.certFingerprint.equals(incoming.certFingerprint, true) }
            ?: replacementId?.let { id -> devices.firstOrNull { it.id == id } }
        return if (existing == null) devices + incoming.copy(isActive = false, connectionState = DeviceState.Available)
        else devices.map { if (it.id == existing.id) it.copy(certFingerprint = incoming.certFingerprint,
            hostLabel = incoming.hostLabel, lastKnownAddress = incoming.lastKnownAddress,
            connectionState = if (it.connectionState == DeviceState.IdentityChanged) DeviceState.Available else it.connectionState) else it }
    }
}
