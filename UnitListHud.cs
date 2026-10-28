using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class UnitListHud : CanvasLayer
{
    [Export] public int PanelWidth = 240;
    [Export] public float PanelHeightFraction = 0.55f; // share of the screen height the list may use, from the top
    [Export] public int Margin = 12;

    private static readonly Color NormalColor = new Color(0.12f, 0.12f, 0.14f, 0.85f);
    private static readonly Color SelectedColor = new Color(0.16f, 0.30f, 0.18f, 0.95f);
    private static readonly Color SelectedBorder = new Color(0.2f, 1f, 0.3f);
    private static readonly Color NoBorder = new Color(0f, 0f, 0f, 0f);

    private sealed class Row
    {
        public PanelContainer Card;
        public StyleBoxFlat Style;
        public Label Label;
        public ProgressBar Bar;
        public StyleBoxFlat Fill;
    }

    private readonly Dictionary<Squad, Row> _rows = new();
    private VBoxContainer _list;
    private SelectionManager _selection;

    public override void _Ready()
    {
        // Outer panel pinned to the left edge. Its bottom edge is a fraction of the screen height,
        // which leaves the rest of the left side empty for future HUD elements.
        var panel = new PanelContainer
        {
            AnchorLeft = 0,
            AnchorTop = 0,
            AnchorRight = 0,
            AnchorBottom = PanelHeightFraction,
            OffsetLeft = Margin,
            OffsetTop = Margin,
            OffsetRight = Margin + PanelWidth,
            OffsetBottom = 0
        };

        var panelStyle = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.45f) };
        panelStyle.SetCornerRadiusAll(6);
        panelStyle.SetContentMarginAll(8);
        panel.AddThemeStyleboxOverride("panel", panelStyle);
        AddChild(panel);

        var root = new VBoxContainer();
        panel.AddChild(root);


        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        root.AddChild(scroll);

        _list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_list);
    }

    public override void _Process(double delta)
    {
        // Add rows for new squads
        foreach (Node node in GetTree().GetNodesInGroup("squads"))
        {
            if (node is Squad squad && !_rows.ContainsKey(squad))
            {
                Row row = CreateRow(squad);
                _rows[squad] = row;
                _list.AddChild(row.Card);
            }
        }

        // Remove rows for squads that no longer exist
        foreach (Squad dead in _rows.Keys.Where(s => !IsInstanceValid(s)).ToList())
        {
            _rows[dead].Card.QueueFree();
            _rows.Remove(dead);
        }

        // Refresh the remaining rows
        foreach (var (squad, row) in _rows)
        {
            float pct = Mathf.Clamp(squad.Health / squad.MaxHealth, 0f, 1f);

            row.Label.Text = $"{squad.UnitType}   {Mathf.CeilToInt(squad.Health)}/{Mathf.CeilToInt(squad.MaxHealth)}";
            row.Bar.MaxValue = squad.MaxHealth;
            row.Bar.Value = squad.Health;
            row.Fill.BgColor = Color.FromHsv(pct * 0.33f, 0.85f, 0.95f);

            row.Style.BgColor = squad.IsSelected ? SelectedColor : NormalColor;
            row.Style.BorderColor = squad.IsSelected ? SelectedBorder : NoBorder;
        }
    }

    private Row CreateRow(Squad squad)
    {
        var card = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };

        var style = new StyleBoxFlat { BgColor = NormalColor, BorderColor = NoBorder };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(4);
        style.SetContentMarginAll(6);
        card.AddThemeStyleboxOverride("panel", style);

        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        card.AddChild(box);

        var label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddChild(label);

        var bar = new ProgressBar
        {
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 10),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        var fill = new StyleBoxFlat();
        bar.AddThemeStyleboxOverride("fill", fill);
        bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.6f) });
        box.AddChild(bar);

        card.GuiInput += ev => OnRowInput(squad, ev);

        return new Row { Card = card, Style = style, Label = label, Bar = bar, Fill = fill };
    }

    private void OnRowInput(Squad squad, InputEvent ev)
    {
        if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb && IsInstanceValid(squad))
        {
            _selection ??= GetTree().GetFirstNodeInGroup("selection_manager") as SelectionManager;
            _selection?.SelectSquad(squad, mb.ShiftPressed);
        }
    }
}