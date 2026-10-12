package com.clipsync.android.ui

import android.widget.Toast
import androidx.activity.compose.BackHandler
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.DeleteOutline
import androidx.compose.material.icons.outlined.History
import androidx.compose.material.icons.outlined.Search
import androidx.compose.material.icons.outlined.Close
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.platform.LocalSoftwareKeyboardController
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import com.clipsync.android.history.ClipboardHistory
import com.clipsync.android.history.HistoryEntry
import com.clipsync.android.history.HistoryFilter
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
    val focusManager = LocalFocusManager.current
    val keyboard = LocalSoftwareKeyboardController.current
    val density = androidx.compose.ui.platform.LocalDensity.current
    val imeInsets = WindowInsets.ime
    var query by rememberSaveable { mutableStateOf("") }
    var searchFocused by remember { mutableStateOf(false) }
    var source by rememberSaveable { mutableStateOf(HistoryFilter.ALL) }
    var confirmClear by remember { mutableStateOf(false) }
    LaunchedEffect(imeInsets, density) {
        var wasVisible = imeInsets.getBottom(density) > 0
        snapshotFlow { imeInsets.getBottom(density) > 0 }.collect { visible ->
            if (wasVisible && !visible && searchFocused) {
                focusManager.clearFocus(force = true)
                searchFocused = false
            }
            wasVisible = visible
        }
    }
    BackHandler {
        if (searchFocused) {
            keyboard?.hide()
            focusManager.clearFocus(force = true)
            searchFocused = false
        } else onBack()
    }
    LaunchedEffect(Unit) { history.refresh() }

    val fromPc = entries.count { it.direction == "From PC" }
    val fromPhone = entries.count { it.direction == "From phone" }
    val visible = remember(entries, query, source) { HistoryFilter.apply(entries, query, source) }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Column {
                        Text("History")
                        if (options != null) Text(
                            "${entries.size} saved ${if (entries.size == 1) "item" else "items"}",
                            style = MaterialTheme.typography.labelSmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                },
                navigationIcon = {
                    IconButton(onClick = {
                        keyboard?.hide()
                        focusManager.clearFocus(force = true)
                        searchFocused = false
                        onBack()
                    }) { Icon(Icons.AutoMirrored.Outlined.ArrowBack, "Back") }
                },
            )
        },
        bottomBar = {
            if (options?.enabled == true && entries.isNotEmpty()) Surface(
                color = MaterialTheme.colorScheme.background,
                tonalElevation = 2.dp,
            ) {
                Row(
                    Modifier.fillMaxWidth().navigationBarsPadding().padding(horizontal = 20.dp, vertical = 12.dp),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.SpaceBetween,
                ) {
                    Text("${visible.size} shown · ${entries.size} of ${options?.limit} items",
                        style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    TextButton(onClick = { confirmClear = true }, enabled = storageError == null) {
                        Text("Clear history", color = MaterialTheme.colorScheme.error)
                    }
                }
            }
        },
    ) { padding ->
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
            else -> Column(
                Modifier.fillMaxSize().padding(padding).clickable(
                    interactionSource = remember { MutableInteractionSource() },
                    indication = null,
                ) {
                    if (searchFocused) {
                        keyboard?.hide()
                        focusManager.clearFocus(force = true)
                        searchFocused = false
                    }
                },
            ) {
                OutlinedTextField(
                    value = query,
                    onValueChange = { query = it },
                    modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 8.dp)
                        .onFocusChanged { searchFocused = it.isFocused },
                    singleLine = true,
                    placeholder = { Text("Search clipboard history") },
                    leadingIcon = { Icon(Icons.Outlined.Search, contentDescription = null) },
                    trailingIcon = {
                        if (query.isNotEmpty()) IconButton(onClick = { query = "" }) {
                            Icon(Icons.Outlined.Close, "Clear search")
                        }
                    },
                    shape = MaterialTheme.shapes.medium,
                )
                Row(
                    Modifier.fillMaxWidth().padding(horizontal = 12.dp),
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                ) {
                    HistoryFilterChip("All", entries.size, source == HistoryFilter.ALL) { source = HistoryFilter.ALL }
                    HistoryFilterChip("From PC", fromPc, source == "From PC") { source = "From PC" }
                    HistoryFilterChip("From phone", fromPhone, source == "From phone") { source = "From phone" }
                }
                when {
                    entries.isEmpty() -> Column(
                        Modifier.fillMaxWidth().weight(1f).padding(24.dp),
                        horizontalAlignment = Alignment.CenterHorizontally,
                        verticalArrangement = Arrangement.Center,
                    ) {
                        Icon(Icons.Outlined.History, null, Modifier.size(48.dp))
                        Text("No clipboard history yet", style = MaterialTheme.typography.titleMedium, modifier = Modifier.padding(16.dp))
                        Text(
                            "Copy text, then return to FerryClip to save it here. Clips received from your PC and Send to PC are also saved.",
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                    visible.isEmpty() -> Column(Modifier.fillMaxSize().padding(24.dp), horizontalAlignment = Alignment.CenterHorizontally,
                        verticalArrangement = Arrangement.Center) {
                        Text("No results", style = MaterialTheme.typography.titleMedium)
                        Text("Try another search or choose a different filter.", color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                    else -> LazyColumn(
                        Modifier.fillMaxSize(),
                        contentPadding = PaddingValues(horizontal = 16.dp, vertical = 12.dp),
                        verticalArrangement = Arrangement.spacedBy(10.dp),
                    ) {
                        val groups = visible.groupBy { HistoryFilter.todayGroup(it.timestamp) }
                        for (group in listOf("Today", "Yesterday", "Earlier")) {
                            val groupEntries = groups[group].orEmpty()
                            if (groupEntries.isNotEmpty()) {
                                item(key = "heading:$group") {
                                    Text(group, modifier = Modifier.fillMaxWidth().padding(start = 4.dp, top = 4.dp),
                                        style = MaterialTheme.typography.labelLarge, color = MaterialTheme.colorScheme.onSurfaceVariant)
                                }
                                items(groupEntries, key = { it.id }) { entry ->
                                    HistoryEntryCard(entry, onCopy = {
                                        scope.launch {
                                            try {
                                                val text = history.payload(entry.id)
                                                if (text == null) Toast.makeText(context, "Preview only — full clip was not saved", Toast.LENGTH_SHORT).show()
                                                else {
                                                    com.clipsync.android.service.SyncRuntime.receiver.engine.hashGuard.observeLocal(text)
                                                    val clip = android.content.ClipData.newPlainText("FerryClip history", text)
                                                    clip.description.extras = android.os.PersistableBundle().apply {
                                                        putBoolean("android.content.extra.IS_SENSITIVE", true)
                                                    }
                                                    context.getSystemService(android.content.ClipboardManager::class.java).setPrimaryClip(clip)
                                                    Toast.makeText(context, "Copied to clipboard", Toast.LENGTH_SHORT).show()
                                                }
                                            } catch (_: Exception) {
                                                Toast.makeText(context, "Could not copy this clip", Toast.LENGTH_SHORT).show()
                                            }
                                        }
                                    }, onDelete = { history.delete(entry.id) })
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    if (confirmClear) AlertDialog(
        onDismissRequest = { confirmClear = false },
        title = { Text("Clear clipboard history?") },
        text = { Text("All ${entries.size} saved ${if (entries.size == 1) "item" else "items"} will be deleted from this phone. This cannot be undone.") },
        confirmButton = {
            TextButton(onClick = { history.clear(); confirmClear = false }) {
                Text("Clear history", color = MaterialTheme.colorScheme.error)
            }
        },
        dismissButton = { TextButton(onClick = { confirmClear = false }) { Text("Cancel") } },
    )
}

@Composable
private fun RowScope.HistoryFilterChip(label: String, count: Int, selected: Boolean, onClick: () -> Unit) {
    FilterChip(
        selected = selected,
        onClick = onClick,
        label = { Text("$label ($count)", maxLines = 1) },
        modifier = Modifier.weight(1f),
    )
}

@Composable
private fun HistoryEntryCard(entry: HistoryEntry, onCopy: () -> Unit, onDelete: () -> Unit) {
    Surface(
        shape = MaterialTheme.shapes.large,
        color = MaterialTheme.colorScheme.surfaceContainerLow,
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
        modifier = Modifier.fillMaxWidth(),
    ) {
        Row(Modifier.padding(start = 16.dp, end = 8.dp, top = 12.dp, bottom = 12.dp), verticalAlignment = Alignment.CenterVertically) {
            Column(Modifier.weight(1f).clickable(onClick = onCopy), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                Text(entry.preview, maxLines = 2, overflow = TextOverflow.Ellipsis)
                Text(entry.direction + " · " + DateFormat.getDateTimeInstance(DateFormat.SHORT, DateFormat.SHORT).format(Date(entry.timestamp)),
                    style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                if (entry.previewOnly) Text("Preview only · clip exceeds 1 MB", style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
            TextButton(onClick = onCopy, contentPadding = PaddingValues(horizontal = 8.dp)) { Text("Copy") }
            IconButton(onClick = onDelete) { Icon(Icons.Outlined.DeleteOutline, "Delete entry") }
        }
    }
}
