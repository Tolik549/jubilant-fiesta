namespace Zayavka.Domain;

/// <summary>
/// Паспорт оборудования — учётная папка конкретной единицы оборудования.
/// Имя папки = название паспорта.
/// </summary>
public sealed class PassportRecord
{
    /// <summary>Название паспорта (имя папки / название оборудования).</summary>
    public required string Name { get; init; }

    /// <summary>Полный путь к папке паспорта.</summary>
    public required string Path { get; init; }

    /// <summary>
    /// Путь к файлу паспорта (Excel .xlsx — приоритет, либо .csv).
    /// Может быть <c>null</c>, если файл ещё не создан.
    /// </summary>
    public string? PassportFile { get; set; }

    /// <summary>Описание последней работы (для отображения в списке).</summary>
    public string LastWork { get; set; } = "Нет работ";
}
