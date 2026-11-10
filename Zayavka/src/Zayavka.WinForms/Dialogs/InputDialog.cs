namespace Zayavka.WinForms.Dialogs;

/// <summary>
/// Простой модальный диалог ввода одной строки
/// (аналог <c>InputBox</c> из исходного проекта на Free Pascal).
/// </summary>
internal static class InputDialog
{
    public static string? Show(IWin32Window? owner, string title, string prompt, string defaultValue = "")
    {
        using var form = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(420, 150),
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
        };

        var label = new Label { Text = prompt, Left = 12, Top = 12, Width = 396, Height = 40 };
        var textBox = new TextBox { Left = 12, Top = 58, Width = 396, Text = defaultValue };
        var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 252, Top = 96, Width = 75 };
        var cancelButton = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Left = 333, Top = 96, Width = 75 };

        form.Controls.AddRange(new Control[] { label, textBox, okButton, cancelButton });
        form.AcceptButton = okButton;
        form.CancelButton = cancelButton;

        var result = form.ShowDialog(owner);
        return result == DialogResult.OK ? textBox.Text : null;
    }
}
