package com.clipsync.android.history

import java.util.Calendar

/** Shared filtering policy for the history list and its source-count chips. */
internal object HistoryFilter {
    fun apply(entries: List<HistoryEntry>, query: String, source: String): List<HistoryEntry> {
        val normalized = query.trim()
        return entries.filter { entry ->
            (source == ALL || entry.direction == source) &&
                (normalized.isEmpty() || entry.searchableText.contains(normalized, ignoreCase = true))
        }
    }

    fun todayGroup(timestamp: Long, now: Long = System.currentTimeMillis()): String {
        val item = Calendar.getInstance().apply { timeInMillis = timestamp }
        val current = Calendar.getInstance().apply { timeInMillis = now }
        if (sameDay(item, current)) return "Today"
        current.add(Calendar.DAY_OF_YEAR, -1)
        if (sameDay(item, current)) return "Yesterday"
        return "Earlier"
    }

    private fun sameDay(left: Calendar, right: Calendar) =
        left.get(Calendar.YEAR) == right.get(Calendar.YEAR) &&
            left.get(Calendar.DAY_OF_YEAR) == right.get(Calendar.DAY_OF_YEAR)

    const val ALL = "All"
}
