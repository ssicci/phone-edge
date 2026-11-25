package io.github.ssicci.phoneedge.companion

import android.Manifest
import android.content.Context
import android.content.pm.PackageManager
import android.provider.Settings

/**
 * A Settings.Global on/off switch this app can flip once it holds WRITE_SECURE_SETTINGS.
 * USB debugging is the only one today; "developer options" (development_settings_enabled)
 * can be added as another instance without touching the service logic.
 */
class SecureToggle(val key: String) {

    /** Current value, or null if it cannot be read. */
    fun read(context: Context): Boolean? =
        try {
            Settings.Global.getInt(context.contentResolver, key) != 0
        } catch (_: Settings.SettingNotFoundException) {
            null
        } catch (_: SecurityException) {
            null
        }

    /** Writes the value and reads it back. Returns null on success, or a short reason. */
    fun write(context: Context, on: Boolean): String? {
        if (!hasPermission(context)) return "WRITE_SECURE_SETTINGS not granted"
        return try {
            Settings.Global.putInt(context.contentResolver, key, if (on) 1 else 0)
            if (read(context) == on) null else "value did not stick (policy?)"
        } catch (e: SecurityException) {
            e.message ?: "SecurityException"
        } catch (e: IllegalArgumentException) {
            e.message ?: "IllegalArgumentException"
        }
    }

    companion object {
        val USB_DEBUGGING = SecureToggle(Settings.Global.ADB_ENABLED)

        fun hasPermission(context: Context): Boolean =
            context.checkSelfPermission(Manifest.permission.WRITE_SECURE_SETTINGS) ==
                PackageManager.PERMISSION_GRANTED
    }
}
