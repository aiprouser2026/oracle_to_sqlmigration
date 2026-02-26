using System;
using System.Collections.Generic;

namespace OracleToSQL.SchemaConverter
{
    /// <summary>
    /// Maps Oracle data types to SQL Server data types
    /// </summary>
    public class DataTypeMapper
    {
        private static readonly Dictionary<string, Func<int?, int?, int?, string>> _typeMappings = new()
        {
            // Numeric types
            ["NUMBER"] = (length, precision, scale) =>
            {
                if (precision == null && scale == null)
                    return "DECIMAL(38, 10)"; // Default precision
                if (scale == null || scale == 0)
                {
                    if (precision <= 2) return "TINYINT";
                    if (precision <= 4) return "SMALLINT";
                    if (precision <= 9) return "INT";
                    if (precision <= 18) return "BIGINT";
                    return $"DECIMAL({precision}, 0)";
                }
                return $"DECIMAL({precision}, {scale})";
            },
            ["INTEGER"] = (_, _, _) => "INT",
            ["INT"] = (_, _, _) => "INT",
            ["SMALLINT"] = (_, _, _) => "SMALLINT",
            ["FLOAT"] = (_, precision, _) => precision != null ? $"FLOAT({precision})" : "FLOAT",
            ["DOUBLE PRECISION"] = (_, _, _) => "FLOAT(53)",
            ["REAL"] = (_, _, _) => "REAL",
            ["BINARY_FLOAT"] = (_, _, _) => "REAL",
            ["BINARY_DOUBLE"] = (_, _, _) => "FLOAT",

            // String types
            ["VARCHAR2"] = (length, _, _) => length != null ? $"NVARCHAR({length})" : "NVARCHAR(4000)",
            ["VARCHAR"] = (length, _, _) => length != null ? $"NVARCHAR({length})" : "NVARCHAR(4000)",
            ["CHAR"] = (length, _, _) => length != null ? $"NCHAR({length})" : "NCHAR(1)",
            ["NCHAR"] = (length, _, _) => length != null ? $"NCHAR({length})" : "NCHAR(1)",
            ["NVARCHAR2"] = (length, _, _) => length != null ? $"NVARCHAR({length})" : "NVARCHAR(4000)",
            ["LONG"] = (_, _, _) => "NVARCHAR(MAX)",

            // Large object types
            ["CLOB"] = (_, _, _) => "NVARCHAR(MAX)",
            ["NCLOB"] = (_, _, _) => "NVARCHAR(MAX)",
            ["BLOB"] = (_, _, _) => "VARBINARY(MAX)",
            ["LONG RAW"] = (_, _, _) => "VARBINARY(MAX)",
            ["RAW"] = (length, _, _) => length != null ? $"VARBINARY({length})" : "VARBINARY(2000)",

            // Date and time types
            ["DATE"] = (_, _, _) => "DATETIME2",
            ["TIMESTAMP"] = (_, precision, _) => precision != null ? $"DATETIME2({precision})" : "DATETIME2",
            ["TIMESTAMP WITH TIME ZONE"] = (_, precision, _) => precision != null ? $"DATETIMEOFFSET({precision})" : "DATETIMEOFFSET",
            ["TIMESTAMP WITH LOCAL TIME ZONE"] = (_, precision, _) => precision != null ? $"DATETIMEOFFSET({precision})" : "DATETIMEOFFSET",
            ["INTERVAL YEAR TO MONTH"] = (_, _, _) => "INT", // Store as months
            ["INTERVAL DAY TO SECOND"] = (_, _, _) => "BIGINT", // Store as milliseconds

            // Other types
            ["ROWID"] = (_, _, _) => "UNIQUEIDENTIFIER",
            ["UROWID"] = (_, _, _) => "UNIQUEIDENTIFIER",
            ["XMLTYPE"] = (_, _, _) => "XML",
            ["BFILE"] = (_, _, _) => "NVARCHAR(255)", // File path only
        };

        /// <summary>
        /// Converts an Oracle data type to SQL Server data type
        /// </summary>
        public string MapDataType(string oracleType, int? length = null, int? precision = null, int? scale = null)
        {
            if (string.IsNullOrEmpty(oracleType))
                return "NVARCHAR(MAX)";

            var upperType = oracleType.ToUpperInvariant();

            // Handle complex types
            if (upperType.Contains("TIMESTAMP"))
            {
                if (upperType.Contains("WITH TIME ZONE") || upperType.Contains("WITH LOCAL TIME ZONE"))
                    return _typeMappings["TIMESTAMP WITH TIME ZONE"](length, precision, scale);
                return _typeMappings["TIMESTAMP"](length, precision, scale);
            }

            if (upperType.Contains("INTERVAL"))
            {
                if (upperType.Contains("YEAR") || upperType.Contains("MONTH"))
                    return _typeMappings["INTERVAL YEAR TO MONTH"](length, precision, scale);
                return _typeMappings["INTERVAL DAY TO SECOND"](length, precision, scale);
            }

            // Try direct mapping
            if (_typeMappings.TryGetValue(upperType, out var mapper))
                return mapper(length, precision, scale);

            // Default fallback
            return "NVARCHAR(MAX)";
        }

        /// <summary>
        /// Gets conversion notes for specific type mappings
        /// </summary>
        public string GetConversionNotes(string oracleType)
        {
            var upperType = oracleType.ToUpperInvariant();

            return upperType switch
            {
                "NUMBER" => "Mapped to DECIMAL. Review precision and scale.",
                "CLOB" or "NCLOB" => "Mapped to NVARCHAR(MAX). Consider data size.",
                "BLOB" => "Mapped to VARBINARY(MAX). Consider data size.",
                "LONG" => "LONG is deprecated. Mapped to NVARCHAR(MAX).",
                "LONG RAW" => "LONG RAW is deprecated. Mapped to VARBINARY(MAX).",
                "INTERVAL YEAR TO MONTH" => "Stored as INT (total months). Application logic needed.",
                "INTERVAL DAY TO SECOND" => "Stored as BIGINT (total milliseconds). Application logic needed.",
                "BFILE" => "BFILE stores external file path only. File content not migrated.",
                "ROWID" or "UROWID" => "Mapped to UNIQUEIDENTIFIER. Original ROWID values will be lost.",
                _ when upperType.Contains("TIMESTAMP WITH TIME ZONE") => "Time zone information preserved in DATETIMEOFFSET.",
                _ => string.Empty
            };
        }

        /// <summary>
        /// Checks if a data type requires special handling during migration
        /// </summary>
        public bool RequiresSpecialHandling(string oracleType)
        {
            var upperType = oracleType.ToUpperInvariant();

            return upperType switch
            {
                "INTERVAL YEAR TO MONTH" or "INTERVAL DAY TO SECOND" => true,
                "BFILE" => true,
                "ROWID" or "UROWID" => true,
                "LONG" or "LONG RAW" => true,
                _ => false
            };
        }

        /// <summary>
        /// Gets all supported Oracle data types
        /// </summary>
        public IEnumerable<string> GetSupportedOracleTypes()
        {
            return _typeMappings.Keys;
        }
    }
}
