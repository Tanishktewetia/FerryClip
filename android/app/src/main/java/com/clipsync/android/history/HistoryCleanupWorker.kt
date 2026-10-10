package com.clipsync.android.history

import android.content.Context
import androidx.work.*
import java.util.concurrent.TimeUnit

class HistoryCleanupWorker(context: Context, parameters: WorkerParameters) : CoroutineWorker(context, parameters) {
    override suspend fun doWork(): Result = try {
        ClipboardHistory.get(applicationContext).cleanup()
        Result.success()
    } catch (e: kotlinx.coroutines.CancellationException) { throw e }
      catch (_: Exception) { Result.retry() }
    companion object {
        private const val NAME = "clipboard_history_cleanup"
        fun schedule(context: Context, hours: Int) {
            val manager = WorkManager.getInstance(context)
            if (hours == 0) manager.cancelUniqueWork(NAME)
            else manager.enqueueUniquePeriodicWork(NAME, ExistingPeriodicWorkPolicy.KEEP,
                PeriodicWorkRequestBuilder<HistoryCleanupWorker>(1, TimeUnit.HOURS).build())
        }
    }
}
