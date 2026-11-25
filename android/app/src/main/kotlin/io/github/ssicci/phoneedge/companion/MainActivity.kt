package io.github.ssicci.phoneedge.companion

import android.Manifest
import android.app.Activity
import android.content.ActivityNotFoundException
import android.content.Intent
import android.content.SharedPreferences
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.provider.Settings
import android.view.View
import android.view.WindowInsets
import android.widget.Button
import android.widget.Switch
import android.widget.TextView

class MainActivity : Activity() {

    private val handler = Handler(Looper.getMainLooper())
    private val ticker = object : Runnable {
        override fun run() {
            refresh()
            handler.postDelayed(this, 1000)
        }
    }
    private val prefsListener = SharedPreferences.OnSharedPreferenceChangeListener { _, key ->
        if (key == Prefs.KEY_LOG) refreshLog()
    }

    private lateinit var autoSwitch: Switch
    private lateinit var permissionText: TextView
    private lateinit var grantHelp: View
    private lateinit var grantCommand: TextView
    private lateinit var batteryText: TextView
    private lateinit var batteryButton: Button
    private lateinit var usbText: TextView
    private lateinit var adbText: TextView
    private lateinit var autoBlockerText: TextView
    private lateinit var logText: TextView

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)
        val root = findViewById<View>(R.id.root)
        root.setOnApplyWindowInsetsListener { v, insets ->
            val bars = insets.getInsets(WindowInsets.Type.systemBars() or WindowInsets.Type.displayCutout())
            v.setPadding(bars.left, bars.top, bars.right, bars.bottom)
            insets
        }

        autoSwitch = findViewById(R.id.auto_switch)
        permissionText = findViewById(R.id.permission_text)
        grantHelp = findViewById(R.id.grant_help)
        grantCommand = findViewById(R.id.grant_command)
        batteryText = findViewById(R.id.battery_text)
        batteryButton = findViewById(R.id.battery_button)
        usbText = findViewById(R.id.usb_text)
        adbText = findViewById(R.id.adb_text)
        autoBlockerText = findViewById(R.id.auto_blocker_text)
        logText = findViewById(R.id.log_text)

        grantCommand.text = getString(R.string.grant_command, packageName)
        autoSwitch.isChecked = Prefs.isAutoEnabled(this)
        autoSwitch.setOnCheckedChangeListener { _, on ->
            Prefs.setAutoEnabled(this, on)
            Prefs.log(this, getString(if (on) R.string.log_auto_on else R.string.log_auto_off))
            // Turning it back on applies the current connection state right away.
            CompanionService.start(this, reevaluate = on)
        }
        batteryButton.setOnClickListener { openBatterySettings() }

        if (checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED) {
            requestPermissions(arrayOf(Manifest.permission.POST_NOTIFICATIONS), 1)
        }
        CompanionService.start(this)
    }

    override fun onResume() {
        super.onResume()
        Prefs.of(this).registerOnSharedPreferenceChangeListener(prefsListener)
        handler.post(ticker)
        refreshLog()
    }

    override fun onPause() {
        Prefs.of(this).unregisterOnSharedPreferenceChangeListener(prefsListener)
        handler.removeCallbacks(ticker)
        super.onPause()
    }

    private fun refresh() {
        val granted = SecureToggle.hasPermission(this)
        permissionText.text = getString(if (granted) R.string.permission_ok else R.string.permission_missing)
        grantHelp.visibility = if (granted) View.GONE else View.VISIBLE

        val unrestricted = DeviceChecks.isBatteryUnrestricted(this)
        batteryText.text = getString(if (unrestricted) R.string.battery_ok else R.string.battery_restricted)
        batteryButton.visibility = if (unrestricted) View.GONE else View.VISIBLE

        val usb = UsbStatus.read(this)
        usbText.text = getString(
            when {
                usb.isDataConnection -> R.string.usb_data
                usb.isPowered -> R.string.usb_power_only
                else -> R.string.usb_none
            },
        )
        adbText.text = getString(
            when (SecureToggle.USB_DEBUGGING.read(this)) {
                true -> R.string.adb_on
                false -> R.string.adb_off
                null -> R.string.adb_unknown
            },
        )
        autoBlockerText.text = getString(
            when (DeviceChecks.autoBlocker(this)) {
                true -> R.string.auto_blocker_on
                false -> R.string.auto_blocker_off
                null -> R.string.auto_blocker_unknown
            },
        )
    }

    private fun refreshLog() {
        val lines = Prefs.logLines(this)
        logText.text = if (lines.isEmpty()) getString(R.string.log_empty) else lines.joinToString("\n")
    }

    private fun openBatterySettings() {
        val pkg = Uri.parse("package:$packageName")
        try {
            startActivity(Intent(Settings.ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS, pkg))
        } catch (_: ActivityNotFoundException) {
            startActivity(Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS, pkg))
        }
    }
}
