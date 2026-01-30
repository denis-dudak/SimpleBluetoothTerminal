[![Codacy Badge](https://api.codacy.com/project/badge/Grade/a3d8a40d7133497caa11051eaac6f1a2)](https://www.codacy.com/manual/kai-morich/SimpleBluetoothTerminal?utm_source=github.com&amp;utm_medium=referral&amp;utm_content=kai-morich/SimpleBluetoothTerminal&amp;utm_campaign=Badge_Grade)

# SimpleBluetoothTerminalNet

This is a Xamarin Android .NET application that provides a line-oriented terminal / console for classic Bluetooth (2.x) devices implementing the Bluetooth Serial Port Profile (SPP).

For an overview on Android Bluetooth communication see 
[Android Bluetooth Overview](https://developer.android.com/guide/topics/connectivity/bluetooth).

This App implements RFCOMM connection to the well-known SPP UUID 00001101-0000-1000-8000-00805F9B34FB

## Technology Stack

- **Platform**: Xamarin Android with .NET 10
- **Language**: C#
- **Minimum Android Version**: Android 5.0 (API 21)
- **Target Android Version**: Android 14 (API 34)

## Project Structure

This is a refactored version of the original Java-based SimpleBluetoothTerminal, now using:
- Xamarin Android .NET SDK
- AndroidX libraries (AppCompat, Material, Fragment)
- Modern .NET project structure (.csproj instead of Gradle)

## Building

Prerequisites:
- .NET SDK 10.0 or later
- Android workload for .NET

Build the project:
```bash
dotnet build SimpleBluetoothTerminalNet.csproj
```

## Motivation

I got various requests asking for help with Android development or source code for my 
[Serial Bluetooth Terminal](https://play.google.com/store/apps/details?id=de.kai_morich.serial_bluetooth_terminal) app.
Here you find a simplified version of my app, refactored to Xamarin Android .NET.
