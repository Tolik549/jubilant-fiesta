using Zayavka.Domain.Abstractions;
using Zayavka.Infrastructure;
using Zayavka.Infrastructure.Configuration;
using Zayavka.Infrastructure.Csv;
using Zayavka.Infrastructure.Excel;

namespace Zayavka.WinForms;

internal static class Program
{
    /// <summary>
    /// Точка входа приложения «Импорт заявок».
    /// </summary>
    [STAThread]
    private static void Main()
    {
        // Настройки приложения по умолчанию (визуальные стили, шрифт и т. п.).
        ApplicationConfiguration.Initialize();

        // Конфиг рядом с exe: passport_config.ini (папка паспортов)
        // и import_requests_config.ini (путь к журналу заявок).
        IAppConfig config = new IniAppConfig();

        // Хранилища паспортов: Excel — приоритет, CSV — резервный формат.
        var passportRepository = new PassportRepository(
            config,
            new IPassportStore[]
            {
                new ExcelPassportStore(),
                new CsvPassportStore(),
            });

        // Сервис журнала заявок (импорт/закрытие/продление).
        var journalService = new ExcelJournalService(config);

        Application.Run(new MainForm(passportRepository, journalService));
    }
}