package com.clipsync.android.ui

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.animateContentSize
import androidx.activity.compose.BackHandler
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Image
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.res.painterResource
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.automirrored.outlined.HelpOutline
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.platform.LocalSoftwareKeyboardController
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.semantics.*
import androidx.compose.ui.text.style.TextDecoration
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import com.clipsync.android.BuildConfig
import com.clipsync.android.service.SyncUiState
import com.clipsync.android.store.*
import java.text.DateFormat
import java.util.Date

@OptIn(ExperimentalMaterial3Api::class, ExperimentalLayoutApi::class)
@Composable
fun MainScreen(state: SyncUiState, lastCrash: String?, notifications: Boolean, battery: Boolean, tile: Boolean, tileDismissed: Boolean,
    onNotifications: () -> Unit, onBattery: () -> Unit, onTile: () -> Unit, onDismissTile: () -> Unit,
    onRedo: () -> Unit, onConnect: (String) -> Unit, onDisconnect: (String) -> Unit, onPair: (String, String?) -> Unit, onCancelPair: () -> Unit,
    onPause: (String, Boolean) -> Unit, onForget: (String) -> Unit, onRename: (String, String) -> Unit,
    onPairCode: (Long, String?) -> Unit, onCopyLogs: () -> Unit, onShareLogs: () -> Unit, onSaveLogs: () -> Unit,
    ) {
    val focusManager = LocalFocusManager.current
    val keyboard = LocalSoftwareKeyboardController.current
    var codeFocused by remember { mutableStateOf(false) }
    val density = androidx.compose.ui.platform.LocalDensity.current
    val imeInsets = WindowInsets.ime
    // Android consumes Back to dismiss the IME before Compose's BackHandler.
    // Observe the actual visible -> hidden edge, not focus changes or a stale flag.
    LaunchedEffect(imeInsets, density) {
        var wasVisible = imeInsets.getBottom(density) > 0
        snapshotFlow { imeInsets.getBottom(density) > 0 }.collect { visible ->
            if (wasVisible && !visible && codeFocused) {
                focusManager.clearFocus(force = true)
                codeFocused = false
            }
            wasVisible = visible
        }
    }
    var help by rememberSaveable { mutableStateOf(false) }
    var settings by rememberSaveable { mutableStateOf(false) }
    var helpFromAbout by rememberSaveable { mutableStateOf(false) }
    var about by rememberSaveable { mutableStateOf(false) }
    var guide by rememberSaveable { mutableStateOf(false) }
    var forgetId by rememberSaveable { mutableStateOf<String?>(null) }
    var replacementId by rememberSaveable { mutableStateOf<String?>(null) }
    var crash by rememberSaveable { mutableStateOf(lastCrash != null) }
    val pageState = androidx.compose.runtime.saveable.rememberSaveableStateHolder()
    if (help) { HelpScreen(onBack = { help = false; if(helpFromAbout) { about = true; helpFromAbout = false } }, onAddTile = onTile); return }
    if (about) { AboutScreen(onBack = { about = false }, onHelp = { about = false; helpFromAbout = true; help = true }, diagnosticsEnabled = BuildConfig.DIAGNOSTICS_ENABLED); return }
    BackHandler(enabled = codeFocused) { keyboard?.hide(); focusManager.clearFocus(); codeFocused = false }
    BackHandler(enabled = settings) { settings = false }
    pageState.SaveableStateProvider(if(settings) "settings" else "devices") {
    Scaffold(containerColor = MaterialTheme.colorScheme.background, topBar = {
        Surface(color = MaterialTheme.colorScheme.background) {
            Column(Modifier.statusBarsPadding().fillMaxWidth().padding(horizontal = 20.dp)) {
                Row(Modifier.fillMaxWidth().heightIn(min = 60.dp), verticalAlignment = Alignment.CenterVertically) {
                    if (!settings) {
                        Image(painterResource(com.clipsync.android.R.drawable.ferryclip_logo), contentDescription = "FerryClip logo",
                            modifier = Modifier.width(48.dp).height(32.dp), contentScale = ContentScale.Fit)
                        Spacer(Modifier.width(12.dp))
                    }
                    Text(if (settings) "Settings" else "FerryClip", style = MaterialTheme.typography.titleLarge, modifier = Modifier.weight(1f))
                    IconButton(onClick = { keyboard?.hide(); focusManager.clearFocus(); help = true }) { Icon(Icons.AutoMirrored.Outlined.HelpOutline, "Help", tint = MaterialTheme.colorScheme.onSurface) }
                    IconButton(onClick = { keyboard?.hide(); focusManager.clearFocus(); settings = !settings }) {
                        Icon(if (settings) Icons.Outlined.Close else Icons.Outlined.Settings,
                            if (settings) "Back to devices" else "Settings", tint = MaterialTheme.colorScheme.onSurface)
                    }
                }
                if (!settings) Row(Modifier.fillMaxWidth().padding(bottom = 16.dp)
                    .semantics { liveRegion = LiveRegionMode.Polite }, verticalAlignment = Alignment.CenterVertically) {
                    Box(Modifier.size(8.dp).background(if (state.connected) MaterialTheme.colorScheme.tertiary else MaterialTheme.colorScheme.onSurfaceVariant, CircleShape))
                    Spacer(Modifier.width(8.dp))
                    val connectedCount = state.devices.count { it.connectionState in setOf(DeviceState.Connected, DeviceState.Paused) }
                    Text(when { state.connecting && connectedCount == 0 -> "Connecting to your PC"; connectedCount > 0 -> "Connected · $connectedCount PC(s)"; else -> "Disconnected" },
                        style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
            }
        }
    }, bottomBar = {
        if (settings) Surface(color = MaterialTheme.colorScheme.background) {
            Text("FerryClip ${BuildConfig.VERSION_NAME} · Your clipboard stays on your local network",
                modifier = Modifier.fillMaxWidth().navigationBarsPadding().padding(horizontal = 24.dp, vertical = 12.dp),
                textAlign = androidx.compose.ui.text.style.TextAlign.Center,
                style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }) { padding ->
        Box(Modifier.fillMaxSize().padding(padding), contentAlignment = Alignment.TopCenter) {
        Column(Modifier.widthIn(max = 600.dp).fillMaxSize().verticalScroll(rememberScrollState())
            .padding(horizontal = if (settings) 16.dp else 20.dp, vertical = if (settings) 12.dp else 24.dp), verticalArrangement = Arrangement.spacedBy(if (settings) 12.dp else 20.dp)) {
            if (settings) {
                SettingsPanel(
                    notifications = notifications,
                    battery = battery,
                    tile = tile,
                    diagnosticsEnabled = BuildConfig.DIAGNOSTICS_ENABLED,
                    onNotifications = onNotifications,
                    onBattery = onBattery,
                    onTile = onTile,
                    onRedo = onRedo,
                    onHelp = { help = true },
                    onAbout = { about = true },
                    onCopyLogs = onCopyLogs,
                    onShareLogs = onShareLogs,
                    onSaveLogs = onSaveLogs,
                )
            } else {

                if (!notifications || !battery || (!tile && !tileDismissed)) Card(colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface), border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant)) {
                    Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        Text("Finish your setup", style = MaterialTheme.typography.titleMedium)
                        if (!notifications) OutlinedButton(onClick = onNotifications, modifier = Modifier.fillMaxWidth().heightIn(min = 48.dp), shape = MaterialTheme.shapes.medium) { Icon(Icons.Outlined.NotificationsNone, null); Spacer(Modifier.width(8.dp)); Text("Enable notifications") }
                        if (!battery) OutlinedButton(onClick = onBattery, modifier = Modifier.fillMaxWidth().heightIn(min = 48.dp), shape = MaterialTheme.shapes.medium) { Icon(Icons.Outlined.BatterySaver, null); Spacer(Modifier.width(8.dp)); Text("Allow background battery use") }
                        if (!tile && !tileDismissed) { OutlinedButton(onClick = { guide = !guide }, modifier = Modifier.fillMaxWidth().heightIn(min = 48.dp), shape = MaterialTheme.shapes.medium) { Icon(Icons.Outlined.GridView, null); Spacer(Modifier.width(8.dp)); Text("Send to PC tile setup") }
                            TextButton(onClick = onDismissTile) { Text("Dismiss tile reminder", textDecoration = TextDecoration.Underline) } }
                        AnimatedVisibility(guide && !tile) { TileGuide(onTile) }
                    }
                }
                if (state.devices.isNotEmpty()) Text("Your PCs", style = MaterialTheme.typography.titleMedium)
                DevicePolicy.ordered(state.devices).forEach { device -> key(device.id) {
                    DeviceTile(device, state.networkLabel, onConnect, onDisconnect, onPause, { forgetId = device.id }, onRename, { replacementId = device.id })
                } }
                state.error?.let { Text(it, color = MaterialTheme.colorScheme.error, modifier = Modifier.semantics { liveRegion = LiveRegionMode.Polite }) }
                var sendNote by remember { mutableStateOf<String?>(null) }
                LaunchedEffect(state.sendFeedback) { sendNote = state.sendFeedback; kotlinx.coroutines.delay(5000); sendNote = null }
                sendNote?.let { Text(it, style = MaterialTheme.typography.bodySmall) }
                AddDevice(state, onPair, onCancelPair, replacementId, { replacementId = it }, { codeFocused = it })

            }
            if (!settings) Column(Modifier.fillMaxWidth().padding(top = 4.dp), verticalArrangement = Arrangement.spacedBy(2.dp)) {
                HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
                Text("PC → phone automatically · Phone → PC with one tap", modifier = Modifier.padding(top = 12.dp), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                TextButton(onClick = { help = true }, contentPadding = PaddingValues(0.dp)) { Text("How FerryClip works") }
            }
            Spacer(Modifier.height(12.dp))
        }
        }
    }
    }
    forgetId?.let { id -> state.devices.firstOrNull { it.id == id }?.let { device ->
        AlertDialog(onDismissRequest = { forgetId = null }, title = { Text("Forget ${device.displayName}?") },
            text = { Text("You'll need to pair again to reconnect.") },
            confirmButton = { TextButton(onClick = { onForget(id); forgetId = null }) { Text("Forget", color = MaterialTheme.colorScheme.error) } },
            dismissButton = { TextButton(onClick = { forgetId = null }) { Text("Cancel") } })
    } }
    if (crash) AlertDialog(onDismissRequest = { crash = false }, title = { Text("Previous session ended unexpectedly") },
        text = { Text(if (BuildConfig.DIAGNOSTICS_ENABLED) "Share the Diagnostics logs from Settings so we can investigate." else "FerryClip can be reopened from your app list. Your paired devices remain saved.") },
        confirmButton = { TextButton(onClick = { crash = false; if (BuildConfig.DIAGNOSTICS_ENABLED) settings = true }) { Text(if (BuildConfig.DIAGNOSTICS_ENABLED) "Open Diagnostics" else "OK") } })
}

@Composable
private fun DeviceTile(device: PairedDevice, network: String, connect: (String) -> Unit, disconnect: (String) -> Unit, pause: (String, Boolean) -> Unit, forget: () -> Unit, rename: (String, String) -> Unit, replacePairing: () -> Unit) {
    var expanded by rememberSaveable { mutableStateOf(false) }
    var editing by rememberSaveable { mutableStateOf(false) }
    var name by rememberSaveable(device.displayName) { mutableStateOf(device.displayName) }
    val dark = isSystemInDarkTheme()
    val color = when {
        device.connectionState == DeviceState.Connected -> if (dark) Color(0xFF6EE7B7) else Color(0xFF047857)
        device.connectionState == DeviceState.Unreachable -> MaterialTheme.colorScheme.error
        device.connectionState == DeviceState.IdentityChanged -> if (dark) Color(0xFFFCD34D) else Color(0xFF92400E)
        device.connectionState == DeviceState.Connecting || device.paused -> if (dark) Color(0xFF93C5FD) else Color(0xFF1D4ED8)
        else -> MaterialTheme.colorScheme.onSurfaceVariant
    }
    val status = when { device.connectionState == DeviceState.IdentityChanged -> "Identity changed"; device.connectionState == DeviceState.Connecting -> "Connecting…"; device.paused -> "Paused"; else -> device.connectionState.name }
    val connected = device.connectionState == DeviceState.Connected || device.connectionState == DeviceState.Paused
    Card(Modifier.fillMaxWidth().animateContentSize(), shape = RoundedCornerShape(16.dp), border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant), colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)) {
        Row(Modifier.fillMaxWidth().clickable(role = Role.Button) { expanded = !expanded }.semantics { stateDescription = if (expanded) "Expanded" else "Collapsed" }.padding(20.dp), verticalAlignment = Alignment.CenterVertically) {
            Box(Modifier.size(10.dp).background(color, CircleShape)); Spacer(Modifier.width(12.dp))
            Column(Modifier.weight(1f)) {
                Text(device.displayName, style = MaterialTheme.typography.titleMedium)
                Text(device.lastConnectedAt?.let { relativeTime(it) } ?: status, maxLines = 1, overflow = TextOverflow.Ellipsis, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
            Spacer(Modifier.width(8.dp))
            Column(horizontalAlignment = Alignment.End) { Text(status, color = color, style = MaterialTheme.typography.labelMedium); Icon(if (expanded) Icons.Outlined.ExpandLess else Icons.Outlined.ExpandMore, null) }
        }
        AnimatedVisibility(expanded) {
            Column(Modifier.padding(start = 20.dp, end = 20.dp, bottom = 20.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                HorizontalDivider()
                Text("Address: ${device.lastKnownAddress}", style = MaterialTheme.typography.bodyMedium)
                Text("PC: ${device.hostLabel}", style = MaterialTheme.typography.bodyMedium)
                Text("Network: $network", style = MaterialTheme.typography.bodyMedium)
                device.connectedSince?.let { Text("Connected since: " + DateFormat.getTimeInstance(DateFormat.SHORT).format(Date(it))) }
                if (device.connectionState == DeviceState.IdentityChanged) Text("This address presents a different PC identity. Compare a fresh code before replacing it.", color = color)
                if (editing) {
                    OutlinedTextField(name, { name = it.take(60) }, label = { Text("Device name") }, modifier = Modifier.fillMaxWidth())
                    TextButton(onClick = { if (name.isNotBlank()) { rename(device.id, name.trim()); editing = false } }) { Text("Save name") }
                } else TextButton(onClick = { editing = true }) { Icon(Icons.Outlined.Edit, null); Spacer(Modifier.width(8.dp)); Text("Rename") }
                if (device.connectionState == DeviceState.IdentityChanged) Button(shape = MaterialTheme.shapes.medium, onClick = replacePairing) { Text("Enter new code") }
                else {
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalAlignment = Alignment.CenterVertically) {
                        Button(shape = MaterialTheme.shapes.medium, onClick = { if (device.isActive) disconnect(device.id) else connect(device.id) }, enabled = device.connectionState != DeviceState.Connecting, modifier = Modifier.weight(1f)) { Text(if (device.isActive) "Disconnect" else "Connect") }
                        OutlinedButton(shape = MaterialTheme.shapes.medium, onClick = { pause(device.id, !device.paused) }) { Icon(if (device.paused) Icons.Outlined.PlayArrow else Icons.Outlined.Pause, null); Text(if (device.paused) "Resume" else "Pause") }
                    }
                }
                OutlinedButton(shape = MaterialTheme.shapes.medium, onClick = forget, modifier = Modifier.fillMaxWidth()) { Icon(Icons.Outlined.DeleteOutline, null); Spacer(Modifier.width(6.dp)); Text("Forget PC") }
            }
        }
    }
}@Composable
private fun AddDevice(state: SyncUiState, pair: (String, String?) -> Unit, cancel: () -> Unit, replacementId: String?, onReplacement: (String?) -> Unit, onCodeFocus: (Boolean) -> Unit) {
    val focusManager = LocalFocusManager.current
    val keyboard = LocalSoftwareKeyboardController.current
    var code by rememberSaveable { mutableStateOf("") }
    var devicesAtPairStart by remember { mutableStateOf<List<PairedDevice>?>(null) }
    var pairSearchStarted by remember { mutableStateOf(false) }
    LaunchedEffect(state.searching, state.pairingMessage, state.devices) {
        val baseline = devicesAtPairStart
        if (baseline != null && state.devices != baseline) { code = ""; devicesAtPairStart = null; pairSearchStarted = false }
        if (state.searching) pairSearchStarted = true
        else if (baseline != null && pairSearchStarted) {
            if (state.pairingMessage == null) code = ""
            devicesAtPairStart = null
            pairSearchStarted = false
        }
    }
    Column(verticalArrangement = Arrangement.spacedBy(16.dp)) {
        Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
            Text(if (replacementId == null) { if (state.devices.isEmpty()) "Connect your first PC" else "Add a PC" } else "Replace PC pairing", style = MaterialTheme.typography.headlineSmall)
            Text("On Windows, choose Generate code to connect. Enter the six digits here.", style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        OutlinedTextField(code, { code = it.filter { digit -> digit in '0'..'9' }.take(6) }, label = { Text("Code from your PC") }, placeholder = { Text("123456") }, singleLine = true,
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number, imeAction = androidx.compose.ui.text.input.ImeAction.Done),
            keyboardActions = androidx.compose.foundation.text.KeyboardActions(onDone = { keyboard?.hide(); focusManager.clearFocus() }), modifier = Modifier.fillMaxWidth().onFocusChanged { onCodeFocus(it.isFocused) }, enabled = !state.searching, textStyle = MaterialTheme.typography.headlineSmall)
        Button(onClick = { keyboard?.hide(); focusManager.clearFocus(); devicesAtPairStart = state.devices; pairSearchStarted = false; pair(code, replacementId) }, enabled = code.length == 6 && !state.searching, shape = MaterialTheme.shapes.medium, modifier = Modifier.fillMaxWidth().heightIn(min = 52.dp)) {
            Text(if (state.searching) "Connecting on your local network…" else if (replacementId == null) "Pair this PC" else "Replace pairing")
        }
        if (replacementId != null) TextButton(onClick = { onReplacement(null) }) { Text("Cancel replacement") }
        if (state.searching) { LinearProgressIndicator(Modifier.fillMaxWidth()); OutlinedButton(onClick = cancel, modifier = Modifier.fillMaxWidth()) { Text("Cancel — try another code") } }
        state.pairingMessage?.let { message ->
            Surface(color = if (state.pairingFailed) MaterialTheme.colorScheme.errorContainer else MaterialTheme.colorScheme.surface,
                contentColor = if (state.pairingFailed) MaterialTheme.colorScheme.onErrorContainer else MaterialTheme.colorScheme.onSurface, shape = MaterialTheme.shapes.medium) {
                Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Row(horizontalArrangement = Arrangement.spacedBy(10.dp), verticalAlignment = Alignment.CenterVertically) {
                        Icon(if (state.pairingFailed) Icons.Outlined.ErrorOutline else Icons.Outlined.CheckCircle, null)
                        Text(if (state.pairingFailed) "Pairing needs attention" else "Pairing complete", style = MaterialTheme.typography.titleSmall)
                    }
                    Text(message, style = MaterialTheme.typography.bodyMedium, modifier = Modifier.semantics { liveRegion = LiveRegionMode.Polite })
                    if (!state.pairingFailed) TextButton(onClick = { code = ""; onReplacement(null); com.clipsync.android.service.SyncRuntime.clearPairingFeedback() }) { Text("Add another PC") }
                }
            }
        }
    }
}private fun relativeTime(timestamp: Long): String {
    val minutes = ((System.currentTimeMillis() - timestamp).coerceAtLeast(0) / 60000)
    return when { minutes < 1 -> "Just now"; minutes < 60 -> "$minutes min ago"; minutes < 1440 -> "${minutes / 60} h ago"; else -> "${minutes / 1440} d ago" }
}
