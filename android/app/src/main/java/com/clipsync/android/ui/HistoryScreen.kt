package com.clipsync.android.ui

import android.widget.Toast
import androidx.activity.compose.BackHandler
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.DeleteOutline
import androidx.compose.material.icons.outlined.History
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import com.clipsync.android.history.ClipboardHistory
import kotlinx.coroutines.launch
import java.text.DateFormat
import java.util.Date

@OptIn(ExperimentalMaterial3Api::class)
@Composable
internal fun HistoryScreen(onBack: () -> Unit) {
    val context = LocalContext.current
    val history = remember { ClipboardHistory.get(context) }
    val options by history.options.collectAsState()
    val entries by history.entries.collectAsState()
    val storageError by history.error.collectAsState()
    val scope = rememberCoroutineScope()
    BackHandler(onBack = onBack)
    LaunchedEffect(Unit) { history.refresh() }
    Scaffold(topBar = { TopAppBar(title = { Text("Clipboard history") }, navigationIcon = {
        IconButton(onClick = onBack) { Icon(Icons.AutoMirrored.Outlined.ArrowBack, "Back") }
    }) }) { padding ->
        when {
            storageError != null -> Column(Modifier.fillMaxSize().padding(padding).padding(24.dp),
                horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.Center) {
                Text(storageError ?: "History unavailable")
                Button(onClick = { history.refresh() }) { Text("Retry") }
            }
            options == null -> Box(Modifier.fillMaxSize().padding(padding), contentAlignment = Alignment.Center) { CircularProgressIndicator() }
            options?.enabled == false -> Column(Modifier.fillMaxSize().padding(padding).padding(24.dp),
                horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.Center) {
                Icon(Icons.Outlined.History, null, Modifier.size(48.dp))
                Text("Clipboard history is off", style = MaterialTheme.typography.titleMedium, modifier = Modifier.padding(16.dp))
                Button(onClick = { history.update { it.copy(enabled = true) } }) { Text("Turn on history") }
            }
            entries.isEmpty() -> Column(Modifier.fillMaxSize().padding(padding).padding(24.dp),
                horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.Center) {
                Icon(Icons.Outlined.History, null, Modifier.size(48.dp))
                Text("No clipboard history yet", style = MaterialTheme.typography.titleMedium, modifier = Modifier.padding(16.dp))
                Text("Copy text, then return to FerryClip to save it here. Clips received from your PC and Send to PC are also saved.", color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
            else -> LazyColumn(Modifier.fillMaxSize().padding(padding), contentPadding = PaddingValues(16.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp)) {
                items(entries, key = { it.id }) { entry ->
                    Surface(shape = MaterialTheme.shapes.large, color = MaterialTheme.colorScheme.surfaceContainerLow,
                        border = androidx.compose.foundation.BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
                        modifier = Modifier.fillMaxWidth().clickable(onClick = {
                            scope.launch {
                                try {
                                    val text = history.payload(entry.id)
                                    if (text == null) Toast.makeText(context, "Preview only — full clip was not saved", Toast.LENGTH_SHORT).show()
                                    else {
                                        com.clipsync.android.service.SyncRuntime.receiver.engine.hashGuard.observeLocal(text)
                                        val clip = android.content.ClipData.newPlainText("FerryClip history", text)
                                        clip.description.extras = android.os.PersistableBundle().apply { putBoolean("android.content.extra.IS_SENSITIVE", true) }
                                        context.getSystemService(android.content.ClipboardManager::class.java).setPrimaryClip(clip)
                                        Toast.makeText(context, "Copied to clipboard", Toast.LENGTH_SHORT).show()
                                    }
                                } catch (_: Exception) { Toast.makeText(context, "Could not copy this clip", Toast.LENGTH_SHORT).show() }
                            }
                        })) {
                        Row(Modifier.padding(16.dp), verticalAlignment = Alignment.CenterVertically) {
                            Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                                Text(entry.preview, maxLines = 2, overflow = TextOverflow.Ellipsis)
                                Text(entry.direction + " · " + DateFormat.getDateTimeInstance(DateFormat.SHORT, DateFormat.SHORT).format(Date(entry.timestamp)),
                                    style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                                if (entry.previewOnly) Text("Preview only · clip exceeds 1 MB", style = MaterialTheme.typography.labelSmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant)
                            }
                            IconButton(onClick = { history.delete(entry.id) }) { Icon(Icons.Outlined.DeleteOutline, "Delete entry") }
                        }
                    }
                }
            }
        }
    }
}
