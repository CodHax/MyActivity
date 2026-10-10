# My Activity - Steps 1 to 5 (complete offline employee app)

.NET 10 MAUI, C#, XAML, MVVM, SQLite, local notifications. No internet, no cloud, no API.

## Set up (once)
1. Install the .NET 10 SDK and the MAUI workload:  `dotnet workload install maui`
2. `dotnet new maui -n MyActivity`, delete the template's MainPage.xaml / MainPage.xaml.cs
3. Copy this folder's contents over the project. Keep the template's `Platforms/` and `Resources/Fonts/`.
4. Packages:
       dotnet add package CommunityToolkit.Mvvm
       dotnet add package sqlite-net-pcl
       dotnet add package SQLitePCLRaw.bundle_green
       dotnet add package Plugin.LocalNotification
5. Platform permissions: see docs/PLATFORM_SETUP.md
6. Run: `dotnet build -t:Run -f net10.0-android`

## Verified here vs. not verified
- VERIFIED (compiled and executed): calculators (EMI/SIP/SWP/Basic), password hashing, validators,
  money formatting, working-day maths -> `tests/MyActivity.LogicTests` (67 checks, run `dotnet run` inside it).
- VERIFIED (static): XML of every XAML/SVG file parses; no C# syntax errors; every page/view model is registered
  in DI; every XAML binding name exists on a view model.
- NOT verified (needs a phone/emulator): MAUI build, UI rendering, notifications, Shell navigation.
  Use docs/TESTING.md as the acceptance checklist.

## Docs
- docs/PLATFORM_SETUP.md  Android/iOS manifest settings
- docs/TESTING.md         manual test plan (offline, notifications, multi-user, security)
- docs/RELEASE.md         release build + signing
- docs/ARCHITECTURE.md    layers, database, notification design
