package io.github.ssicci.phoneedge.companion

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.content.pm.ServiceInfo
import android.database.ContentObserver
import android.os.Handler
import android.os.IBinder
import android.os.Looper
import android.os.SystemClock
import android.provider.Settings
import android.util.Log

/**
 * Foreground service that watches USB and turns USB debugging on for a USB data connection
 * and off when it goes away. The broadcasts it needs cannot be received by a manifest
 * receiver, so they are registered here while the service runs.
 */
class CompanionService : Service() {

    private val handler = Handler(Looper.getMainLooper())
    private val evaluateRunnable = Runnable { evaluate() }
    private var lastConnected: Boolean? = null

    private val receiver = object : BroadcastReceiver() {
        override fun onReceive(context: Context, intent: Intent) {
            val usbIntent = if (intent.action == UsbStatus.ACTION_USB_STATE) intent else null
            val status = UsbStatus.read(context, usbIntent)
            Log.i(TAG, "event ${intent.action?.substringAfterLast('.')} t=${SystemClock.elapsedRealtime()} ${status.describe()}")
            // Turning USB debugging on or off makes the phone re-enumerate on USB, which reports
            // "disconnected" for a moment while the cable stays in. So act on "connected" at once,
            // but wait a little before treating "disconnected" as real.
            val delay = when {
                status.isDataConnection -> 0L
                status.isPowered -> OFF_DELAY_STILL_POWERED_MS
                else -> OFF_DELAY_UNPLUGGED_MS
            }
            handler.removeCallbacks(evaluateRunnable)
            handler.postDelayed(evaluateRunnable, delay)
        }
    }

    private val adbObserver = object : ContentObserver(handler) {
        override fun onChange(selfChange: Boolean) {
            val ctx = this@CompanionService
            val on = SecureToggle.USB_DEBUGGING.read(ctx)
            Log.i(TAG, "adb_enabled=$on t=${SystemClock.elapsedRealtime()}")
            if (SystemClock.uptimeMillis() > UsbDebugging.selfWriteUntil && on != null) {
                val state = getString(if (on) R.string.state_on else R.string.state_off)
                Prefs.log(ctx, getString(R.string.log_changed_elsewhere, state))
            }
            updateNotification(UsbStatus.read(ctx))
        }
    }

    override fun onCreate() {
        super.onCreate()
        createChannel()
        startForeground(
            NOTIFICATION_ID,
            buildNotification(UsbStatus.read(this)),
            ServiceInfo.FOREGROUND_SERVICE_TYPE_SPECIAL_USE,
        )
        val filter = IntentFilter().apply {
            addAction(UsbStatus.ACTION_USB_STATE)
            addAction(Intent.ACTION_POWER_CONNECTED)
            addAction(Intent.ACTION_POWER_DISCONNECTED)
        }
        // System broadcasts still reach a not-exported receiver.
        registerReceiver(receiver, filter, RECEIVER_NOT_EXPORTED)
        contentResolver.registerContentObserver(
            Settings.Global.getUriFor(Settings.Global.ADB_ENABLED), false, adbObserver,
        )
        val prefs = Prefs.of(this)
        lastConnected = if (prefs.contains(KEY_LAST)) prefs.getBoolean(KEY_LAST, false) else null
        Prefs.log(this, getString(R.string.log_service_started))
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        if (intent?.action == ACTION_REEVALUATE) lastConnected = null
        handler.removeCallbacks(evaluateRunnable)
        handler.post(evaluateRunnable)
        return START_STICKY
    }

    override fun onDestroy() {
        unregisterReceiver(receiver)
        contentResolver.unregisterContentObserver(adbObserver)
        handler.removeCallbacksAndMessages(null)
        super.onDestroy()
    }

    override fun onBind(intent: Intent?): IBinder? = null

    /** Acts only when the connection state changes, so manual changes in between are left alone. */
    private fun evaluate() {
        val status = UsbStatus.read(this)
        updateNotification(status)
        val connected = status.isDataConnection
        if (connected == lastConnected) return
        lastConnected = connected
        Prefs.of(this).edit().putBoolean(KEY_LAST, connected).apply()
        val what = getString(if (connected) R.string.log_usb_connected else R.string.log_usb_disconnected)
        Prefs.log(this, "$what (${status.describe()})")
        if (!Prefs.isAutoEnabled(this)) return
        if (SecureToggle.USB_DEBUGGING.read(this) == connected) return
        UsbDebugging.set(this, connected, R.string.src_auto)
    }

    private fun createChannel() {
        val channel = NotificationChannel(
            CHANNEL_ID, getString(R.string.channel_name), NotificationManager.IMPORTANCE_LOW,
        )
        channel.setShowBadge(false)
        getSystemService(NotificationManager::class.java).createNotificationChannel(channel)
    }

    private fun updateNotification(status: UsbStatus) {
        getSystemService(NotificationManager::class.java).notify(NOTIFICATION_ID, buildNotification(status))
    }

    private fun buildNotification(status: UsbStatus): Notification {
        val usb = getString(
            when {
                status.isDataConnection -> R.string.usb_data
                status.isPowered -> R.string.usb_power_only
                else -> R.string.usb_none
            },
        )
        val adb = getString(
            when (SecureToggle.USB_DEBUGGING.read(this)) {
                true -> R.string.adb_on
                false -> R.string.adb_off
                null -> R.string.adb_unknown
            },
        )
        val text = when {
            !SecureToggle.hasPermission(this) -> getString(R.string.notif_no_permission)
            !Prefs.isAutoEnabled(this) -> "$usb · $adb · " + getString(R.string.notif_auto_off)
            else -> "$usb · $adb"
        }
        val open = PendingIntent.getActivity(
            this, 0, Intent(this, MainActivity::class.java), PendingIntent.FLAG_IMMUTABLE,
        )
        return Notification.Builder(this, CHANNEL_ID)
            .setSmallIcon(R.drawable.ic_usb)
            .setContentTitle(text)
            .setContentIntent(open)
            .setOngoing(true)
            .setForegroundServiceBehavior(Notification.FOREGROUND_SERVICE_IMMEDIATE)
            .build()
    }

    companion object {
        private const val TAG = "PhoneEdgeCompanion"
        private const val CHANNEL_ID = "status"
        private const val NOTIFICATION_ID = 1
        private const val KEY_LAST = "last_connected"
        const val ACTION_REEVALUATE = "io.github.ssicci.phoneedge.companion.REEVALUATE"

        /** Cable pulled (no power): short wait so a flaky contact does not flip the setting. */
        const val OFF_DELAY_UNPLUGGED_MS = 1_000L

        /** Still powered but no data: usually the re-enumeration after an adb change. */
        const val OFF_DELAY_STILL_POWERED_MS = 6_000L

        fun start(context: Context, reevaluate: Boolean = false) {
            val intent = Intent(context, CompanionService::class.java)
            if (reevaluate) intent.action = ACTION_REEVALUATE
            context.startForegroundService(intent)
        }
    }
}
