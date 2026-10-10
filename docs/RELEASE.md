# Release build (Android)

## 1. Before building
- csproj: set <ApplicationId> (e.g. com.yourcompany.myactivity), <ApplicationDisplayVersion> (1.0.0), <ApplicationVersion> (integer, +1 each release).
- AndroidManifest: android:allowBackup="false"; decide on USE_EXACT_ALARM (see PLATFORM_SETUP.md).
- Run the logic tests and docs/TESTING.md on a real device.

## 2. Signing key (once; back it up, losing it means you cannot update the app)
    keytool -genkey -v -keystore myactivity.keystore -alias myactivity -keyalg RSA -keysize 2048 -validity 10000

## 3. csproj release properties (add; do not commit the passwords)
    <PropertyGroup Condition="'$(Configuration)' == 'Release' and '$(TargetFramework)' == 'net10.0-android'">
      <AndroidKeyStore>true</AndroidKeyStore>
      <AndroidSigningKeyStore>myactivity.keystore</AndroidSigningKeyStore>
      <AndroidSigningKeyAlias>myactivity</AndroidSigningKeyAlias>
      <AndroidSigningKeyPass>env:ANDROID_KEY_PASS</AndroidSigningKeyPass>
      <AndroidSigningStorePass>env:ANDROID_STORE_PASS</AndroidSigningStorePass>
      <AndroidPackageFormat>apk</AndroidPackageFormat>   <!-- aab for Google Play -->
    </PropertyGroup>

## 4. Build
    dotnet publish -f net10.0-android -c Release
Output: bin/Release/net10.0-android/publish/*-Signed.apk  (or .aab)

## 5. After building: smoke test the RELEASE build
The release build uses the trimmer. If anything that works in Debug is missing in Release (empty lists, crashes
reading the database), the cause is trimming of reflection-based code (sqlite-net). Fix: add to the csproj
    <PropertyGroup Condition="'$(Configuration)' == 'Release'"><TrimMode>partial</TrimMode></PropertyGroup>
or set <PublishTrimmed>false</PublishTrimmed>, then re-test every screen.

## 6. Install on employees' phones
Send the signed APK (enable "install unknown apps" once) or use Play Store internal testing.
