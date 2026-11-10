using Zayavka.Domain;

namespace Zayavka.WinForms.Dialogs;

/// <summary>
/// Диалог выбора паспорта оборудования из списка (с поиском по названию).
/// </summary>
internal sealed class PassportPickerDialog : Form
{
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly TextBox _search = new() { Dock = DockStyle.Top, PlaceholderText = "Поиск..." };
    private readonly List<PassportRecord> _all;

    public PassportPickerDialog(string prompt, IReadOnlyList<PassportRecord> passports)
    {
        _all = passports.ToList();

        Text = "Выбор паспорта оборудования";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 380);
        MinimumSize = new Size(400, 300);
        ShowInTaskbar = false;

        _list.Format += (_, e) => e.Value = e.ListItem is PassportRecord p ? p.Name : e.Value;
        _list.DoubleClick += (_, _) =>
        {
            if (_list.SelectedItem is not null)
            {
                DialogResult = DialogResult.OK;
            }
        };
        _search.TextChanged += (_, _) => Fill(_search.Text);

        var label = new Label
        {
            Text = prompt,
            Dock = DockStyle.Top,
            Height = 34,
            Padding = new Padding(6, 8, 6, 0),
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 42,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(6),
        };
        var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, AutoSize = true };
        var ok = new Button { Text = "Выбрать", DialogResult = DialogResult.OK, AutoSize = true };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);

        Controls.Add(_list);
        Controls.Add(_search);
        Controls.Add(label);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;

        Fill(string.Empty);
    }

    /// <summary>Выбранный паспорт (или <c>null</c>, если выбор отменён).</summary>
    public PassportRecord? SelectedPassport => _list.SelectedItem as PassportRecord;

    private void Fill(string? filter)
    {
        var query = (filter ?? string.Empty).Trim();

        _list.BeginUpdate();
        try
        {
            _list.Items.Clear();

            foreach (var passport in _all)
            {
                if (query.Length > 0 &&
                    passport.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                _list.Items.Add(passport);
            }

            if (_list.Items.Count > 0)
            {
                _list.SelectedIndex = 0;
            }
        }
        finally
        {
            _list.EndUpdate();
        }
    }
}
