# GUI Application - Oracle to SQL Server Migration Tool

## 🎨 Graphical User Interface

A modern, cross-platform desktop application for migrating Oracle databases to SQL Server.

---

## ✨ Features

### Visual Interface Includes:
- **Connection Testing** - Test Oracle and SQL Server connections with one click
- **Schema Analysis** - Analyze Oracle database structure visually
- **Migration Options** - Configure parallelism and batch sizes
- **Real-Time Progress** - Progress bars and status updates
- **Live Logging** - See migration progress in real-time
- **Validation** - Verify migration results after completion

---

## 🚀 How to Run the GUI

### Option 1: Run from Source (Development)

```bash
cd /Users/ljadhav/Postman/OracleToSQLMigration/OracleToSQLMigration/src/GUI/OracleToSQL.GUI
dotnet run
```

### Option 2: Build and Run Executable

```bash
# Build release version
cd /Users/ljadhav/Postman/OracleToSQLMigration
dotnet build --configuration Release

# Run the executable
cd OracleToSQLMigration/src/GUI/OracleToSQL.GUI/bin/Release/net7.0
./OracleToSQL.GUI
```

### Option 3: Publish as Self-Contained Application

```bash
# For macOS
dotnet publish -c Release -r osx-x64 --self-contained

# For Windows
dotnet publish -c Release -r win-x64 --self-contained

# For Linux
dotnet publish -c Release -r linux-x64 --self-contained
```

---

## 🖥️ GUI Layout

```
┌─────────────────────────────────────────────────────────────┐
│  Oracle to SQL Server Migration Tool                        │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  Oracle Connection                                           │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ Data Source=host:1521/ORCL;User Id=...;Password=...;  │ │
│  └────────────────────────────────────────────────────────┘ │
│  [ Test Oracle Connection ]                                  │
│                                                              │
│  SQL Server Connection                                       │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ Server=host;Database=db;User Id=...;Password=...;     │ │
│  └────────────────────────────────────────────────────────┘ │
│  [ Test SQL Server Connection ]                              │
│                                                              │
│  Migration Options                                           │
│  Parallel: [4]  Batch Size: [10000]  Type: [Full Migration]│
│                                                              │
│  [ 1. Analyze Schema ] [ 2. Start Migration ] [ 3. Validate]│
│                                                              │
│  Status: Ready                                               │
│  ████████████████░░░░░░░░  75% - Migrating CUSTOMERS       │
│                                                              │
│  ┌──────────────────── Log Output ────────────────────────┐ │
│  │ [12:34:56] Starting migration...                       │ │
│  │ [12:34:57] Analyzing schema...                         │ │
│  │ [12:34:58] Found 50 tables                             │ │
│  │ [12:35:10] Migrating data...                           │ │
│  │ [12:35:11] CUSTOMERS: 1,000,000 rows (45.2%)           │ │
│  │ [12:35:12] ORDERS: 500,000 rows (78.5%)                │ │
│  └────────────────────────────────────────────────────────┘ │
│                                                              │
│  Oracle: ✓ Connected | SQL Server: ✓ Connected              │
└─────────────────────────────────────────────────────────────┘
```

---

## 📖 How to Use the GUI

### Step 1: Start the Application
```bash
cd OracleToSQLMigration/src/GUI/OracleToSQL.GUI
dotnet run
```

A window will open with the migration interface.

### Step 2: Enter Connection Strings

**Oracle Connection:**
```
Data Source=oracle_host:1521/ORCL;User Id=myuser;Password=mypass;
```

**SQL Server Connection:**
```
Server=sql_host;Database=TargetDB;User Id=sa;Password=mypass;TrustServerCertificate=True;
```

### Step 3: Test Connections

1. Click "Test Oracle Connection" button
2. Click "Test SQL Server Connection" button
3. Verify both connections show "✓ Connected" in the status bar

### Step 4: Configure Migration Options

- **Parallel Tables**: How many tables to migrate simultaneously (1-16)
  - Recommended: 4-8 for most systems

- **Batch Size**: Rows to process per batch (1,000-100,000)
  - Recommended: 10,000 for mixed data, 50,000 for large tables

- **Migration Type**:
  - Schema Only - Just create tables and structures
  - Data Only - Migrate data (assumes schema exists)
  - Full Migration - Both schema and data (recommended)

### Step 5: Analyze Schema

1. Click "1. Analyze Schema" button
2. Wait for analysis to complete
3. Review the log output:
   - Number of tables
   - Total rows
   - Views, sequences, procedures, etc.

### Step 6: Start Migration

1. Click "2. Start Migration" button
2. Watch the real-time progress:
   - Progress bar shows overall completion
   - Log shows table-by-table progress
   - Status updates every few seconds

### Step 7: Validate Results

1. Click "3. Validate" button
2. Review validation results:
   - Row count comparison
   - Success/failure for each table

---

## 🎯 GUI Components Explained

### Main Components

| Component | Purpose | Class/File |
|-----------|---------|------------|
| **MainWindow.axaml** | UI Layout (XAML) | GUI design in XML |
| **MainWindow.axaml.cs** | Code-Behind | Event handlers and logic |
| **App.axaml** | Application setup | Avalonia app configuration |
| **Program.cs** | Entry point | Launches the GUI |

### Key Features in Code

**Connection Testing:**
```csharp
private async void TestOracleButton_Click(object? sender, RoutedEventArgs e)
{
    // Tests Oracle connection
    _oracleConnected = await _oracleManager.TestConnectionAsync(connectionString);
}
```

**Real-Time Progress:**
```csharp
var progress = new Progress<MigrationProgress>(p =>
{
    // Updates progress bar and status
    UpdateStatus($"{p.TableName}: {p.PercentComplete:F1}%", p.PercentComplete);
});
```

**Live Logging:**
```csharp
public void LogMessage(string message)
{
    // Adds timestamped messages to the log window
    LogTextBlock.Text += $"[{timestamp}] {message}\n";
}
```

---

## 🔧 Customization

### Changing Colors/Theme

Edit `App.axaml`:
```xml
<Application.Styles>
    <FluentTheme Mode="Dark" />  <!-- or "Light" -->
</Application.Styles>
```

### Adding New Features

1. **Add UI Element** in `MainWindow.axaml`
2. **Add Event Handler** in `MainWindow.axaml.cs`
3. **Wire up logic** using existing services

Example - Add "Export Schema" button:
```xml
<!-- In MainWindow.axaml -->
<Button Content="Export Schema"
        Click="ExportSchemaButton_Click"/>
```

```csharp
// In MainWindow.axaml.cs
private async void ExportSchemaButton_Click(object? sender, RoutedEventArgs e)
{
    var json = JsonSerializer.Serialize(_analyzedSchema);
    await File.WriteAllTextAsync("schema.json", json);
    LogMessage("Schema exported to schema.json");
}
```

---

## 🌐 Cross-Platform Support

### Tested Platforms:
- ✅ **macOS** (10.13+)
- ✅ **Windows** (7, 8, 10, 11)
- ✅ **Linux** (Ubuntu, Debian, Fedora, etc.)

### Platform-Specific Features:
- **Native look and feel** on each platform
- **Platform-appropriate** file dialogs
- **Native window controls**

---

## 🎨 Architecture

```
GUI Layer (Avalonia UI)
         ↓
    MainWindow
         ↓
    ┌────┴────┬────────┬──────────┐
    ↓         ↓        ↓          ↓
Services: SchemaAnalyzer SchemaConverter DataMigrator
         ↓         ↓        ↓          ↓
    Core Layer (Shared Logic)
         ↓         ↓        ↓          ↓
  Oracle.ManagedDataAccess  Microsoft.Data.SqlClient
```

The GUI is a **thin presentation layer** that uses all the existing migration logic from the CLI version!

---

## 💡 Tips & Tricks

1. **Large Databases**: Increase batch size to 50,000+ for faster migration
2. **Many Small Tables**: Increase parallel tables to 8-16
3. **Slow Network**: Decrease batch size to 5,000
4. **Monitor Progress**: Keep the GUI window visible to see real-time updates
5. **Cancel Anytime**: Click "Cancel" button to stop gracefully
6. **Review Logs**: Scroll through the log output to see detailed progress
7. **Connection Issues**: Test connections before analyzing or migrating

---

## 🐛 Troubleshooting

### GUI Doesn't Start
```bash
# Check .NET version
dotnet --version

# Should be 7.0 or higher
# If not, install .NET 7 SDK
```

### Build Errors
```bash
# Clean and rebuild
dotnet clean
dotnet build
```

### Window Doesn't Appear
```bash
# Run with verbose logging
dotnet run --verbosity detailed
```

---

## 📊 Comparison: CLI vs GUI

| Feature | CLI | GUI |
|---------|-----|-----|
| **Interface** | Command-line | Visual window |
| **Learning Curve** | Technical | Easy |
| **Automation** | Easy (scripts) | Not designed for it |
| **Real-time Visual Feedback** | Limited | Excellent |
| **Progress Bars** | No | Yes |
| **Connection Testing** | Manual | One-click |
| **Log Viewing** | Scrollback | Built-in viewer |
| **Best For** | Automation, servers | Interactive use |

---

## 🎁 What You Get

- ✅ **Modern UI** with Avalonia (cross-platform)
- ✅ **All CLI features** in visual form
- ✅ **Real-time progress** tracking
- ✅ **Connection testing** with one click
- ✅ **Live logging** window
- ✅ **Cancellation** support
- ✅ **Validation** built-in
- ✅ **Works on** Windows, macOS, and Linux

---

## 🚀 Quick Start

**3 commands to run the GUI:**

```bash
cd /Users/ljadhav/Postman/OracleToSQLMigration
dotnet build
cd OracleToSQLMigration/src/GUI/OracleToSQL.GUI && dotnet run
```

**That's it! The GUI window will open!**

---

*Enjoy the graphical migration experience!* 🎨
