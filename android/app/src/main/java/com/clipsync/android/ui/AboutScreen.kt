package com.clipsync.android.ui

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.automirrored.outlined.OpenInNew
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalUriHandler
import androidx.compose.ui.unit.dp
import com.clipsync.android.BuildConfig
import androidx.compose.ui.text.font.FontFamily

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AboutScreen(onBack: () -> Unit, onHelp: () -> Unit, diagnosticsEnabled: Boolean) {
    var showingLicenses by rememberSaveable { mutableStateOf(false) }
    BackHandler { if (showingLicenses) showingLicenses = false else onBack() }
    if (showingLicenses) { OpenSourceLicensesScreen(onBack = { showingLicenses = false }); return }
    val links = LocalUriHandler.current
    Scaffold(topBar = { TopAppBar(title = { Text("About FerryClip") }, navigationIcon = {
        IconButton(onClick = onBack) { Icon(Icons.AutoMirrored.Outlined.ArrowBack, "Back to Settings") }
    }, colors = TopAppBarDefaults.topAppBarColors(containerColor = MaterialTheme.colorScheme.background)) }) { padding ->
        Column(Modifier.fillMaxSize().padding(padding).verticalScroll(rememberScrollState()).padding(24.dp), verticalArrangement = Arrangement.spacedBy(20.dp)) {
            BrandHeader("Your clipboard, between your devices")
            Text("Made for your PC and your phone", style = MaterialTheme.typography.headlineMedium)
            Text("FerryClip shares plain text over your local Wi-Fi. Windows copies arrive automatically on Android. To send phone text back, tap Send to PC in the notification or the Send to PC tile.", style = MaterialTheme.typography.bodyLarge)
            HorizontalDivider()
            Text("Private by design", style = MaterialTheme.typography.titleMedium)
            Text("Connections use TLS and saved certificate identities. Pair by comparing a code, choose one active PC, and pause whenever you want. Clipboard text is kept in memory, not saved as a history or written into app logs.")
            Text("Version ${BuildConfig.VERSION_NAME} · Text sync", style = MaterialTheme.typography.bodyMedium)
            Text("Project: Tanishktewetia / FerryClip", style = MaterialTheme.typography.bodyMedium)
            OutlinedButton(onClick = { showingLicenses = true }, modifier = Modifier.fillMaxWidth().heightIn(min = 48.dp), shape = MaterialTheme.shapes.medium) { Text("Open source licenses") }
            Button(onClick = onHelp, modifier = Modifier.fillMaxWidth().heightIn(min = 48.dp), shape = MaterialTheme.shapes.medium) { Text("Read the FerryClip guide") }
            OutlinedButton(onClick = { links.openUri(PROJECT_URL) }, modifier = Modifier.fillMaxWidth().heightIn(min = 48.dp), shape = MaterialTheme.shapes.medium) { Text("GitHub · Source & documentation"); Spacer(Modifier.width(8.dp)); Icon(Icons.AutoMirrored.Outlined.OpenInNew, "Opens externally") }
            OutlinedButton(onClick = { links.openUri("$PROJECT_URL/issues") }, modifier = Modifier.fillMaxWidth().heightIn(min = 48.dp), shape = MaterialTheme.shapes.medium) { Text("Report an issue on GitHub"); Spacer(Modifier.width(8.dp)); Icon(Icons.AutoMirrored.Outlined.OpenInNew, "Opens externally") }
            if (diagnosticsEnabled) Text("Need support? Settings → Diagnostics lets you share connection logs. Remove personal details before posting a public issue.", style = MaterialTheme.typography.bodySmall)
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun OpenSourceLicensesScreen(onBack: () -> Unit) {
    val context = LocalContext.current
    val uriHandler = LocalUriHandler.current
    val document = remember {
        context.assets.open("open-source-licenses.txt").bufferedReader().use { parseLicenseDocument(it.readText()) }
    }
    Scaffold(topBar = { TopAppBar(title = { Text("Open source licenses") }, navigationIcon = {
        IconButton(onClick = onBack) { Icon(Icons.AutoMirrored.Outlined.ArrowBack, "Back to About") }
    }, colors = TopAppBarDefaults.topAppBarColors(containerColor = MaterialTheme.colorScheme.background)) }) { padding ->
        LazyColumn(
            Modifier.fillMaxSize().padding(padding),
            contentPadding = PaddingValues(horizontal = 20.dp, vertical = 16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            item {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text(document.title, style = MaterialTheme.typography.headlineSmall)
                    document.introduction.forEach { Text(it, style = MaterialTheme.typography.bodyMedium) }
                    Text("Android dependencies · ${document.dependencies.size}", style = MaterialTheme.typography.titleLarge,
                        modifier = Modifier.padding(top = 8.dp))
                    Text("Package versions, license identifiers, copyright holders, and project sources.",
                        style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
            items(document.dependencies, key = { it.artifact }) { dependency ->
                Card(
                    shape = MaterialTheme.shapes.medium,
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceContainerLow),
                    border = androidx.compose.foundation.BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
                ) {
                    Column(Modifier.fillMaxWidth().padding(14.dp), verticalArrangement = Arrangement.spacedBy(5.dp)) {
                        Text(dependency.artifact, style = MaterialTheme.typography.titleSmall, fontFamily = FontFamily.Monospace)
                        Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                            Text("Version ${dependency.version}", style = MaterialTheme.typography.labelMedium,
                                color = MaterialTheme.colorScheme.primary)
                            Text(dependency.license, style = MaterialTheme.typography.labelMedium)
                        }
                        Text(dependency.copyrightHolder, style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                        TextButton(onClick = { runCatching { uriHandler.openUri(dependency.projectUrl) } },
                            contentPadding = PaddingValues(horizontal = 0.dp, vertical = 2.dp)) {
                            Text("Project information")
                            Spacer(Modifier.width(6.dp))
                            Icon(Icons.AutoMirrored.Outlined.OpenInNew, contentDescription = null)
                        }
                    }
                }
            }
            items(document.legalSections, key = { it.title }) { section ->
                Card(
                    shape = MaterialTheme.shapes.medium,
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceContainerLow),
                    border = androidx.compose.foundation.BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
                ) {
                    Column(Modifier.fillMaxWidth().padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                        Text(section.title, style = MaterialTheme.typography.titleMedium)
                        section.paragraphs.forEach { paragraph ->
                            Text(paragraph, style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                    }
                }
            }
        }
    }
}

private data class LicenseDependency(
    val artifact: String,
    val version: String,
    val license: String,
    val copyrightHolder: String,
    val projectUrl: String,
)

private data class LicenseSection(val title: String, val paragraphs: List<String>)

private data class LicenseDocument(
    val title: String,
    val introduction: List<String>,
    val dependencies: List<LicenseDependency>,
    val legalSections: List<LicenseSection>,
)

private fun parseLicenseDocument(raw: String): LicenseDocument {
    val lines = raw.lines()
    val tableStart = lines.indexOfFirst { it.trim().startsWith("| Artifact |") }
    var tableEnd = tableStart + 1
    if (tableStart >= 0) {
        while (tableEnd < lines.size && (lines[tableEnd].isBlank() || lines[tableEnd].trim().startsWith("|"))) tableEnd++
    }
    val tableLines = if (tableStart >= 0) lines.subList(tableStart + 1, tableEnd).filter { it.trim().startsWith("|") } else emptyList()
    val dependencies = tableLines.mapNotNull { line ->
        val fields = line.trim().removePrefix("|").removeSuffix("|").split('|').map(String::trim)
        if (fields.size < 5 || fields[0].equals("Artifact", true) || fields.all { it.matches(Regex("[-: ]+")) }) return@mapNotNull null
        LicenseDependency(fields[0].trim('`'), fields[1].trim('`'), fields[2].trim('`'), fields[3].trim('`'), fields[4])
    }
    val introEnd = if (tableStart >= 0) tableStart else lines.size
    val introduction = lines.take(introEnd).drop(1).joinToString("\n").split(Regex("\n\\s*\n"))
        .map(::cleanLicenseMarkup).filter(String::isNotBlank)

    val legalStart = tableEnd.coerceAtMost(lines.size)
    val headings = setOf("Windows", "MIT License (FerryClip)", "MIT License", "Apache License 2.0 (Android dependencies)",
        "Apache License", "END OF TERMS AND CONDITIONS", "APPENDIX: How to apply the Apache License to your work.")
    val sections = mutableListOf<LicenseSection>()
    var title = "Additional notices"
    var paragraph = mutableListOf<String>()
    var content = mutableListOf<String>()
    fun flushParagraph() {
        val value = cleanLicenseMarkup(paragraph.joinToString("\n"))
        if (value.isNotBlank()) content += value
        paragraph = mutableListOf()
    }
    fun flushSection() {
        flushParagraph()
        if (content.isNotEmpty()) sections += LicenseSection(title, content.toList())
        content = mutableListOf()
    }
    lines.drop(legalStart).forEach { line ->
        val trimmed = line.trim()
        when {
            trimmed.startsWith("```") -> flushParagraph()
            trimmed in headings -> { flushSection(); title = trimmed }
            trimmed.isEmpty() -> flushParagraph()
            else -> paragraph += line
        }
    }
    flushSection()
    return LicenseDocument(lines.firstOrNull()?.trim().orEmpty(), introduction, dependencies, sections)
}

private fun cleanLicenseMarkup(value: String): String = value
    .replace("```", "")
    .replace("`", "")
    .replace(Regex("\\*\\*(.*?)\\*\\*"), "$1")
    .trim()
