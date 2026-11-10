using System.Globalization;
using System.Text;
using Zayavka.Domain;
using Zayavka.Domain.Abstractions;

namespace Zayavka.Infrastructure.Csv;

/// <summary>
/// Резервное хранилище паспорта в формате CSV (разделитель «;», кодировка Windows-1251).
/// Используется, если паспорт ведётся не в Excel.
/// </summary>
/// <remarks>
/// Заголовок: Дата;Наработка;Наименование работ;Бригада;Тип;Часы;Запчасти;Примечания;
/// Начало работ;Окончание работ;№ заявки.
/// Значимые столбцы: 0 — дата, 2 — наименование работ, 8 — начало, 9 — окончание, 10 — № заявки.
/// Заголовок старых файлов дополняется новыми столбцами автоматически (миграция).
/// </remarks>
public sealed class CsvPassportStore : IPassportStore
{
    private const char Delimiter = ';';
    private const int DateColumn = 0;
    private const int WorkColumn = 2;
    private const int WorkStartColumn = 8;
    private const int WorkEndColumn = 9;
    private const int RequestColumn = 10;
    private const int ColumnCount = 11;

    private static readonly string[] Header =
    {
        "Дата",
        "Наработка",
        "Наименование работ",
        "Бригада",
        "Тип",
        "Часы",
        "Запчасти",
        "Примечания",
        "Начало работ",
        "Окончание работ",
        "№ заявки",
    };

    private static readonly Encoding Windows1251 = CreateEncoding();

    public bool CanHandle(string filePath) =>
        string.Equals(Path.GetExtension(filePath), ".csv", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<PassportWorkEntry> ReadWorkEntries(string filePath)
    {
        var result = new List<PassportWorkEntry>();

        if (!File.Exists(filePath))
        {
            return result;
        }

        var lineNumber = 0;
        foreach (var line in File.ReadLines(filePath, Windows1251))
        {
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = line.Split(Delimiter);

            // Пропускаем строку заголовка.
            if (lineNumber == 1)
            {
                var first = fields.ElementAtOrDefault(DateColumn)?.Trim() ?? string.Empty;
                if (string.Equals(first, "Дата", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            var date = fields.ElementAtOrDefault(DateColumn)?.Trim() ?? string.Empty;
            var work = fields.ElementAtOrDefault(WorkColumn)?.Trim() ?? string.Empty;

            if (date.Length == 0 && work.Length == 0)
            {
                continue;
            }

            result.Add(new PassportWorkEntry
            {
                Date = date,
                WorkName = work,
                StartDate = Field(fields, WorkStartColumn),
                EndDate = Field(fields, WorkEndColumn),
                RequestNumber = Field(fields, RequestColumn),
            });
        }

        return result;
    }

    private static string Field(string[] fields, int index) =>
        fields.ElementAtOrDefault(index)?.Trim() ?? string.Empty;

    public void AddWorkEntry(string filePath, PassportWorkEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Читаем существующие строки (если файл есть), отделяя заголовок.
        var lines = File.Exists(filePath)
            ? File.ReadAllLines(filePath, Windows1251).ToList()
            : new List<string>();

        var dataLines = new List<string>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (i == 0 && LooksLikeHeader(lines[i]))
            {
                continue; // заголовок перезапишем новым (миграция)
            }

            dataLines.Add(lines[i]);
        }

        // Вставляем запись в хронологическом порядке по дате.
        var insertIndex = FindInsertIndex(dataLines, entry.Date);
        dataLines.Insert(insertIndex, FormatRow(entry));

        var output = new List<string>(dataLines.Count + 1) { string.Join(Delimiter, Header) };
        output.AddRange(dataLines);

        File.WriteAllLines(filePath, output, Windows1251);
    }

    public void CreateEmpty(string filePath, PassportWorkEntry startEntry)
    {
        ArgumentNullException.ThrowIfNull(startEntry);

        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllLines(
            filePath,
            new[] { string.Join(Delimiter, Header), FormatRow(startEntry) },
            Windows1251);
    }

    private static string FormatRow(PassportWorkEntry entry)
    {
        var fields = new string[ColumnCount];
        fields[DateColumn] = entry.Date;
        fields[WorkColumn] = entry.WorkName;
        fields[WorkStartColumn] = entry.StartDate;
        fields[WorkEndColumn] = entry.EndDate;
        fields[RequestColumn] = entry.RequestNumber;
        return string.Join(Delimiter, fields);
    }

    private static bool LooksLikeHeader(string line)
    {
        var first = line.Split(Delimiter).ElementAtOrDefault(DateColumn)?.Trim();
        return string.Equals(first, "Дата", StringComparison.OrdinalIgnoreCase);
    }

    private static int FindInsertIndex(List<string> dataLines, string dateText)
    {
        if (!TryParseDate(dateText, out var newDate))
        {
            return dataLines.Count;
        }

        for (var i = 0; i < dataLines.Count; i++)
        {
            var fields = dataLines[i].Split(Delimiter);
            if (TryParseDate(Field(fields, DateColumn), out var existing) && existing > newDate)
            {
                return i;
            }
        }

        return dataLines.Count;
    }

    private static bool TryParseDate(string value, out DateTime date) =>
        DateTime.TryParseExact(value, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static Encoding CreateEncoding()
    {
        // Кодировка 1251 доступна после регистрации провайдера кодовых страниц.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1251);
    }
}
