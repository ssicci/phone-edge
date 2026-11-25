package io.github.ssicci.phoneedge.companion

import android.content.Context
import android.os.PowerManager
import android.provider.Settings

/** Read-only checks shown on the main screen. */
object DeviceChecks {

    /** Samsung "Unrestricted" battery usage is the same as being on the battery-optimization allow list. */
    fun isBatteryUnrestricted(context: Context): Boolean =
        context.getSystemService(PowerManager::class.java)
            .isIgnoringBatteryOptimizations(context.packageName)

    /**
     * Samsung Auto Blocker ("rampart") main switch: true = on, false = off, null = unknown
     * (not a Samsung phone, or the key is not readable on this build).
     */
    fun autoBlocker(context: Context): Boolean? =
        try {
            Settings.Secure.getString(context.contentResolver, "rampart_main_switch_enabled")
                ?.toIntOrNull()?.let { it != 0 }
        } catch (_: SecurityException) {
            null
        }
}
