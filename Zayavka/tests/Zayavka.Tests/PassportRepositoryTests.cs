using Zayavka.Domain;
using Zayavka.Domain.Abstractions;
using Zayavka.Infrastructure;
using Zayavka.Infrastructure.Configuration;
using Zayavka.Infrastructure.Csv;
using Zayavka.Infrastructure.Excel;

namespace Zayavka.Tests;

public sealed class PassportRepositoryTests : IDisposable
{
    private readonly string _root;
    private readonly IniAppConfig _config;
    private readonly PassportRepository _repository;

    public PassportRepositoryTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "zayavka_repo_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        _config = new IniAppConfig(Path.Combine(_root, "passport_config.ini"));
        _config.SetPassportDirectory(Path.Combine(_root, "Паспорта"));

        _repository = new PassportRepository(
            _config,
            new IPassportStore[] { new ExcelPassportStore(), new CsvPassportStore() },
            () => new DateTime(2025, 11, 3));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Игнорируем проблемы очистки временных файлов.
        }
    }

    [Fact]
    public void PassportDirectory_IsPersistedToIniFile()
    {
        var saved = File.ReadAllText(_config.ConfigPath);

        Assert.Contains(_config.PassportDirectory, saved);
        Assert.Contains("passport_config.ini", _config.ConfigPath);
    }

    [Fact]
    public void TryCreateNew_CreatesFolderAndExcelPassport()
    {
        var created = _repository.TryCreateNew("1МНР-1", out var record, out var error);

        Assert.True(created, error);
        Assert.NotNull(record);
        Assert.Equal("1МНР-1", record!.Name);
        Assert.Equal(Path.Combine(_config.PassportDirectory, "1МНР-1"), record.Path);
        Assert.NotNull(record.PassportFile);
        Assert.True(File.Exists(record.PassportFile!));
        Assert.EndsWith("паспорт.xlsx", record.PassportFile!);
    }

    [Fact]
    public void TryCreateNew_ExistingPassport_ReturnsError()
    {
        _repository.TryCreateNew("1КЭН-1", out _, out _);

        var createdAgain = _repository.TryCreateNew("1КЭН-1", out _, out var error);

        Assert.False(createdAgain);
        Assert.NotNull(error);
    }

    [Fact]
    public void AddWork_WritesIntoExcelFile_AndUpdatesLastWork()
    {
        _repository.TryCreateNew("1КЭН-2", out var record, out _);

        _repository.AddWork(record!, "Замена подшипника");

        var reloaded = _repository.GetAll().Single(p => p.Name == "1КЭН-2");
        Assert.Equal("Замена подшипника", reloaded.LastWork);

        var entries = _repository.GetWorkEntries(reloaded);
        Assert.Equal(2, entries.Count);
        Assert.Equal("03.11.2025", entries[1].Date);
        Assert.Equal("Замена подшипника", entries[1].WorkName);
    }

    [Fact]
    public void GetAll_ReturnsOnlyFolders()
    {
        _repository.TryCreateNew("1КЭН-3", out _, out _);
        File.WriteAllText(Path.Combine(_config.PassportDirectory, "readme.txt"), "текст");

        var all = _repository.GetAll();

        Assert.Single(all);
        Assert.Equal("1КЭН-3", all[0].Name);
    }

    [Fact]
    public void FindByEquipment_ExactMatchPreferredOverSubstring()
    {
        _repository.TryCreateNew("Паспорт на 1КЭН-1", out _, out _);
        _repository.TryCreateNew("Паспорт на 1КЭН-10", out _, out _);

        var matches = _repository.FindByEquipment("1КЭН-1");

        var match = Assert.Single(matches);
        Assert.Equal("Паспорт на 1КЭН-1", match.Name);
    }

    [Fact]
    public void FindByEquipment_NoMatches_ReturnsEmpty()
    {
        _repository.TryCreateNew("Паспорт на 1МНР-1", out _, out _);

        Assert.Empty(_repository.FindByEquipment("9XXX-9"));
    }

    [Fact]
    public void AddWork_WithEntry_InsertsChronologically()
    {
        _repository.TryCreateNew("1КЭН-5", out var record, out _);

        _repository.AddWork(record!, new PassportWorkEntry
        {
            Date = "01.10.2025",
            WorkName = "Ранняя работа",
            RequestNumber = "70",
        });

        var entries = _repository.GetWorkEntries(record!);
        Assert.Equal(2, entries.Count);

        // Запись от 01.10.2025 встала раньше стартовой от 03.11.2025.
        Assert.Equal("01.10.2025", entries[0].Date);
        Assert.Equal("Ранняя работа", entries[0].WorkName);
        Assert.Equal("70", entries[0].RequestNumber);
        Assert.Equal("03.11.2025", entries[1].Date);
    }
}
