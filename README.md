# 📂 MyDesktopOrganizer

**MyDesktopOrganizer** is a Windows desktop application developed in **WPF (.NET 8)** that allows you to organize your icons into "boxes" or containers, similar to Fences. Keep your desktop clean and tidy by grouping files, folders, and shortcuts.

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)
![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)

## 🚀 Features

*   **Organizer Boxes:** Create multiple containers to classify your files.
*   **Drag & Drop:** Drag files from the desktop to the boxes and vice versa.
*   **Navigation:** Support for folders within the boxes.
*   **Customization:** Change colors, opacity, icon size, and corner radius.
*   **Hide Icons:** Double-click on the desktop to hide/show everything.
*   **Persistence:** Your layout is saved and loaded automatically.

---

## 🛠️ Development and Debugging

To run the application in development mode and test changes quickly:

1.  Open a terminal (PowerShell or CMD) in the project folder.
2.  Run the following command:

```bash
dotnet run
```

This will compile and run the application. You can use `Ctrl + C` in the terminal to stop the process if needed.

---

## 📦 Create Installer (Distribution)

To generate a professional `.exe` installer that any user can use, we use **Inno Setup**. Follow these steps:

### Step 1: Publish the Application

Before creating the installer, you need to compile the application in *Release* mode and generate a single, self-contained executable file (without external dependencies).

Run this exact command in your terminal:

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

**Note:** This will generate the necessary files in `bin\Release\net8.0-windows\win-x64\publish\`.

### Step 2: Compile the Inno Setup Script

1. Ensure you have Inno Setup installed.
2. Locate the `installer.iss` file in the project root directory.
3. Double-click it to open it with the Inno Setup compiler.
4. Press the **Run** button (▶️) in the toolbar or the `F9` key.

### Step 3: Result

Once the process is complete:

* The generated installer will be automatically saved in the **`Output`** folder within the project directory.
* The file will be named `Installer_MyDesktopOrganizer.exe`.

---

## 📄 Key Structure

* `MainWindow.xaml`: Main logic for the boxes and desktop.
* `App.xaml`: Single instance control and Tray Icon.
* `installer.iss`: Configuration for generating the installer.
* `layout.json`: Local file where the position of your boxes is saved.