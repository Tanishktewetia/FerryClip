package com.clipsync.android.transport

/** Safe, bounded private-IPv4 candidate enumeration for code-directed pairing. */
object Ipv4Subnet {
    private const val MAX_HOSTS = 1024

    fun hosts(address: String, prefixLength: Int): List<String> {
        val local = parse(address) ?: return emptyList()
        if (!isPrivate(local) || prefixLength !in 1..30) return emptyList()
        val mask = (0xFFFFFFFFL shl (32 - prefixLength)) and 0xFFFFFFFFL
        val network = local and mask
        val broadcast = network or (mask xor 0xFFFFFFFFL)
        val count = broadcast - network - 1
        if (count <= 0 || count > MAX_HOSTS) return emptyList()
        return (network + 1 until broadcast)
            .filter { it != local }
            .map(::format)
    }

    fun contains(address: String, prefixLength: Int, candidate: String): Boolean {
        val base = parse(address) ?: return false
        val peer = parse(candidate) ?: return false
        if (prefixLength !in 0..32 || !isPrivate(base) || !isPrivate(peer)) return false
        val mask = if (prefixLength == 0) 0L else (0xFFFFFFFFL shl (32 - prefixLength)) and 0xFFFFFFFFL
        return (base and mask) == (peer and mask)
    }

    fun isRfc1918Address(address: String): Boolean = parse(address)?.let { ip ->
        val first = (ip shr 24).toInt()
        val second = ((ip shr 16) and 255).toInt()
        first == 10 || (first == 172 && second in 16..31) || (first == 192 && second == 168)
    } ?: false

    private fun parse(value: String): Long? {
        val parts = value.split('.')
        if (parts.size != 4) return null
        var result = 0L
        for (part in parts) {
            if (part.isEmpty() || part.length > 3 || (part.length > 1 && part.startsWith('0'))) return null
            val octet = part.toIntOrNull() ?: return null
            if (octet !in 0..255) return null
            result = (result shl 8) or octet.toLong()
        }
        return result
    }

    private fun isPrivate(ip: Long): Boolean {
        fun inRange(base: Long, prefix: Int): Boolean {
            val mask = (0xFFFFFFFFL shl (32 - prefix)) and 0xFFFFFFFFL
            return ip and mask == base and mask
        }
        return inRange(0x0A000000L, 8) || inRange(0xAC100000L, 12) || inRange(0xC0A80000L, 16) || inRange(0x64400000L, 10)
    }

    private fun format(value: Long) = listOf(24, 16, 8, 0).joinToString(".") { ((value shr it) and 255).toString() }
}