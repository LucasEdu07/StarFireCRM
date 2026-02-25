using System;
using System.IO;
using System.Linq;

namespace ExtintorCrm.App.Infrastructure.Documents
{
    public sealed class DocumentoStorageService
    {
        public StoredDocumentoArquivo StoreForPagamento(Guid pagamentoId, string sourceFilePath)
        {
            return StoreFile(
                sourceFilePath,
                Path.Combine("pagamentos", pagamentoId.ToString("N")));
        }

        public StoredDocumentoArquivo StoreForAlvara(Guid clienteId, string sourceFilePath)
        {
            return StoreFile(
                sourceFilePath,
                Path.Combine("clientes", clienteId.ToString("N"), "alvara"));
        }

        public string ResolveAbsolutePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new InvalidOperationException("Caminho relativo do anexo está vazio.");
            }

            var documentsRoot = AppDataPaths.DocumentsDirectory;
            var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(documentsRoot, normalized));
            var rootFullPath = Path.GetFullPath(documentsRoot);
            if (!fullPath.StartsWith(rootFullPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Caminho do anexo inválido.");
            }

            return fullPath;
        }

        public bool DeleteByRelativePath(string relativePath)
        {
            try
            {
                var absolutePath = ResolveAbsolutePath(relativePath);
                if (!File.Exists(absolutePath))
                {
                    return false;
                }

                File.Delete(absolutePath);
                TryDeleteEmptyParents(Path.GetDirectoryName(absolutePath), AppDataPaths.DocumentsDirectory);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static StoredDocumentoArquivo StoreFile(string sourceFilePath, string relativeFolder)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            {
                throw new InvalidOperationException("Arquivo de origem do anexo não encontrado.");
            }

            var documentsRoot = AppDataPaths.DocumentsDirectory;
            var folderRelativeNormalized = relativeFolder.Replace('/', Path.DirectorySeparatorChar);
            var folderAbsolute = Path.Combine(documentsRoot, folderRelativeNormalized);
            Directory.CreateDirectory(folderAbsolute);

            var fileInfo = new FileInfo(sourceFilePath);
            var originalName = string.IsNullOrWhiteSpace(fileInfo.Name)
                ? "documento"
                : fileInfo.Name;
            var sanitizedName = SanitizeFileName(originalName);
            var documentId = Guid.NewGuid();
            var storedName = $"{documentId:N}_{sanitizedName}";
            var destinationPath = Path.Combine(folderAbsolute, storedName);

            File.Copy(sourceFilePath, destinationPath, overwrite: false);

            var relativePath = Path.Combine(relativeFolder, storedName)
                .Replace('\\', '/');

            return new StoredDocumentoArquivo
            {
                DocumentoId = documentId,
                NomeOriginal = originalName,
                CaminhoRelativo = relativePath,
                TamanhoBytes = fileInfo.Length
            };
        }

        private static string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(fileName
                .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
                .ToArray())
                .Trim();

            return string.IsNullOrWhiteSpace(sanitized) ? "documento" : sanitized;
        }

        private static void TryDeleteEmptyParents(string? startDirectory, string stopAtDirectory)
        {
            if (string.IsNullOrWhiteSpace(startDirectory))
            {
                return;
            }

            var current = Path.GetFullPath(startDirectory);
            var stopAt = Path.GetFullPath(stopAtDirectory);
            while (current.StartsWith(stopAt, StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(current, stopAt, StringComparison.OrdinalIgnoreCase))
            {
                if (Directory.EnumerateFileSystemEntries(current).Any())
                {
                    return;
                }

                Directory.Delete(current);
                var parent = Path.GetDirectoryName(current);
                if (string.IsNullOrWhiteSpace(parent))
                {
                    return;
                }

                current = Path.GetFullPath(parent);
            }
        }
    }

    public sealed class StoredDocumentoArquivo
    {
        public Guid DocumentoId { get; set; }
        public string NomeOriginal { get; set; } = string.Empty;
        public string CaminhoRelativo { get; set; } = string.Empty;
        public long TamanhoBytes { get; set; }
    }
}
