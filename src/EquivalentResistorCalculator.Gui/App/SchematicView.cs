using System.Numerics;
using Hexa.NET.ImGui;
using EquivalentResistorCalculator.Core.Models;
using EquivalentResistorCalculator.Core.Parsing;

namespace EquivalentResistorCalculator.Gui.App;

internal sealed class SchematicView
{
    private const float LeafWidth = 64f;
    private const float LeafHeight = 28f;
    private const float WireGap = 24f;
    private const float BranchGap = 14f;
    private const float JunctionStub = 16f;
    private const float TerminalStub = 16f;
    private const float DotRadius = 3f;
    private const float MinScale = 0.35f;

    private readonly record struct NodeLayout(float Width, float Height, float ConnectorY);

    public void Render(CombinationResult? selected, float regionHeight)
    {
        string header = selected is null
            ? "no combination selected"
            : SearchPanel.BuildClipboardText(selected);
        ImGui.TextUnformatted(header);

        ImGui.BeginChild("##schematic", new Vector2(0f, regionHeight), ImGuiChildFlags.Borders,
            ImGuiWindowFlags.HorizontalScrollbar);

        if (selected is null)
        {
            ImGui.TextDisabled("no combination selected");
        }
        else
        {
            DrawSchematic(selected.Tree);
        }

        ImGui.EndChild();
    }

    private void DrawSchematic(CombinationNode root)
    {
        var layout = ComputeLayout(root);

        // A parallel node's own junction dot sits exactly at the network boundary it occupies;
        // drawing a separate terminal dot/stub there would produce a coincident double dot.
        bool leftIsJunction = LeftBoundaryIsParallelJunction(root);
        bool rightIsJunction = root.Operation == CombinationOperation.Parallel;
        float leftStub = leftIsJunction ? 0f : TerminalStub;
        float rightStub = rightIsJunction ? 0f : TerminalStub;

        float totalWidth = layout.Width + leftStub + rightStub;
        float totalHeight = layout.Height;

        Vector2 avail = ImGui.GetContentRegionAvail();
        float scale = Math.Min(1f, Math.Min(avail.X / totalWidth, Math.Max(avail.Y, 1f) / totalHeight));
        scale = Math.Max(scale, MinScale);

        Vector2 drawSize = new(totalWidth * scale, totalHeight * scale);
        Vector2 childOrigin = ImGui.GetCursorScreenPos();
        float offsetX = Math.Max(0f, (avail.X - drawSize.X) / 2f);
        float offsetY = Math.Max(0f, (avail.Y - drawSize.Y) / 2f);
        Vector2 origin = childOrigin + new Vector2(offsetX, offsetY);

        var drawList = ImGui.GetWindowDrawList();
        uint wireColor = ImGui.GetColorU32(ImGuiCol.Text);
        uint boxColor = ImGui.GetColorU32(ImGuiCol.Text);

        float connectorY = layout.ConnectorY * scale;
        Vector2 rootOrigin = origin + new Vector2(leftStub * scale, 0f);

        if (!leftIsJunction)
        {
            Vector2 leftTerminal = origin + new Vector2(0f, connectorY);
            ImGui.AddCircleFilled(drawList, leftTerminal, DotRadius * scale, wireColor);
            ImGui.AddLine(drawList, leftTerminal, new Vector2(rootOrigin.X, leftTerminal.Y), wireColor, Math.Max(1f, scale));
        }

        DrawNode(drawList, root, rootOrigin, scale, layout, wireColor, boxColor);

        if (!rightIsJunction)
        {
            float networkRightX = rootOrigin.X + layout.Width * scale;
            Vector2 rightTerminal = new(networkRightX + rightStub * scale, origin.Y + connectorY);
            ImGui.AddLine(drawList, new Vector2(networkRightX, origin.Y + connectorY), rightTerminal, wireColor, Math.Max(1f, scale));
            ImGui.AddCircleFilled(drawList, rightTerminal, DotRadius * scale, wireColor);
        }

        ImGui.Dummy(drawSize);
    }

    private static bool LeftBoundaryIsParallelJunction(CombinationNode root)
    {
        var node = root;
        while (node.Operation == CombinationOperation.Series)
            node = node.Previous!;
        return node.Operation == CombinationOperation.Parallel;
    }

    private static NodeLayout ComputeLayout(CombinationNode node)
    {
        if (node.Previous is null)
            return new NodeLayout(LeafWidth, LeafHeight, LeafHeight / 2f);

        var prevLayout = ComputeLayout(node.Previous);
        var leafLayout = new NodeLayout(LeafWidth, LeafHeight, LeafHeight / 2f);

        return node.Operation == CombinationOperation.Series
            ? SeriesLayout(prevLayout, leafLayout)
            : ParallelLayout(prevLayout, leafLayout);
    }

    private static NodeLayout SeriesLayout(NodeLayout prev, NodeLayout leaf)
    {
        float top = Math.Max(prev.ConnectorY, leaf.ConnectorY);
        float bottom = Math.Max(prev.Height - prev.ConnectorY, leaf.Height - leaf.ConnectorY);
        return new NodeLayout(prev.Width + WireGap + leaf.Width, top + bottom, top);
    }

    private static NodeLayout ParallelLayout(NodeLayout top, NodeLayout bottom)
    {
        float branchWidth = Math.Max(top.Width, bottom.Width);
        float width = 2f * JunctionStub + branchWidth;
        float height = top.Height + BranchGap + bottom.Height;
        float topConnGlobal = top.ConnectorY;
        float bottomConnGlobal = top.Height + BranchGap + bottom.ConnectorY;
        float trunkY = (topConnGlobal + bottomConnGlobal) / 2f;
        return new NodeLayout(width, height, trunkY);
    }

    private void DrawNode(ImDrawListPtr dl, CombinationNode node, Vector2 originScreen, float scale,
        NodeLayout layout, uint wireColor, uint boxColor)
    {
        if (node.Previous is null)
        {
            DrawLeaf(dl, node.Resistor, originScreen, scale, layout, boxColor);
            return;
        }

        var prevLayout = ComputeLayout(node.Previous);
        var leafLayout = new NodeLayout(LeafWidth, LeafHeight, LeafHeight / 2f);
        float thickness = Math.Max(1f, scale);

        if (node.Operation == CombinationOperation.Series)
        {
            float top = Math.Max(prevLayout.ConnectorY, leafLayout.ConnectorY);

            var prevOrigin = originScreen + new Vector2(0f, (top - prevLayout.ConnectorY) * scale);
            DrawNode(dl, node.Previous, prevOrigin, scale, prevLayout, wireColor, boxColor);

            float wireY = originScreen.Y + top * scale;
            float prevRightX = originScreen.X + prevLayout.Width * scale;
            float leafLeftX = prevRightX + WireGap * scale;
            ImGui.AddLine(dl, new Vector2(prevRightX, wireY), new Vector2(leafLeftX, wireY), wireColor, thickness);

            var leafOrigin = new Vector2(leafLeftX, originScreen.Y + (top - leafLayout.ConnectorY) * scale);
            DrawLeaf(dl, node.Resistor, leafOrigin, scale, leafLayout, boxColor);
        }
        else
        {
            float branchWidth = Math.Max(prevLayout.Width, leafLayout.Width);
            float topConnGlobal = prevLayout.ConnectorY;
            float bottomConnGlobal = prevLayout.Height + BranchGap + leafLayout.ConnectorY;
            float trunkY = (topConnGlobal + bottomConnGlobal) / 2f;

            Vector2 leftDot = originScreen + new Vector2(0f, trunkY * scale);
            float leftBusX = originScreen.X + JunctionStub * scale;
            float rightBusX = originScreen.X + (JunctionStub + branchWidth) * scale;
            Vector2 rightDot = originScreen + new Vector2((2f * JunctionStub + branchWidth) * scale, trunkY * scale);

            ImGui.AddCircleFilled(dl, leftDot, DotRadius * scale, wireColor);
            ImGui.AddCircleFilled(dl, rightDot, DotRadius * scale, wireColor);
            ImGui.AddLine(dl, leftDot, new Vector2(leftBusX, leftDot.Y), wireColor, thickness);
            ImGui.AddLine(dl, rightDot, new Vector2(rightBusX, rightDot.Y), wireColor, thickness);
            ImGui.AddLine(dl, new Vector2(leftBusX, originScreen.Y + topConnGlobal * scale),
                new Vector2(leftBusX, originScreen.Y + bottomConnGlobal * scale), wireColor, thickness);
            ImGui.AddLine(dl, new Vector2(rightBusX, originScreen.Y + topConnGlobal * scale),
                new Vector2(rightBusX, originScreen.Y + bottomConnGlobal * scale), wireColor, thickness);

            var prevOrigin = originScreen + new Vector2(JunctionStub * scale, 0f);
            DrawNode(dl, node.Previous, prevOrigin, scale, prevLayout, wireColor, boxColor);
            if (prevLayout.Width < branchWidth)
                ImGui.AddLine(dl, new Vector2(originScreen.X + (JunctionStub + prevLayout.Width) * scale, originScreen.Y + topConnGlobal * scale),
                    new Vector2(rightBusX, originScreen.Y + topConnGlobal * scale), wireColor, thickness);

            var leafOrigin = originScreen + new Vector2(JunctionStub * scale, (prevLayout.Height + BranchGap) * scale);
            DrawLeaf(dl, node.Resistor, leafOrigin, scale, leafLayout, boxColor);
            if (leafLayout.Width < branchWidth)
                ImGui.AddLine(dl, new Vector2(originScreen.X + (JunctionStub + leafLayout.Width) * scale, originScreen.Y + bottomConnGlobal * scale),
                    new Vector2(rightBusX, originScreen.Y + bottomConnGlobal * scale), wireColor, thickness);
        }
    }

    private static void DrawLeaf(ImDrawListPtr dl, Resistor resistor, Vector2 originScreen, float scale,
        NodeLayout layout, uint boxColor)
    {
        Vector2 topLeft = originScreen;
        Vector2 bottomRight = originScreen + new Vector2(layout.Width * scale, layout.Height * scale);
        ImGui.AddRect(dl, topLeft, bottomRight, boxColor, 2f * scale, ImDrawFlags.None, Math.Max(1f, scale));

        string label = string.IsNullOrEmpty(resistor.Label)
            ? ResistanceParser.Format(resistor.Value)
            : resistor.Label;
        Vector2 textSize = ImGui.CalcTextSize(label);
        Vector2 textPos = topLeft + (bottomRight - topLeft) / 2f - textSize / 2f;
        ImGui.AddText(dl, textPos, boxColor, label);
    }
}
