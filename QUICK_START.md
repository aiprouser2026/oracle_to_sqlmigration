# Quick Start Guide
## Oracle to SQL Server Migration Suite

Get started with the migration tool in 5 minutes!

---

## Step 1: Build the Project

```bash
cd /Users/ljadhav/Postman/OracleToSQLMigration
dotnet build
```

Expected output: `Build succeeded. 0 Warning(s) 0 Error(s)`

---

## Step 2: Verify Installation

```bash
cd OracleToSQLMigration/src/CLI/OracleToSQL.CLI
dotnet run -- help
```

You should see the help menu with all available commands.

---

## Step 3: Prepare Connection Strings

### Oracle Connection String Format:
```
Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=your_host)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=your_service)));User Id=your_user;Password=your_password;
```

Or simplified:
```
Data Source=your_host:1521/your_service;User Id=your_user;Password=your_password;
```

### SQL Server Connection String Format:
```
Server=your_server;Database=your_database;User Id=your_user;Password=your_password;TrustServerCertificate=True;
```

Or with Windows Authentication:
```
Server=your_server;Database=your_database;Integrated Security=True;TrustServerCertificate=True;
```

---

## Step 4: Test Your Connections

```bash
dotnet run -- test-connection \
  --oracle "Data Source=...;User Id=...;Password=..." \
  --sqlserver "Server=...;Database=...;User Id=...;Password=..."
```

Expected output:
```
Testing Oracle connection...
✓ Oracle connection successful
Testing SQL Server connection...
✓ SQL Server connection successful
```

---

## Step 5: Run Your First Migration

### Option A: Quick Full Migration (Fastest)

```bash
dotnet run -- migrate full \
  --oracle "your_oracle_connection_string" \
  --sqlserver "your_sqlserver_connection_string" \
  --parallel 4 \
  --verbose
```

### Option B: Step-by-Step Migration (Recommended for first time)

#### 1. Analyze Oracle Schema
```bash
dotnet run -- analyze \
  --oracle "your_oracle_connection_string" \
  --output schema.json \
  --verbose
```

This creates `schema.json` with all database metadata.

#### 2. Generate SQL Server Scripts
```bash
dotnet run -- convert \
  --input schema.json \
  --output ./migration_scripts
```

This creates scripts in `./migration_scripts/`:
- `01_pre_migration.sql` - Schema creation
- `02_create_tables.sql` - Table definitions
- `03_create_indexes.sql` - Index creation
- `04_create_constraints.sql` - Constraints and FKs
- `05_post_migration.sql` - Statistics update
- `warnings.txt` - Conversion warnings (if any)

#### 3. Review Generated Scripts (Important!)
```bash
# Open and review each script
cat ./migration_scripts/warnings.txt
# Review other scripts as needed
```

#### 4. Migrate Schema
```bash
dotnet run -- migrate schema \
  --oracle "your_oracle_connection_string" \
  --sqlserver "your_sqlserver_connection_string"
```

#### 5. Migrate Data
```bash
dotnet run -- migrate data \
  --oracle "your_oracle_connection_string" \
  --sqlserver "your_sqlserver_connection_string" \
  --parallel 4 \
  --batch 10000 \
  --verbose
```

**Parameters:**
- `--parallel 4` - Migrate 4 tables in parallel (adjust based on your system)
- `--batch 10000` - Process 10,000 rows per batch (adjust for memory)
- `--verbose` - Show detailed progress

#### 6. Validate Migration
```bash
dotnet run -- validate \
  --oracle "your_oracle_connection_string" \
  --sqlserver "your_sqlserver_connection_string"
```

Expected output:
```
✓ TABLE1: 1,000,000 rows
✓ TABLE2: 500,000 rows
✓ TABLE3: 250,000 rows
...
Validation complete!
Valid tables: 15 / 15
```

---

## Step 6: Verify Results

Connect to your SQL Server database and verify:

```sql
-- Check table count
SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE';

-- Check row counts
SELECT
    SCHEMA_NAME(schema_id) + '.' + name AS TableName,
    SUM(row_count) AS RowCount
FROM sys.dm_db_partition_stats ps
INNER JOIN sys.objects o ON ps.object_id = o.object_id
WHERE o.type = 'U' AND index_id < 2
GROUP BY schema_id, name
ORDER BY name;

-- Check indexes
SELECT
    OBJECT_NAME(object_id) AS TableName,
    name AS IndexName,
    type_desc
FROM sys.indexes
WHERE object_id IN (SELECT object_id FROM sys.objects WHERE type = 'U')
ORDER BY OBJECT_NAME(object_id), name;
```

---

## Common Issues & Solutions

### Issue: "Oracle connection failed"
**Solution:**
- Verify Oracle is running: `tnsping your_service_name`
- Check credentials
- Verify network connectivity
- Ensure Oracle client libraries are installed

### Issue: "SQL Server connection failed"
**Solution:**
- Verify SQL Server is running
- Check if TCP/IP protocol is enabled
- Verify firewall rules
- Use `TrustServerCertificate=True` for self-signed certificates

### Issue: "Out of memory during migration"
**Solution:**
- Reduce `--batch` size to 5000 or less
- Reduce `--parallel` to 2 or 1
- Migrate large tables separately

### Issue: "Foreign key violation during data migration"
**Solution:**
- The tool automatically orders tables by dependencies
- If issues persist, use `migrate schema` first, then `migrate data`
- Check for circular references in your schema

### Issue: "Slow migration performance"
**Solution:**
- Increase `--parallel` (e.g., 8 on powerful servers)
- Increase `--batch` size (e.g., 50000 for large tables)
- Use `--verbose` to identify bottlenecks
- Disable antivirus temporarily
- Use SSD storage

---

## Performance Tuning Tips

### For Small Databases (<10 GB)
```bash
dotnet run -- migrate full \
  --oracle "..." \
  --sqlserver "..." \
  --parallel 2 \
  --batch 10000
```

### For Medium Databases (10-500 GB)
```bash
dotnet run -- migrate full \
  --oracle "..." \
  --sqlserver "..." \
  --parallel 8 \
  --batch 50000
```

### For Large Databases (>500 GB)
```bash
# Migrate in batches or specific tables
# Consider migrating overnight or during off-peak hours
dotnet run -- migrate data \
  --oracle "..." \
  --sqlserver "..." \
  --parallel 16 \
  --batch 100000
```

---

## Example: Complete Migration Script

Create a file `migrate.sh` (Linux/Mac) or `migrate.bat` (Windows):

```bash
#!/bin/bash

# Configuration
ORACLE_CONN="Data Source=oracle_host:1521/ORCL;User Id=myuser;Password=mypass;"
SQLSERVER_CONN="Server=sql_host;Database=TargetDB;User Id=sa;Password=mypass;TrustServerCertificate=True;"

echo "Step 1: Testing connections..."
dotnet run -- test-connection --oracle "$ORACLE_CONN" --sqlserver "$SQLSERVER_CONN"

echo "Step 2: Analyzing Oracle schema..."
dotnet run -- analyze --oracle "$ORACLE_CONN" --output schema.json --verbose

echo "Step 3: Generating SQL scripts..."
dotnet run -- convert --input schema.json --output ./scripts

echo "Step 4: Migrating schema..."
dotnet run -- migrate schema --oracle "$ORACLE_CONN" --sqlserver "$SQLSERVER_CONN"

echo "Step 5: Migrating data..."
dotnet run -- migrate data --oracle "$ORACLE_CONN" --sqlserver "$SQLSERVER_CONN" --parallel 4 --batch 10000 --verbose

echo "Step 6: Validating migration..."
dotnet run -- validate --oracle "$ORACLE_CONN" --sqlserver "$SQLSERVER_CONN"

echo "Migration complete!"
```

Make it executable:
```bash
chmod +x migrate.sh
./migrate.sh
```

---

## Next Steps

1. ✅ **Complete your first migration** using this guide
2. 📚 **Read the full README.md** for advanced features
3. 📖 **Review IMPLEMENTATION_SUMMARY.md** for technical details
4. 🔧 **Customize** migration options for your needs
5. 🚀 **Scale up** to larger databases

---

## Getting Help

If you encounter issues:
1. Check the console output for error messages
2. Use `--verbose` flag for detailed logging
3. Review generated `warnings.txt` file
4. Check connection strings
5. Verify database permissions
6. Review the source code for customization options

---

## Success! 🎉

You now have a fully functional Oracle to SQL Server migration tool!

**Happy Migrating!**
