using System.Globalization;
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
    private enum SortColumn { Rank, Result, ErrorPercent, Depth }

    internal const string ErrorPercentFormat = "+0.000;-0.000;0.000";
    private const float MinSchematicHeight = 120f;

    private static readonly string[] PackageFilterLabels = { "All", "ThroughHole", "SMD" };
    private static readonly Vector4 ErrorColor = new(1f, 0.4f, 0.4f, 1f);

    private readonly SettingsService _settingsService;
    private readonly StockRepository _stockRepository;
    private readonly SearchCoordinator _searchCoordinator;

    private string _targetText = string.Empty;
    private string? _parseError;
    private int _depth;
    private int _packageFilterIndex;
    private SortColumn _sortColumn = SortColumn.Rank;
    private bool _sortAscending = true;
    private IReadOnlyList<CombinationResult>? _lastSnapshotResults;
    private CombinationResult? _selectedResult;
    private readonly SchematicView _schematicView = new();

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

    public void ResubmitCurrentSearch() => Resubmit();

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
        RenderResultsArea();
    }

    private void RenderResultsArea()
    {
        var snapshot = _searchCoordinator.Snapshot;

        if (snapshot.Results.Count > 0)
        {
            if (!ReferenceEquals(snapshot.Results, _lastSnapshotResults))
            {
                _lastSnapshotResults = snapshot.Results;
                _selectedResult = snapshot.Results[0];
            }

            RenderResultsTable(snapshot.Results);
        }
        else
        {
            _lastSnapshotResults = null;
            _selectedResult = null;

            string text = snapshot.Status switch
            {
                SearchStatus.NoStock => "No resistors in stock matching the current filter",
                SearchStatus.Searching => "Searching...",
                SearchStatus.Done => "No combination found",
                _ => "Enter a target resistance to search",
            };
            ImGui.TextUnformatted(text);
        }

        ImGui.Separator();

        float footerReserve = ImGui.GetFrameHeightWithSpacing() * 2f;
        float schematicHeight = Math.Max(MinSchematicHeight, ImGui.GetContentRegionAvail().Y - footerReserve);
        _schematicView.Render(_selectedResult, schematicHeight);
    }

    private void RenderResultsTable(IReadOnlyList<CombinationResult> results)
    {
        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Resizable;
        if (!ImGui.BeginTable("##results", 6, flags))
            return;

        ImGui.TableSetupColumn("#");
        ImGui.TableSetupColumn("Combination");
        ImGui.TableSetupColumn("Result");
        ImGui.TableSetupColumn("Error %");
        ImGui.TableSetupColumn("Depth");
        ImGui.TableSetupColumn("");

        ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
        ImGui.TableSetColumnIndex(0);
        ImGui.TableHeader("#");
        ImGui.TableSetColumnIndex(1);
        ImGui.TableHeader("Combination");
        ImGui.TableSetColumnIndex(2);
        RenderSortableHeader("Result", SortColumn.Result);
        ImGui.TableSetColumnIndex(3);
        RenderSortableHeader("Error %", SortColumn.ErrorPercent);
        ImGui.TableSetColumnIndex(4);
        RenderSortableHeader("Depth", SortColumn.Depth);
        ImGui.TableSetColumnIndex(5);
        ImGui.TableHeader("");

        foreach (var (rank, result) in SortRows(results))
        {
            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            bool isSelected = ReferenceEquals(result, _selectedResult);
            if (ImGui.Selectable(rank.ToString(CultureInfo.InvariantCulture), isSelected,
                    ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowOverlap))
                _selectedResult = result;

            ImGui.TableSetColumnIndex(1);
            ImGui.TextUnformatted(result.Description);

            ImGui.TableSetColumnIndex(2);
            ImGui.TextUnformatted(ResistanceParser.Format(result.TotalResistance));

            ImGui.TableSetColumnIndex(3);
            ImGui.TextUnformatted(result.ErrorPercent.ToString(ErrorPercentFormat, CultureInfo.InvariantCulture));

            ImGui.TableSetColumnIndex(4);
            ImGui.Text(result.Depth.ToString(CultureInfo.InvariantCulture));

            ImGui.TableSetColumnIndex(5);
            if (ImGui.SmallButton($"Copy##{rank}"))
                ImGui.SetClipboardText(BuildClipboardText(result));
        }

        ImGui.EndTable();
    }

    private void RenderSortableHeader(string label, SortColumn column)
    {
        bool isActive = _sortColumn == column;
        string text = isActive ? label + (_sortAscending ? " ^" : " v") : label;
        if (ImGui.Selectable(text, isActive))
        {
            if (isActive)
                _sortAscending = !_sortAscending;
            else
            {
                _sortColumn = column;
                _sortAscending = true;
            }
        }
    }

    private IEnumerable<(int Rank, CombinationResult Result)> SortRows(IReadOnlyList<CombinationResult> results)
    {
        var ranked = results.Select((result, index) => (Rank: index + 1, Result: result));

        IOrderedEnumerable<(int Rank, CombinationResult Result)> ordered = _sortColumn switch
        {
            SortColumn.Result => ranked.OrderBy(x => x.Result.TotalResistance),
            SortColumn.ErrorPercent => ranked.OrderBy(x => Math.Abs(x.Result.ErrorPercent)),
            SortColumn.Depth => ranked.OrderBy(x => x.Result.Depth),
            _ => ranked.OrderBy(x => x.Rank),
        };

        return _sortAscending ? ordered : ordered.Reverse();
    }

    internal static string BuildClipboardText(CombinationResult result)
        => $"{result.Description} = {ResistanceParser.Format(result.TotalResistance)} ({result.ErrorPercent.ToString(ErrorPercentFormat, CultureInfo.InvariantCulture)}%)";

    private void OnTargetChanged()
    {
        if (string.IsNullOrWhiteSpace(_targetText))
        {
            _parseError = null;
            Resubmit();
            return;
        }

        _parseError = ResistanceParser.TryParse(_targetText, out _) ? null : "invalid";
        Resubmit();
    }

    private void Resubmit()
    {
        if (!ResistanceParser.TryParse(_targetText, out double ohms))
        {
            _searchCoordinator.Clear();
            return;
        }

        string fileName = _stockRepository.SelectedFileName ?? _stockRepository.Select(_settingsService.Current.StockFile);
        var loadResult = _stockRepository.Load(fileName);
        var filtered = StockRepository.FilterByPackage(loadResult.Resistors, _settingsService.Current.PackageFilter);
        _searchCoordinator.Submit(ohms, _depth, filtered);
    }
}
