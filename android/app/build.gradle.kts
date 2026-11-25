plugins {
    id("com.android.application")
}

android {
    namespace = "io.github.ssicci.phoneedge.companion"
    compileSdk = 37

    defaultConfig {
        applicationId = "io.github.ssicci.phoneedge.companion"
        // Android 12 (API 31): see docs/companion.md, "Build".
        minSdk = 31
        targetSdk = 37
        versionCode = 1
        versionName = "0.1.0"
    }

    buildTypes {
        release {
            isMinifyEnabled = true
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"))
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    buildFeatures {
        buildConfig = false
    }
}
