# Platform setup

## Android - Platforms/Android/AndroidManifest.xml
Inside <manifest>:

    <uses-permission android:name="android.permission.POST_NOTIFICATIONS" />
    <uses-permission android:name="android.permission.VIBRATE" />
    <uses-permission android:name="android.permission.WAKE_LOCK" />
    <uses-permission android:name="android.permission.RECEIVE_BOOT_COMPLETED" />
    <uses-permission android:name="android.permission.SCHEDULE_EXACT_ALARM" android:maxSdkVersion="32" />
    <uses-permission android:name="android.permission.USE_EXACT_ALARM" />

On the <application> element (recommended for a private HR-type app):

    android:allowBackup="false"

This stops `adb backup` / cloud auto-backup from copying the local database off the device.

Notes
- RECEIVE_BOOT_COMPLETED lets the plugin restore reminders after a phone restart.
- USE_EXACT_ALARM needs no prompt but Google Play restricts it to alarm/calendar apps.
  For Play Store: remove USE_EXACT_ALARM and remove android:maxSdkVersion from SCHEDULE_EXACT_ALARM;
  the app then asks the user to allow exact alarms.
- Some phones (Xiaomi, Oppo, Vivo, Samsung "sleeping apps") kill background alarms. In their battery settings set
  My Activity to "No restrictions" / "Auto-start allowed". This is a phone setting, not something the app can force.

## iOS - Platforms/iOS/Info.plist

    <key>UIBackgroundModes</key>
    <array>
        <string>fetch</string>
        <string>remote-notification</string>
    </array>

iOS keeps at most 64 pending local notifications per app. Attendance uses up to ~28 (14 days x 2) and meetings are
scheduled only 60 days ahead; heavy users with many meetings may exceed 64 on iOS (Android has no such limit).
Reminders are rebuilt every time the app opens, so nothing is lost permanently.
