package io.github.ssicci.phoneedge.companion

import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.os.BatteryManager

/** A snapshot of the USB and power state, read from the sticky system broadcasts. */
data class UsbStatus(
    /** USB_STATE "connected": a USB host (PC) is talking to the phone over the data lines. */
    val connected: Boolean,
    val configured: Boolean,
    /** Enabled USB functions such as "adb", "mtp" (from USB_STATE extras). */
    val functions: List<String>,
    /** BatteryManager.EXTRA_PLUGGED: 0 = on battery. */
    val plugged: Int,
) {
    val isDataConnection: Boolean get() = connected
    val isPowered: Boolean get() = plugged != 0

    fun describe(): String =
        "connected=$connected configured=$configured plugged=${pluggedName(plugged)}" +
            " functions=${functions.joinToString(",").ifEmpty { "-" }}"

    companion object {
        const val ACTION_USB_STATE = "android.hardware.usb.action.USB_STATE"
        private val NON_FUNCTION_EXTRAS =
            setOf("connected", "configured", "host_connected", "unlocked", "data_unlocked")

        fun read(context: Context, usbIntent: Intent? = null): UsbStatus {
            val usb = usbIntent ?: context.registerReceiver(null, IntentFilter(ACTION_USB_STATE))
            val battery = context.registerReceiver(null, IntentFilter(Intent.ACTION_BATTERY_CHANGED))
            val extras = usb?.extras
            val functions = extras?.keySet()
                ?.filter { it !in NON_FUNCTION_EXTRAS && extras.getBoolean(it, false) }
                ?.sorted() ?: emptyList()
            return UsbStatus(
                connected = usb?.getBooleanExtra("connected", false) ?: false,
                configured = usb?.getBooleanExtra("configured", false) ?: false,
                functions = functions,
                plugged = battery?.getIntExtra(BatteryManager.EXTRA_PLUGGED, 0) ?: 0,
            )
        }

        fun pluggedName(p: Int): String = when (p) {
            0 -> "none"
            BatteryManager.BATTERY_PLUGGED_AC -> "ac"
            BatteryManager.BATTERY_PLUGGED_USB -> "usb"
            BatteryManager.BATTERY_PLUGGED_WIRELESS -> "wireless"
            BatteryManager.BATTERY_PLUGGED_DOCK -> "dock"
            else -> "other($p)"
        }
    }
}
