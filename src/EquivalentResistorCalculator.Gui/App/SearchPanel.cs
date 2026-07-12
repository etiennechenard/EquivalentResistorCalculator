using System.Numerics;
using Hexa.NET.ImGui;
using EquivalentResistorCalculator.Core.Models;
using EquivalentResistorCalculator.Core.Parsing;
using EquivalentResistorCalculator.Core.Search;
using EquivalentResistorCalculator.Core.Stock;
using EquivalentResistorCalculator.Gui.Settings;

namespace EquivalentResistorCalculator.Gui.App;

internal sealed class SearchPanel
{
    private static readonly string[] PackageFilterLabels = { "All", "ThroughHole", "SMD" };
    private static readonly Vector4 ErrorColor = new(1f, 0.4f, 0.4f, 1f);

    private readonly SettingsService _settingsService;
    private readonly StockRepository _stockRepository;
    private readonly SearchCoordinator _searchCoordinator;

    private string _targetText = string.Empty;
    private string? _parseError;
    private int _depth;
    private int _packageFilterIndex;

    public SearchPanel(SettingsService settingsService, StockRepository stockRepository, SearchCoordinator searchCoordinator)
    {
        _settingsService = settingsService;
        _stockRepository = stockRepository;
        _searchCoordinator = searchCoordinator;

        _depth = _settingsService.Current.MaxDepth;
        _packageFilterIndex = _settingsService.Current.PackageFilter switch
        {
            PackageType.ThroughHole => 1,
            PackageType.SMD => 2,
            _ => 0,
        };
    }

    public void Render()
    {
        ImGui.Text("Target:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120f);
        if (ImGui.InputText("##target", ref _targetText, 32))
            OnTargetChanged();

        if (_parseError is not null)
        {
            ImGui.SameLine();
            ImGui.TextColored(ErrorColor, _parseError);
        }

        ImGui.SameLine();
        ImGui.Text("Depth:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(60f);
        int depth = _depth;
        if (ImGui.InputInt("##depth", ref depth))
        {
            _depth = Math.Clamp(depth, 1, 20);
            _settingsService.Current.MaxDepth = _depth;
            _settingsService.Save();
            Resubmit();
        }

        ImGui.SameLine();
        ImGui.Text("Filter:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120f);
        int filterIndex = _packageFilterIndex;
        if (ImGui.Combo("##filter", ref filterIndex, PackageFilterLabels, PackageFilterLabels.Length))
        {
            _packageFilterIndex = filterIndex;
            _settingsService.Current.PackageFilter = filterIndex switch
            {
                1 => PackageType.ThroughHole,
                2 => PackageType.SMD,
                _ => null,
            };
            _settingsService.Save();
            Resubmit();
        }

        ImGui.Separator();
        RenderResultsPlaceholder();
    }

    private void RenderResultsPlaceholder()
    {
        var snapshot = _searchCoordinator.Snapshot;
        string text = snapshot.Status switch
        {
            SearchStatus.NoStock => "No resistors in stock matching the current filter",
            SearchStatus.Searching => "Searching…",
            SearchStatus.Done when snapshot.Results.Count == 0 => "No combination found",
            SearchStatus.Done => $"{snapshot.Results.Count} result(s) found (table rendering not yet implemented)",
            _ => "Enter a target resistance to search",
        };
        ImGui.TextUnformatted(text);
    }

    private void OnTargetChanged()
    {
        if (string.IsNullOrWhiteSpace(_targetText))
        {
            _parseError = null;
            return;
        }

        if (ResistanceParser.TryParse(_targetText, out _))
        {
            _parseError = null;
            Resubmit();
        }
        else
        {
            _parseError = "invalid";
        }
    }

    private void Resubmit()
    {
        if (!ResistanceParser.TryParse(_targetText, out double ohms))
            return;

        string fileName = _stockRepository.SelectedFileName ?? _stockRepository.Select(_settingsService.Current.StockFile);
        var loadResult = _stockRepository.Load(fileName);
        var filtered = StockRepository.FilterByPackage(loadResult.Resistors, _settingsService.Current.PackageFilter);
        _searchCoordinator.Submit(ohms, _depth, filtered);
    }
}
