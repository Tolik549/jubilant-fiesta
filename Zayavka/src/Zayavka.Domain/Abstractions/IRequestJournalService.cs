namespace Zayavka.Domain.Abstractions;

/// <summary>
/// Сервис работы с журналом местных заявок (Excel): импорт открытых заявок,
/// закрытие и продление заявки с записью обратно в файл журнала.
/// </summary>
public interface IRequestJournalService
{
    /// <summary>Путь к файлу журнала заявок (из <c>import_requests_config.ini</c>).</summary>
    string JournalPath { get; set; }

    /// <summary>
    /// Импортирует открытые заявки из журнала. Открытой считается заявка,
    /// у которой пусты столбцы «Разрешение начальника цеха» и
    /// «Комментарий к разрешению».
    /// </summary>
    IReadOnlyList<RequestRecord> ImportOpenRequests();

    /// <summary>
    /// Читает заявку по номеру строки журнала (1-based) — актуальные данные
    /// (включая дату фактического окончания/закрытия). Возвращает <c>null</c>,
    /// если строки нет.
    /// </summary>
    RequestRecord? ReadRequest(int sourceRow);

    /// <summary>
    /// Закрывает заявку: проставляет дату/время фактического окончания и
    /// разрешение «Разрешено».
    /// </summary>
    void CloseRequest(RequestRecord request);

    /// <summary>
    /// Продлевает заявку: добавляет в конец журнала новую заявку с новой датой
    /// окончания и возвращает её номер.
    /// </summary>
    string ExtendRequest(RequestRecord request, string newEndDate);
}
