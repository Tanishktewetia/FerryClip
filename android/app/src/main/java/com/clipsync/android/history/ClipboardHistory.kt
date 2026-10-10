package com.clipsync.android.history

import android.content.Context
import com.clipsync.android.logging.FileLogger
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock

/** One process-wide owner; DB and DataStore work never blocks clipboard/network operations. */
class ClipboardHistory private constructor(context: Context) {
    private val app = context.applicationContext
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private val mutex = Mutex()
    private var lastRecordedHash: String? = null
    private var lastRecordedDirection: String? = null
    private val preferences = HistoryPreferences(app)
    private val db = HistoryDatabase(app)
    private val mutableOptions = MutableStateFlow<HistoryOptions?>(null)
    val options: StateFlow<HistoryOptions?> = mutableOptions.asStateFlow()
    private val mutableEntries = MutableStateFlow<List<HistoryEntry>>(emptyList())
    val entries: StateFlow<List<HistoryEntry>> = mutableEntries.asStateFlow()
    private val mutableError = MutableStateFlow<String?>(null)
    val error: StateFlow<String?> = mutableError.asStateFlow()
    init {
        scope.launch {
            safely { preferences.options.collect { settings ->
                safely { mutex.withLock {
                    mutableOptions.value = settings
                    db.prune(settings, System.currentTimeMillis())
                    mutableEntries.value = db.list()
                    HistoryCleanupWorker.schedule(app, settings.retentionHours)
                    mutableOptions.value = settings
                } }
            } }
        }
    }
    fun update(transform: (HistoryOptions) -> HistoryOptions) = scope.launch {
        safely { mutex.withLock {
            preferences.update(transform)
            val next = preferences.options.first()
            db.prune(next, System.currentTimeMillis())
            mutableEntries.value = db.list()
            HistoryCleanupWorker.schedule(app, next.retentionHours)
            mutableOptions.value = next
        } }
    }
    fun record(text: String, direction: String, sensitive: Boolean = false) {
        val timestamp = System.currentTimeMillis()
        scope.launch { safely { mutex.withLock {
            val settings = preferences.options.first()
            if (settings.enabled && !(settings.skipSensitive && sensitive) && text.isNotEmpty()) {
                val hash = com.clipsync.core.hash(text.toByteArray(Charsets.UTF_8))
                if (hash == lastRecordedHash && direction == lastRecordedDirection) return@withLock
                db.insert(text, direction, settings, timestamp)
                lastRecordedHash = hash
                lastRecordedDirection = direction
                mutableEntries.value = db.list()
            }
        } } }
    }
    suspend fun payload(id: Long): String? = withContext(Dispatchers.IO) { mutex.withLock {
        db.prune(preferences.options.first(), System.currentTimeMillis())
        mutableEntries.value = db.list()
        db.payload(id)
    } }
    fun delete(id: Long) = scope.launch { safely { mutex.withLock { db.delete(id); mutableEntries.value = db.list() } } }
    fun clear() = scope.launch { safely { mutex.withLock { db.clear(); mutableEntries.value = emptyList(); lastRecordedHash = null; lastRecordedDirection = null } } }
    suspend fun cleanup() = withContext(Dispatchers.IO) { mutex.withLock {
        db.prune(preferences.options.first(), System.currentTimeMillis()); mutableEntries.value = db.list()
    } }
    fun refresh() = scope.launch { safely {
        mutex.withLock {
            val settings = preferences.options.first()
            mutableOptions.value = settings
            db.prune(settings, System.currentTimeMillis())
            mutableEntries.value = db.list()
            HistoryCleanupWorker.schedule(app, settings.retentionHours)
        }
    } }
    private suspend fun safely(block: suspend () -> Unit) {
        try { block(); mutableError.value = null } catch (e: CancellationException) { throw e }
        catch (e: Exception) { mutableError.value = "Clipboard history could not be opened. Please retry."; FileLogger.warn("History storage operation failed: " + e.javaClass.simpleName) }
    }
    companion object {
        @Volatile private var instance: ClipboardHistory? = null
        fun get(context: Context): ClipboardHistory = instance ?: synchronized(this) {
            instance ?: ClipboardHistory(context).also { instance = it }
        }
    }
}
