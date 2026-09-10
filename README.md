# YubiEnroller

**YubiEnroller** is a modern, standalone Windows desktop application designed to manage **YubiKey PIV** (Personal Identity Verification) credentials and enroll certificates against **Windows Active Directory Certificate Services (AD CS)**.

Built with **C# / .NET 8 (WPF)** and the official **Yubico.YubiKey SDK**, it packages into a **zero-dependency, single-file executable (`YubiEnroller.exe`)** that runs natively on client machines without requiring Python, .NET runtimes, or external drivers.

---

## Key Features

- **PIV Slot 9a (Authentication) Certificate Inspection**:
  - Automatically reads and displays enrolled X.509 certificate metadata:
    - **Subject Common Name (CN)** and **User Principal Name (UPN)**
    - **Issuing Certificate Authority**
    - **Validity Range** with a real-time status pill (e.g. `● Valid (354 days remaining)`)
    - **SHA-256 Thumbprint** and Serial Number
    - **Key Algorithm & Size** (e.g. RSA 2048-bit on-chip)
- **Windows CA Certificate Enrollment**:
  - Generates asymmetric key pairs **directly on the YubiKey PIV chip** (private key never leaves the hardware).
  - Builds standard PKCS#10 Certificate Signing Requests (CSR) with UPN Subject Alternative Names and Smart Card Logon EKUs.
  - Submits the CSR to your Windows Active Directory Certificate Authority using `certreq -submit` with selectable enterprise templates (`SmartcardLogon`, `SmartcardUser`, `User`, etc.).
  - Writes the issued certificate back to PIV Slot 9a.
- **PIN Management**:
  - Change PIV PIN with attempt counter protection to prevent key lockout.
  - Validates PIN lengths (6 to 8 characters) and confirmation matching.
- **Taskbar / Status Bar Telemetry**:
  - Displays hardware telemetry in real-time:
    - **Device Model** (e.g., `YubiKey 5 NFC`)
    - **Serial Number** (e.g., `SN: 19482012`)
    - **Firmware Version** (e.g., `FW: 5.4.3`)
    - **PIN Retries Remaining** (e.g., `PIN Retries: 3`)
    - **Windows CA Status**
- **Interactive Simulator / Demo Mode**:
  - Toggle between physical hardware detection and built-in simulator mode to test all UI states, PIN changes, and simulated CA issuances without needing physical tokens or domain connectivity.

---

## Standalone Executable (For End Users)

The application compiles into a single standalone `.exe` located at:
```
publish\YubiEnroller.exe
```

### Running the App
1. Copy `YubiEnroller.exe` to any Windows 10 or 11 workstation.
2. Double-click `YubiEnroller.exe` to launch. No installer or runtime setup is required.

---

## Project Structure

```
c:\apps\yubi-enroller\
├── publish\
│   └── YubiEnroller.exe            # Self-contained standalone binary
├── src\
│   └── YubiEnroller\
│       ├── App.xaml / App.xaml.cs   # Executive dark design system & converters
│       ├── MainWindow.xaml / .cs    # Main UI layout, certificate card & taskbar
│       ├── Models\
│       │   ├── DeviceTelemetry.cs   # Model, serial, firmware, retries
│       │   ├── CertificateModel.cs  # X.509 certificate parser
│       │   └── AppSettings.cs       # Persistent CA settings
│       ├── Services\
│       │   ├── IYubiKeyService.cs   # Hardware abstraction interface
│       │   ├── YubiKeyHardwareService.cs # Yubico.YubiKey SDK CCID/PIV implementation
│       │   ├── YubiKeySimulatorService.cs # In-memory simulator
│       │   ├── WindowsCaEnrollmentService.cs # certreq Windows CA integration
│       │   └── PivRsaSignatureGenerator.cs # On-token CSR signing
│       ├── ViewModels\
│       │   ├── MainViewModel.cs     # Main screen bindings
│       │   ├── EnrollViewModel.cs   # Enrollment workflow & stepper
│       │   └── ChangePinViewModel.cs# PIN change & validation
│       └── Views\
│           ├── EnrollDialog.xaml    # CA enrollment modal
│           ├── ChangePinDialog.xaml # Change PIN modal
│           ├── CertDetailsDialog.xaml # Full certificate viewer
│           └── SettingsDialog.xaml  # CA config & template preferences
└── tests\
    └── YubiEnroller.Tests\
        ├── SimulatorAndEnrollmentTests.cs # Automated xUnit tests
        └── UiCaptureTests.cs        # Headless screenshot rendering
```

---

## Building from Source

To build and run tests:
```powershell
# Restore and build solution
dotnet build

# Run all automated tests
dotnet test

# Publish self-contained single-file executable
dotnet publish src/YubiEnroller/YubiEnroller.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/
```
