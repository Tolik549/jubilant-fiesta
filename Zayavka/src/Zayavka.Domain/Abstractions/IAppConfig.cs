namespace Zayavka.Domain.Abstractions;

/// <summary>
/// Конфигурация приложения. Путь к папке паспортов хранится в файле
/// <c>passport_config.ini</c> рядом с exe (первая строка — путь).
/// </summary>
public interface IAppConfig
{
    /// <summary>Текущая папка паспортов оборудования.</summary>
    string PassportDirectory { get; }

    /// <summary>Значение по умолчанию, если конфиг не задан.</summary>
    string DefaultPassportDirectory { get; }

    /// <summary>Сохраняет папку паспортов в конфиг.</summary>
    void SetPassportDirectory(string path);

    /// <summary>
    /// Путь к файлу журнала заявок (<c>import_requests_config.ini</c>).
    /// Пустая строка — журнал ещё не выбран.
    /// </summary>
    string JournalPath { get; }

    /// <summary>Сохраняет путь к журналу заявок в конфиг.</summary>
    void SetJournalPath(string path);
}
