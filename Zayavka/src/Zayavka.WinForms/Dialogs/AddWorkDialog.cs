using System.Globalization;

namespace Zayavka.WinForms.Dialogs;

/// <summary>
/// Диалог ввода описания выполненной работы для дозаписи в паспорт.
/// Дополнительно можно указать период работ (начало/окончание).
/// </summary>
internal sealed class AddWorkDialog : Form
{
    private readonly TextBox _txtWork = new()
    {
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Left = 12,
        Top = 34,
        Width = 456,
        Height = 120,
    };

    private readonly CheckBox _chkPeriod = new() { Text = "Указать период работ", Left = 12, Top = 162, Width = 220 };
    private readonly DateTimePicker _dtStart = CreateDatePicker(76, 184);
    private readonly DateTimePicker _dtEnd = CreateDatePicker(320, 184);

    public AddWorkDialog(string passportName)
    {
        Text = "Добавить запись о работе";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(480, 296);
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;

        var label = new Label
        {
            Text = $"Описание работ для «{passportName}»:",
            Left = 12,
            Top = 10,
            Width = 456,
            Height = 20,
        };

        var lblStart = new Label { Text = "Начало:", Left = 12, Top = 188, Width = 60 };
        var lblEnd = new Label { Text = "Окончание:", Left = 238, Top = 188, Width = 78 };

        _chkPeriod.CheckedChanged += (_, _) =>
        {
            _dtStart.Enabled = _chkPeriod.Checked;
            _dtEnd.Enabled = _chkPeriod.Checked;
        };

        var okButton = new Button { Text = "Записать", DialogResult = DialogResult.OK, Left = 312, Top = 256, Width = 75 };
        var cancelButton = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Left = 393, Top = 256, Width = 75 };

        Controls.AddRange(new Control[]
        {
            label, _txtWork, _chkPeriod, lblStart, _dtStart, lblEnd, _dtEnd, okButton, cancelButton,
        });

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    public string WorkText => _txtWork.Text.Trim();

    /// <summary>Указан ли период работ.</summary>
    public bool HasPeriod => _chkPeriod.Checked;

    /// <summary>Начало работ (дд.ММ.гггг чч:мм) или пусто.</summary>
    public string StartText => HasPeriod
        ? _dtStart.Value.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture)
        : string.Empty;

    /// <summary>Окончание работ (дд.ММ.гггг чч:мм) или пусто.</summary>
    public string EndText => HasPeriod
        ? _dtEnd.Value.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture)
        : string.Empty;

    private static DateTimePicker CreateDatePicker(int left, int top) => new()
    {
        Left = left,
        Top = top,
        Width = 150,
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "dd.MM.yyyy HH:mm",
        ShowUpDown = true,
        Enabled = false,
        Value = DateTime.Today,
    };
}
