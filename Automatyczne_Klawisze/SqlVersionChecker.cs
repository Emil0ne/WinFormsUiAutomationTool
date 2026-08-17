using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient; // Jeśli w projekcie używasz System.Data.SqlClient, zmień using

namespace Automatyczne_Klawisze
{
    public class BazaWersjaInfo
    {
        public string NazwaBazy { get; set; }
        public string Wersja { get; set; }
        public bool CzyPoprawnaEnova { get; set; }
    }

    public static class SqlVersionChecker
    {
        private const string ZapytanieWersjiSql = @"
DECLARE @DBName NVARCHAR(258);
DECLARE @SQL NVARCHAR(MAX);

IF OBJECT_ID('tempdb..#EnovaVersions') IS NOT NULL 
    DROP TABLE #EnovaVersions;

CREATE TABLE #EnovaVersions (
    NazwaBazy NVARCHAR(128),
    WersjaEnova NVARCHAR(50)
);

DECLARE db_cursor CURSOR FOR 
SELECT name 
FROM sys.databases 
WHERE state_desc = 'ONLINE' 
  AND name NOT IN ('master', 'model', 'msdb', 'tempdb');

OPEN db_cursor;
FETCH NEXT FROM db_cursor INTO @DBName;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @SQL = N'
    USE ' + QUOTENAME(@DBName) + N';
    
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = ''dbo'' AND TABLE_NAME = ''SystemInfos'')
    BEGIN
        DECLARE @RawVer NVARCHAR(100);
        SELECT TOP 1 @RawVer = [Value] 
        FROM dbo.SystemInfos 
        WHERE [Ident] = 100 AND [Value] LIKE ''soneta:%'';

        IF @RawVer IS NOT NULL
        BEGIN
            DECLARE @CleanVer NVARCHAR(50) = REPLACE(@RawVer, ''soneta:'', '''');
            DECLARE @FormattedVer NVARCHAR(50);
            
            IF LEN(@CleanVer) >= 8
            BEGIN
                SET @FormattedVer = SUBSTRING(@CleanVer, 1, 4) + ''.'' + 
                                    CAST(CAST(SUBSTRING(@CleanVer, 5, 2) AS INT) AS NVARCHAR) + ''.'' + 
                                    CAST(CAST(SUBSTRING(@CleanVer, 7, 2) AS INT) AS NVARCHAR);
            END
            ELSE
            BEGIN
                SET @FormattedVer = @CleanVer;
            END

            INSERT INTO #EnovaVersions VALUES (' + QUOTENAME(@DBName, '''') + N', @FormattedVer);
        END
        ELSE
        BEGIN
            INSERT INTO #EnovaVersions VALUES (' + QUOTENAME(@DBName, '''') + N', ''Brak wpisu wersji enova365'');
        END
    END
    ELSE
    BEGIN
        INSERT INTO #EnovaVersions VALUES (' + QUOTENAME(@DBName, '''') + N', ''Baza inna niż enova365'');
    END';

    BEGIN TRY
        EXEC sp_executesql @SQL;
    END TRY
    BEGIN CATCH
        INSERT INTO #EnovaVersions VALUES (@DBName, 'Błąd dostępu / inna baza');
    END CATCH;

    FETCH NEXT FROM db_cursor INTO @DBName;
END;

CLOSE db_cursor;
DEALLOCATE db_cursor;

SELECT NazwaBazy, WersjaEnova FROM #EnovaVersions;";

        public static string ZbudujConnectionString(string serwer, string login, string haslo)
        {
            if (string.IsNullOrWhiteSpace(login))
                return $"Server={serwer};Database=master;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=15;";

            return $"Server={serwer};Database=master;User Id={login};Password={haslo};TrustServerCertificate=True;Connect Timeout=15;";
        }

        public static async Task<Dictionary<string, BazaWersjaInfo>> PobierzMapyWersjiAsync(string connString)
        {
            var wynik = new Dictionary<string, BazaWersjaInfo>(StringComparer.OrdinalIgnoreCase);

            using (var conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(ZapytanieWersjiSql, conn))
                {
                    cmd.CommandTimeout = 120;
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string db = reader.GetString(0);
                            string ver = reader.GetString(1);

                            bool isValid = !ver.StartsWith("Baza inna") &&
                                           !ver.StartsWith("Błąd") &&
                                           !ver.StartsWith("Brak wpisu");

                            wynik[db] = new BazaWersjaInfo
                            {
                                NazwaBazy = db,
                                Wersja = ver,
                                CzyPoprawnaEnova = isValid
                            };
                        }
                    }
                }
            }

            return wynik;
        }

        public static int PorownajWersje(string v1, string v2)
        {
            if (Version.TryParse(v1, out var ver1) && Version.TryParse(v2, out var ver2))
            {
                return ver1.CompareTo(ver2);
            }
            return string.Compare(v1, v2, StringComparison.OrdinalIgnoreCase);
        }
    }
}