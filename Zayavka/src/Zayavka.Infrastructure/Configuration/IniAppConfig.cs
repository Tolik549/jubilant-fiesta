using System.Text;
using Zayavka.Domain.Abstractions;

namespace Zayavka.Infrastructure.Configuration;

/// <summary>
/// Конфигурация приложения на основе ini-файлов рядом с exe:
/// <list type="bullet">
/// <item><c>passport_config.ini</c> — путь к папке паспортов оборудования;</item>
/// <item><c>import_requests_config.ini</c> — путь к файлу журнала заявок.</item>
/// </list>
/// Первая непустая, не комментарная строка файла — значение.
/// </summary>
public sealed class IniAppConfig : IAppConfig
{
    private const string DefaultFolderName = "Паспорта оборудования";

    private readonly string _configPath;
    private readonly string _journalConfigPath;

    public IniAppConfig(string? configPath = null, string? journalConfigPath = null)
    {
        _configPath = configPath ?? Path.Combine(AppContext.BaseDirectory, "passport_config.ini");
        _journalConfigPath = journalConfigPath ?? Path.Combine(AppContext.BaseDirectory, "import_requests_config.ini");
    }

    /// <summary>Путь к файлу конфигурации паспортов (для тестов и диагностики).</summary>
    public string ConfigPath => _configPath;

    /// <summary>Путь к файлу конфигурации журнала заявок (для тестов и диагностики).</summary>
    public string JournalConfigPath => _journalConfigPath;

    public string PassportDirectory => ReadFirstValue(_configPath) ?? DefaultPassportDirectory;

    public string DefaultPassportDirectory =>
        Path.Combine(AppContext.BaseDirectory, DefaultFolderName);

    public void SetPassportDirectory(string path) =>
        WriteValue(_configPath, path,
            "; Путь к папке паспортов оборудования (первая строка — путь).");

    public string JournalPath => ReadFirstValue(_journalConfigPath) ?? string.Empty;

    public void SetJournalPath(string path) =>
        WriteValue(_journalConfigPath, path,
            "; Путь к файлу журнала заявок (первая строка — путь).");

    private static string? ReadFirstValue(string configPath)
    {
        try
        {
            if (File.Exists(configPath))
            {
                foreach (var raw in File.ReadAllLines(configPath))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
                    {
                        continue;
                    }

                    return line;
                }
            }
        }
        catch
        {
            // Повреждённый/недоступный конфиг — используем значение по умолчанию.
        }

        return null;
    }

    private static void WriteValue(string configPath, string value, string comment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var directory = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var content = comment + Environment.NewLine + value;
        File.WriteAllText(configPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}

