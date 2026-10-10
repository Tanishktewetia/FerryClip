package com.clipsync.android.history

import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

/** Encrypts both previews and complete clips; the key never enters SQLite. */
internal class HistoryCipher(private val keyProvider: () -> SecretKey = ::historyKey) {
    fun encrypt(text: String, field: String): ByteArray {
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.ENCRYPT_MODE, keyProvider())
        cipher.updateAAD("ferryclip_history_v2:$field".toByteArray(Charsets.UTF_8))
        return byteArrayOf(1) + cipher.iv + cipher.doFinal(text.toByteArray(Charsets.UTF_8))
    }
    fun decrypt(encoded: ByteArray, field: String): String {
        require(encoded.size >= 29 && encoded[0] == 1.toByte()) { "Invalid history ciphertext" }
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.DECRYPT_MODE, keyProvider(), GCMParameterSpec(128, encoded.copyOfRange(1, 13)))
        cipher.updateAAD("ferryclip_history_v2:$field".toByteArray(Charsets.UTF_8))
        // Authentication failures propagate; never display ciphertext or fall back to plaintext.
        return cipher.doFinal(encoded.copyOfRange(13, encoded.size)).toString(Charsets.UTF_8)
    }
}

@Synchronized
private fun historyKey(): SecretKey {
    val alias = "ferryclip_history_aes_v2"
    val store = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
    if (!store.containsAlias(alias)) {
        KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore").apply {
            init(KeyGenParameterSpec.Builder(alias, KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setKeySize(256).build())
            generateKey()
        }
    }
    return store.getKey(alias, null) as SecretKey
}
