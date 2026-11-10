using System.Globalization;
using ClosedXML.Excel;
using Zayavka.Domain;
using Zayavka.Domain.Abstractions;
using Zayavka.Infrastructure.IO;

namespace Zayavka.Infrastructure.Excel;

/// <summary>
/// Сервис журнала местных заявок в формате Excel (первый лист книги).
/// Импортирует открытые заявки, закрывает и продлевает их, записывая
/// изменения обратно в файл журнала.
/// </summary>
public sealed class ExcelJournalService : IRequestJournalService
{
    // Столбцы журнала (0-based индексы — как в исходном проекте; в ClosedXML +1).
    private const int ColNumber = 0;        // A  Номер заявки
    private const int ColDate = 1;          // B  Дата подачи
    private const int ColWorkshop = 3;      // D  Цех
    private const int ColPrefix = 4;        // E  Префикс
    private const int ColName = 5;          // F  Сокращённое диспетчерское наименование
    private const int ColEquipment = 6;     // G  Номер оборудования
    private const int ColVolume = 10;       // K  Объём работ
    private const int ColStartDate = 11;    // L  Дата начала
    private const int ColStartTime = 12;    // M  Время начала
    private const int ColEndDate = 13;      // N  Дата окончания
    private const int ColEndTime = 14;      // O  Время окончания
    private const int ColEmergency = 15;    // P  Аварийная готовность
    private const int ColActualDate = 20;   // U  Дата фактического окончания
    private const int ColActualTime = 21;   // V  Время фактического окончания
    private const int ColPermission = 22;   // W  Разрешение начальника цеха
    private const int ColComment = 23;      // X  Комментарий к разрешению
    private const int ColReserveDate = 24;  // Y  Дата ввода в резерв
    private const int ColReserveTime = 25;  // Z  Время ввода в резерв

    private readonly IAppConfig _config;
    private readonly Func<DateTime> _now;

    public ExcelJournalService(IAppConfig config, Func<DateTime>? nowProvider = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _now = nowProvider ?? (() => DateTime.Now);
    }

    public string JournalPath
    {
        get => _config.JournalPath;
        set => _config.SetJournalPath(value);
    }

    public IReadOnlyList<RequestRecord> ImportOpenRequests()
    {
        var result = new List<RequestRecord>();
        var path = JournalPath;

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return result;
        }

        using var workbook = new XLWorkbook(path);
        var sheet = GetJournalSheet(workbook);
        if (sheet is null)
        {
            return result;
        }

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;

        // Строка 1 — заголовок, данные начинаются со строки 2.
        for (var row = 2; row <= lastRow; row++)
        {
            var permission = ReadText(sheet, row, ColPermission);
            var comment = ReadText(sheet, row, ColComment);

            // Открытая заявка — разрешение и комментарий пусты.
            if (permission.Length != 0 || comment.Length != 0)
            {
                continue;
            }

            var record = ReadRequestRow(sheet, row);

            if (record.Number.Length == 0 && record.Name.Length == 0 &&
                record.Equipment.Length == 0 && record.Volume.Length == 0)
            {
                continue;
            }

            result.Add(record);
        }

        return result;
    }

    public RequestRecord? ReadRequest(int sourceRow)
    {
        var path = JournalPath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path) || sourceRow < 2)
        {
            return null;
        }

        using var workbook = new XLWorkbook(path);
        var sheet = GetJournalSheet(workbook);
        if (sheet is null)
        {
            return null;
        }

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        return sourceRow > lastRow ? null : ReadRequestRow(sheet, sourceRow);
    }

    public void CloseRequest(RequestRecord request)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureJournalExists();

        using var workbook = new XLWorkbook(JournalPath);
        var sheet = GetJournalSheet(workbook)
            ?? throw new InvalidOperationException("В журнале нет листов.");

        var row = request.SourceRow;

        var reserveDate = ReadText(sheet, row, ColReserveDate);
        var reserveTime = ExcelCellReader.ReadTime(sheet.Cell(row, ColReserveTime + 1));

        // Дата/время фактического окончания: из резерва, иначе — текущие.
        var actualDate = reserveDate.Length > 0
            ? reserveDate
            : _now().ToString("dd.MM.yy", CultureInfo.InvariantCulture);
        var actualTime = reserveTime.Length > 0
            ? reserveTime
            : _now().ToString("HH:mm", CultureInfo.InvariantCulture);

        SetText(sheet, row, ColActualDate, actualDate);
        SetTimeText(sheet, row, ColActualTime, actualTime);
        SetText(sheet, row, ColPermission, "Разрешено");

        SaveJournal(workbook);
    }

    public string ExtendRequest(RequestRecord request, string newEndDate)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(newEndDate);
        EnsureJournalExists();

        using var workbook = new XLWorkbook(JournalPath);
        var sheet = GetJournalSheet(workbook)
            ?? throw new InvalidOperationException("В журнале нет листов.");

        var sourceRow = request.SourceRow;
        var lastRow = FindLastRowWithData(sheet, ColNumber);
        var newNumber = GetNextRequestNumber(sheet, lastRow);
        var newRow = lastRow + 1;

        var cleanVolume = CleanVolume(request.Volume);

        SetText(sheet, newRow, ColNumber, newNumber);
        SetText(sheet, newRow, ColDate, _now().ToString("dd.MM.yy", CultureInfo.InvariantCulture));
        SetText(sheet, newRow, ColWorkshop, ReadText(sheet, sourceRow, ColWorkshop));
        SetText(sheet, newRow, ColPrefix, ReadText(sheet, sourceRow, ColPrefix));
        SetText(sheet, newRow, ColName, ReadText(sheet, sourceRow, ColName));
        SetText(sheet, newRow, ColEquipment, ReadText(sheet, sourceRow, ColEquipment));
        SetText(sheet, newRow, ColVolume, cleanVolume + " (Продление заявки №" + request.Number + ")");
        SetText(sheet, newRow, ColStartDate, ReadText(sheet, sourceRow, ColEndDate));
        SetText(sheet, newRow, ColStartTime, ExcelCellReader.ReadTime(sheet.Cell(sourceRow, ColEndTime + 1)));
        SetText(sheet, newRow, ColEndDate, newEndDate);
        SetTimeText(sheet, newRow, ColEndTime, "17:00");
        SetText(sheet, newRow, ColEmergency, "СЗ");
        SetText(sheet, sourceRow, ColComment, "Заявка продлена. См. заявку №" + newNumber);

        SaveJournal(workbook);
        return newNumber;
    }

    private static IXLWorksheet? GetJournalSheet(XLWorkbook workbook) =>
        workbook.Worksheets.Count > 0 ? workbook.Worksheets.First() : null;

    /// <summary>Читает одну строку журнала в запись заявки.</summary>
    private static RequestRecord ReadRequestRow(IXLWorksheet sheet, int row)
    {
        var permission = ReadText(sheet, row, ColPermission);
        var comment = ReadText(sheet, row, ColComment);

        return new RequestRecord
        {
            Number = ReadText(sheet, row, ColNumber),
            DateStr = ReadText(sheet, row, ColDate),
            StartDate = ReadText(sheet, row, ColStartDate),
            StartTime = ExcelCellReader.ReadTime(sheet.Cell(row, ColStartTime + 1)),
            Workshop = ReadText(sheet, row, ColWorkshop),
            Prefix = ReadText(sheet, row, ColPrefix),
            Name = ReadText(sheet, row, ColName),
            Equipment = ReadText(sheet, row, ColEquipment),
            Volume = ReadText(sheet, row, ColVolume),
            EndDate = ReadText(sheet, row, ColEndDate),
            EndTime = ExcelCellReader.ReadTime(sheet.Cell(row, ColEndTime + 1)),
            ActualDate = ReadText(sheet, row, ColActualDate),
            ActualTime = ExcelCellReader.ReadTime(sheet.Cell(row, ColActualTime + 1)),
            Status = permission.Length == 0 && comment.Length == 0 ? "ОТКРЫТА" : "ЗАКРЫТА",
            ReserveDate = ReadText(sheet, row, ColReserveDate),
            ReserveTime = ExcelCellReader.ReadTime(sheet.Cell(row, ColReserveTime + 1)),
            SourceRow = row,
        };
    }

    private void EnsureJournalExists()
    {
        var path = JournalPath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            throw new FileNotFoundException(
                "Файл журнала заявок не найден. Сначала выберите файл журнала.",
                path);
        }
    }

    private static string ReadText(IXLWorksheet sheet, int row, int column) =>
        ExcelCellReader.ReadText(sheet.Cell(row, column + 1));

    private static void SetText(IXLWorksheet sheet, int row, int column, string? value)
    {
        var cell = sheet.Cell(row, column + 1);
        cell.Style.NumberFormat.Format = "@"; // текстовый формат, как в исходном проекте
        cell.Value = value ?? string.Empty;
    }

    private static void SetTimeText(IXLWorksheet sheet, int row, int column, string? value) =>
        SetText(sheet, row, column, ExcelCellReader.NormalizeTime(value));

    private static int FindLastRowWithData(IXLWorksheet sheet, int column)
    {
        var last = sheet.LastRowUsed()?.RowNumber() ?? 0;
        for (var row = last; row >= 2; row--)
        {
            if (ReadText(sheet, row, column).Length != 0)
            {
                return row;
            }
        }

        return 0;
    }

    private static string GetNextRequestNumber(IXLWorksheet sheet, int lastRow)
    {
        if (lastRow == 0)
        {
            return "1";
        }

        var text = ReadText(sheet, lastRow, ColNumber);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? (number + 1).ToString(CultureInfo.InvariantCulture)
            : "1";
    }

    /// <summary>Удаляет из объёма работ пометки о предыдущих продлениях.</summary>
    private static string CleanVolume(string? volume)
    {
        var text = volume ?? string.Empty;
        const string marker = " (Продление заявки №";

        while (true)
        {
            var start = text.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
            {
                break;
            }

            var end = text.IndexOf(')', start);
            if (end < 0)
            {
                break;
            }

            text = text.Remove(start, end - start + 1);
        }

        return text.Trim();
    }

    private void SaveJournal(XLWorkbook workbook)
    {
        var path = JournalPath;

        // Резервная копия перед перезаписью (аналог BackupJournalFile).
        CreateBackup(path);

        SafeFileWriter.SaveViaTemp(path, tempPath => workbook.SaveAs(tempPath));
    }

    private static string? CreateBackup(string fileName)
    {
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(fileName)) ?? ".";
            var name = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            var backup = Path.Combine(directory, $"{name}_backup_{DateTime.Now:yyyyMMdd_HHmmss}{extension}");
            File.Copy(fileName, backup, overwrite: true);
            return backup;
        }
        catch
        {
            return null;
        }
    }
}
