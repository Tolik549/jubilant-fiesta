using ClosedXML.Excel;
using Zayavka.Domain;
using Zayavka.Infrastructure.Excel;

namespace Zayavka.Tests;

public sealed class ExcelPassportStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly ExcelPassportStore _store = new();

    public ExcelPassportStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "zayavka_excel_" + Guid.NewGuid().ToString("N"));
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
    public void CanHandle_RecognizesXlsx()
    {
        Assert.True(_store.CanHandle("паспорт.xlsx"));
        Assert.True(_store.CanHandle("PASSPORT.XLSX"));
        Assert.False(_store.CanHandle("паспорт.csv"));
    }

    [Fact]
    public void CreateEmpty_WritesHeaderAndStartRow()
    {
        var file = Path.Combine(_dir, "паспорт.xlsx");

        _store.CreateEmpty(file, new PassportWorkEntry("03.11.2025", "Создание паспорта оборудования"));

        Assert.True(File.Exists(file));

        using var workbook = new XLWorkbook(file);
        var sheet = workbook.Worksheet("паспорт");

        Assert.Equal("дата", sheet.Cell(1, 1).GetString());
        Assert.Equal("наименование работ", sheet.Cell(1, 3).GetString());
        Assert.Equal("03.11.2025", sheet.Cell(2, 1).GetString());
        Assert.Equal("Создание паспорта оборудования", sheet.Cell(2, 3).GetString());
    }

    [Fact]
    public void AddWorkEntry_AddsRowAfterExisting()
    {
        var file = Path.Combine(_dir, "паспорт.xlsx");
        _store.CreateEmpty(file, new PassportWorkEntry("03.11.2025", "Создание паспорта оборудования"));

        _store.AddWorkEntry(file, new PassportWorkEntry("05.11.2025", "Замер вибрации"));

        using var workbook = new XLWorkbook(file);
        var sheet = workbook.Worksheet("паспорт");

        Assert.Equal("05.11.2025", sheet.Cell(3, 1).GetString());
        Assert.Equal("Замер вибрации", sheet.Cell(3, 3).GetString());
    }

    [Fact]
    public void AddWorkEntry_WhenFileMissing_CreatesWithHeader()
    {
        var file = Path.Combine(_dir, "новый.xlsx");

        _store.AddWorkEntry(file, new PassportWorkEntry("05.11.2025", "Первая работа"));

        Assert.True(File.Exists(file));

        var entries = _store.ReadWorkEntries(file);
        Assert.Single(entries);
        Assert.Equal("Первая работа", entries[0].WorkName);
    }

    [Fact]
    public void ReadWorkEntries_ReturnsAllRowsInOrder()
    {
        var file = Path.Combine(_dir, "паспорт.xlsx");
        _store.CreateEmpty(file, new PassportWorkEntry("03.11.2025", "Создание паспорта оборудования"));
        _store.AddWorkEntry(file, new PassportWorkEntry("05.11.2025", "Замер вибрации"));

        var entries = _store.ReadWorkEntries(file);

        Assert.Equal(2, entries.Count);
        Assert.Equal("03.11.2025", entries[0].Date);
        Assert.Equal("05.11.2025", entries[1].Date);
        Assert.Equal("Замер вибрации", entries[1].WorkName);
    }

    [Fact]
    public void AddWorkEntry_InsertsEarlierDateInChronologicalOrder()
    {
        var file = Path.Combine(_dir, "паспорт.xlsx");
        _store.CreateEmpty(file, new PassportWorkEntry("10.11.2025", "Работа 10"));
        _store.AddWorkEntry(file, new PassportWorkEntry("20.11.2025", "Работа 20"));

        // Более ранняя дата должна встать между первой и второй строками.
        _store.AddWorkEntry(file, new PassportWorkEntry("15.11.2025", "Работа 15"));

        var entries = _store.ReadWorkEntries(file);
        Assert.Equal(3, entries.Count);
        Assert.Equal("10.11.2025", entries[0].Date);
        Assert.Equal("15.11.2025", entries[1].Date);
        Assert.Equal("20.11.2025", entries[2].Date);
    }

    [Fact]
    public void AddWorkEntry_WritesStartEndAndRequestNumber()
    {
        var file = Path.Combine(_dir, "паспорт.xlsx");
        _store.CreateEmpty(file, new PassportWorkEntry("03.11.2025", "Создание"));

        _store.AddWorkEntry(file, new PassportWorkEntry
        {
            Date = "05.11.2025",
            WorkName = "ТО насоса",
            StartDate = "05.11.2025 08:00",
            EndDate = "07.11.2025 17:00",
            RequestNumber = "70",
        });

        var entries = _store.ReadWorkEntries(file);
        Assert.Equal("05.11.2025 08:00", entries[1].StartDate);
        Assert.Equal("07.11.2025 17:00", entries[1].EndDate);
        Assert.Equal("70", entries[1].RequestNumber);
    }

    [Fact]
    public void AddWorkEntry_MigratesOldHeader()
    {
        var file = Path.Combine(_dir, "старый.xlsx");
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("паспорт");
            sheet.Cell(1, 1).Value = "дата";
            sheet.Cell(1, 3).Value = "наименование работ";
            sheet.Cell(2, 1).Value = "01.11.2025";
            sheet.Cell(2, 3).Value = "Старая работа";
            workbook.SaveAs(file);
        }

        _store.AddWorkEntry(file, new PassportWorkEntry("05.11.2025", "Новая работа"));

        using var result = new XLWorkbook(file);
        var resultSheet = result.Worksheet("паспорт");
        Assert.Equal("Начало работ", resultSheet.Cell(1, 8).GetString());
        Assert.Equal("Окончание работ", resultSheet.Cell(1, 9).GetString());
        Assert.Equal("№ заявки", resultSheet.Cell(1, 10).GetString());

        // Старая строка на месте, новая добавлена ниже (01.11 < 05.11).
        Assert.Equal("Старая работа", resultSheet.Cell(2, 3).GetString());
        Assert.Equal("Новая работа", resultSheet.Cell(3, 3).GetString());
    }
}
