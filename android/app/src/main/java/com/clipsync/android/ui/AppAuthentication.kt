package com.clipsync.android.ui

import androidx.biometric.BiometricManager
import androidx.biometric.BiometricPrompt
import androidx.core.content.ContextCompat
import androidx.fragment.app.FragmentActivity

internal object AppAuthentication {
    private val authenticators = BiometricManager.Authenticators.BIOMETRIC_WEAK or BiometricManager.Authenticators.DEVICE_CREDENTIAL
    fun authenticate(activity: FragmentActivity, success: () -> Unit, failure: (String) -> Unit) {
        if (BiometricManager.from(activity).canAuthenticate(authenticators) != BiometricManager.BIOMETRIC_SUCCESS) {
            failure("Set up a screen-lock PIN or biometrics in Android Settings first.")
            return
        }
        val prompt = BiometricPrompt(activity, ContextCompat.getMainExecutor(activity), object : BiometricPrompt.AuthenticationCallback() {
            override fun onAuthenticationSucceeded(result: BiometricPrompt.AuthenticationResult) { success() }
            override fun onAuthenticationError(errorCode: Int, errString: CharSequence) { failure(errString.toString()) }
        })
        prompt.authenticate(BiometricPrompt.PromptInfo.Builder().setTitle("Unlock FerryClip")
            .setSubtitle("Use your fingerprint or phone screen lock")
            .setAllowedAuthenticators(authenticators).build())
    }
}
