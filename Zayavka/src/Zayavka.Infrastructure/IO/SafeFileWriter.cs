namespace Zayavka.Infrastructure.IO;

/// <summary>
/// Безопасное сохранение файлов: сначала запись во временный файл на локальном
/// диске, затем копирование на целевой путь (в том числе сетевой).
/// Это повторяет логику исходного проекта и исключает цепочку
/// «создать .tmp → удалить оригинал → переименовать», которую файловый сервер
/// или антивирус может расценить как ransomware-активность.
/// </summary>
public static class SafeFileWriter
{
    /// <summary>
    /// Записывает файл «через временный»: <paramref name="writeToTemp"/> пишет
    /// данные в переданный временный путь, после чего тот копируется в
    /// <paramref name="targetPath"/>. С целевого файла снимается атрибут
    /// «только для чтения».
    /// </summary>
    public static void SaveViaTemp(string targetPath, Action<string> writeToTemp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(writeToTemp);

        var fullTarget = Path.GetFullPath(targetPath);
        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"zayavka_{Guid.NewGuid():N}{Path.GetExtension(fullTarget)}");

        try
        {
            writeToTemp(tempPath);

            var directory = Path.GetDirectoryName(fullTarget);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(tempPath, fullTarget, overwrite: true);

            var attributes = File.GetAttributes(fullTarget);
            if (attributes.HasFlag(FileAttributes.ReadOnly))
            {
                File.SetAttributes(fullTarget, attributes & ~FileAttributes.ReadOnly);
            }
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    /// <summary>Создаёт резервную копию файла рядом с оригиналом (суффикс .bak).</summary>
    public static string? TryBackup(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var backupPath = filePath + ".bak";
            File.Copy(filePath, backupPath, overwrite: true);
            return backupPath;
        }
        catch
        {
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Временный файл удалит ОС — игнорируем.
        }
    }
}
