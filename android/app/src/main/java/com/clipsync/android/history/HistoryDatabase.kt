package com.clipsync.android.history

import android.content.ContentValues
import android.content.Context
import android.database.sqlite.SQLiteDatabase
import android.database.sqlite.SQLiteOpenHelper

/** List queries never load the full clipboard payload into Compose. */
data class HistoryEntry(val id: Long, val preview: String, val timestamp: Long,
    val direction: String, val previewOnly: Boolean, val originalBytes: Int)

internal class HistoryDatabase(context: Context, private val cipher: HistoryCipher = HistoryCipher()) : SQLiteOpenHelper(context, "clipboard_history.db", null, 2) {
    private var compactAfterMigration = false
    override fun onConfigure(db: SQLiteDatabase) {
        super.onConfigure(db)
        db.rawQuery("PRAGMA secure_delete=ON", null).use { it.moveToFirst() }
        db.rawQuery("PRAGMA journal_size_limit=1048576", null).use { it.moveToFirst() }
    }
    override fun onCreate(db: SQLiteDatabase) {
        db.execSQL("""CREATE TABLE entries (
            id INTEGER PRIMARY KEY AUTOINCREMENT, preview BLOB NOT NULL, payload BLOB,
            created INTEGER NOT NULL, direction TEXT NOT NULL, original_bytes INTEGER NOT NULL,
            stored_bytes INTEGER NOT NULL)""")
        db.execSQL("CREATE INDEX entries_created ON entries(created, id)")
    }
    override fun onUpgrade(db: SQLiteDatabase, oldVersion: Int, newVersion: Int) {
        if (oldVersion < 2) {
            // SQLite upgrades are transactional. Encrypt existing rows without losing history.
            db.rawQuery("SELECT id, preview, payload FROM entries", null).use { c ->
                while (c.moveToNext()) {
                    val preview = cipher.encrypt(c.getString(1), "preview")
                    val payload = if (c.isNull(2)) null else cipher.encrypt(c.getString(2), "payload")
                    db.update("entries", ContentValues().apply {
                        put("preview", preview); put("payload", payload)
                        put("stored_bytes", preview.size + (payload?.size ?: 0) + 128)
                    }, "id = ?", arrayOf(c.getLong(0).toString()))
                }
            }
            compactAfterMigration = true
        }
    }
    override fun onOpen(db: SQLiteDatabase) {
        super.onOpen(db)
        if (compactAfterMigration) {
            // Runs after the upgrade transaction; remove freed legacy plaintext pages.
            db.execSQL("VACUUM")
            compactAfterMigration = false
        }
    }

    fun insert(text: String, direction: String, options: HistoryOptions, now: Long) {
        val bytes = text.toByteArray(Charsets.UTF_8).size
        val preview = cipher.encrypt(HistoryStoragePolicy.preview(text), "preview")
        val payload = text.takeIf { bytes <= HistoryStoragePolicy.ITEM_BYTES }?.let { cipher.encrypt(it, "payload") }
        val values = ContentValues().apply {
            put("preview", preview); put("payload", payload); put("created", now)
            put("direction", direction); put("original_bytes", bytes)
            put("stored_bytes", (payload?.size ?: 0) + preview.size + 128)
        }
        writableDatabase.beginTransaction()
        try {
            writableDatabase.insertOrThrow("entries", null, values)
            prune(options, now)
            writableDatabase.setTransactionSuccessful()
        } finally { writableDatabase.endTransaction() }
    }
    fun prune(options: HistoryOptions, now: Long) {
        val db = writableDatabase
        if (options.retentionHours > 0) db.delete("entries", "created <= ?",
            arrayOf((now - options.retentionHours * 3600000L).toString()))
        db.execSQL("DELETE FROM entries WHERE id NOT IN (SELECT id FROM entries ORDER BY created DESC, id DESC LIMIT ?)", arrayOf(options.limit))
        val oldest = mutableListOf<Pair<Long, Long>>()
        db.rawQuery("SELECT id, stored_bytes FROM entries ORDER BY created ASC, id ASC", null).use { c ->
            while (c.moveToNext()) oldest += c.getLong(0) to c.getLong(1)
        }
        HistoryStoragePolicy.evictions(oldest).forEach { db.delete("entries", "id = ?", arrayOf(it.toString())) }
    }
    fun list(): List<HistoryEntry> = readableDatabase.rawQuery(
        "SELECT id, preview, created, direction, payload IS NULL, original_bytes FROM entries ORDER BY created DESC, id DESC", null
    ).use { c -> buildList { while (c.moveToNext()) add(HistoryEntry(c.getLong(0), cipher.decrypt(c.getBlob(1), "preview"), c.getLong(2), c.getString(3), c.getInt(4) != 0, c.getInt(5))) } }
    fun payload(id: Long): String? = readableDatabase.rawQuery("SELECT payload FROM entries WHERE id = ?", arrayOf(id.toString())).use {
        if (it.moveToFirst() && !it.isNull(0)) cipher.decrypt(it.getBlob(0), "payload") else null
    }
    fun delete(id: Long) { writableDatabase.delete("entries", "id = ?", arrayOf(id.toString())) }
    fun clear() { writableDatabase.delete("entries", null, null); writableDatabase.execSQL("VACUUM") }
}

internal object HistoryStoragePolicy {
    const val ITEM_BYTES = 1024 * 1024
    const val TOTAL_BYTES = 19L * 1024 * 1024 // headroom for SQLite pages/indexes under ~20 MiB
    fun preview(text: String): String {
        val end = minOf(text.length, 512)
        // Do not split a UTF-16 surrogate pair in the preview.
        return text.substring(0, if (end > 0 && end < text.length && text[end - 1].isHighSurrogate()) end - 1 else end)
    }
    fun evictions(oldest: List<Pair<Long, Long>>): List<Long> {
        var total = oldest.sumOf { it.second }
        return buildList { for ((id, bytes) in oldest) { if (total <= TOTAL_BYTES) break; add(id); total -= bytes } }
    }
}
