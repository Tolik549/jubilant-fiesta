namespace Zayavka.Domain.Abstractions;

/// <summary>
/// Репозиторий паспортов оборудования: сканирование папки, создание паспортов
/// и дозапись работ в файл паспорта.
/// </summary>
public interface IPassportRepository
{
    /// <summary>Текущая папка паспортов (из конфига).</summary>
    string PassportDirectory { get; set; }

    /// <summary>Возвращает все паспорта (подпапки верхнего уровня) с последней работой.</summary>
    IReadOnlyList<PassportRecord> GetAll();

    /// <summary>Возвращает все записи журнала работ выбранного паспорта.</summary>
    IReadOnlyList<PassportWorkEntry> GetWorkEntries(PassportRecord passport);


    /// <summary>
    /// Создаёт новый паспорт (папку + файл Excel <c>паспорт.xlsx</c> со стартовой записью).
    /// </summary>
    bool TryCreateNew(string name, out PassportRecord? record, out string? error);

    /// <summary>Находит паспорта по обозначению оборудования (напр. «1КЭН-3»).</summary>
    IReadOnlyList<PassportRecord> FindByEquipment(string equipmentDescriptor);

    /// <summary>
    /// Дозаписывает работу (дата — текущая) в файл паспорта (Excel). Если файла нет — он создаётся.
    /// </summary>
    void AddWork(PassportRecord passport, string workText);

    /// <summary>
    /// Добавляет произвольную запись в файл паспорта (дата, начало/окончание работ, № заявки).
    /// Строка вставляется в хронологическом порядке. Если файла нет — он создаётся.
    /// </summary>
    void AddWork(PassportRecord passport, PassportWorkEntry entry);
}
