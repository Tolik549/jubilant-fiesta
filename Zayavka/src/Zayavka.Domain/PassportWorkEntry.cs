namespace Zayavka.Domain;

/// <summary>
/// Одна запись журнала работ в файле паспорта оборудования.
/// Соответствует строке листа «паспорт» (Excel) или строке файла паспорта (CSV).
/// </summary>
public sealed class PassportWorkEntry
{
    public PassportWorkEntry()
    {
    }

    public PassportWorkEntry(string date, string workName)
    {
        Date = date;
        WorkName = workName;
    }

    /// <summary>Дата записи в формате dd.MM.yyyy.</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>Наименование (описание) работ.</summary>
    public string WorkName { get; set; } = string.Empty;

    /// <summary>Начало работ (дд.ММ.гггг чч:мм); пусто — не задано.</summary>
    public string StartDate { get; set; } = string.Empty;

    /// <summary>Окончание работ (дд.ММ.гггг чч:мм); пусто — не задано.</summary>
    public string EndDate { get; set; } = string.Empty;

    /// <summary>Номер заявки-источника; пусто для ручных записей.</summary>
    public string RequestNumber { get; set; } = string.Empty;
}
