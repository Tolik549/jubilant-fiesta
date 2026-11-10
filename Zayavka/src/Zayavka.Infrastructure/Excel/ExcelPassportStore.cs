using System.Globalization;
using ClosedXML.Excel;
using Zayavka.Domain;
using Zayavka.Domain.Abstractions;
using Zayavka.Infrastructure.IO;

namespace Zayavka.Infrastructure.Excel;

/// <summary>
/// Хранилище паспорта оборудования в формате Excel (.xlsx).
/// Записи журнала работ ведутся на листе «паспорт» (регистронезависимо;
/// если листа нет — используется первый лист).
/// </summary>
/// <remarks>
/// Соответствие столбцов (Excel, 1-based):
/// 1 = дата; 2 = Наработка, ч.; 3 = наименование работ;
/// 4 = МТР, расходники; 5 = ед.изм.; 6 = кол-во; 7 = ссылка на доки;
/// 8 = Начало работ; 9 = Окончание работ; 10 = № заявки.
/// Новые столбцы 8–10 дописываются автоматически (миграция старых паспортов).
/// </remarks>
public sealed class ExcelPassportStore : IPassportStore
{
    /// <summary>Имя листа паспорта.</summary>
    public const string SheetName = "паспорт";

    private const int DateColumn = 1;
    private const int WorkColumn = 3;
    private const int WorkStartColumn = 8;  // H
    private const int WorkEndColumn = 9;    // I
    private const int RequestColumn = 10;   // J

    private static readonly string[] Header =
    {
        "дата",
        "Наработка, ч.",
        "наименование работ",
        "МТР, расходники",
        "ед.изм.",
        "кол-во",
        "ссылка на доки",
        "Начало работ",
        "Окончание работ",
        "№ заявки",
    };

    private static readonly string[] SupportedExtensions = { ".xlsx", ".xlsm" };

    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return SupportedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<PassportWorkEntry> ReadWorkEntries(string filePath)
    {
        var result = new List<PassportWorkEntry>();

        using var workbook = new XLWorkbook(filePath);
        var sheet = GetPassportSheet(workbook);
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;

        for (var row = 2; row <= lastRow; row++)
        {
            var date = ReadCellText(sheet.Cell(row, DateColumn));
            var work = ReadCellText(sheet.Cell(row, WorkColumn));

            if (date.Length == 0 && work.Length == 0)
            {
                continue;
            }

            result.Add(new PassportWorkEntry
            {
                Date = date,
                WorkName = work,
                StartDate = ReadCellText(sheet.Cell(row, WorkStartColumn)),
                EndDate = ReadCellText(sheet.Cell(row, WorkEndColumn)),
                RequestNumber = ReadCellText(sheet.Cell(row, RequestColumn)),
            });
        }

        return result;
    }

    public void AddWorkEntry(string filePath, PassportWorkEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        using var workbook = File.Exists(filePath) ? new XLWorkbook(filePath) : new XLWorkbook();
        var sheet = GetPassportSheet(workbook);
        EnsureHeader(sheet);

        var targetRow = FindInsertRow(sheet, entry.Date);
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;

        // Дата записи в середине журнала: вставляем строку со сдвигом вниз,
        // чтобы сохранить хронологический порядок.
        if (targetRow <= lastRow)
        {
            sheet.Row(targetRow).InsertRowsAbove(1);
        }

        WriteEntry(sheet, targetRow, entry);
        sheet.Columns().AdjustToContents();

        SafeFileWriter.SaveViaTemp(filePath, tempPath => workbook.SaveAs(tempPath));
    }

    public void CreateEmpty(string filePath, PassportWorkEntry startEntry)
    {
        ArgumentNullException.ThrowIfNull(startEntry);

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet(SheetName);
        WriteHeader(sheet);
        WriteEntry(sheet, 2, startEntry);
        sheet.Columns().AdjustToContents();

        SafeFileWriter.SaveViaTemp(filePath, tempPath => workbook.SaveAs(tempPath));
    }

    private static void WriteEntry(IXLWorksheet sheet, int row, PassportWorkEntry entry)
    {
        SetCell(sheet, row, DateColumn, entry.Date);
        SetCell(sheet, row, WorkColumn, entry.WorkName);
        SetCell(sheet, row, WorkStartColumn, entry.StartDate);
        SetCell(sheet, row, WorkEndColumn, entry.EndDate);
        SetCell(sheet, row, RequestColumn, entry.RequestNumber);
    }

    private static void SetCell(IXLWorksheet sheet, int row, int column, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        sheet.Cell(row, column).Value = value;
    }

    /// <summary>Определяет строку для вставки записи (хронологический порядок по дате).</summary>
    private static int FindInsertRow(IXLWorksheet sheet, string dateText)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;

        if (!TryParseDate(dateText, out var newDate))
        {
            return lastRow + 1; // дата не задана — в конец журнала
        }

        for (var row = 2; row <= lastRow; row++)
        {
            if (TryParseDate(ReadCellText(sheet.Cell(row, DateColumn)), out var existing) &&
                existing > newDate)
            {
                return row;
            }
        }

        return lastRow + 1;
    }

    private static bool TryParseDate(string value, out DateTime date) =>
        DateTime.TryParseExact(value, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static IXLWorksheet GetPassportSheet(XLWorkbook workbook)
    {
        if (workbook.Worksheets.Count == 0)
        {
            return workbook.AddWorksheet(SheetName);
        }

        return workbook.Worksheets.FirstOrDefault(
                   w => string.Equals(w.Name, SheetName, StringComparison.OrdinalIgnoreCase))
               ?? workbook.Worksheets.First();
    }

    private static void EnsureHeader(IXLWorksheet sheet)
    {
        if (sheet.LastRowUsed() is null)
        {
            WriteHeader(sheet);
            return;
        }

        // Миграция старых паспортов: дописываем только ОТСУТСТВУЮЩИЕ названия
        // колонок (например, новые «Начало работ»/«Окончание работ»/«№ заявки»),
        // не затрагивая существующие данные и заголовки.
        for (var i = 0; i < Header.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            if (cell.IsEmpty())
            {
                cell.Value = Header[i];
                cell.Style.Font.Bold = true;
            }
        }
    }

    private static void WriteHeader(IXLWorksheet sheet)
    {
        for (var i = 0; i < Header.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = Header[i];
            cell.Style.Font.Bold = true;
        }
    }

    private static string ReadCellText(IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return string.Empty;
        }

        if (cell.DataType == XLDataType.DateTime)
        {
            return cell.GetDateTime().ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        }

        return cell.GetString().Trim();
    }
}
