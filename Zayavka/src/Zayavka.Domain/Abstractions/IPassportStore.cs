namespace Zayavka.Domain.Abstractions;

/// <summary>
/// Хранилище файла паспорта оборудования (формат Excel или CSV).
/// Реализации: <c>ExcelPassportStore</c> (.xlsx) и <c>CsvPassportStore</c> (.csv).
/// </summary>
public interface IPassportStore
{
    /// <summary>Поддерживает ли хранилище указанный файл (обычно по расширению).</summary>
    bool CanHandle(string filePath);

    /// <summary>Читает все записи журнала работ из файла паспорта.</summary>
    IReadOnlyList<PassportWorkEntry> ReadWorkEntries(string filePath);

    /// <summary>
    /// Добавляет запись в журнал работ, сохраняя хронологический порядок
    /// (строка вставляется по дате записи со сдвигом последующих строк вниз).
    /// </summary>
    void AddWorkEntry(string filePath, PassportWorkEntry entry);

    /// <summary>
    /// Создаёт новый файл паспорта с заголовком и стартовой записью
    /// (например, «Создание паспорта оборудования»).
    /// </summary>
    void CreateEmpty(string filePath, PassportWorkEntry startEntry);
}
