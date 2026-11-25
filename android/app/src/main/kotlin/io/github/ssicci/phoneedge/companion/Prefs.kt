package io.github.ssicci.phoneedge.companion

import android.content.Context
import android.content.SharedPreferences
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

/** Settings and the recent-event log, kept in one SharedPreferences file. */
object Prefs {
    private const val FILE = "companion"
    private const val KEY_AUTO = "auto_enabled"
    const val KEY_LOG = "event_log"
    private const val LOG_SIZE = 20
    private const val SEP = "\u001e"

    fun of(context: Context): SharedPreferences =
        context.applicationContext.getSharedPreferences(FILE, Context.MODE_PRIVATE)

    fun isAutoEnabled(context: Context): Boolean = of(context).getBoolean(KEY_AUTO, true)

    fun setAutoEnabled(context: Context, on: Boolean) {
        of(context).edit().putBoolean(KEY_AUTO, on).apply()
    }

    /** Adds one line to the in-app log (newest first, at most [LOG_SIZE] lines). */
    @Synchronized
    fun log(context: Context, message: String) {
        val stamp = SimpleDateFormat("MM-dd HH:mm:ss", Locale.ROOT).format(Date())
        val lines = (listOf("$stamp  $message") + logLines(context)).take(LOG_SIZE)
        of(context).edit().putString(KEY_LOG, lines.joinToString(SEP)).apply()
        android.util.Log.i("PhoneEdgeCompanion", message)
    }

    fun logLines(context: Context): List<String> =
        of(context).getString(KEY_LOG, null)?.split(SEP)?.filter { it.isNotEmpty() } ?: emptyList()
}
