using Hexa.NET.ImGui;

namespace EquivalentResistorCalculator.Gui.App;

internal sealed class MainWindow : IDisposable
{
    public bool ShouldClose { get; private set; }

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

        ImGui.End();
    }

    public void Dispose()
    {
    }
}
