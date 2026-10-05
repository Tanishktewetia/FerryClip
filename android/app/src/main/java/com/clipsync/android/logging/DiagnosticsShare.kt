package com.clipsync.android.logging

/** The user explicitly chose to copy these metadata-only diagnostics; never mark that clip sensitive. */
data class DiagnosticClipboardPayload(val label: String, val text: String, val isSensitive: Boolean = false)

object DiagnosticsShare {
    fun clipboardPayload(logText: String) = DiagnosticClipboardPayload("FerryClip Diagnostics", logText)
}