plugins {
    id("com.android.application")
}

// The app version follows Twinput: <Version>X.Y.Z</Version> in the repository's Directory.Build.props.
val phoneEdgeVersion: String = run {
    val props = rootDir.resolveSibling("Directory.Build.props")
    val match = Regex("<Version>\\s*(\\d+\\.\\d+\\.\\d+)\\s*</Version>").find(props.readText())
        ?: throw GradleException("No <Version>X.Y.Z</Version> found in $props")
    match.groupValues[1]
}
val phoneEdgeVersionCode: Int = phoneEdgeVersion.split('.').map(String::toInt)
    .let { (x, y, z) ->
        require(y < 100 && z < 100) { "Version $phoneEdgeVersion: minor and patch must be below 100" }
        x * 10000 + y * 100 + z
    }

// Release signing comes only from environment variables (CI secrets, or set by hand on the
// maintainer's PC). Without them a release build stops with an error; debug builds are unaffected.
val signingEnvNames = listOf(
    "COMPANION_KEYSTORE_FILE",
    "COMPANION_KEYSTORE_PASSWORD",
    "COMPANION_KEY_ALIAS",
    "COMPANION_KEY_PASSWORD",
)
val signingEnv: Map<String, String> = signingEnvNames
    .mapNotNull { name -> System.getenv(name)?.takeIf { it.isNotBlank() }?.let { name to it } }
    .toMap()
val signingProblem: String? = when {
    signingEnv.size < signingEnvNames.size ->
        "Release signing needs these environment variables: " +
            signingEnvNames.filter { it !in signingEnv }.joinToString(", ")
    !file(signingEnv.getValue("COMPANION_KEYSTORE_FILE")).isFile ->
        "COMPANION_KEYSTORE_FILE does not point to a file"
    else -> null
}

android {
    namespace = "io.github.ssicci.phoneedge.companion"
    compileSdk = 37

    defaultConfig {
        applicationId = "io.github.ssicci.phoneedge.companion"
        // Android 12 (API 31): see docs/companion.md, "Build".
        minSdk = 31
        targetSdk = 37
        versionCode = phoneEdgeVersionCode
        versionName = phoneEdgeVersion
    }

    signingConfigs {
        create("release") {
            if (signingProblem == null) {
                storeFile = file(signingEnv.getValue("COMPANION_KEYSTORE_FILE"))
                storePassword = signingEnv.getValue("COMPANION_KEYSTORE_PASSWORD")
                keyAlias = signingEnv.getValue("COMPANION_KEY_ALIAS")
                keyPassword = signingEnv.getValue("COMPANION_KEY_PASSWORD")
            }
        }
    }

    buildTypes {
        release {
            isMinifyEnabled = true
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"))
            signingConfig = signingConfigs.getByName("release")
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

val checkReleaseSigning by tasks.registering {
    val problem = signingProblem
    doLast {
        if (problem != null) throw GradleException("$problem (see docs/companion.md, \"Release signing\").")
    }
}
tasks.configureEach {
    if (name == "preReleaseBuild") dependsOn(checkReleaseSigning)
}
