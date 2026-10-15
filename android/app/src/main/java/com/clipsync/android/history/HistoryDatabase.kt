package com.clipsync.android.history

import android.content.ContentValues
import android.content.Context
import android.database.sqlite.SQLiteDatabase
import android.database.sqlite.SQLiteOpenHelper

/** List queries never load the full clipboard payload into Compose. */
data class HistoryEntry(val id: Long, val preview: String, val timestamp: Long,
    val direction: String, val previewOnly: Boolean, val originalBytes: Int, val searchableText: String,
    val pinned: Boolean = false)

internal class HistoryDatabase(context: Context, private val cipher: HistoryCipher = HistoryCipher()) : SQLiteOpenHelper(context, "clipboard_history.db", null, 5) {
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
            stored_bytes INTEGER NOT NULL, event_id TEXT, content_hash TEXT,
            pinned INTEGER NOT NULL DEFAULT 0)""")
        db.execSQL("CREATE TABLE source_cursors (origin TEXT PRIMARY KEY NOT NULL, sequence INTEGER NOT NULL)")
        db.execSQL("CREATE UNIQUE INDEX entries_event_id ON entries(event_id)")
        db.execSQL("CREATE INDEX entries_created ON entries(created, id)")
        db.execSQL("CREATE INDEX entries_content_hash ON entries(content_hash)")
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
        if (oldVersion < 3) {
            db.execSQL("ALTER TABLE entries ADD COLUMN event_id TEXT")
            db.execSQL("CREATE TABLE source_cursors (origin TEXT PRIMARY KEY NOT NULL, sequence INTEGER NOT NULL)")
            db.execSQL("CREATE UNIQUE INDEX entries_event_id ON entries(event_id)")
        }
        if (oldVersion < 4) {
            db.execSQL("ALTER TABLE entries ADD COLUMN content_hash TEXT")
            db.execSQL("CREATE INDEX entries_content_hash ON entries(content_hash)")
            val fingerprints = mutableListOf<Pair<Long, String>>()
            db.rawQuery("SELECT id, payload FROM entries WHERE payload IS NOT NULL", null).use { c ->
                while (c.moveToNext()) fingerprints += c.getLong(0) to cipher.fingerprint(cipher.decrypt(c.getBlob(1), "payload"))
            }
            fingerprints.forEach { (id, fingerprint) ->
                db.update("entries", ContentValues().apply { put("content_hash", fingerprint) }, "id = ?", arrayOf(id.toString()))
            }
            val duplicateIds = mutableListOf<Long>()
            val seenFingerprints = mutableSetOf<String>()
            db.rawQuery("SELECT id, content_hash FROM entries WHERE content_hash IS NOT NULL ORDER BY created DESC, id DESC", null).use { c ->
                while (c.moveToNext()) if (!seenFingerprints.add(c.getString(1))) duplicateIds += c.getLong(0)
            }
            duplicateIds.forEach { id -> db.delete("entries", "id = ?", arrayOf(id.toString())) }
        }
        if (oldVersion < 5) db.execSQL("ALTER TABLE entries ADD COLUMN pinned INTEGER NOT NULL DEFAULT 0")
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
        val db = writableDatabase
        db.beginTransaction()
        try {
            insertInTransaction(db, text, direction, now, null)
            prune(options, now)
            db.setTransactionSuccessful()
        } finally { db.endTransaction() }
    }

    /** Keeps a persistent source cursor so Clear history doesn't make replayed states look new. */
    fun recordRemoteEvent(text: String, origin: String, sequence: Long, options: HistoryOptions,
        now: Long, sensitive: Boolean): Boolean {
        require(origin.isNotBlank() && sequence > 0)
        val db = writableDatabase
        db.beginTransaction()
        try {
            val previous = db.rawQuery("SELECT sequence FROM source_cursors WHERE origin = ?", arrayOf(origin))
                .use { if (it.moveToFirst()) it.getLong(0) else 0L }
            if (sequence <= previous) {
                db.setTransactionSuccessful()
                return false
            }
            db.insertWithOnConflict("source_cursors", null, ContentValues().apply {
                put("origin", origin); put("sequence", sequence)
            }, SQLiteDatabase.CONFLICT_REPLACE)
            val saved = options.enabled && !(options.skipSensitive && sensitive) && text.isNotEmpty()
            if (saved) {
                insertInTransaction(db, text, "From PC", now, "$origin:$sequence")
                prune(options, now)
            }
            db.setTransactionSuccessful()
            return saved
        } finally { db.endTransaction() }
    }

    fun latestRemoteSequence(origin: String): Long = readableDatabase.rawQuery(
        "SELECT sequence FROM source_cursors WHERE origin = ?", arrayOf(origin)
    ).use { if (it.moveToFirst()) it.getLong(0) else 0L }

    private fun insertInTransaction(db: SQLiteDatabase, text: String, direction: String, now: Long, eventId: String?) {
        val fingerprint = cipher.fingerprint(text)
        val duplicateId = db.rawQuery("SELECT id FROM entries WHERE content_hash = ? LIMIT 1", arrayOf(fingerprint))
            .use { if (it.moveToFirst()) it.getLong(0) else null }
        if (duplicateId != null) {
            // Keep one row per exact clip, but refresh its recency and direction when copied again.
            db.update("entries", ContentValues().apply { put("created", now); put("direction", direction) },
                "id = ?", arrayOf(duplicateId.toString()))
            return
        }
        val bytes = text.toByteArray(Charsets.UTF_8).size
        val preview = cipher.encrypt(HistoryStoragePolicy.preview(text), "preview")
        val payload = text.takeIf { bytes <= HistoryStoragePolicy.ITEM_BYTES }?.let { cipher.encrypt(it, "payload") }
        val values = ContentValues().apply {
            put("preview", preview); put("payload", payload); put("created", now)
            put("direction", direction); put("original_bytes", bytes)
            put("stored_bytes", (payload?.size ?: 0) + preview.size + 160)
            put("content_hash", fingerprint)
            if (eventId == null) putNull("event_id") else put("event_id", eventId)
        }
        db.insertOrThrow("entries", null, values)
    }
    fun prune(options: HistoryOptions, now: Long) {
        val db = writableDatabase
        if (options.retentionHours > 0) db.delete("entries", "created <= ? AND pinned = 0",
            arrayOf((now - options.retentionHours * 3600000L).toString()))
        db.execSQL("DELETE FROM entries WHERE pinned = 0 AND id NOT IN (SELECT id FROM entries WHERE pinned = 0 ORDER BY created DESC, id DESC LIMIT ?)", arrayOf(options.limit))
        val oldest = mutableListOf<Pair<Long, Long>>()
        db.rawQuery("SELECT id, stored_bytes FROM entries WHERE pinned = 0 ORDER BY created ASC, id ASC", null).use { c ->
            while (c.moveToNext()) oldest += c.getLong(0) to c.getLong(1)
        }
        HistoryStoragePolicy.evictions(oldest).forEach { db.delete("entries", "id = ?", arrayOf(it.toString())) }
    }
    private data class EntryMetadata(val id: Long, val preview: String, val timestamp: Long,
        val direction: String, val previewOnly: Boolean, val originalBytes: Int, val pinned: Boolean)

    fun list(): List<HistoryEntry> {
        // Read payloads individually after closing the metadata cursor: a single cursor window
        // must not hold many large encrypted clips at once.
        val metadata = readableDatabase.rawQuery(
            "SELECT id, preview, created, direction, payload IS NULL, original_bytes, pinned FROM entries ORDER BY created DESC, id DESC", null
        ).use { c -> buildList {
            while (c.moveToNext()) add(EntryMetadata(c.getLong(0), cipher.decrypt(c.getBlob(1), "preview"),
                c.getLong(2), c.getString(3), c.getInt(4) != 0, c.getInt(5), c.getInt(6) != 0))
        } }
        return metadata.map { row ->
            val fullText = if (row.previewOnly) row.preview else payload(row.id) ?: row.preview
            HistoryEntry(row.id, row.preview, row.timestamp, row.direction, row.previewOnly, row.originalBytes, fullText, row.pinned)
        }
    }
    fun payload(id: Long): String? = readableDatabase.rawQuery("SELECT payload FROM entries WHERE id = ?", arrayOf(id.toString())).use {
        if (it.moveToFirst() && !it.isNull(0)) cipher.decrypt(it.getBlob(0), "payload") else null
    }
    fun delete(id: Long) { writableDatabase.delete("entries", "id = ?", arrayOf(id.toString())) }
    fun setPinned(id: Long, pinned: Boolean) {
        writableDatabase.update("entries", ContentValues().apply { put("pinned", if (pinned) 1 else 0) },
            "id = ?", arrayOf(id.toString()))
    }
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
