#!/bin/bash

echo "=================================="
echo "Oracle to SQL Server Migration GUI"
echo "=================================="
echo ""

cd "$(dirname "$0")/OracleToSQLMigration/src/GUI/OracleToSQL.GUI"

echo "Starting GUI application..."
echo ""

dotnet run

echo ""
echo "GUI closed."
