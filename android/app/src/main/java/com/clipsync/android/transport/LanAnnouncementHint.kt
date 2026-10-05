package com.clipsync.android.transport
/** Untrusted private-LAN discovery metadata. TLS/code proof remains mandatory. */
data class LanAnnouncementHint(val address: String, val port: Int) {
 companion object {
  fun parse(text: String): LanAnnouncementHint? {
   if(text.length > 128) return null
   val parts = text.split('|')
   if(parts.size != 3 || parts[0] != "FERRYCLIP-LAN/1" || !Ipv4Subnet.isRfc1918Address(parts[1])) return null
   val port = parts[2].toIntOrNull() ?: return null
   if(port !in 1..65535) return null
   return LanAnnouncementHint(parts[1], port)
  }
 }
}
