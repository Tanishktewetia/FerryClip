plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
    id("org.jetbrains.kotlin.plugin.compose")
}

android {
    namespace = "com.clipsync.android"
    compileSdk = 34

    defaultConfig {
        applicationId = "com.clipsync.android"
        minSdk = 24
        targetSdk = 34
        versionCode = 17
        versionName = "0.8.11"

        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
        resourceConfigurations += "en"
    }

    buildTypes {
        debug {
            buildConfigField("boolean", "DIAGNOSTICS_ENABLED", "true")
        }
        release {
            buildConfigField("boolean", "DIAGNOSTICS_ENABLED", "false")
            isMinifyEnabled = true
            isShrinkResources = true
            signingConfig = signingConfigs.getByName("debug")
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


