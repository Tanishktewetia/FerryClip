package com.clipsync.android.transport

import android.net.ConnectivityManager
import android.net.Network
import android.net.NetworkCapabilities
import java.net.Inet4Address
import java.net.InetSocketAddress
import java.net.NetworkInterface
import java.net.Socket

/** A route is either Android-managed or explicitly source-bound to a local tether interface. */
data class LocalPeerRoute(
    val network: Network? = null,
    val sourceAddress: Inet4Address? = null,
    val prefixLength: Int? = null,
) {
    fun createSocket(): Socket {
        val socket = Socket()
        try {
            // Explicitly bind an unconnected socket before any traffic: never use the VPN default.
            network?.bindSocket(socket)
            sourceAddress?.let { socket.bind(InetSocketAddress(it, 0)) }
            return socket
        } catch (error: Exception) {
            runCatching { socket.close() }
            throw error
        }
    }
}

/** Uses local transport routes without requiring Internet validation. */
object LocalNetwork {
    @Suppress("DEPRECATION")
    fun routesForLocalPeer(manager: ConnectivityManager): List<LocalPeerRoute> {
        val networks = manager.allNetworks.toList()
        val active = manager.activeNetwork
        return buildList {
            val managed = networks.filter { manager.getNetworkCapabilities(it)?.let(::supportsLocalPeerRoute) == true }
            if (active != null && active in managed && manager.getNetworkCapabilities(active)?.hasTransport(NetworkCapabilities.TRANSPORT_VPN) != true) add(LocalPeerRoute(network = active))
            managed.sortedBy { if (manager.getNetworkCapabilities(it)?.hasTransport(NetworkCapabilities.TRANSPORT_VPN) == true) 1 else 0 }.forEach { add(LocalPeerRoute(network = it)) }

            // Some Android builds do not expose the phone's own hotspot/tether link as a
            // ConnectivityManager Network. Bind to its app-visible private AP address instead.
            // Retain a source-bound alternative even when Android exposes the Wi-Fi link.
            // Some VPN transitions invalidate Network.socketFactory while the LAN interface remains up.
            tetherInterfaceAddresses()
                .forEach { add(LocalPeerRoute(sourceAddress = it.address, prefixLength = it.prefixLength)) }
        }.distinct()
    }

    fun routesForAddress(manager: ConnectivityManager, address: String): List<LocalPeerRoute> {
        return routesForLocalPeer(manager).sortedBy { route ->
            val matching = if (route.sourceAddress != null) Ipv4Subnet.contains(route.sourceAddress.hostAddress ?: "", route.prefixLength ?: 32, address)
                else manager.getLinkProperties(route.network!!)?.linkAddresses.orEmpty().any { Ipv4Subnet.contains(it.address.hostAddress ?: "", it.prefixLength, address) }
            routePriority(matching, route.sourceAddress != null, route.network != null && manager.getNetworkCapabilities(route.network)?.hasTransport(NetworkCapabilities.TRANSPORT_VPN) == true)
        }
    }

    internal fun routePriority(matching: Boolean, tether: Boolean, vpn: Boolean): Int = when { matching && tether -> 0; matching && !vpn -> 1; vpn -> 3; else -> 2 }

    fun routeForLocalPeer(manager: ConnectivityManager): LocalPeerRoute? = routesForLocalPeer(manager).firstOrNull()

    fun tetherInterfaceAddresses(): List<TetherAddress> = try {
        val result = mutableListOf<TetherAddress>()
        val interfaces = NetworkInterface.getNetworkInterfaces() ?: return emptyList()
        while (interfaces.hasMoreElements()) {
            val networkInterface = interfaces.nextElement()
            if (networkInterface.name.lowercase().let { it.startsWith("rmnet") || it.startsWith("ccmni") || it.startsWith("tun") || it.startsWith("wg") || it.startsWith("ppp") }) continue
            if (!runCatching { networkInterface.isUp && !networkInterface.isLoopback }.getOrDefault(false)) continue
            networkInterface.interfaceAddresses.forEach { link ->
                val address = link.address as? Inet4Address ?: return@forEach
                val prefix = link.networkPrefixLength.toInt()
                val text = address.hostAddress ?: return@forEach
                if (address.isLoopbackAddress || !Ipv4Subnet.isRfc1918Address(text) || Ipv4Subnet.hosts(text, prefix).isEmpty()) return@forEach
                result += TetherAddress(address, prefix)
            }
        }
        result.distinctBy { it.address.hostAddress to it.prefixLength }
    } catch (_: Exception) { emptyList() }

    data class TetherAddress(val address: Inet4Address, val prefixLength: Int)

    fun supportsLocalPeerRoute(caps: NetworkCapabilities) = caps.hasTransport(NetworkCapabilities.TRANSPORT_WIFI) ||
        caps.hasTransport(NetworkCapabilities.TRANSPORT_ETHERNET) || caps.hasTransport(NetworkCapabilities.TRANSPORT_VPN)
}