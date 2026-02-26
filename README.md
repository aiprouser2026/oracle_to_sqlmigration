# Oracle to SQL Server Migration Suite

A comprehensive .NET application for migrating Oracle databases to Microsoft SQL Server, supporting schema, data, stored procedures, functions, triggers, and more.

## Project Structure

```
OracleToSQLMigration/
├── src/
│   ├── Core/                          # Core models, interfaces, and utilities
│   ├── SchemaAnalyzer/               # Oracle schema analysis
│   ├── SchemaConverter/              # Oracle → SQL Server conversion
│   ├── DataMigrator/                 # Data transfer engine
│   ├── CodeConverter/                # PL/SQL → T-SQL conversion
│   ├── ValidationEngine/             # Migration validation
│   └── CLI/                          # Command-line interface
├── tests/
└── docs/
```

## Features

- ✅ **Complete Schema Migration**: Tables, indexes, constraints, foreign keys
- ✅ **Data Migration**: High-performance bulk data transfer with batch processing
- ✅ **Validation**: Pre and post-migration validation
- ✅ **Parallel Processing**: Multi-threaded migration for improved performance
- ✅ **CLI Interface**: Command-line tool for automation and scripting
- ✅ **GUI Interface**: Cross-platform desktop application (Avalonia UI) ⭐ NEW!
- ✅ **Real-time Progress**: Visual progress bars and live logging
- ✅ **Connection Testing**: One-click connection verification

## Core Models (Completed)

### Database Objects
- `DatabaseSchema` - Complete database schema representation
- `TableMetadata` - Table structure and metadata
- `ColumnMetadata` - Column definitions with type mappings
- `IndexMetadata` - Index definitions (B-tree, bitmap, function-based)
- `ConstraintMetadata` - Constraints (PK, FK, CHECK, UNIQUE)
- `ForeignKeyMetadata` - Foreign key relationships
- `ViewMetadata` - Views and materialized views
- `SequenceMetadata` - Oracle sequences
- `StoredProcedureMetadata` - Stored procedures
- `FunctionMetadata` - Functions
- `TriggerMetadata` - Triggers
- `PackageMetadata` - Oracle packages

### Interfaces
- `ISchemaAnalyzer` - Schema analysis interface
- `ISchemaConverter` - Schema conversion interface
- `IDataMigrator` - Data migration interface
- `IConnectionManager` - Connection management interface

## Requirements

- .NET 7.0 or higher
- Oracle.ManagedDataAccess.Core
- Microsoft.Data.SqlClient
- Serilog (for logging)

## Getting Started

### Build the Solution

```bash
cd OracleToSQLMigration
dotnet build
```

### Run Tests

```bash
dotnet test
```

## Usage

### Choose Your Interface

**Option 1: GUI (Graphical User Interface)** ⭐ NEW!
```bash
# Run the desktop application
cd OracleToSQLMigration
./run-gui.sh

# OR
cd OracleToSQLMigration/src/GUI/OracleToSQL.GUI
dotnet run
```

**Option 2: CLI (Command-Line Interface)**
```bash
# Build the solution
cd OracleToSQLMigration
dotnet build

# Run the CLI tool
cd OracleToSQLMigration/src/CLI/OracleToSQL.CLI
dotnet run -- help
```

### CLI Commands

```bash
# Analyze Oracle schema
dotnet run -- analyze --oracle "Data Source=...;User Id=...;Password=..." --output schema.json

# Generate conversion scripts
dotnet run -- convert --input schema.json --output ./scripts

# Migrate schema only
dotnet run -- migrate schema --oracle "..." --sqlserver "Server=...;Database=...;User Id=...;Password=..."

# Migrate data only
dotnet run -- migrate data --oracle "..." --sqlserver "..." --parallel 4 --batch 10000

# Full migration (schema + data)
dotnet run -- migrate full --oracle "..." --sqlserver "..." --parallel 4

# Validate migration
dotnet run -- validate --oracle "..." --sqlserver "..."

# Test connections
dotnet run -- test-connection --oracle "..." --sqlserver "..."
```

### Example Workflow

```bash
# 1. Test connections
dotnet run -- test-connection --oracle "..." --sqlserver "..."

# 2. Analyze Oracle schema
dotnet run -- analyze --oracle "..." --output schema.json

# 3. Generate SQL Server scripts
dotnet run -- convert --input schema.json --output ./migration_scripts

# 4. Review generated scripts in ./migration_scripts/

# 5. Execute full migration
dotnet run -- migrate full --oracle "..." --sqlserver "..." --parallel 4 --verbose

# 6. Validate results
dotnet run -- validate --oracle "..." --sqlserver "..."
```

## Data Type Mappings

| Oracle | SQL Server |
|--------|------------|
| NUMBER | DECIMAL/INT/BIGINT |
| VARCHAR2 | VARCHAR/NVARCHAR |
| CLOB | VARCHAR(MAX)/NVARCHAR(MAX) |
| BLOB | VARBINARY(MAX) |
| DATE | DATETIME2 |
| TIMESTAMP | DATETIME2 |
| RAW | VARBINARY |

## Development Status

- [x] Project structure and core models
- [x] Core interfaces
- [x] Connection managers (Oracle, SQL Server)
- [x] Oracle schema analyzer implementation
- [x] Schema converter implementation
- [x] Data migration engine with SqlBulkCopy
- [x] Validation engine
- [x] CLI application with all commands
- [ ] Code converter (PL/SQL → T-SQL) - Future enhancement
- [ ] GUI application (WPF/Avalonia) - Future enhancement
- [ ] Unit tests - Future enhancement
- [ ] Integration tests - Future enhancement
- [ ] Enhanced documentation - Future enhancement

## License

MIT License

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
