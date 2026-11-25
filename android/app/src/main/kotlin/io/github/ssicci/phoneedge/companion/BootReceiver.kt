package io.github.ssicci.phoneedge.companion

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent

/** Starts the service after boot and after the app is updated. */
class BootReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        when (intent.action) {
            Intent.ACTION_BOOT_COMPLETED, Intent.ACTION_MY_PACKAGE_REPLACED ->
                CompanionService.start(context)
        }
    }
}
