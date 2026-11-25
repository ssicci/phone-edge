package io.github.ssicci.phoneedge.companion

import android.content.Context
import android.os.SystemClock

/** Turns USB debugging on or off and records it in the in-app log. */
object UsbDebugging {
    /** Until this uptime, a change of adb_enabled is ours and not "changed by something else". */
    @Volatile
    var selfWriteUntil = 0L

    /** [sourceRes] names who asked, e.g. R.string.src_auto. Returns true on success. */
    fun set(context: Context, on: Boolean, sourceRes: Int): Boolean {
        val start = SystemClock.uptimeMillis()
        selfWriteUntil = start + 3000
        val error = SecureToggle.USB_DEBUGGING.write(context, on)
        val ms = SystemClock.uptimeMillis() - start
        val source = context.getString(sourceRes)
        val state = context.getString(if (on) R.string.state_on else R.string.state_off)
        if (error == null) {
            Prefs.log(context, context.getString(R.string.log_set_ok, source, state, ms))
        } else {
            Prefs.log(context, context.getString(R.string.log_set_failed, source, state, error))
        }
        return error == null
    }
}
