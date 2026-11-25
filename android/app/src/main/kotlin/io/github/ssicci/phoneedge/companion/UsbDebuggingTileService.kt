package io.github.ssicci.phoneedge.companion

import android.app.PendingIntent
import android.content.Intent
import android.os.Build
import android.service.quicksettings.Tile
import android.service.quicksettings.TileService

/** Quick Settings tile: turn USB debugging on or off by hand. */
class UsbDebuggingTileService : TileService() {

    override fun onStartListening() {
        refresh()
    }

    override fun onClick() {
        if (!SecureToggle.hasPermission(this)) {
            openApp()
            return
        }
        val on = SecureToggle.USB_DEBUGGING.read(this) ?: false
        UsbDebugging.set(this, !on, R.string.src_tile)
        refresh()
    }

    private fun refresh() {
        val tile = qsTile ?: return
        val on = SecureToggle.USB_DEBUGGING.read(this)
        tile.state = when {
            !SecureToggle.hasPermission(this) || on == null -> Tile.STATE_UNAVAILABLE
            on -> Tile.STATE_ACTIVE
            else -> Tile.STATE_INACTIVE
        }
        tile.label = getString(R.string.tile_label)
        tile.subtitle = getString(if (on == true) R.string.state_on else R.string.state_off)
        tile.updateTile()
    }

    @Suppress("DEPRECATION")
    private fun openApp() {
        val intent = Intent(this, MainActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        if (Build.VERSION.SDK_INT >= 34) {
            startActivityAndCollapse(PendingIntent.getActivity(this, 0, intent, PendingIntent.FLAG_IMMUTABLE))
        } else {
            startActivityAndCollapse(intent)
        }
    }
}
