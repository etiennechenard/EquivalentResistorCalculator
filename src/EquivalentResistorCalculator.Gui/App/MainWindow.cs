using System.Diagnostics;
using Hexa.NET.ImGui;
using EquivalentResistorCalculator.Core.Search;
using EquivalentResistorCalculator.Core.Stock;
using EquivalentResistorCalculator.Gui.Settings;

namespace EquivalentResistorCalculator.Gui.App;

internal sealed class MainWindow : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly SettingsService _settingsService;
    private readonly StockRepository _stockRepository;
    private readonly SearchCoordinator _searchCoordinator;
    private readonly SearchPanel _searchPanel;
    private readonly Stopwatch _pollStopwatch = Stopwatch.StartNew();

    private IReadOnlyList<string> _stockFiles = Array.Empty<string>();
    private string _selectedFileName = string.Empty;
    private string _stockSummary = string.Empty;

    public bool ShouldClose { get; private set; }

    public MainWindow()
    {
        _settingsService = new SettingsService();

        var stocksFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EquivalentResistorCalculator",
            "stocks");
        _stockRepository = new StockRepository(stocksFolder);

        LoadStockFile(_settingsService.Current.StockFile);

        _searchCoordinator = new SearchCoordinator();
        _searchPanel = new SearchPanel(_settingsService, _stockRepository, _searchCoordinator);
    }

    public void Render()
    {
        var vp = ImGui.GetMainViewport();

        var noDecor = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse
                    | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove
                    | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus;

        ImGui.SetNextWindowPos(vp.WorkPos);
        ImGui.SetNextWindowSize(vp.WorkSize);
        ImGui.SetNextWindowViewport(vp.ID);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.Begin("##main", noDecor);
        ImGui.PopStyleVar(2);

        if (_pollStopwatch.Elapsed >= PollInterval)
        {
            _pollStopwatch.Restart();
            PollStockChanges();
        }

        _searchPanel.Render();
        ImGui.Separator();
        RenderStockBar();

        ImGui.End();
    }

    public void Dispose()
    {
        _searchCoordinator.Dispose();
    }

    private void RenderStockBar()
    {
        ImGui.Text("Stock:");
        ImGui.SameLine();

        ImGui.SetNextItemWidth(200f);
        if (ImGui.BeginCombo("##stockfile", _selectedFileName))
        {
            foreach (var file in _stockFiles)
            {
                bool isSelected = string.Equals(file, _selectedFileName, StringComparison.OrdinalIgnoreCase);
                if (ImGui.Selectable(file, isSelected) && !isSelected)
                {
                    LoadStockFile(file);
                    _searchPanel.ResubmitCurrentSearch();
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        ImGui.TextUnformatted(_stockSummary);

        ImGui.SameLine();
        if (ImGui.Button("Edit stock file"))
            ShellExecute(Path.Combine(_stockRepository.StocksFolder, _selectedFileName));

        ImGui.SameLine();
        if (ImGui.Button("Open stocks folder"))
            ShellExecute(_stockRepository.StocksFolder);

        if (_searchCoordinator.Snapshot.Status == SearchStatus.Searching)
        {
            ImGui.SameLine();
            ImGui.TextUnformatted("Searching…");
        }
    }

    private static void ShellExecute(string path)
        => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();

    private void PollStockChanges()
    {
        if (_stockRepository.HasSelectedFileContentChanged())
        {
            LoadStockFile(_selectedFileName);
            _searchPanel.ResubmitCurrentSearch();
            return;
        }

        if (_stockRepository.HasFolderListingChanged())
        {
            var names = _stockRepository.EnumerateStockFiles();
            bool selectedStillExists = names.Any(f => string.Equals(f, _selectedFileName, StringComparison.OrdinalIgnoreCase));
            if (selectedStillExists)
            {
                _stockFiles = names;
            }
            else
            {
                LoadStockFile(_selectedFileName);
                _searchPanel.ResubmitCurrentSearch();
            }
        }
    }

    private void LoadStockFile(string? requestedFileName)
    {
        string selected = _stockRepository.Select(requestedFileName);
        var result = _stockRepository.Load(selected);

        _stockFiles = _stockRepository.EnumerateStockFiles();
        _selectedFileName = result.FileName;

        int matchingCount = StockRepository.FilterByPackage(result.Resistors, _settingsService.Current.PackageFilter).Count;
        _stockSummary = result.SkippedRowCount > 0
            ? $"{result.FileName}: {matchingCount} rows ({result.SkippedRowCount} skipped)"
            : $"{result.FileName}: {matchingCount} rows";

        _settingsService.Current.StockFile = _selectedFileName;
        _settingsService.Save();
    }
}
