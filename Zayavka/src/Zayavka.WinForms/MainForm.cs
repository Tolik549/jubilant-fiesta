using System.Reflection;
using Zayavka.Domain.Abstractions;
using Zayavka.WinForms.Views;

namespace Zayavka.WinForms;

/// <summary>
/// Главное окно приложения с двумя вкладками:
/// «Паспорта оборудования» и «Импорт заявок».
/// </summary>
public sealed class MainForm : Form
{
    private readonly IPassportRepository _passportRepository;
    private readonly IRequestJournalService _journalService;

    public MainForm(IPassportRepository passportRepository, IRequestJournalService journalService)
    {
        _passportRepository = passportRepository ?? throw new ArgumentNullException(nameof(passportRepository));
        _journalService = journalService ?? throw new ArgumentNullException(nameof(journalService));
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = $"Импорт заявок {VersionText()}";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1230, 634);
        MinimumSize = new Size(900, 520);
        Font = new Font("Segoe UI", 9F);

        var tabControl = new TabControl { Dock = DockStyle.Fill };

        var passportPage = new TabPage("Паспорта оборудования") { Padding = new Padding(6) };
        passportPage.Controls.Add(new PassportTabControl(_passportRepository) { Dock = DockStyle.Fill });

        var importPage = new TabPage("Импорт заявок") { Padding = new Padding(6) };
        importPage.Controls.Add(new ImportRequestsTabControl(_journalService, _passportRepository) { Dock = DockStyle.Fill });

        tabControl.TabPages.Add(passportPage);
        tabControl.TabPages.Add(importPage);

        Controls.Add(tabControl);
    }

    /// <summary>Версия сборки для отображения в заголовке окна.</summary>
    private static string VersionText()
    {
        var info = typeof(MainForm).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(info))
        {
            return info;
        }

        return typeof(MainForm).Assembly.GetName().Version?.ToString(3) ?? string.Empty;
    }
}
