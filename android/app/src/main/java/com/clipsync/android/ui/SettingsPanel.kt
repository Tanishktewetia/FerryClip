package com.clipsync.android.ui

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Image
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.BatterySaver
import androidx.compose.material.icons.outlined.ChevronRight
import androidx.compose.material.icons.outlined.GridView
import androidx.compose.material.icons.outlined.HelpOutline
import androidx.compose.material.icons.outlined.Info
import androidx.compose.material.icons.outlined.NotificationsNone
import androidx.compose.material.icons.outlined.RestartAlt
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.clipsync.android.R

@Composable
internal fun SettingsPanel(
    version: String,
    notifications: Boolean,
    battery: Boolean,
    tile: Boolean,
    replayOnConnect: Boolean,
    diagnosticsEnabled: Boolean,
    onNotifications: () -> Unit,
    onBattery: () -> Unit,
    onTile: () -> Unit,
    onRedo: () -> Unit,
    onHelp: () -> Unit,
    onAbout: () -> Unit,
    onReplayChanged: (Boolean) -> Unit,
    onCopyLogs: () -> Unit,
    onShareLogs: () -> Unit,
    onSaveLogs: () -> Unit,
) {
    val uriHandler = androidx.compose.ui.platform.LocalUriHandler.current
    Column(verticalArrangement = Arrangement.spacedBy(16.dp)) {
        Card(
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant),
            border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
            shape = MaterialTheme.shapes.large,
        ) {
            Row(Modifier.fillMaxWidth().padding(18.dp), verticalAlignment = Alignment.CenterVertically) {
                Surface(Modifier.size(52.dp), shape = CircleShape, color = MaterialTheme.colorScheme.surface) {
                    Image(painterResource(R.drawable.ferryclip_logo), "FerryClip", Modifier.padding(8.dp))
                }
                Spacer(Modifier.width(14.dp))
                Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(3.dp)) {
                    Text("Settings", style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.SemiBold)
                    Text("Local clipboard sharing · Android + Windows", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
        }

        SettingsCard(title = "Sync behavior", subtitle = "Choose what happens when a PC reconnects") {
            Row(Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 14.dp), verticalAlignment = Alignment.CenterVertically) {
                Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    Text("Apply latest text on reconnect", style = MaterialTheme.typography.titleSmall)
                    Text("Restore the newest clip when the connection returns.", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                Switch(checked = replayOnConnect, onCheckedChange = onReplayChanged)
            }
        }

        SettingsCard(title = "Background & shortcuts", subtitle = "One-time Android permissions for reliable syncing") {
            SettingsActionRow(Icons.Outlined.NotificationsNone, "Notifications", "${if (notifications) "Allowed" else "Needed"} · send and connection status", notifications, "Open", onNotifications)
            HorizontalDivider(Modifier.padding(horizontal = 14.dp), color = MaterialTheme.colorScheme.outlineVariant)
            SettingsActionRow(Icons.Outlined.BatterySaver, "Background activity", "${if (battery) "Unrestricted" else "Needs attention"} · reduce battery delays", battery, "Review", onBattery)
            HorizontalDivider(Modifier.padding(horizontal = 14.dp), color = MaterialTheme.colorScheme.outlineVariant)
            SettingsActionRow(Icons.Outlined.GridView, "Send to PC shortcut", "${if (tile) "Ready" else "Optional"} · Quick Settings tile", tile, "Set up", onTile)
        }

        SettingsCard(title = "Help & app", subtitle = "Quick access when you need it") {
            SettingsActionRow(Icons.Outlined.HelpOutline, "Help & troubleshooting", "Pairing, networks and clipboard behavior", true, "Open", onHelp)
            HorizontalDivider(Modifier.padding(horizontal = 14.dp), color = MaterialTheme.colorScheme.outlineVariant)
            SettingsActionRow(Icons.Outlined.Info, "About FerryClip", "Version $version · TLS 1.3 · text only", true, "Details", onAbout)
            HorizontalDivider(Modifier.padding(horizontal = 14.dp), color = MaterialTheme.colorScheme.outlineVariant)
            SettingsActionRow(Icons.Outlined.Info, "FerryClip website", "ferryclip.vercel.app · guides & downloads", true, "Open website", { uriHandler.openUri("https://ferryclip.vercel.app") })
            HorizontalDivider(Modifier.padding(horizontal = 14.dp), color = MaterialTheme.colorScheme.outlineVariant)
            SettingsActionRow(Icons.Outlined.RestartAlt, "Setup guide", "Replay the one-time setup walkthrough", true, "Replay", onRedo)
        }

        if (diagnosticsEnabled) DiagnosticsSection(onCopyLogs, onShareLogs, onSaveLogs)
        Text("FerryClip $version · Your clipboard stays on your local network", Modifier.align(Alignment.CenterHorizontally), style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}

@Composable
private fun SettingsCard(title: String, subtitle: String, content: @Composable ColumnScope.() -> Unit) {
    Card(
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
        shape = MaterialTheme.shapes.large,
    ) {
        Column {
            Column(Modifier.fillMaxWidth().padding(start = 16.dp, top = 16.dp, end = 16.dp, bottom = 10.dp), verticalArrangement = Arrangement.spacedBy(3.dp)) {
                Text(title, style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
                Text(subtitle, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
            content()
        }
    }
}

@Composable
private fun SettingsActionRow(
    icon: androidx.compose.ui.graphics.vector.ImageVector,
    title: String,
    detail: String,
    ready: Boolean,
    action: String,
    onClick: () -> Unit,
) {
    Row(
        Modifier.fillMaxWidth().clickable(onClick = onClick).padding(horizontal = 16.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Icon(icon, null, tint = MaterialTheme.colorScheme.primary)
        Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
            Text(title, style = MaterialTheme.typography.titleSmall)
            Text(detail, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        Icon(Icons.Outlined.ChevronRight, action, tint = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}