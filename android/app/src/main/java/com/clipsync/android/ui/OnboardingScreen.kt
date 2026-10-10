package com.clipsync.android.ui

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowForward
import androidx.compose.material.icons.automirrored.outlined.Send
import androidx.compose.material.icons.outlined.*
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp

@Composable
fun OnboardingScreen(
    step: Int,
    notifications: Boolean,
    battery: Boolean,
    tile: Boolean,
    advance: (Int) -> Unit,
    finish: () -> Unit,
    requestNotifications: () -> Unit,
    requestBattery: () -> Unit,
    addTile: () -> Unit,
    exitReplay: (() -> Unit)? = null,
) {
    val page = step.coerceIn(0, 3)
    val complete = when (page) { 2 -> notifications && battery; 3 -> tile; else -> true }
    BackHandler(enabled = page > 0 || exitReplay != null) {
        if (page > 0) advance(page - 1) else exitReplay?.invoke()
    }

    Scaffold(
        containerColor = MaterialTheme.colorScheme.background,
        bottomBar = {
            Surface(color = MaterialTheme.colorScheme.surface) {
                Column(
                    Modifier.fillMaxWidth().navigationBarsPadding().padding(horizontal = 20.dp, vertical = 8.dp),
                    verticalArrangement = Arrangement.spacedBy(2.dp),
                ) {
                    Button(
                        onClick = { if (page == 3) finish() else advance(page + 1) },
                        shape = MaterialTheme.shapes.medium,
                        modifier = Modifier.fillMaxWidth().heightIn(min = 52.dp),
                    ) { Text(if (page == 3) "Open dashboard" else "Continue") }
                    Row(Modifier.fillMaxWidth().heightIn(min = 40.dp), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                        if (page > 0) TextButton(onClick = { advance(page - 1) }) { Text("Back") } else Spacer(Modifier.width(1.dp))
                        if (page > 0 && !complete) TextButton(onClick = { if (page == 3) finish() else advance(page + 1) }) { Text("Skip for now") }
                    }
                }
            }
        },
    ) { padding ->
        Column(
            Modifier.fillMaxSize().padding(padding).widthIn(max = 560.dp).verticalScroll(rememberScrollState())
                .padding(horizontal = 20.dp, vertical = 16.dp),
            verticalArrangement = Arrangement.spacedBy(18.dp),
        ) {
            BrandHeader("Android + Windows")
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                    Text("Set up FerryClip", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.SemiBold)
                    Text("${page + 1} of 4", style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                    repeat(4) { index ->
                        Box(Modifier.weight(1f).height(4.dp).background(if (index <= page) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.outlineVariant, CircleShape))
                    }
                }
            }
            SetupIllustration(page, notifications, battery, tile, addTile)
            when (page) {
                0 -> {
                    IntroBlock("Your clipboard, in sync", "Copy on Windows and paste on Android. Send text back with one tap.")
                    DirectionRow(Icons.Outlined.LaptopWindows, "PC → phone", "Copy on Windows. Paste on your phone.")
                    DirectionRow(Icons.AutoMirrored.Outlined.Send, "Phone → PC", "Copy on Android, then tap Send to PC.")
                    Text("Works over the same Wi-Fi, hotspot, or local VPN.", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                1 -> {
                    IntroBlock("Pair your PC", "Use a temporary six-digit code. No account needed.")
                    SetupStep(1, "Same network", "Connect both devices to Wi-Fi or a hotspot.")
                    SetupStep(2, "Generate a code", "On Windows, choose Generate code to connect.")
                    SetupStep(3, "Enter it here", "Type the code in FerryClip on Android.")
                }
                2 -> {
                    IntroBlock("Stay connected", "Allow background activity so PC copies can arrive while your screen is off.")
                    PermissionRow(Icons.Outlined.NotificationsNone, "Notifications", "Connection status and one-tap Send", notifications,
                        if (notifications) "Allowed" else "Allow", requestNotifications)
                    HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
                    PermissionRow(Icons.Outlined.BatterySaver, "Background activity", "Helps sync recover while the screen is off", battery,
                        if (battery) "Allowed" else "Allow", requestBattery)
                    Text("Both are optional and can be changed later in Settings.", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                3 -> {
                    IntroBlock("Send from any app", "Add Send to PC to Quick Settings, then tap it after copying text.")
                    SetupStep(1, "Open Quick Settings", "Swipe down twice from the top of your screen.")
                    SetupStep(2, "Edit tiles", "Tap the pencil or Edit button.")
                    SetupStep(3, "Add Send to PC", "Drag the tile into your active shortcuts.")
                    SetupStep(4, "Finish", "Tap Done or Back to save.")
                    TileStatus(tile, addTile)
                    Text("The notification’s Send to PC action is another one-tap option.", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
        }
    }
}

@Composable
fun BrandHeader(subtitle: String) {
    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(10.dp)) {
        Surface(Modifier.size(44.dp), shape = MaterialTheme.shapes.medium, color = MaterialTheme.colorScheme.primaryContainer, contentColor = MaterialTheme.colorScheme.onPrimaryContainer) {
            Icon(Icons.Outlined.ContentCopy, null, Modifier.padding(10.dp))
        }
        Column(verticalArrangement = Arrangement.spacedBy(1.dp)) {
            Text("FerryClip", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
            Text(subtitle, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}

@Composable
private fun IntroBlock(title: String, description: String) {
    Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
        Text(title, style = MaterialTheme.typography.headlineSmall, fontWeight = FontWeight.Bold)
        Text(description, style = MaterialTheme.typography.bodyLarge, color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}

@Composable
private fun DirectionRow(icon: androidx.compose.ui.graphics.vector.ImageVector, title: String, detail: String) {
    Row(Modifier.fillMaxWidth().heightIn(min = 56.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(12.dp)) {
        Icon(icon, null, tint = MaterialTheme.colorScheme.primary)
        Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
            Text(title, style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.SemiBold)
            Text(detail, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}

@Composable
private fun SetupStep(number: Int, title: String, detail: String) {
    Row(Modifier.fillMaxWidth().heightIn(min = 48.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(12.dp)) {
        Surface(Modifier.size(28.dp), shape = CircleShape, color = MaterialTheme.colorScheme.primaryContainer, contentColor = MaterialTheme.colorScheme.onPrimaryContainer) {
            Box(contentAlignment = Alignment.Center) { Text(number.toString(), style = MaterialTheme.typography.labelMedium, fontWeight = FontWeight.Bold) }
        }
        Column(verticalArrangement = Arrangement.spacedBy(1.dp)) {
            Text(title, style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.SemiBold)
            Text(detail, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}

@Composable
private fun PermissionRow(
    icon: androidx.compose.ui.graphics.vector.ImageVector,
    title: String,
    detail: String,
    enabled: Boolean,
    action: String,
    onClick: () -> Unit,
) {
    Row(Modifier.fillMaxWidth().heightIn(min = 72.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(12.dp)) {
        Icon(icon, null, tint = if (enabled) MaterialTheme.colorScheme.tertiary else MaterialTheme.colorScheme.primary)
        Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
            Text(title, style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.SemiBold)
            Text(detail, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        if (enabled) Text(action, style = MaterialTheme.typography.labelLarge, color = MaterialTheme.colorScheme.tertiary)
        else OutlinedButton(onClick = onClick, contentPadding = PaddingValues(horizontal = 12.dp), modifier = Modifier.heightIn(min = 40.dp)) { Text(action) }
    }
}

@Composable
private fun TileStatus(added: Boolean, onAdd: () -> Unit) {
    Surface(color = MaterialTheme.colorScheme.surfaceVariant, shape = MaterialTheme.shapes.medium) {
        Row(Modifier.fillMaxWidth().heightIn(min = 56.dp).padding(horizontal = 14.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            Icon(if (added) Icons.Outlined.CheckCircle else Icons.Outlined.Add, null, tint = if (added) MaterialTheme.colorScheme.tertiary else MaterialTheme.colorScheme.primary)
            Text(if (added) "Send to PC is added" else "Quick Settings tile", Modifier.weight(1f), style = MaterialTheme.typography.titleSmall)
            if (!added) TextButton(onClick = onAdd) { Text("Add") }
        }
    }
}

@Composable
private fun SetupIllustration(page: Int, notifications: Boolean, battery: Boolean, tile: Boolean, onAddTile: () -> Unit) {
    Surface(shape = MaterialTheme.shapes.large, color = MaterialTheme.colorScheme.surfaceVariant, contentColor = MaterialTheme.colorScheme.onSurface) {
        Column(Modifier.fillMaxWidth().padding(18.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            when (page) {
                0 -> {
                    Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.SpaceEvenly) {
                        DeviceGlyph(Icons.Outlined.LaptopWindows, "Windows")
                        Icon(Icons.Outlined.SyncAlt, "Clipboard sync", Modifier.size(26.dp), tint = MaterialTheme.colorScheme.primary)
                        DeviceGlyph(Icons.Outlined.PhoneAndroid, "Android")
                    }
                    HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceEvenly) {
                        Text("COPY", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.primary)
                        Icon(Icons.AutoMirrored.Outlined.ArrowForward, null, Modifier.size(16.dp), tint = MaterialTheme.colorScheme.onSurfaceVariant)
                        Text("PASTE", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                }
                1 -> {
                    Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.SpaceBetween) {
                        Icon(Icons.Outlined.LaptopWindows, "Windows", Modifier.size(26.dp))
                        Text("PAIR CODE", style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        Icon(Icons.Outlined.PhoneAndroid, "Android", Modifier.size(26.dp))
                    }
                    Text("— — —   — — —", style = MaterialTheme.typography.headlineMedium, color = MaterialTheme.colorScheme.primary, fontWeight = FontWeight.Bold)
                    Text("Example only · use the live code from Windows", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                2 -> {
                    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                        Icon(Icons.Outlined.BatteryChargingFull, null, Modifier.size(32.dp), tint = MaterialTheme.colorScheme.primary)
                        Column {
                            Text("Screen off", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.SemiBold)
                            Text("Your connection stays ready", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                    }
                    HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
                    Text("${if (notifications) "✓" else "○"} Notifications     ${if (battery) "✓" else "○"} Background activity", style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                3 -> {
                    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                        Surface(Modifier.size(44.dp), shape = MaterialTheme.shapes.medium, color = MaterialTheme.colorScheme.primaryContainer, contentColor = MaterialTheme.colorScheme.onPrimaryContainer) {
                            Icon(Icons.AutoMirrored.Outlined.Send, null, Modifier.padding(10.dp))
                        }
                        Column {
                            Text("Send to PC", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.SemiBold)
                            Text(if (tile) "Ready in Quick Settings" else "Add once · send with one tap", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                        Spacer(Modifier.weight(1f))
                        if (!tile) IconButton(onClick = onAddTile) { Icon(Icons.Outlined.Add, "Add tile") }
                    }
                }
            }
        }
    }
}

@Composable
private fun DeviceGlyph(icon: androidx.compose.ui.graphics.vector.ImageVector, label: String) {
    Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(4.dp)) {
        Icon(icon, label, Modifier.size(38.dp))
        Text(label, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}

@Composable
fun TileGuide(addTile: () -> Unit) {
    Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
        Text("Add Send to PC", style = MaterialTheme.typography.titleMedium)
        SetupIllustration(3, notifications = false, battery = false, tile = false, onAddTile = addTile)
        listOf("Open Quick Settings", "Tap the pencil or Edit", "Add Send to PC", "Tap Done or Back").forEachIndexed { index, instruction -> SetupStep(index + 1, instruction, "") }
        Button(onClick = addTile, modifier = Modifier.fillMaxWidth().heightIn(min = 48.dp), shape = MaterialTheme.shapes.medium) { Text("Add Send to PC tile") }
    }
}
