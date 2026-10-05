package com.clipsync.android.transport

import android.content.Context
import android.net.ConnectivityManager
import android.net.NetworkCapabilities
import android.net.nsd.NsdManager
import android.net.nsd.NsdServiceInfo
import com.clipsync.android.store.SyncSettings
import kotlinx.coroutines.*
import kotlin.coroutines.resume
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.sync.Semaphore
import kotlinx.coroutines.sync.withPermit
import java.net.Inet4Address

/** Discovery hints are untrusted. The TLS client must authenticate every candidate. */
data class PcEndpoint(val address: String, val port: Int = TlsClipboardClient.PORT, val route: LocalPeerRoute? = null) {
    companion object {
        fun parse(value: String): PcEndpoint? {
            val text = value.trim()
            if (text.isEmpty()) return null
            val address: String
            val port: Int
            if (text.startsWith("[")) {
                val end = text.indexOf(']')
                if (end <= 1) return null
                address = text.substring(1, end)
                val suffix = text.substring(end + 1)
                if (suffix.isNotEmpty() && !suffix.startsWith(":")) return null
                port = if (suffix.isEmpty()) TlsClipboardClient.PORT else suffix.drop(1).toIntOrNull() ?: return null
            } else if (text.count { it == ':' } > 1) {
                address = text
                port = TlsClipboardClient.PORT
            } else {
                val parts = text.split(':')
                if (parts.size !in 1..2) return null
                address = parts[0]
                port = if (parts.size == 1) TlsClipboardClient.PORT else parts[1].toIntOrNull() ?: return null
            }
            if (address.isBlank() || port !in 1..65535) return null
            return PcEndpoint(address, port)
        }
    }
}
class PcDiscovery(context: Context, private val settings: SyncSettings) {
    private val manager = context.getSystemService(ConnectivityManager::class.java)
    private val nsd = context.getSystemService(NsdManager::class.java)

    @Suppress("DEPRECATION")
    suspend fun scan(duration: Long = 30000, shouldStop: () -> Boolean = { false }, connect: suspend (PcEndpoint) -> Unit) = coroutineScope {
        val candidates = Channel<PcEndpoint>(Channel.UNLIMITED)
        val seen = mutableSetOf<PcEndpoint>()
        val jobs = mutableListOf<Job>()
        val probeSlots = Semaphore(24)
        val resolveQueue = Channel<NsdServiceInfo>(16)
        val localRoutes = LocalNetwork.routesForLocalPeer(manager)
        var discoveryStarted = false
        val listener = object : NsdManager.DiscoveryListener {
            override fun onDiscoveryStarted(type: String) { discoveryStarted = true }
            override fun onDiscoveryStopped(type: String) { discoveryStarted = false }
            override fun onStartDiscoveryFailed(type: String, code: Int) { discoveryStarted = false }
            override fun onStopDiscoveryFailed(type: String, code: Int) { }
            override fun onServiceLost(info: NsdServiceInfo) { }
            override fun onServiceFound(info: NsdServiceInfo) {
                if (info.serviceType.trimEnd('.') == "_clipsync._tcp") resolveQueue.trySend(info)
            }
        }
        val announcementSlots = Semaphore(4)
        val announced = mutableSetOf<PcEndpoint>()
        val announcements = launch(Dispatchers.IO) {
            val udp = java.net.DatagramSocket(null)
            val closer = launch(Dispatchers.Default, start = CoroutineStart.UNDISPATCHED) { try { awaitCancellation() } finally { udp.close() } }
            try {
                udp.reuseAddress = true; udp.bind(java.net.InetSocketAddress(48653)); udp.soTimeout = 500
                val bytes = ByteArray(256)
                while(isActive) {
                    val packet = java.net.DatagramPacket(bytes, bytes.size)
                    try { udp.receive(packet) } catch (_: java.net.SocketTimeoutException) { continue }
                    val hint = LanAnnouncementHint.parse(String(packet.data, packet.offset, packet.length, Charsets.US_ASCII)) ?: continue
                    LocalNetwork.routesForAddress(manager, hint.address).forEach { route ->
                        val endpoint = PcEndpoint(hint.address, hint.port, route)
                        if(announced.add(endpoint)) launch(Dispatchers.IO) { try { announcementSlots.withPermit { connect(endpoint) } } catch(e: CancellationException) { throw e } catch(_: Exception) { } }
                    }
                }
            } catch (e: CancellationException) { throw e } catch (_: java.net.SocketException) { }
            finally { closer.cancel(); udp.close() }
        }
        val resolver = launch {
            for (info in resolveQueue) {
                withTimeoutOrNull(2000) {
                    suspendCancellableCoroutine<Unit> { continuation ->
                        nsd.resolveService(info, object : NsdManager.ResolveListener {
                            override fun onResolveFailed(service: NsdServiceInfo, code: Int) { if (continuation.isActive) continuation.resume(Unit) }
                            override fun onServiceResolved(service: NsdServiceInfo) {
                                val ip = service.host
                                if (continuation.isActive && ip is Inet4Address && !ip.isLoopbackAddress && !ip.isAnyLocalAddress && service.port in 1..65535)
                                    candidates.trySend(PcEndpoint(ip.hostAddress!!, service.port))
                                if (continuation.isActive) continuation.resume(Unit)
                            }
                        })
                    }
                }
            }
        }
        // Saved peers go FIRST, not behind thousands of nonexistent subnet hosts.
        settings.devices().sortedByDescending { it.isActive }.forEach { device -> PcEndpoint.parse(device.lastKnownAddress)?.let { saved ->
            LocalNetwork.routesForAddress(manager, saved.address).forEach { candidates.trySend(saved.copy(route = it)) }
        } }
        if (settings.lastAddress.isNotBlank()) LocalNetwork.routesForAddress(manager, settings.lastAddress).forEach { candidates.trySend(PcEndpoint(settings.lastAddress, settings.lastPort, it)) }
        // Enumerate only bounded RFC1918/CGN private prefixes from current LAN-capable interfaces.
        // Local interface candidates cover phone hotspots, VPN/Ethernet LAN routes, and multicast-DNS isolation.
        manager.allNetworks.filter { network ->
            manager.getNetworkCapabilities(network)?.let { caps ->
                caps.hasTransport(NetworkCapabilities.TRANSPORT_WIFI) ||
                    caps.hasTransport(NetworkCapabilities.TRANSPORT_ETHERNET) ||
                    caps.hasTransport(NetworkCapabilities.TRANSPORT_VPN)
            } == true
        }.forEach { network ->
            manager.getLinkProperties(network)?.linkAddresses.orEmpty().forEach linkLoop@{ link ->
                val ip = link.address as? Inet4Address ?: return@linkLoop
                if (ip.isLoopbackAddress || ip.isAnyLocalAddress) return@linkLoop
                Ipv4Subnet.hosts(ip.hostAddress ?: return@linkLoop, link.prefixLength)
                    .forEach { candidates.trySend(PcEndpoint(it, route = LocalPeerRoute(network = network))) }
            }
        }
        // VPNs may expose a /32 interface address while advertising LAN routes separately.
        manager.allNetworks.filter { manager.getNetworkCapabilities(it)?.hasTransport(NetworkCapabilities.TRANSPORT_VPN) == true }.forEach { network ->
            manager.getLinkProperties(network)?.routes.orEmpty().forEach routeLoop@{ route ->
                val destination = route.destination.address as? Inet4Address ?: return@routeLoop
                Ipv4Subnet.hosts(destination.hostAddress ?: return@routeLoop, route.destination.prefixLength)
                    .forEach { candidates.trySend(PcEndpoint(it, route = LocalPeerRoute(network = network))) }
            }
        }
        localRoutes.filter { it.network == null && it.sourceAddress != null }.forEach { route ->
            val localAddress = route.sourceAddress?.hostAddress ?: return@forEach
            val prefix = route.prefixLength ?: return@forEach
            Ipv4Subnet.hosts(localAddress, prefix).forEach { candidates.trySend(PcEndpoint(it, route = route)) }
        }
        PcEndpoint.parse(settings.address)?.let { candidates.trySend(it) }
        settings.devices().sortedByDescending { it.isActive }.forEach { device -> PcEndpoint.parse(device.lastKnownAddress)?.let { candidates.trySend(it) } }
        if (settings.lastAddress.isNotBlank()) candidates.trySend(PcEndpoint(settings.lastAddress, settings.lastPort))
        try { nsd.discoverServices("_clipsync._tcp.", NsdManager.PROTOCOL_DNS_SD, listener); discoveryStarted = true } catch (_: RuntimeException) { }
        val producer = launch {
            for (candidate in candidates) {
                val routes = candidate.route?.let(::listOf) ?: LocalNetwork.routesForAddress(manager, candidate.address)
                val endpoints = if (routes.isEmpty()) listOf(candidate) else routes.map { candidate.copy(route = it) }
                for (endpoint in endpoints) {
                    if (!seen.add(endpoint)) continue
                    jobs += launch(Dispatchers.IO) {
                        try { probeSlots.withPermit { connect(endpoint) } }
                        catch (e: CancellationException) { throw e }
                        catch (_: Exception) { }
                    }
                }
            }
        }
        try {
            val deadline = android.os.SystemClock.elapsedRealtime() + duration
            while (kotlin.coroutines.coroutineContext.isActive && !shouldStop()) {
                val remaining = deadline - android.os.SystemClock.elapsedRealtime()
                if (remaining <= 0) break
                delay(minOf(100L, remaining))
            }
        } finally {
            producer.cancelAndJoin()
            jobs.forEach { it.cancel() }
            jobs.joinAll()
            announcements.cancelAndJoin()
            resolver.cancelAndJoin()
            candidates.close(); resolveQueue.close()
            if (discoveryStarted) runCatching { nsd.stopServiceDiscovery(listener) }
        }
    }
    suspend fun <T> race(pairing: Boolean, connect: suspend (PcEndpoint) -> T): T = coroutineScope {
        val result = CompletableDeferred<T>()
        val worker = launch { scan(12000, shouldStop = { result.isCompleted }) { endpoint -> result.complete(connect(endpoint)) } }
        try { withTimeout(12500) { result.await() } }
        catch (e: TimeoutCancellationException) { throw java.io.IOException("No authenticated PC found on Wi-Fi", e) }
        finally { worker.cancelAndJoin() }
    }
}
