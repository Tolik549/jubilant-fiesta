namespace Zayavka.Domain;

/// <summary>
/// Запись заявки из журнала местных заявок (Excel).
/// Соответствует записи <c>TRequestRecord</c> исходного проекта на Free Pascal.
/// </summary>
public sealed class RequestRecord
{
    /// <summary>Номер заявки (колонка A).</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>Дата подачи (колонка B).</summary>
    public string DateStr { get; set; } = string.Empty;

    /// <summary>Дата начала работ (колонка L).</summary>
    public string StartDate { get; set; } = string.Empty;

    /// <summary>Время начала работ (колонка M).</summary>
    public string StartTime { get; set; } = string.Empty;

    /// <summary>Цех (колонка D) — используется только для фильтрации.</summary>
    public string Workshop { get; set; } = string.Empty;

    /// <summary>Префикс (колонка E).</summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>Сокращённое диспетчерское наименование (колонка F).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Номер оборудования (колонка G).</summary>
    public string Equipment { get; set; } = string.Empty;

    /// <summary>Объём работ (колонка K).</summary>
    public string Volume { get; set; } = string.Empty;

    /// <summary>Дата окончания (колонка N).</summary>
    public string EndDate { get; set; } = string.Empty;

    /// <summary>Время окончания (колонка O).</summary>
    public string EndTime { get; set; } = string.Empty;

    /// <summary>Дата фактического окончания ремонта / закрытия (колонка U).</summary>
    public string ActualDate { get; set; } = string.Empty;

    /// <summary>Время фактического окончания ремонта / закрытия (колонка V).</summary>
    public string ActualTime { get; set; } = string.Empty;

    /// <summary>Статус: ОТКРЫТА / ЗАКРЫТА / ПРОДЛЕНА.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Номер строки в журнале (1-based, как в Excel).</summary>
    public int SourceRow { get; set; }

    /// <summary>Дата ввода в резерв (колонка Y).</summary>
    public string ReserveDate { get; set; } = string.Empty;

    /// <summary>Время ввода в резерв (колонка Z).</summary>
    public string ReserveTime { get; set; } = string.Empty;
}
