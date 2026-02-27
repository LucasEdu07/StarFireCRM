using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using ExtintorCrm.App.Infrastructure.Settings;
using ExtintorCrm.App.UseCases.Common;
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

        public async Task<OperationResult> TryCreateBackupAsync(string folderPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return OperationResult.Failure(
                    title: "Backup nao executado",
                    message: "A pasta de backup nao foi informada.",
                    code: "BACKUP_FOLDER_REQUIRED",
                    nextStep: "Defina a pasta em Configuracoes > Backup e tente novamente.");
            }

            try
            {
                var backupPath = await CreateBackupAsync(folderPath, cancellationToken);
                return OperationResult.Success(
                    title: "Backup executado",
                    message: "Backup concluido com sucesso.",
                    code: "BACKUP_CREATE_OK",
                    nextStep: "Confira o arquivo gerado na pasta de backup.",
                    details: new[] { $"Arquivo: {backupPath}" });
            }
            catch (Exception ex)
            {
                return OperationResult.Failure(
                    title: "Falha no backup",
                    message: "Nao foi possivel concluir o backup.",
                    code: "BACKUP_CREATE_ERROR",
                    nextStep: "Valide permissao da pasta e espaco em disco antes de tentar novamente.",
                    details: new[] { ex.Message });
            }
        }

        public Task<BackupRestoreResult> RestoreBackupAsync(string backupFilePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(backupFilePath) || !File.Exists(backupFilePath))
            {
                throw new InvalidOperationException("Arquivo de backup não encontrado.");
            }

            ValidateBackupArchive(backupFilePath, cancellationToken);

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

        public async Task<OperationResult> TryRestoreBackupAsync(string backupFilePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(backupFilePath) || !File.Exists(backupFilePath))
            {
                return OperationResult.Failure(
                    title: "Restauracao nao executada",
                    message: "Arquivo de backup nao encontrado.",
                    code: "BACKUP_FILE_NOT_FOUND",
                    nextStep: "Selecione um arquivo .zip valido e tente novamente.");
            }

            try
            {
                var restoreResult = await RestoreBackupAsync(backupFilePath, cancellationToken);
                if (!restoreResult.DatabaseRestored && !restoreResult.SettingsRestored && !restoreResult.DocumentsRestored)
                {
                    return OperationResult.Failure(
                        title: "Restauracao incompleta",
                        message: "Arquivo de backup invalido ou sem dados restauraveis.",
                        code: "BACKUP_RESTORE_EMPTY",
                        nextStep: "Escolha outro arquivo de backup e repita a operacao.");
                }

                var details = new List<string>();
                if (restoreResult.DatabaseRestored)
                {
                    details.Add("Banco restaurado.");
                }

                if (restoreResult.SettingsRestored)
                {
                    details.Add("Configuracoes restauradas.");
                }

                if (restoreResult.DocumentsRestored)
                {
                    details.Add("Documentos restaurados.");
                }

                return OperationResult.Success(
                    title: "Backup restaurado",
                    message: "Restauracao concluida com sucesso.",
                    code: "BACKUP_RESTORE_OK",
                    nextStep: "Confira clientes, pagamentos e anexos apos recarregar a tela.",
                    details: details);
            }
            catch (Exception ex)
            {
                return OperationResult.Failure(
                    title: "Falha na restauracao",
                    message: "Nao foi possivel restaurar o backup selecionado.",
                    code: "BACKUP_RESTORE_ERROR",
                    nextStep: "Valide o arquivo e tente novamente. Se o erro persistir, acione o suporte.",
                    details: new[] { ex.Message });
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

        private static void ValidateBackupArchive(string backupFilePath, CancellationToken cancellationToken)
        {
            using var archive = ZipFile.OpenRead(backupFilePath);
            if (archive.Entries.Count == 0)
            {
                throw new InvalidOperationException("Arquivo de backup vazio ou invalido.");
            }

            var hasExpectedContent = false;
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var normalized = NormalizeZipEntryName(entry.FullName);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                if (normalized.Contains("../", StringComparison.Ordinal) ||
                    normalized.Contains("..\\", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Arquivo de backup invalido: caminho inseguro encontrado.");
                }

                if (normalized.Equals("data/crm.db", StringComparison.OrdinalIgnoreCase) ||
                    normalized.Equals("data/appsettings.json", StringComparison.OrdinalIgnoreCase) ||
                    normalized.StartsWith("data/documents/", StringComparison.OrdinalIgnoreCase))
                {
                    hasExpectedContent = true;
                }
            }

            if (!hasExpectedContent)
            {
                throw new InvalidOperationException("Arquivo de backup invalido: conteudo esperado nao encontrado.");
            }
        }

        private static string NormalizeZipEntryName(string rawEntryName)
        {
            return rawEntryName
                .Replace('\\', '/')
                .TrimStart('/');
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
