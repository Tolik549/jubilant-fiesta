using System.Globalization;
using Zayavka.Domain;
using Zayavka.Domain.Abstractions;

namespace Zayavka.Infrastructure;

/// <summary>
/// Репозиторий паспортов оборудования. Сканирует подпапки верхнего уровня папки
/// паспортов (имя подпапки = название паспорта) и ведёт записи в файл паспорта
/// (Excel — приоритетный формат, CSV — резервный).
/// </summary>
public sealed class PassportRepository : IPassportRepository
{
    /// <summary>Имя файла паспорта Excel, создаваемого по умолчанию.</summary>
    public const string DefaultExcelFileName = "паспорт.xlsx";

    private static readonly string[] PassportFileNames =
    {
        "паспорт.xlsx",
        "паспорт.xlsm",
        "паспорт.csv",
    };

    private readonly IAppConfig _config;
    private readonly IReadOnlyList<IPassportStore> _stores;
    private readonly Func<DateTime> _today;

    public PassportRepository(
        IAppConfig config,
        IEnumerable<IPassportStore> stores,
        Func<DateTime>? todayProvider = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _stores = (stores ?? throw new ArgumentNullException(nameof(stores))).ToList();
        _today = todayProvider ?? (() => DateTime.Today);
    }

    public string PassportDirectory
    {
        get => _config.PassportDirectory;
        set => _config.SetPassportDirectory(value);
    }

    public IReadOnlyList<PassportRecord> GetAll()
    {
        var directory = PassportDirectory;
        if (!Directory.Exists(directory))
        {
            return Array.Empty<PassportRecord>();
        }

        var result = new List<PassportRecord>();

        List<string> subFolders;
        try
        {
            subFolders = Directory.EnumerateDirectories(directory)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return result;
        }

        foreach (var subFolder in subFolders)
        {
            var record = new PassportRecord
            {
                Name = Path.GetFileName(subFolder),
                Path = subFolder,
            };

            record.PassportFile = FindPassportFile(subFolder);
            record.LastWork = GetLastWork(record.PassportFile);
            result.Add(record);
        }

        return result;
    }

    public IReadOnlyList<PassportWorkEntry> GetWorkEntries(PassportRecord passport)
    {
        ArgumentNullException.ThrowIfNull(passport);

        var file = ResolveExistingFile(passport);
        var store = file is null ? null : FindStore(file);
        if (file is null || store is null)
        {
            return Array.Empty<PassportWorkEntry>();
        }

        try
        {
            return store.ReadWorkEntries(file);
        }
        catch
        {
            return Array.Empty<PassportWorkEntry>();
        }
    }

    public IReadOnlyList<PassportRecord> FindByEquipment(string equipmentDescriptor)
    {
        var descriptor = (equipmentDescriptor ?? string.Empty).Trim();
        if (descriptor.Length == 0)
        {
            return Array.Empty<PassportRecord>();
        }

        var all = GetAll();
        if (all.Count == 0)
        {
            return Array.Empty<PassportRecord>();
        }

        var normalized = NormalizeEquipment(descriptor);

        // 1. Точное совпадение нормализованных имён (чтобы «1КЭН-1» не давал «1КЭН-10»).
        var exact = all
            .Where(p => string.Equals(NormalizeEquipment(p.Name), normalized, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (exact.Count > 0)
        {
            return exact;
        }

        // 2. Частичное совпадение в обе стороны (как в исходном проекте).
        return all
            .Where(p =>
            {
                var name = NormalizeEquipment(p.Name);
                return name.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                       || normalized.Contains(name, StringComparison.OrdinalIgnoreCase);
            })
            .ToList();
    }

    /// <summary>Срезает служебный префикс «Паспорт на »/«Паспорт » из названия.</summary>
    private static string NormalizeEquipment(string name)
    {
        var value = (name ?? string.Empty).Trim();

        foreach (var prefix in new[] { "Паспорт на ", "Паспорт " })
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return value[prefix.Length..].Trim();
            }
        }

        return value;
    }

    public bool TryCreateNew(string name, out PassportRecord? record, out string? error)
    {
        record = null;
        error = null;

        var trimmedName = (name ?? string.Empty).Trim();
        if (trimmedName.Length == 0)
        {
            error = "Не задано название паспорта.";
            return false;
        }

        if (trimmedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            error = "Название содержит недопустимые символы.";
            return false;
        }

        var root = PassportDirectory;

        try
        {
            Directory.CreateDirectory(root);
        }
        catch (Exception ex)
        {
            error = $"Не удалось получить доступ к папке паспортов: {ex.Message}";
            return false;
        }

        var folder = Path.Combine(root, trimmedName);
        if (Directory.Exists(folder))
        {
            error = $"Паспорт «{trimmedName}» уже существует.";
            return false;
        }

        try
        {
            Directory.CreateDirectory(folder);

            var file = Path.Combine(folder, DefaultExcelFileName);
            var store = FindStore(file)
                ?? throw new InvalidOperationException($"Нет хранилища для формата «{Path.GetExtension(file)}».");

            store.CreateEmpty(file, new PassportWorkEntry(FormatDate(_today()), "Создание паспорта оборудования"));

            record = new PassportRecord
            {
                Name = trimmedName,
                Path = folder,
                PassportFile = file,
                LastWork = GetLastWork(file),
            };
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public void AddWork(PassportRecord passport, string workText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workText);
        AddWork(passport, new PassportWorkEntry(FormatDate(_today()), workText.Trim()));
    }

    public void AddWork(PassportRecord passport, PassportWorkEntry entry)
    {
        ArgumentNullException.ThrowIfNull(passport);
        ArgumentNullException.ThrowIfNull(entry);

        var file = EnsurePassportFile(passport);

        var store = FindStore(file)
            ?? throw new InvalidOperationException($"Формат файла не поддерживается: {file}");

        store.AddWorkEntry(file, entry);
        passport.LastWork = GetLastWork(file);
    }

    /// <summary>Возвращает файл паспорта, создавая Excel-файл при его отсутствии.</summary>
    private string EnsurePassportFile(PassportRecord passport)
    {
        var file = ResolveExistingFile(passport);
        if (file is not null)
        {
            return file;
        }

        // Файла нет — создаём Excel-паспорт (приоритетный формат).
        Directory.CreateDirectory(passport.Path);
        file = Path.Combine(passport.Path, DefaultExcelFileName);

        var creator = FindStore(file)
            ?? throw new InvalidOperationException($"Нет хранилища для формата «{Path.GetExtension(file)}».");

        creator.CreateEmpty(file, new PassportWorkEntry(FormatDate(_today()), "Создание паспорта оборудования"));
        passport.PassportFile = file;
        return file;
    }

    private static string? ResolveExistingFile(PassportRecord passport)
    {
        if (!string.IsNullOrEmpty(passport.PassportFile) && File.Exists(passport.PassportFile))
        {
            return passport.PassportFile;
        }

        return FindPassportFile(passport.Path);
    }

    private string GetLastWork(string? file)
    {
        if (string.IsNullOrEmpty(file) || !File.Exists(file))
        {
            return "Нет работ";
        }

        var store = FindStore(file);
        if (store is null)
        {
            return "Нет работ";
        }

        try
        {
            var entries = store.ReadWorkEntries(file);
            if (entries.Count == 0)
            {
                return "Нет работ";
            }

            // Выбираем запись с максимальной датой; при равных датах — 
            // ту, что добавлена позже (ниже в файле, больше индекс строки).
            var best = entries
                .Select((entry, index) => (Entry: entry, Order: ParseDateOrder(entry.Date), Index: index))
                .OrderByDescending(x => x.Order)
                .ThenByDescending(x => x.Index)
                .First();

            return string.IsNullOrWhiteSpace(best.Entry.WorkName) ? "Нет работ" : best.Entry.WorkName;
        }
        catch
        {
            return "Нет работ";
        }
    }

    private IPassportStore? FindStore(string filePath) =>
        _stores.FirstOrDefault(store => store.CanHandle(filePath));

    private static string? FindPassportFile(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return null;
        }

        // 1. Явно заданные имена (Excel в приоритете).
        foreach (var name in PassportFileNames)
        {
            var candidate = Path.Combine(folder, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        // 2. Любой Excel-файл.
        foreach (var pattern in new[] { "*.xlsx", "*.xlsm" })
        {
            var candidate = Directory.EnumerateFiles(folder, pattern)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (candidate is not null)
            {
                return candidate;
            }
        }

        // 3. Любой CSV.
        return Directory.EnumerateFiles(folder, "*.csv")
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static string FormatDate(DateTime date) =>
        date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    private static long ParseDateOrder(string value)
    {
        if (DateTime.TryParseExact(
                value,
                "dd.MM.yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            return date.Ticks;
        }

        return long.MinValue;
    }
}
