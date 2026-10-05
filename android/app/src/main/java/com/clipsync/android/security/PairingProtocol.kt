package com.clipsync.android.security

import java.io.EOFException
import java.io.InputStream
import java.util.Locale
import java.net.Inet6Address
import java.net.InetAddress

/** Phase 4's bootstrap protocol, before the CSP1 stream begins. */
object PairingProtocol {
    fun fingerprint(value: String): String {
        require(value.matches(Regex("[0-9a-fA-F]{64}"))) { "Invalid fingerprint" }
        return value.uppercase(Locale.ROOT)
    }
    fun validateOffer(line: String, remote: String) {
        val fields = line.split('|')
        require(fields.size == 2 && fields[0] == "PAIR") { "Invalid pairing response" }
        require(fingerprint(fields[1]) == fingerprint(remote)) { "Pairing identity mismatch" }
    }
    fun readLine(input: InputStream): String {
        val bytes = ArrayList<Byte>()
        while (true) {
            val next = input.read()
            if (next == -1) throw EOFException("PC closed the connection")
            if (next == 10) return bytes.toByteArray().toString(Charsets.US_ASCII).trimEnd('\r')
            require(next in 32..126 || next == 13) { "Invalid bootstrap response" }
            require(bytes.size < 256) { "Bootstrap response too long" }
            bytes.add(next.toByte())
        }
    }
}

/** Numeric IPv4 only: no DNS, scans, discovery, URL parsing, or mobile-data route. */
object ManualAddress {
    fun parse(value: String): String {
        val address = value.trim().removePrefix("[").removeSuffix("]")
        if (address.contains(':')) {
            require(address.matches(Regex("(?i)[0-9a-f:.]+(%[a-z0-9_.-]+)?"))) { "Enter a numeric IPv6 address" }
            val parsed = try { InetAddress.getByName(address) } catch (e: java.net.UnknownHostException) { throw IllegalArgumentException("Enter a valid numeric IPv6 address", e) }
            require(parsed is Inet6Address && !parsed.isAnyLocalAddress && !parsed.isLoopbackAddress && !parsed.isMulticastAddress) {
                "Enter the PC's IPv6 address, not a loopback or unspecified address"
            }
            return parsed.hostAddress ?: address
        }
        val pieces = address.split('.')
        require(pieces.size == 4) { "Enter the PC's IPv4 or IPv6 address" }
        val bytes = pieces.map {
            require(it.matches(Regex("[0-9]{1,3}"))) { "Enter a valid IPv4 address" }
            require(it.length == 1 || !it.startsWith('0')) { "Remove leading zeroes" }
            it.toInt().also { n -> require(n in 0..255) { "Enter a valid IPv4 address" } }
        }
        require(bytes[0] in 1..223 && bytes[0] != 127 && bytes.last() !in listOf(0, 255)) {
            "Enter the PC's network address, not a loopback or broadcast address"
        }
        return bytes.joinToString(".")
    }
}
