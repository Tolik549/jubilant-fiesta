using System.Diagnostics;
using System.Globalization;
using System.Text;
using Zayavka.Domain;
using Zayavka.Domain.Abstractions;
using Zayavka.WinForms.Dialogs;

namespace Zayavka.WinForms.Views;

/// <summary>
/// Вкладка «Паспорта оборудования»: список паспортов, создание нового,
/// добавление работ (запись в файл паспорта Excel/CSV) и просмотр деталей.
/// </summary>
public sealed class PassportTabControl : UserControl
{
    private readonly IPassportRepository _repository;

    private readonly ListView _lvPassports = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        MultiSelect = false,
        HideSelection = false,
        GridLines = true,
    };

    private readonly TextBox _txtDetails = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Font = new Font("Consolas", 9F),
    };

    private readonly ToolStripTextBox _txtSearch = new() { Width = 200 };

    private readonly ToolStripStatusLabel _lblStatus = new()
    {
        Spring = true,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private List<PassportRecord> _allPassports = new();

    public PassportTabControl(IPassportRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        BuildUi();
        Load += (_, _) => RefreshList();
    }

    private void BuildUi()
    {
        _lvPassports.Columns.Add("Паспорт", 220);
        _lvPassports.Columns.Add("Последняя работа", 460);
        _lvPassports.SelectedIndexChanged += (_, _) => ShowDetails();

        var toolStrip = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
        toolStrip.Items.Add(new ToolStripButton("Указать папку", null, (_, _) => ChooseFolder()));
        toolStrip.Items.Add(new ToolStripButton("Обновить", null, (_, _) => RefreshList()));
        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(new ToolStripLabel("Поиск:"));
        _txtSearch.ToolTipText = "Поиск по названию паспорта или последней работе";
        _txtSearch.TextChanged += (_, _) => ApplyFilter();
        toolStrip.Items.Add(_txtSearch);
        toolStrip.Items.Add(new ToolStripButton("Сброс", null, (_, _) => _txtSearch.Text = string.Empty));

        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(_lblStatus);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.Panel1,
        };

        split.Panel1.Controls.Add(_lvPassports);
        split.Panel2.Controls.Add(BuildDetailsPanel());

        Controls.Add(split);
        Controls.Add(toolStrip);
        Controls.Add(statusStrip);

        // Значение SplitterDistance задаём после того, как контрол получил размер.
        split.SplitterDistance = 720;
    }

    private Control BuildDetailsPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 38,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 2, 0, 2),
        };

        buttons.Controls.Add(MakeButton("Создать паспорт", CreatePassport));
        buttons.Controls.Add(MakeButton("Добавить работу", AddWork));
        buttons.Controls.Add(MakeButton("Открыть папку", OpenPassportFolder));
        buttons.Controls.Add(MakeButton("Открыть файл", OpenPassportFile));

        panel.Controls.Add(_txtDetails);
        panel.Controls.Add(buttons);
        return panel;
    }

    private static Button MakeButton(string text, Action onClick)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 28, Margin = new Padding(0, 0, 6, 0) };
        button.Click += (_, _) => onClick();
        return button;
    }

    private void RefreshList()
    {
        try
        {
            _allPassports = _repository.GetAll().ToList();
        }
        catch (Exception ex)
        {
            _allPassports = new List<PassportRecord>();
            MessageBox.Show(this, $"Не удалось прочитать список паспортов: {ex.Message}", "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        ApplyFilter();

        _lblStatus.Text = $"Папка: {_repository.PassportDirectory}   |   Паспортов: {_allPassports.Count}";
    }

    private void ApplyFilter()
    {
        var query = _txtSearch.Text.Trim();

        _lvPassports.BeginUpdate();
        try
        {
            _lvPassports.Items.Clear();

            foreach (var passport in _allPassports)
            {
                if (query.Length > 0 &&
                    passport.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                    passport.LastWork.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var item = new ListViewItem(passport.Name) { Tag = passport };
                item.SubItems.Add(passport.LastWork);
                _lvPassports.Items.Add(item);
            }
        }
        finally
        {
            _lvPassports.EndUpdate();
        }

        ShowDetails();
    }

    private PassportRecord? GetSelected() =>
        _lvPassports.SelectedItems.Count > 0
            ? _lvPassports.SelectedItems[0].Tag as PassportRecord
            : null;

    private void ShowDetails()
    {
        var passport = GetSelected();
        if (passport is null)
        {
            _txtDetails.Text = "Выберите паспорт из списка.";
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Паспорт: {passport.Name}");
        builder.AppendLine($"Папка:   {passport.Path}");
        builder.AppendLine($"Файл:    {passport.PassportFile ?? "не найден (создастся при добавлении работы)"}");
        builder.AppendLine();
        builder.AppendLine("Журнал работ:");
        builder.AppendLine(new string('-', 60));

        var entries = _repository.GetWorkEntries(passport);
        if (entries.Count == 0)
        {
            builder.AppendLine("(записей нет)");
        }
        else
        {
            foreach (var entry in entries)
            {
                builder.AppendLine($"{entry.Date,-12} {entry.WorkName}");
            }
        }

        _txtDetails.Text = builder.ToString();
        _txtDetails.SelectionStart = 0;
    }

    private void ChooseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Выберите папку с паспортами оборудования",
            UseDescriptionForTitle = true,
            SelectedPath = _repository.PassportDirectory,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            _repository.PassportDirectory = dialog.SelectedPath;
            RefreshList();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CreatePassport()
    {
        var name = InputDialog.Show(this, "Новый паспорт", "Введите название оборудования:");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (!_repository.TryCreateNew(name, out _, out var error))
        {
            MessageBox.Show(this, error ?? "Не удалось создать паспорт.", "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        RefreshList();
        SetStatus($"Паспорт «{name.Trim()}» создан.");
    }

    private void AddWork()
    {
        var passport = GetSelected();
        if (passport is null)
        {
            MessageBox.Show(this, "Сначала выберите паспорт из списка.", "Добавить работу",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new AddWorkDialog(passport.Name);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.WorkText.Length == 0)
        {
            return;
        }

        try
        {
            if (dialog.HasPeriod)
            {
                _repository.AddWork(passport, new PassportWorkEntry
                {
                    Date = DateTime.Today.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                    WorkName = dialog.WorkText,
                    StartDate = dialog.StartText,
                    EndDate = dialog.EndText,
                });
            }
            else
            {
                _repository.AddWork(passport, dialog.WorkText);
            }

            RefreshList();
            SetStatus($"Запись добавлена в паспорт «{passport.Name}».");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Не удалось записать работу в файл паспорта.\n\n" + ex.Message +
                "\n\nПроверьте, что файл паспорта не открыт в Excel.",
                "Ошибка записи", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenPassportFolder()
    {
        var passport = GetSelected();
        if (passport is not null)
        {
            OpenInShell(passport.Path);
        }
    }

    private void OpenPassportFile()
    {
        var passport = GetSelected();
        if (passport is null)
        {
            return;
        }

        if (string.IsNullOrEmpty(passport.PassportFile) || !File.Exists(passport.PassportFile))
        {
            MessageBox.Show(this, "Файл паспорта ещё не создан.", "Открыть файл",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        OpenInShell(passport.PassportFile);
    }

    private void OpenInShell(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SetStatus(string message) => _lblStatus.Text = message;
}
