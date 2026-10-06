# Release signing (CS-236)

The store build is signed with one keystore that lives outside the repository. Its passwords exist only in the
environment of the shell that starts Unity: never in the repo, never in `ProjectSettings`, never in a chat.
Meta requires every later upload to carry the same signature as the first, so **losing this keystore or its
passwords means the app can never be updated** - back both up before the first upload.

Sources: Meta, "Meta Horizon Store manifest / release build" https://developers.meta.com/horizon/resources/publish-mobile-manifest/
and VRC.Quest.Packaging.2 (APK signature scheme v2) https://developers.meta.com/horizon/resources/publish-quest-req/;
Unity, `PlayerSettings.Android.useCustomKeystore` / `keystoreName` / `keystorePass` / `keyaliasName` / `keyaliasPass`.

## 1. Create the keystore (owner, once)

Run in PowerShell, in a folder **outside** the repository (for example `D:\Keys`). `keytool` ships with the JDK
inside the Unity install (`<Unity>\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe`) if it is
not on `PATH`. It asks for the passwords interactively, so they are never typed on a command line or saved in
shell history.

```powershell
keytool -genkeypair -v -keystore D:\Keys\cosmic-simulation-xr.keystore -storetype PKCS12 -alias cosmicsimulationxr -keyalg RSA -keysize 2048 -validity 10000
```

- It asks for a keystore password (twice), then name / organisation / country - answer with the publisher
  identity used on the store. A PKCS12 keystore uses one password for the store and the key, so
  `COSMIC_KEYSTORE_PASS` and `COSMIC_KEYALIAS_PASS` below are the same value.
- Copy the `.keystore` file to two other places (a password manager attachment and an offline drive), and store
  the password in the password manager.

## 2. Set the four variables (owner, each build session)

In a fresh PowerShell window. `Read-Host -AsSecureString` keeps the password off the screen and out of history.

```powershell
$env:COSMIC_KEYSTORE_PATH = 'D:\Keys\cosmic-simulation-xr.keystore'
$env:COSMIC_KEYALIAS      = 'cosmicsimulationxr'
$env:COSMIC_KEYSTORE_PASS = [System.Net.NetworkCredential]::new('', (Read-Host 'Keystore password' -AsSecureString)).Password
$env:COSMIC_KEYALIAS_PASS = $env:COSMIC_KEYSTORE_PASS
```

Then start Unity (or the Hub) **from that same window**, so the editor inherits the variables. An editor that was
already running does not see them; close it first. They vanish when the window closes, which is the point - do
not make them permanent with `setx`.

## 3. Build

**Cosmic Simulation → Quest 3 → Build Store APK** → `Builds/Quest3/CosmicSimulationXR-store.apk`
(batch: `-executeMethod GalaxyExplorer.Build.Quest3Build.BuildStoreApk`).

- Any of the four variables missing, or the keystore file not found: the build refuses with a `[GEBuild]` error
  naming what is missing, and produces nothing.
- The keystore settings are applied for that build only and put back afterwards; **Build APK** stays
  debug-signed and unchanged.
- A wrong password fails in the Gradle signing step with the keytool error; nothing is produced.
- Every upload needs a higher version code: bump `Quest3ProjectSetup.AppVersion` first.

## 4. Verify

```powershell
& "$env:ANDROID_HOME\build-tools\<version>\apksigner.bat" verify --verbose --print-certs Builds\Quest3\CosmicSimulationXR-store.apk
```

Expect `Verified using v2 scheme (APK Signature Scheme v2): true` and a certificate DN that is the one typed in
step 1, **not** `CN=Android Debug`.
