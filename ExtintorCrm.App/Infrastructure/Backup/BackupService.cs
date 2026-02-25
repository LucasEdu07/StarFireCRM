using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using ExtintorCrm.App.Infrastructure.Settings;
using Microsoft.Data.Sqlite;

namespace ExtintorCrm.App.Infrastructure.Backup
{
    public sealed class BackupService
    {
        public Task<string> CreateBackupAsync(string folderPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                throw new InvalidOperationException("A pasta de backup não foi informada.");
            }

            Directory.CreateDirectory(folderPath);

            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var backupFile = Path.Combine(folderPath, $"star-fire-backup-{timestamp}.zip");
            var dbPath = AppDbContext.GetDatabasePath();
            var settingsPath = AppSettingsService.GetFilePath();
            var documentsPath = AppDataPaths.DocumentsDirectory;
            var stagingDir = Path.Combine(Path.GetTempPath(), $"star-fire-backup-stage-{Guid.NewGuid():N}");
            Directory.CreateDirectory(stagingDir);

            try
            {
                StageIfExists(dbPath, Path.Combine(stagingDir, "crm.db"), cancellationToken);
                StageIfExists($"{dbPath}-wal", Path.Combine(stagingDir, "crm.db-wal"), cancellationToken);
                StageIfExists($"{dbPath}-shm", Path.Combine(stagingDir, "crm.db-shm"), cancellationToken);
                StageIfExists(settingsPath, Path.Combine(stagingDir, "appsettings.json"), cancellationToken);
                StageDirectoryIfExists(documentsPath, Path.Combine(stagingDir, "documents"), cancellationToken);

                if (File.Exists(backupFile))
                {
                    File.Delete(backupFile);
                }

                using var archive = ZipFile.Open(backupFile, ZipArchiveMode.Create);
                AddIfExists(archive, Path.Combine(stagingDir, "crm.db"), "data/crm.db", cancellationToken);
                AddIfExists(archive, Path.Combine(stagingDir, "crm.db-wal"), "data/crm.db-wal", cancellationToken);
                AddIfExists(archive, Path.Combine(stagingDir, "crm.db-shm"), "data/crm.db-shm", cancellationToken);
                AddIfExists(archive, Path.Combine(stagingDir, "appsettings.json"), "data/appsettings.json", cancellationToken);
                AddDirectoryIfExists(archive, Path.Combine(stagingDir, "documents"), "data/documents", cancellationToken);
            }
            finally
            {
                TryDeleteDirectory(stagingDir);
            }

            return Task.FromResult(backupFile);
        }

        public Task<BackupRestoreResult> RestoreBackupAsync(string backupFilePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(backupFilePath) || !File.Exists(backupFilePath))
            {
                throw new InvalidOperationException("Arquivo de backup não encontrado.");
            }

            var tempDir = Path.Combine(Path.GetTempPath(), $"star-fire-restore-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                ZipFile.ExtractToDirectory(backupFilePath, tempDir, true);
                var extractedDataDir = Path.Combine(tempDir, "data");

                var databaseRestored = RestoreDatabaseIfExists(
                    Path.Combine(extractedDataDir, "crm.db"),
                    cancellationToken);

                var settingsRestored = CopyIfExists(
                    Path.Combine(extractedDataDir, "appsettings.json"),
                    AppSettingsService.GetFilePath(),
                    cancellationToken);
                var documentsRestored = CopyDirectoryIfExists(
                    Path.Combine(extractedDataDir, "documents"),
                    AppDataPaths.DocumentsDirectory,
                    cancellationToken);

                return Task.FromResult(new BackupRestoreResult
                {
                    DatabaseRestored = databaseRestored,
                    SettingsRestored = settingsRestored,
                    DocumentsRestored = documentsRestored
                });
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }
        }

        private static void AddIfExists(ZipArchive archive, string sourcePath, string entryName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(sourcePath))
            {
                return;
            }

            archive.CreateEntryFromFile(sourcePath, entryName, CompressionLevel.Optimal);
        }

        private static bool CopyIfExists(string sourcePath, string destinationPath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(sourcePath))
            {
                return false;
            }

            var destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            File.Copy(sourcePath, destinationPath, true);
            return true;
        }

        private static bool CopyDirectoryIfExists(string sourceDirectory, string destinationDirectory, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(sourceDirectory))
            {
                return false;
            }

            Directory.CreateDirectory(destinationDirectory);
            foreach (var sourceFile in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = Path.GetRelativePath(sourceDirectory, sourceFile);
                var destinationFile = Path.Combine(destinationDirectory, relativePath);
                var destinationDir = Path.GetDirectoryName(destinationFile);
                if (!string.IsNullOrWhiteSpace(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                }

                File.Copy(sourceFile, destinationFile, true);
            }

            return true;
        }

        private static bool RestoreDatabaseIfExists(string sourceDbPath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(sourceDbPath))
            {
                return false;
            }

            var destinationDbPath = AppDbContext.GetDatabasePath();
            var destinationDir = Path.GetDirectoryName(destinationDbPath);
            if (!string.IsNullOrWhiteSpace(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            var sourceCsb = new SqliteConnectionStringBuilder
            {
                DataSource = sourceDbPath,
                Mode = SqliteOpenMode.ReadOnly
            };

            var destinationCsb = new SqliteConnectionStringBuilder
            {
                DataSource = destinationDbPath,
                Mode = SqliteOpenMode.ReadWriteCreate
            };

            using var sourceConnection = new SqliteConnection(sourceCsb.ConnectionString);
            using var destinationConnection = new SqliteConnection(destinationCsb.ConnectionString);

            sourceConnection.Open();
            destinationConnection.Open();

            using (var command = destinationConnection.CreateCommand())
            {
                command.CommandText = "PRAGMA busy_timeout = 5000; PRAGMA journal_mode=WAL;";
                command.ExecuteNonQuery();
            }

            sourceConnection.BackupDatabase(destinationConnection);
            return true;
        }

        private static void StageIfExists(string sourcePath, string stagingPath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(sourcePath))
            {
                return;
            }

            var stagingDir = Path.GetDirectoryName(stagingPath);
            if (!string.IsNullOrWhiteSpace(stagingDir))
            {
                Directory.CreateDirectory(stagingDir);
            }

            using var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var output = new FileStream(stagingPath, FileMode.Create, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
        }

        private static void StageDirectoryIfExists(string sourceDirectory, string stagingDirectory, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(sourceDirectory))
            {
                return;
            }

            foreach (var sourceFile in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = Path.GetRelativePath(sourceDirectory, sourceFile);
                var destinationPath = Path.Combine(stagingDirectory, relativePath);
                var destinationDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                }

                using var input = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                input.CopyTo(output);
            }
        }

        private static void AddDirectoryIfExists(ZipArchive archive, string sourceDirectory, string entryRoot, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(sourceDirectory))
            {
                return;
            }

            foreach (var sourceFile in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = Path.GetRelativePath(sourceDirectory, sourceFile)
                    .Replace('\\', '/');
                var entryName = $"{entryRoot.TrimEnd('/')}/{relativePath}";
                archive.CreateEntryFromFile(sourceFile, entryName, CompressionLevel.Optimal);
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
                // Ignora falha de limpeza de diretório temporário.
            }
        }
    }

    public sealed class BackupRestoreResult
    {
        public bool DatabaseRestored { get; set; }
        public bool SettingsRestored { get; set; }
        public bool DocumentsRestored { get; set; }
    }
}
