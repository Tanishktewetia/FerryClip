package com.clipsync.android.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.selection.SelectionContainer
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Download
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.clipsync.android.logging.FileLogger

/** Beta-only support UI kept in its own file so production builds can omit the section cleanly. */
@Composable
internal fun DiagnosticsSection(copy: () -> Unit, share: () -> Unit, save: () -> Unit) {
    var expanded by rememberSaveable { mutableStateOf(false) }
    var logs by remember { mutableStateOf("") }
    Card(colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        border = androidx.compose.foundation.BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
        shape = MaterialTheme.shapes.large) {
        Column(Modifier.fillMaxWidth().padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Text("Diagnostics · beta", style = MaterialTheme.typography.titleMedium)
            Text("Status and clip size/hash only; never the copied text. Copy logs, then use Send to PC to transfer them over the paired connection.",
                style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedButton(onClick = copy, modifier = Modifier.weight(1f), contentPadding = PaddingValues(8.dp)) { Text("Copy logs") }
                OutlinedButton(onClick = share, modifier = Modifier.weight(1f), contentPadding = PaddingValues(8.dp)) { Text("Share logs") }
            }
            TextButton(onClick = save) { Icon(Icons.Outlined.Download, null); Spacer(Modifier.width(8.dp)); Text("Save to Downloads") }
            TextButton(onClick = { expanded = !expanded; if (expanded) logs = FileLogger.getRecentLogs(60).takeLast(12000) }) {
                Text(if (expanded) "Hide recent logs" else "View recent logs")
            }
            if (expanded) SelectionContainer {
                Text(logs, style = MaterialTheme.typography.bodySmall, modifier = Modifier.fillMaxWidth().heightIn(max = 240.dp)
                    .verticalScroll(rememberScrollState()).background(MaterialTheme.colorScheme.surfaceVariant).padding(12.dp))
            }
        }
    }
}