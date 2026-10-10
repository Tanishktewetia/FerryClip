package com.clipsync.android.history

import android.content.Context
import androidx.datastore.preferences.core.*
import androidx.datastore.preferences.preferencesDataStore
import kotlinx.coroutines.flow.*

private val Context.historyPreferences by preferencesDataStore("clipboard_history_settings")

data class HistoryOptions(
    val enabled: Boolean = true,
    val limit: Int = 100,
    val skipSensitive: Boolean = true,
    val appLock: Boolean = false,
    val retentionHours: Int = 0,
)

class HistoryPreferences(context: Context) {
    private val store = context.applicationContext.historyPreferences
    val options: Flow<HistoryOptions> = store.data.map { p ->
        HistoryOptions(p[ENABLED] ?: true, p[LIMIT] ?: 100, p[SENSITIVE] ?: true,
            p[LOCK] ?: false, p[RETENTION] ?: 0)
    }
    suspend fun update(transform: (HistoryOptions) -> HistoryOptions) {
        store.edit { p ->
            val next = transform(HistoryOptions(p[ENABLED] ?: true, p[LIMIT] ?: 100,
                p[SENSITIVE] ?: true, p[LOCK] ?: false, p[RETENTION] ?: 0))
            require(next.limit in listOf(50, 100, 500))
            require(next.retentionHours in listOf(0, 24, 168))
            p[ENABLED] = next.enabled; p[LIMIT] = next.limit; p[SENSITIVE] = next.skipSensitive
            p[LOCK] = next.appLock; p[RETENTION] = next.retentionHours
        }
    }
    companion object {
        private val ENABLED = booleanPreferencesKey("save_history")
        private val LIMIT = intPreferencesKey("history_limit")
        private val SENSITIVE = booleanPreferencesKey("skip_sensitive")
        private val LOCK = booleanPreferencesKey("app_lock")
        private val RETENTION = intPreferencesKey("retention_hours")
    }
}