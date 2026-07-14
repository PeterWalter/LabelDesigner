<!-- generated-by: gsd-doc-writer -->
# LabelDesigner — Getting Started

## Prerequisites

| Requirement | Version |
|---|---|
| Windows | 10 version 1903 (build 18362) or later; Windows 11 recommended |
| .NET SDK | **10.0.100** (pinned in `global.json`) |
| Visual Studio | **2026** (17.x) with the **Windows App SDK** workload |
| Target platform | x86 / x64 / ARM64 |

Install the Windows App SDK workload in Visual Studio Installer:

> **Visual Studio Installer → Modify → Workloads → Windows application development**

The .NET 10 SDK is bundled with Visual Studio 2026. You can also install it standalone from [dot.net](https://dot.net).

---

## Clone and restore

```bash
git clone https://github.com/PeterWalter/LabelDesigner.git
cd LabelDesigner
dotnet restore LabelDesigner.slnx
```

---

## First run

Open the solution in Visual Studio and press **F5**, or from the terminal:

```bash
dotnet build LabelDesigner.slnx
```

The WinUI 3 app uses MSIX packaging. The first build takes longer while the toolchain generates the app package. Subsequent builds are incremental.

> **Note:** Because this is a WinUI 3 / Windows App SDK project, `dotnet run` is not supported. Use Visual Studio or `dotnet build` followed by launching the packaged app.

---

## Common setup issues

**Wrong .NET SDK version**
`global.json` pins the SDK to `10.0.100`. If you have an older SDK installed, `dotnet build` will report a version mismatch. Install the .NET 10 SDK from [dot.net](https://dot.net).

**Missing Windows App SDK workload**
Build errors referencing `Microsoft.WindowsAppSDK` or WinUI types indicate the workload is not installed. Open Visual Studio Installer and add **Windows application development**.

**Win2D `Win2DWarnNoPlatform` warning in tests**
The test project sets `<Win2DWarnNoPlatform>true</Win2DWarnNoPlatform>` to suppress the Win2D platform warning in non-UI tests. This is expected and safe.

**Syncfusion license warning at startup**
A community / trial license key is registered in `App.xaml.cs`. If you replace it with your own key, update the string passed to `SyncfusionLicenseProvider.RegisterLicense(...)`.

**HiDPI / blank canvas on first launch**
If the canvas is blank on a HiDPI monitor, verify that `DpiService.PixelsPerMm` is initialized before any element is added. This is handled automatically in `DesignerViewModel` when the window handle becomes available.

---

## Next steps

- [DEVELOPMENT.md](DEVELOPMENT.md) — build commands, code style, PR process
- [TESTING.md](TESTING.md) — running and writing tests
- [ARCHITECTURE.md](ARCHITECTURE.md) — system design and component map
- [USER_GUIDE.md](USER_GUIDE.md) — end-user guide
