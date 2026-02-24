using System;
using System.Collections.Generic;
using System.IO;

namespace ExtintorCrm.App.Infrastructure;

public static class AppDataPaths
{
    private const string AppFolderName = "StarFire";
    private const string DataFolderName = "data";
    private const string BackupFolderName = "backups";
    private static readonly object SyncRoot = new();
    private static bool _initialized;
    private static string _appRootDirectory = string.Empty;
    private static string _dataDirectory = string.Empty;
    private static string _backupDirectory = string.Empty;
    private static string _databasePath = string.Empty;
    private static string _settingsPath = string.Empty;

    public static string AppRootDirectory
    {
        get
        {
            EnsureInitialized();
            return _appRootDirectory;
        }
    }

    public static string DataDirectory
    {
        get
        {
            EnsureInitialized();
            return _dataDirectory;
        }
    }

    public static string DefaultBackupDirectory
    {
        get
        {
            EnsureInitialized();
            return _backupDirectory;
        }
    }

    public static string DatabasePath
    {
        get
        {
            EnsureInitialized();
            return _databasePath;
        }
    }

    public static string SettingsPath
    {
        get
        {
            EnsureInitialized();
            return _settingsPath;
        }
    }

    private static string LegacyDataDirectory => Path.Combine(AppContext.BaseDirectory, DataFolderName);

    public static void EnsureInitialized()
    {
        lock (SyncRoot)
        {
            if (_initialized)
            {
                return;
            }

            foreach (var candidateRoot in GetCandidateRoots())
            {
                if (!TryInitializeRoot(candidateRoot))
                {
                    continue;
                }

                _initialized = true;
                return;
            }

            throw new InvalidOperationException(
                "Não foi possível inicializar pasta de dados com permissão de escrita.");
        }
    }

    private static IEnumerable<string> GetCandidateRoots()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        if (!string.IsNullOrWhiteSpace(local))
        {
            yield return Path.Combine(local, AppFolderName);
        }

        if (!string.IsNullOrWhiteSpace(roaming))
        {
            yield return Path.Combine(roaming, AppFolderName);
        }

        yield return Path.Combine(AppContext.BaseDirectory, AppFolderName);
        yield return Path.Combine(AppContext.BaseDirectory, DataFolderName, "..", AppFolderName);
        yield return Path.Combine(Path.GetTempPath(), AppFolderName);
    }

    private static bool TryInitializeRoot(string candidateRoot)
    {
        try
        {
            var root = Path.GetFullPath(candidateRoot);
            var dataDir = Path.Combine(root, DataFolderName);
            var backupDir = Path.Combine(dataDir, BackupFolderName);
            var dbPath = Path.Combine(dataDir, "crm.db");
            var settingsPath = Path.Combine(dataDir, "appsettings.json");

            Directory.CreateDirectory(root);
            Directory.CreateDirectory(dataDir);
            Directory.CreateDirectory(backupDir);
            ValidateWriteAccess(dataDir);

            _appRootDirectory = root;
            _dataDirectory = dataDir;
            _backupDirectory = backupDir;
            _databasePath = dbPath;
            _settingsPath = settingsPath;

            MigrateLegacyDataIfNeeded();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ValidateWriteAccess(string dataDir)
    {
        var probeFile = Path.Combine(dataDir, ".write-test.tmp");
        File.WriteAllText(probeFile, DateTime.UtcNow.ToString("O"));
        File.Delete(probeFile);
    }

    private static void MigrateLegacyDataIfNeeded()
    {
        if (!Directory.Exists(LegacyDataDirectory))
        {
            return;
        }

        if (string.Equals(Path.GetFullPath(LegacyDataDirectory), Path.GetFullPath(_dataDirectory), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CopyIfDestinationMissing(Path.Combine(LegacyDataDirectory, "crm.db"), _databasePath);
        CopyIfDestinationMissing(Path.Combine(LegacyDataDirectory, "crm.db-wal"), $"{_databasePath}-wal");
        CopyIfDestinationMissing(Path.Combine(LegacyDataDirectory, "crm.db-shm"), $"{_databasePath}-shm");
        CopyIfDestinationMissing(Path.Combine(LegacyDataDirectory, "appsettings.json"), _settingsPath);

        var legacyBackupDir = Path.Combine(LegacyDataDirectory, BackupFolderName);
        if (!Directory.Exists(legacyBackupDir))
        {
            return;
        }

        foreach (var backupFile in Directory.GetFiles(legacyBackupDir, "*.zip"))
        {
            var fileName = Path.GetFileName(backupFile);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            var destination = Path.Combine(_backupDirectory, fileName);
            CopyIfDestinationMissing(backupFile, destination);
        }
    }

    private static void CopyIfDestinationMissing(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath) || File.Exists(destinationPath))
        {
            return;
        }

        var destinationDir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDir))
        {
            Directory.CreateDirectory(destinationDir);
        }

        File.Copy(sourcePath, destinationPath, overwrite: false);
    }
}
