using System.Text;
using Zayavka.Domain;
using Zayavka.Infrastructure.Csv;

namespace Zayavka.Tests;

public sealed class CsvPassportStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly CsvPassportStore _store = new();

    public CsvPassportStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "zayavka_csv_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // Игнорируем проблемы очистки временных файлов.
        }
    }

    [Fact]
    public void CanHandle_RecognizesCsv()
    {
        Assert.True(_store.CanHandle("паспорт.csv"));
        Assert.False(_store.CanHandle("паспорт.xlsx"));
    }

    [Fact]
    public void CreateAndAppend_Roundtrip_UsesWindows1251()
    {
        var file = Path.Combine(_dir, "паспорт.csv");

        _store.CreateEmpty(file, new PassportWorkEntry("03.11.2025", "Создание паспорта оборудования"));
        _store.AddWorkEntry(file, new PassportWorkEntry("05.11.2025", "Замер вибрации"));

        // Заголовок должен читаться в кодировке Windows-1251.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var header = File.ReadLines(file, Encoding.GetEncoding(1251)).First();
        Assert.StartsWith("Дата;Наработка;Наименование работ", header);

        var entries = _store.ReadWorkEntries(file);
        Assert.Equal(2, entries.Count);
        Assert.Equal("Замер вибрации", entries[1].WorkName);
    }

    [Fact]
    public void AddWorkEntry_InsertsChronologically_AndMigratesHeader()
    {
        var file = Path.Combine(_dir, "паспорт.csv");
        _store.CreateEmpty(file, new PassportWorkEntry("10.11.2025", "Работа 10"));
        _store.AddWorkEntry(file, new PassportWorkEntry("20.11.2025", "Работа 20"));
        _store.AddWorkEntry(file, new PassportWorkEntry("15.11.2025", "Работа 15"));

        var entries = _store.ReadWorkEntries(file);
        Assert.Equal(3, entries.Count);
        Assert.Equal("10.11.2025", entries[0].Date);
        Assert.Equal("15.11.2025", entries[1].Date);
        Assert.Equal("20.11.2025", entries[2].Date);

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var header = File.ReadLines(file, Encoding.GetEncoding(1251)).First();
        Assert.Contains("Начало работ", header);
        Assert.Contains("№ заявки", header);
    }
}
