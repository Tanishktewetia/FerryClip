import java.util.Properties
import org.gradle.api.GradleException

plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
    id("org.jetbrains.kotlin.plugin.compose")
}

val releaseSigningPropertiesFile = rootProject.file("keystore.properties")
val releaseSigningProperties = Properties()
val releaseSigningRequested = gradle.startParameter.taskNames.any { requestedTask ->
    val taskName = requestedTask.substringAfterLast(':')
    taskName.contains("Release", ignoreCase = true) ||
        taskName.equals("build", ignoreCase = true) ||
        taskName.equals("assemble", ignoreCase = true)
}
var releaseSigningPropertiesLoadFailed = false
if (releaseSigningRequested && releaseSigningPropertiesFile.isFile) {
    try {
        releaseSigningPropertiesFile.inputStream().use(releaseSigningProperties::load)
    } catch (_: Exception) {
        releaseSigningPropertiesLoadFailed = true
    }
}
val releaseStoreFile = releaseSigningProperties.getProperty("storeFile")
val releaseStorePassword = releaseSigningProperties.getProperty("storePassword")
val releaseKeyAlias = releaseSigningProperties.getProperty("keyAlias")
val releaseKeyPassword = releaseSigningProperties.getProperty("keyPassword")
val releaseSigningConfigured = releaseSigningPropertiesFile.isFile &&
    !releaseSigningPropertiesLoadFailed &&
    listOf(releaseStoreFile, releaseStorePassword, releaseKeyAlias, releaseKeyPassword).all { !it.isNullOrBlank() }

val verifyReleaseSigningConfig = tasks.register("verifyReleaseSigningConfig") {
    doLast {
        if (!releaseSigningPropertiesFile.isFile) {
            throw GradleException("Release signing requires android/keystore.properties. Copy android/keystore.properties.example to android/keystore.properties and set the local signing values.")
        }
        if (releaseSigningPropertiesLoadFailed) {
            throw GradleException("android/keystore.properties could not be loaded. Check its properties-file format without sharing its contents.")
        }
        if (!releaseSigningConfigured) {
            throw GradleException("android/keystore.properties must define storeFile, storePassword, keyAlias, and keyPassword.")
        }
        if (!rootProject.file(releaseStoreFile!!).isFile) {
            throw GradleException("The release keystore configured by android/keystore.properties was not found.")
        }
    }
}
tasks.configureEach {
    if (name == "preReleaseBuild") {
        dependsOn(verifyReleaseSigningConfig)
    }
}

android {
    namespace = "com.clipsync.android"
    compileSdk = 34

    defaultConfig {
        applicationId = "com.clipsync.android"
        minSdk = 24
        targetSdk = 34
        versionCode = 22
        versionName = "1.0.0"

        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
        resourceConfigurations += "en"
    }

    signingConfigs {
        create("release") {
            if (releaseSigningConfigured) {
                storeFile = rootProject.file(releaseStoreFile!!)
                storePassword = releaseStorePassword
                keyAlias = releaseKeyAlias
                keyPassword = releaseKeyPassword
            }
        }
    }

    buildTypes {
        debug {
            buildConfigField("boolean", "DIAGNOSTICS_ENABLED", "true")
        }
        release {
            buildConfigField("boolean", "DIAGNOSTICS_ENABLED", "false")
            isMinifyEnabled = true
            isShrinkResources = true
            signingConfig = if (releaseSigningConfigured) signingConfigs.getByName("release") else signingConfigs.getByName("debug")
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro"
            )
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = "17"
    }

    testOptions { unitTests.isIncludeAndroidResources = true }

    buildFeatures {
        compose = true
        buildConfig = true
    }
}

dependencies {
    implementation(project(":core"))

    // Compose BOM
    val composeBom = platform("androidx.compose:compose-bom:2024.12.01")
    implementation(composeBom)
    androidTestImplementation(composeBom)

    // Compose
    implementation("androidx.compose.material3:material3")
    implementation("androidx.compose.material:material-icons-extended")
    implementation("androidx.compose.ui:ui")
    implementation("androidx.compose.ui:ui-tooling-preview")
    debugImplementation("androidx.compose.ui:ui-tooling")

    // AndroidX
    implementation("androidx.core:core-ktx:1.13.1")
    implementation("androidx.lifecycle:lifecycle-runtime-ktx:2.8.7")
    implementation("androidx.activity:activity-compose:1.9.3")
    implementation("androidx.fragment:fragment-ktx:1.8.5")
    implementation("androidx.biometric:biometric:1.1.0")
    implementation("androidx.datastore:datastore-preferences:1.1.1")
    implementation("androidx.work:work-runtime-ktx:2.9.1")

    // Testing
    testImplementation("junit:junit:4.13.2")
    testImplementation("org.robolectric:robolectric:4.14.1")
    androidTestImplementation("androidx.test.ext:junit:1.2.1")
}


