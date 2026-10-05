package com.clipsync.android.transport

import android.content.Context
import android.net.ConnectivityManager
import android.net.NetworkCapabilities
import android.os.Build
import android.util.Base64
import com.clipsync.android.security.*
import kotlinx.coroutines.*
import java.net.InetSocketAddress
import java.net.Socket
import javax.net.ssl.SSLContext
import javax.net.ssl.SSLSocket

data class PcIdentity(val fingerprint: String, val host: String, val pairingOpen: Boolean)

/** No clipboard frames on these ALPN-isolated sockets. Untrusted metadata is only a discovery hint. */
class ControlClient(private val context: Context, private val expectedPin: String? = null) {
    private var stage = "route_selection"
    @Volatile private var deadlineExpired = false
    @Volatile var observedFingerprint: String? = null
        private set
    @Volatile private var socket: Socket? = null
    private var closed = false
    @Synchronized private fun own(value: Socket) { if (closed) { value.close(); error("Cancelled") }; socket = value }
    @Synchronized fun close() { closed = true; runCatching { socket?.close() }; socket = null }
    private suspend fun <T> use(endpoint: PcEndpoint, pairing: Boolean, block: suspend (SSLSocket, String, KeyStoreIdentity) -> T): T = coroutineScope {
        val cleanup = launch(Dispatchers.Default, start = CoroutineStart.UNDISPATCHED) { try { awaitCancellation() } finally { close() } }
        val deadline = launch(Dispatchers.Default) { delay(5000); deadlineExpired = true; close() }
        try {
            if (Build.VERSION.SDK_INT < 29) throw ConnectionProblem("Secure sync requires Android 10 or newer with TLS 1.3.")
            stage = "route_selection"
            val manager = context.getSystemService(ConnectivityManager::class.java)
            @Suppress("DEPRECATION")
            val route = endpoint.route ?: LocalNetwork.routesForAddress(manager, endpoint.address).firstOrNull()
                ?: throw ConnectionProblem("Connect both devices to a reachable Wi-Fi, hotspot, Ethernet, or VPN network.")
            currentCoroutineContext().ensureActive()
            stage = "socket_create"
            val tcp = route.createSocket(); own(tcp)
            stage = "connect"
            tcp.tcpNoDelay = true
            tcp.connect(InetSocketAddress(endpoint.address, endpoint.port), if (pairing) 1800 else 650); tcp.soTimeout = 2500
            currentCoroutineContext().ensureActive()
            stage = "keystore_identity"
            val identity = KeyStoreIdentity()
            val trust = PinnedTrustManager(expectedPin, pairing || expectedPin == null) { observedFingerprint = it }
            stage = "tls_context"
            val tls = SSLContext.getInstance("TLS").apply { init(arrayOf(identity), arrayOf(trust), null) }
            val ssl = tls.socketFactory.createSocket(tcp, endpoint.address, endpoint.port, true) as SSLSocket; own(ssl)
            val protocol = if (pairing) "clipsync-pair/1" else "clipsync-probe/1"
            ssl.enabledProtocols = arrayOf("TLSv1.3")
            ssl.sslParameters = ssl.sslParameters.apply { applicationProtocols = arrayOf(protocol) }
            stage = "tls"
            ssl.startHandshake()
            stage = "protocol"
            check(ssl.applicationProtocol == protocol) { "Update the Windows app to Phase 8.5 before pairing." }
            val remote = trust.peerFingerprint ?: error("Missing PC identity")
            // Initial handshake/offer deadline remains active until the first server line arrives.
            stage = "offer"
            val first = PairingProtocol.readLine(ssl.inputStream)
            val arrived = android.os.SystemClock.elapsedRealtime()
            if (pairing) {
                when (first) {
                    "PAIR_CLOSED" -> throw ConnectionProblem("Choose Generate code to connect on the PC to restart pairing.")
                    "PAIR_BUSY" -> throw ConnectionProblem("This PC is already confirming another request. Try again after it finishes.")
                }
                PairingProtocol.validateOffer(first, remote)
                val meta = PairingProtocol.readLine(ssl.inputStream).split('|')
                require(meta.size == 3 && meta[0] == "META")
                offer = Offer(decodeHost(meta[1]), meta[2].toLong().coerceIn(1, 120000), arrived)
            } else {
                val fields = first.split('|'); require(fields.size == 3 && fields[0] == "INFO")
                info = PcIdentity(remote, decodeHost(fields[1]), fields[2] == "1")
            }
            deadline.cancel()
            stage = "confirmation"
            block(ssl, remote, identity)
        } catch (e: CancellationException) { throw e }
        catch (e: WrongPairingCodeException) { throw e }
        catch (e: Exception) {
            currentCoroutineContext().ensureActive()
            // Never log exception messages from remote peers or clipboard content.
            if (ConnectionDiagnostics.shouldLogControlFailure(stage, e.javaClass.simpleName, android.os.SystemClock.elapsedRealtime()))
                com.clipsync.android.logging.FileLogger.warn("Control connection failed: stage=$stage type=${e.javaClass.simpleName} deadline=$deadlineExpired")
            val reason = if (deadlineExpired) java.net.SocketTimeoutException() else e
            throw ConnectionProblem(ControlFailure.message(stage, reason), e)
        } finally { deadline.cancel(); cleanup.cancel(); close() }
    }
    data class Offer(val host: String, val remaining: Long, val arrived: Long)
    private var offer: Offer? = null
    private var info: PcIdentity? = null
    suspend fun probe(endpoint: PcEndpoint): PcIdentity = use(endpoint, false) { _, _, _ -> checkNotNull(info) }
    suspend fun pair(endpoint: PcEndpoint, code: String): PcIdentity = use(endpoint, true) { ssl, remote, _ ->
        require(code.matches(Regex("[0-9]{6}"))) { "Enter the six-digit code shown on the PC." }
        val prompt = checkNotNull(offer)
        val expires = launchDeadline(prompt.remaining)
        try {
            ssl.soTimeout = 5000
            withContext(Dispatchers.IO) {
                ssl.outputStream.write(("CONFIRM|" + code + "\n").toByteArray(Charsets.US_ASCII))
                ssl.outputStream.flush()
                val reply = PairingProtocol.readLine(ssl.inputStream)
                when {
                    reply == "PAIR_TIMEOUT" -> throw java.net.SocketTimeoutException("Pairing code expired")
                    reply == "PAIR_CLOSED" -> throw ConnectionProblem("Pairing expired. Open a new code on the PC and retry.")
                    reply == "PAIR_REJECTED" || reply.startsWith("WRONG|") -> throw WrongPairingCodeException()
                    reply == "PAIR_BUSY" -> throw ConnectionProblem("This PC is confirming another pairing. Try again shortly.")
                    reply.startsWith("PAIRED|") -> runCatching { val name = (android.os.Build.MODEL ?: "Android phone").take(48); ssl.outputStream.write(("NAME|" + Base64.encodeToString(name.toByteArray(Charsets.UTF_8), Base64.NO_WRAP) + "\n").toByteArray(Charsets.US_ASCII)); ssl.outputStream.flush() }.let { Unit }
                    else -> throw ConnectionProblem("Unexpected pairing response.")
                }
            }
            PcIdentity(remote, prompt.host, false)
        } finally { expires.cancel() }
    }
    private fun launchDeadline(duration: Long) = CoroutineScope(Dispatchers.IO).launch { delay(duration); close() }
    private fun decodeHost(value: String) = Base64.decode(value, Base64.NO_WRAP).toString(Charsets.UTF_8).take(80)
}
