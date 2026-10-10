package com.clipsync.android.ui

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.HelpOutline
import androidx.compose.material.icons.automirrored.outlined.OpenInNew
import androidx.compose.material.icons.outlined.BatterySaver
import androidx.compose.material.icons.outlined.ChevronRight
import androidx.compose.material.icons.outlined.GridView
import androidx.compose.material.icons.outlined.Info
import androidx.compose.material.icons.outlined.Public
import androidx.compose.material.icons.outlined.NotificationsNone
import androidx.compose.material.icons.outlined.RestartAlt
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp

@Composable
internal fun SettingsPanel(
    notifications: Boolean,
    battery: Boolean,
    tile: Boolean,
    diagnosticsEnabled: Boolean,
    onNotifications: () -> Unit,
    onBattery: () -> Unit,
    onTile: () -> Unit,
    onRedo: () -> Unit,
    onHelp: () -> Unit,
    onAbout: () -> Unit,
    onCopyLogs: () -> Unit,
    onShareLogs: () -> Unit,
    onSaveLogs: () -> Unit,
) {
    val uriHandler = androidx.compose.ui.platform.LocalUriHandler.current
    Column(verticalArrangement = Arrangement.spacedBy(16.dp)) {
        SettingsSection("BACKGROUND & SHORTCUTS") {
            SettingsActionRow(Icons.Outlined.NotificationsNone, "Notifications", if (notifications) "Allowed · connection status and Send" else "Needed for status and one-tap Send", notifications, onNotifications)
            HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
            SettingsActionRow(Icons.Outlined.BatterySaver, "Background activity", if (battery) "Unrestricted · helps sync while screen is off" else "Reduce battery limits for reliable sync", battery, onBattery)
            HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
            SettingsActionRow(Icons.Outlined.GridView, "Send to PC shortcut", if (tile) "Added · Quick Settings tile" else "Optional · Quick Settings tile", tile, onTile)
        }

        SettingsSection("HELP & APP") {
            SettingsActionRow(Icons.AutoMirrored.Outlined.HelpOutline, "Help & troubleshooting", "Pairing, networks and clipboard behavior", false, onHelp)
            HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
            SettingsActionRow(Icons.Outlined.Info, "About FerryClip", "TLS 1.3 · text only", false, onAbout)
            HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
            SettingsActionRow(Icons.Outlined.Public, "FerryClip website", "Downloads, release notes, and setup guides.", false,
                { uriHandler.openUri("https://ferryclip.app") }, external = true)
            HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
            SettingsActionRow(Icons.Outlined.RestartAlt, "Setup guide", "Replay the one-time setup walkthrough", false, onRedo)
        }

        if (diagnosticsEnabled) DiagnosticsSection(onCopyLogs, onShareLogs, onSaveLogs)

    }
}

@Composable
private fun SettingsSection(title: String, content: @Composable ColumnScope.() -> Unit) {
    Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
        Text(title, modifier = Modifier.padding(start = 4.dp, bottom = 2.dp), style = MaterialTheme.typography.labelMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant, fontWeight = FontWeight.SemiBold)
        Surface(
            shape = MaterialTheme.shapes.large,
            color = MaterialTheme.colorScheme.surfaceContainerLow,
            border = androidx.compose.foundation.BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
        ) { Column(content = content) }
    }
}

@Composable
private fun SettingsActionRow(
    icon: androidx.compose.ui.graphics.vector.ImageVector,
    title: String,
    detail: String,
    ready: Boolean,
    onClick: () -> Unit,
    external: Boolean = false,
) {
    Row(
        Modifier.fillMaxWidth().heightIn(min = 64.dp).clickable(onClick = onClick).padding(horizontal = 14.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(14.dp),
    ) {
        Icon(icon, null, Modifier.size(22.dp), tint = if (ready) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurfaceVariant)
        Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
            Text(title, style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Medium)
            Text(detail, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        Icon(if (external) Icons.AutoMirrored.Outlined.OpenInNew else Icons.Outlined.ChevronRight,
            if (external) "Opens in browser" else "Open", Modifier.size(20.dp), tint = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}
