using System.Globalization;
using Zayavka.Domain;
using Zayavka.Domain.Abstractions;
using Zayavka.WinForms.Dialogs;

namespace Zayavka.WinForms.Views;

/// <summary>
/// Вкладка «Импорт заявок»: импорт открытых заявок из журнала Excel,
/// фильтр по цеху, закрытие и продление заявок с записью обратно в журнал.
/// Перенос логики <c>ImportRequestsTab.pas</c>.
/// </summary>
public sealed class ImportRequestsTabControl : UserControl
{
    private const string AllWorkshops = "Все цеха";

    private readonly IRequestJournalService _journal;
    private readonly IPassportRepository _passports;

    private readonly ListView _lvRequests = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        MultiSelect = false,
        HideSelection = false,
        GridLines = true,
    };

    private readonly ToolStripComboBox _cbWorkshop = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 160,
    };

    private readonly ToolStripStatusLabel _lblStatus = new()
    {
        Spring = true,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private readonly List<RequestRecord> _requests = new();

    public ImportRequestsTabControl(IRequestJournalService journal, IPassportRepository passports)
    {
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _passports = passports ?? throw new ArgumentNullException(nameof(passports));
        BuildUi();
        Load += (_, _) => UpdateStatus(
            string.IsNullOrEmpty(_journal.JournalPath)
                ? "Файл журнала не выбран. Нажмите «Выбрать файл»."
                : $"Журнал: {_journal.JournalPath}");
    }

    private void BuildUi()
    {
        _lvRequests.Columns.Add("№ заявки", 80);
        _lvRequests.Columns.Add("Дата подачи", 85);
        _lvRequests.Columns.Add("Префикс", 70);
        _lvRequests.Columns.Add("Оборудование", 90);
        _lvRequests.Columns.Add("Номер", 70);
        _lvRequests.Columns.Add("Объем работ", 360);
        _lvRequests.Columns.Add("Дата окончания", 100);
        _lvRequests.Columns.Add("Статус", 90);
        _lvRequests.Columns.Add("Дата ввода в резерв", 130);
        _lvRequests.Columns.Add("Время ввода в резерв", 130);

        var menu = new ContextMenuStrip();
        menu.Items.Add("Закрыть заявку", null, (_, _) => CloseRequest());
        menu.Items.Add("Продлить заявку", null, (_, _) => ExtendRequest());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Копировать объём работ", null, (_, _) => CopyVolume());
        menu.Items.Add("Записать в паспорт", null, (_, _) => WriteToPassport());
        _lvRequests.ContextMenuStrip = menu;
        _lvRequests.MouseDown += OnListMouseDown;

        var toolStrip = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
        toolStrip.Items.Add(new ToolStripButton("Выбрать файл", null, (_, _) => ChooseJournalFile()));
        toolStrip.Items.Add(new ToolStripButton("Импорт", null, (_, _) => Import()));
        toolStrip.Items.Add(new ToolStripButton("Открыть журнал", null, (_, _) => OpenJournal()));
        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(new ToolStripButton("Копировать объём работ", null, (_, _) => CopyVolume()));
        toolStrip.Items.Add(new ToolStripButton("Записать в паспорт", null, (_, _) => WriteToPassport()));
        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(new ToolStripLabel("Фильтр по цеху:"));
        _cbWorkshop.SelectedIndexChanged += (_, _) => ApplyFilter();
        toolStrip.Items.Add(_cbWorkshop);

        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(_lblStatus);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 42,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(6, 4, 6, 4),
        };
        buttons.Controls.Add(MakeButton("Закрыть заявку", CloseRequest));
        buttons.Controls.Add(MakeButton("Продлить заявку", ExtendRequest));
        buttons.Controls.Add(MakeButton("Копировать объём работ", CopyVolume));
        buttons.Controls.Add(MakeButton("Записать в паспорт", WriteToPassport));

        Controls.Add(_lvRequests);
        Controls.Add(buttons);
        Controls.Add(statusStrip);
        Controls.Add(toolStrip);
    }

    private static Button MakeButton(string text, Action onClick)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 28, Margin = new Padding(0, 0, 6, 0) };
        button.Click += (_, _) => onClick();
        return button;
    }

    private void OnListMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
        {
            return;
        }

        var item = _lvRequests.GetItemAt(e.X, e.Y);
        if (item is not null)
        {
            item.Selected = true;
        }
    }

    private RequestRecord? GetSelected() =>
        _lvRequests.SelectedItems.Count > 0
            ? _lvRequests.SelectedItems[0].Tag as RequestRecord
            : null;

    private void UpdateStatus(string message) => _lblStatus.Text = message;

    private void ChooseJournalFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Выберите файл журнала заявок",
            Filter = "Журнал заявок (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|Все файлы (*.*)|*.*",
            CheckFileExists = true,
        };

        var current = _journal.JournalPath;
        if (!string.IsNullOrEmpty(current))
        {
            var directory = Path.GetDirectoryName(current);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                dialog.InitialDirectory = directory;
            }
        }

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            _journal.JournalPath = dialog.FileName;
            UpdateStatus($"Журнал: {dialog.FileName}");
        }
        catch (Exception ex)
        {
            Warn(ex.Message);
        }
    }

    private void Import()
    {
        var path = _journal.JournalPath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Warn("Файл журнала не найден. Выберите файл журнала заявок.");
            return;
        }

        try
        {
            _requests.Clear();
            _requests.AddRange(_journal.ImportOpenRequests());
            LoadWorkshopFilter();
            ApplyFilter();
            UpdateStatus($"Импортировано открытых заявок: {_requests.Count}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Не удалось прочитать журнал.\n\n" + ex.Message +
                "\n\nЕсли журнал открыт в Excel, закройте его и повторите.",
                "Ошибка импорта", MessageBoxButtons.OK, MessageBoxIcon.Error);
            UpdateStatus("Ошибка импорта журнала.");
        }
    }

    private void LoadWorkshopFilter()
    {
        var workshops = _requests
            .Where(r => !string.IsNullOrWhiteSpace(r.Workshop))
            .Select(r => r.Workshop.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(w => w, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _cbWorkshop.Items.Clear();
        _cbWorkshop.Items.Add(AllWorkshops);
        foreach (var workshop in workshops)
        {
            _cbWorkshop.Items.Add(workshop);
        }

        if (_cbWorkshop.Items.Count > 0)
        {
            _cbWorkshop.SelectedIndex = 0;
        }
    }

    private void ApplyFilter()
    {
        var filter = _cbWorkshop.SelectedItem as string;
        if (string.IsNullOrEmpty(filter))
        {
            filter = AllWorkshops;
        }

        _lvRequests.BeginUpdate();
        try
        {
            _lvRequests.Items.Clear();

            foreach (var request in _requests)
            {
                if (filter != AllWorkshops &&
                    !string.Equals(request.Workshop, filter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var item = new ListViewItem(request.Number) { Tag = request };
                item.SubItems.Add(request.DateStr);
                item.SubItems.Add(request.Prefix);
                item.SubItems.Add(request.Name);
                item.SubItems.Add(request.Equipment);
                item.SubItems.Add(request.Volume);
                item.SubItems.Add(request.EndDate);
                item.SubItems.Add(request.Status);
                item.SubItems.Add(request.ReserveDate);
                item.SubItems.Add(request.ReserveTime);
                _lvRequests.Items.Add(item);
            }
        }
        finally
        {
            _lvRequests.EndUpdate();
        }
    }

    private void OpenJournal()
    {
        var path = _journal.JournalPath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Warn("Файл журнала не найден.");
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Warn(ex.Message);
        }
    }

    private void CloseRequest()
    {
        var request = GetSelected();
        if (request is null)
        {
            Warn("Выберите заявку в списке.");
            return;
        }

        if (string.IsNullOrEmpty(_journal.JournalPath) || !File.Exists(_journal.JournalPath))
        {
            Warn("Файл журнала не найден. Выполните импорт заново.");
            return;
        }

        try
        {
            _journal.CloseRequest(request);
            request.Status = "ЗАКРЫТА";
            ApplyFilter();
            UpdateStatus($"Заявка №{request.Number} успешно закрыта.");
            MessageBox.Show(this, $"Заявка №{request.Number} успешно закрыта!",
                "Закрытие заявки", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Не удалось закрыть заявку в журнале.\n\n" + ex.Message +
                "\n\nЕсли журнал открыт в Excel, закройте его и повторите.",
                "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExtendRequest()
    {
        var request = GetSelected();
        if (request is null)
        {
            Warn("Выберите заявку в списке.");
            return;
        }

        if (string.IsNullOrEmpty(_journal.JournalPath) || !File.Exists(_journal.JournalPath))
        {
            Warn("Файл журнала не найден. Выполните импорт заново.");
            return;
        }

        var input = InputDialog.Show(this, "Продление заявки",
            "Введите новую дату окончания заявки (дд.мм.гггг):");
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        if (!TryParseDate(input.Trim(), out var newEndDate))
        {
            Warn("Неверный формат даты!");
            return;
        }

        // Как в исходном проекте: дата для журнала пишется в формате dd.MM.yy.
        var newEndDateStr = newEndDate.ToString("dd.MM.yy", CultureInfo.InvariantCulture);

        try
        {
            var newNumber = _journal.ExtendRequest(request, newEndDateStr);
            request.Status = "ПРОДЛЕНА";
            ApplyFilter();
            UpdateStatus($"Заявка успешно продлена! Новый номер: {newNumber}");
            MessageBox.Show(this, $"Заявка успешно продлена! Новый номер: {newNumber}",
                "Продление заявки", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Не удалось продлить заявку в журнале.\n\n" + ex.Message +
                "\n\nЕсли журнал открыт в Excel, закройте его и повторите.",
                "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>Копирует «Объём работ» выбранной заявки в буфер обмена.</summary>
    private void CopyVolume()
    {
        var request = GetSelected();
        if (request is null)
        {
            Warn("Выберите заявку в списке.");
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Volume))
        {
            Warn("У выбранной заявки пустой объём работ.");
            return;
        }

        try
        {
            Clipboard.SetText(request.Volume);
            UpdateStatus($"Объём работ заявки №{request.Number} скопирован в буфер обмена.");
        }
        catch (Exception ex)
        {
            Warn(ex.Message);
        }
    }

    /// <summary>
    /// Записывает «Объём работ» заявки в паспорт соответствующего оборудования.
    /// Дата записи — по дате закрытия заявки (U/V), иначе по дате окончания (N/O),
    /// иначе текущая; строка вставляется в хронологическом порядке.
    /// </summary>
    private void WriteToPassport()
    {
        var selected = GetSelected();
        if (selected is null)
        {
            Warn("Выберите заявку в списке.");
            return;
        }

        if (string.IsNullOrWhiteSpace(selected.Volume))
        {
            Warn("У выбранной заявки пустой объём работ — нечего записывать.");
            return;
        }

        // Свежие данные заявки: дата закрытия могла появиться позже импорта.
        var request = _journal.ReadRequest(selected.SourceRow) ?? selected;

        var passport = ResolvePassport(request);
        if (passport is null)
        {
            return;
        }

        var entry = BuildEntry(request);

        try
        {
            _passports.AddWork(passport, entry);
            UpdateStatus($"Запись добавлена в паспорт «{passport.Name}» (дата {entry.Date}).");
            MessageBox.Show(this,
                $"Объём работ записан в паспорт «{passport.Name}»." +
                $"\nДата записи: {entry.Date}",
                "Паспорт оборудования", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Не удалось записать в паспорт.\n\n" + ex.Message +
                "\n\nЕсли файл паспорта открыт в Excel, закройте его и повторите.",
                "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private PassportRecord? ResolvePassport(RequestRecord request)
    {
        var descriptor = BuildEquipmentDescriptor(request);
        var matches = descriptor.Length > 0
            ? _passports.FindByEquipment(descriptor)
            : new List<PassportRecord>();

        if (matches.Count == 1)
        {
            return matches[0];
        }

        var all = _passports.GetAll();
        if (all.Count == 0)
        {
            Warn("Нет доступных паспортов оборудования. Укажите папку паспортов на вкладке «Паспорта оборудования».");
            return null;
        }

        var candidates = matches.Count > 1 ? matches : all;
        var prompt = matches.Count > 1
            ? $"По обозначению «{descriptor}» найдено несколько паспортов. Выберите нужный:"
            : $"Паспорт для «{descriptor}» не найден автоматически. Выберите паспорт:";

        using var dialog = new PassportPickerDialog(prompt, candidates);
        return dialog.ShowDialog(this) == DialogResult.OK ? dialog.SelectedPassport : null;
    }

    private static PassportWorkEntry BuildEntry(RequestRecord request)
    {
        // Дата записи — по дате закрытия заявки (U/V), иначе по дате окончания (N/O),
        // иначе текущая дата.
        var recordDate = FirstNonEmpty(
            request.ActualDate,
            request.EndDate,
            DateTime.Today.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));

        var endDate = FirstNonEmpty(request.ActualDate, request.EndDate);
        var endTime = FirstNonEmpty(request.ActualTime, request.EndTime);

        return new PassportWorkEntry
        {
            Date = recordDate,
            WorkName = request.Volume.Trim(),
            StartDate = Combine(request.StartDate, request.StartTime),
            EndDate = Combine(endDate, endTime),
            RequestNumber = request.Number.Trim(),
        };
    }

    /// <summary>Обозначение оборудования из заявки, например «1КЭН-3».</summary>
    private static string BuildEquipmentDescriptor(RequestRecord request)
    {
        var prefix = request.Prefix.Trim();
        var name = request.Name.Trim();
        var number = request.Equipment.Trim();

        if (name.Length > 0 && number.Length > 0)
        {
            return $"{prefix}{name}-{number}";
        }

        return (prefix + name + number).Trim();
    }

    private static string Combine(string? date, string? time)
    {
        var datePart = (date ?? string.Empty).Trim();
        var timePart = (time ?? string.Empty).Trim();

        if (datePart.Length == 0)
        {
            return string.Empty;
        }

        return timePart.Length == 0 ? datePart : $"{datePart} {timePart}";
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static bool TryParseDate(string value, out DateTime date)
    {
        var formats = new[] { "dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "d.M.yy" };

        return DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture,
                   DateTimeStyles.None, out date)
               || DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out date);
    }

    private void Warn(string message) =>
        MessageBox.Show(this, message, "Импорт заявок", MessageBoxButtons.OK, MessageBoxIcon.Information);
}
