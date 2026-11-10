using ClosedXML.Excel;
using Zayavka.Domain;
using Zayavka.Infrastructure.Configuration;
using Zayavka.Infrastructure.Excel;

namespace Zayavka.Tests;

public sealed class ExcelJournalServiceTests : IDisposable
{
    private readonly string _root;
    private readonly IniAppConfig _config;

    public ExcelJournalServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "zayavka_journal_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        _config = new IniAppConfig(
            Path.Combine(_root, "passport_config.ini"),
            Path.Combine(_root, "import_requests_config.ini"));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Игнорируем проблемы очистки временных файлов.
        }
    }

    private ExcelJournalService CreateService() =>
        new(_config, () => new DateTime(2025, 11, 3, 10, 30, 0));

    private string CreateJournal(params (int Number, string Workshop, string Volume, string Permission, string Comment)[] rows)
    {
        var path = Path.Combine(_root, "Журнал.xlsx");

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Заявки");
        sheet.Cell(1, 1).Value = "Номер заявки";
        sheet.Cell(1, 4).Value = "Цех";
        sheet.Cell(1, 5).Value = "Префикс";
        sheet.Cell(1, 6).Value = "Наименование";
        sheet.Cell(1, 7).Value = "Номер";
        sheet.Cell(1, 11).Value = "Объем работ";
        sheet.Cell(1, 14).Value = "Дата окончания";
        sheet.Cell(1, 23).Value = "Разрешение";
        sheet.Cell(1, 24).Value = "Комментарий";

        var row = 2;
        foreach (var item in rows)
        {
            sheet.Cell(row, 1).Value = item.Number.ToString();
            sheet.Cell(row, 2).Value = "01.01.2025";
            sheet.Cell(row, 4).Value = item.Workshop;
            sheet.Cell(row, 5).Value = "1";
            sheet.Cell(row, 6).Value = "КЭН";
            sheet.Cell(row, 7).Value = "3";
            sheet.Cell(row, 11).Value = item.Volume;
            sheet.Cell(row, 14).Value = "15.01.2025";
            if (item.Permission.Length > 0)
            {
                sheet.Cell(row, 23).Value = item.Permission;
            }

            if (item.Comment.Length > 0)
            {
                sheet.Cell(row, 24).Value = item.Comment;
            }

            row++;
        }

        workbook.SaveAs(path);
        return path;
    }

    [Fact]
    public void ImportOpenRequests_ReturnsOnlyOpenRows()
    {
        var path = CreateJournal(
            (70, "КТЦ", "ТО насоса", "", ""),
            (71, "ЭЦ", "ТО ЭД", "Разрешено", ""),
            (72, "ЭЦ", "Замена", "", "продлено"));
        _config.SetJournalPath(path);
        var service = CreateService();

        var requests = service.ImportOpenRequests();

        var request = Assert.Single(requests);
        Assert.Equal("70", request.Number);
        Assert.Equal("КТЦ", request.Workshop);
        Assert.Equal("ТО насоса", request.Volume);
        Assert.Equal("ОТКРЫТА", request.Status);
        Assert.Equal(2, request.SourceRow);
    }

    [Fact]
    public void ImportOpenRequests_MissingFile_ReturnsEmpty()
    {
        _config.SetJournalPath(Path.Combine(_root, "нет_файла.xlsx"));
        var service = CreateService();

        Assert.Empty(service.ImportOpenRequests());
    }

    [Fact]
    public void CloseRequest_WritesPermissionAndActualDate()
    {
        var path = CreateJournal((70, "КТЦ", "ТО насоса", "", ""));
        _config.SetJournalPath(path);
        var service = CreateService();
        var request = service.ImportOpenRequests().Single();

        service.CloseRequest(request);

        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheets.First();
        Assert.Equal("Разрешено", sheet.Cell(2, 23).GetString());
        Assert.Equal("03.11.25", sheet.Cell(2, 21).GetString());
        Assert.Equal("10:30", sheet.Cell(2, 22).GetString());

        // После закрытия заявка больше не считается открытой.
        Assert.Empty(service.ImportOpenRequests());
    }

    [Fact]
    public void ExtendRequest_AddsNewRowWithNextNumber()
    {
        var path = CreateJournal(
            (70, "КТЦ", "ТО насоса", "", ""),
            (71, "ЭЦ", "ТО ЭД", "Разрешено", ""),
            (72, "ЭЦ", "Замена", "Разрешено", ""));
        _config.SetJournalPath(path);
        var service = CreateService();
        var request = service.ImportOpenRequests().Single();

        var newNumber = service.ExtendRequest(request, "20.02.25");

        Assert.Equal("73", newNumber);

        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheets.First();

        // Новая строка (5) содержит продлённую заявку.
        Assert.Equal("73", sheet.Cell(5, 1).GetString());
        Assert.Equal("20.02.25", sheet.Cell(5, 14).GetString());
        Assert.Equal("ТО насоса (Продление заявки №70)", sheet.Cell(5, 11).GetString());

        // В исходной заявке появился комментарий-ссылка.
        Assert.Equal("Заявка продлена. См. заявку №73", sheet.Cell(2, 24).GetString());
    }

    [Fact]
    public void SaveJournal_CreatesBackupFile()
    {
        var path = CreateJournal((70, "КТЦ", "ТО насоса", "", ""));
        _config.SetJournalPath(path);
        var service = CreateService();

        service.CloseRequest(service.ImportOpenRequests().Single());

        var backups = Directory.GetFiles(_root, "*_backup_*");
        Assert.NotEmpty(backups);
    }
}
