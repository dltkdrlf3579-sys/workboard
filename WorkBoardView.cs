using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;

public partial class Board
{
    static readonly ResourceDictionary theme = LoadTheme();
    static readonly Dictionary<string, Brush> colors = new Dictionary<string, Brush>();
    static readonly Brush ink = Paint("#202422"), muted = Paint("#616863"), accent = Paint("#17603F");
    static readonly Brush line = Paint("#E2E5E2"), canvas = Paint("#FFFFFF");
    readonly Dictionary<Job, CheckBox> checks = new Dictionary<Job, CheckBox>();
    StackPanel headerHost;
    CheckBox selectAll;
    Button trashButton, resetButton;
    TextBlock totals, heading;

    static ResourceDictionary LoadTheme()
    {
        using (var stream = typeof(Board).Assembly.GetManifestResourceStream("WorkBoard.Theme.xaml"))
            return (ResourceDictionary)XamlReader.Load(stream);
    }

    static Brush Paint(string hex)
    {
        Brush brush;
        if (!colors.TryGetValue(hex, out brush))
        {
            brush = (Brush)new BrushConverter().ConvertFromString(hex);
            brush.Freeze();
            colors.Add(hex, brush);
        }
        return brush;
    }

    static void SetAppearance(Window window)
    {
        window.Resources.MergedDictionaries.Add(theme);
        window.FontFamily = new FontFamily("Malgun Gothic, Segoe UI");
        window.FontSize = 13;
        window.Foreground = ink;
        window.Background = canvas;
        window.UseLayoutRounding = true;
        window.SnapsToDevicePixels = true;
        TextOptions.SetTextFormattingMode(window, TextFormattingMode.Display);
    }

    static void SizeToWorkArea(Window window, double maxWidth, double maxHeight, double minWidth, double minHeight)
    {
        var area = SystemParameters.WorkArea;
        var owner = window.Owner;
        if (owner != null)
        {
            var source = PresentationSource.FromVisual(owner);
            if (source != null && source.CompositionTarget != null)
            {
                var handle = new System.Windows.Interop.WindowInteropHelper(owner).Handle;
                var bounds = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
                var scale = source.CompositionTarget.TransformFromDevice;
                area = new Rect(0, 0, bounds.Width * scale.M11, bounds.Height * scale.M22);
            }
        }
        window.MinWidth = Math.Min(minWidth, area.Width);
        window.MinHeight = Math.Min(minHeight, area.Height);
        window.Width = Math.Max(window.MinWidth, Math.Min(maxWidth, area.Width * 0.92));
        window.Height = Math.Max(window.MinHeight, Math.Min(maxHeight, area.Height * 0.92));
        if (owner == null)
        {
            window.Left = area.Left + (area.Width - window.Width) / 2;
            window.Top = area.Top + (area.Height - window.Height) / 2;
        }
    }

    static StackPanel Line() { return new StackPanel { Orientation = Orientation.Horizontal }; }
    static void DockTop(DockPanel panel, UIElement child)
    {
        DockPanel.SetDock(child, Dock.Top);
        panel.Children.Add(child);
    }

    static TextBlock Text(string value, double size, Brush color, bool bold = false)
    {
        return new TextBlock {
            Text = value, FontSize = size, Foreground = color,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis
        };
    }

    static System.Windows.Shapes.Path Glyph(string data, Brush stroke, int size)
    {
        return new System.Windows.Shapes.Path {
            Data = Geometry.Parse(data), Stroke = stroke, StrokeThickness = 1.5,
            StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round, Stretch = Stretch.Uniform,
            Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center
        };
    }
    const string calendarIcon = "M2,4 H16 V16 H2 Z M2,8 H16 M6,2 V6 M12,2 V6";
    const string plusIcon = "M8,2 V14 M2,8 H14";
    const string trashIcon = "M2,5 H16 M5,5 L6,17 H12 L13,5 M7,5 V2 H11 V5";
    const string exportIcon = "M9,2 V12 M5,8 L9,12 L13,8 M2,12 V16 H16 V12";

    static Button Btn(string caption, Action action, bool primary = false)
    {
        var button = new Button { Content = caption, Style = (Style)theme[primary ? "Primary" : "Action"] };
        button.Click += (s,e) => {
            try { action(); }
            catch (Exception ex) { MessageBox.Show(ex.GetBaseException().Message, "처리할 수 없습니다"); }
        };
        return button;
    }
    static Button Quiet(string caption, Action action)
    {
        var button = Btn(caption, action);
        button.Style = (Style)theme["Quiet"];
        return button;
    }
    static void WithIcon(Button button, string caption, string shape, Brush color)
    {
        var content = Line();
        var icon = Glyph(shape, color, 14);
        icon.Margin = new Thickness(0, 0, 7, 0);
        content.Children.Add(icon);
        content.Children.Add(new TextBlock { Text = caption, VerticalAlignment = VerticalAlignment.Center });
        button.Content = content;
        AutomationProperties.SetName(button, caption);
    }
    static Border Rule() { return new Border { Height = 1, Background = line }; }

    void BuildBoard()
    {
        Title = "업무 목록";
        SizeToWorkArea(this, 1520, 990, 820, 420);
        SetAppearance(this);
        var page = new DockPanel { Margin = new Thickness(28, 24, 28, 22) };
        Content = new Border { Background = canvas, Child = page };
        var bar = new DockPanel { Margin = new Thickness(0,0,0,21) };
        DockTop(page, bar);
        var actions = Line();
        DockPanel.SetDock(actions, Dock.Right); bar.Children.Add(actions);
        trashButton = Quiet("휴지통", () => SwitchMode("휴지통"));
        WithIcon(trashButton, "휴지통", trashIcon, muted);
        actions.Children.Add(trashButton);
        var create = Btn("새 업무", () => Edit(null), true);
        WithIcon(create, "새 업무", plusIcon, Brushes.White);
        create.Margin = new Thickness(12,0,0,0);
        actions.Children.Add(create);
        var title = Line(); bar.Children.Add(title);
        heading = Text("업무 목록",28,ink,true);
        title.Children.Add(heading);

        var card = new DockPanel();
        page.Children.Add(card);
        DockTop(card,Rule());
        var nav = new DockPanel { Margin = new Thickness(0), Height = 58 };
        DockTop(card, nav);
        var filters = Line();
        filters.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(filters, Dock.Right);
        nav.Children.Add(filters); nav.Children.Add(tabs);
        filters.Children.Add(Glyph(calendarIcon, muted, 15));
        var dateLabel = Text("접수일", 13, muted, true);
        dateLabel.Margin = new Thickness(7,0,13,0); filters.Children.Add(dateLabel);
        foreach (var pair in new[] { Tuple.Create(start,"시작일"), Tuple.Create(end,"종료일") })
        {
            var box = pair.Item1;
            if (box == end)
            {
                var dash = Text("–",13,muted); dash.Margin = new Thickness(6,0,6,0); filters.Children.Add(dash);
            }
            box.Tag = pair.Item2;
            AutomationProperties.SetName(box, "접수일 "+pair.Item2);
            box.ToolTip = "예: 11/23 또는 2026-11-23. 하루만 조회하려면 시작일만 입력하세요.";
            box.KeyDown += (s,e) => { if (e.Key == Key.Enter) { Filter(); e.Handled = true; } };
            filters.Children.Add(box);
        }
        var apply = Btn("조회", Filter); apply.Height = 34; apply.Margin = new Thickness(8,0,2,0);
        filters.Children.Add(apply);
        resetButton = Quiet("초기화", () => { start.Clear(); end.Clear(); Filter(); });
        filters.Children.Add(resetButton);
        start.TextChanged += (s,e) => UpdateReset(); end.TextChanged += (s,e) => UpdateReset();

        headerHost = new StackPanel { Background = Brushes.White };
        DockTop(card, Rule()); DockTop(card, headerHost); DockTop(card, Rule());
        totals = Text("",12,muted);
        var footer = new Border {
            BorderBrush = line, BorderThickness = new Thickness(0,1,0,0),
            Height = 46, Padding = new Thickness(18,0,13,0)
        };
        var selectionBar = new DockPanel();
        export = Btn("선택 항목 엑셀 내보내기", Export);
        WithIcon(export, "선택 항목 엑셀 내보내기", exportIcon, muted);
        export.ToolTip = "목록에서 체크한 업무를 엑셀로 저장합니다.";
        ToolTipService.SetShowOnDisabled(export,true);
        export.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(export,Dock.Right); selectionBar.Children.Add(export);
        selectionBar.Children.Add(totals); footer.Child = selectionBar;
        DockPanel.SetDock(footer, Dock.Bottom); card.Children.Add(footer);
        var scroll = new ScrollViewer { Content = rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                                        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        scroll.ScrollChanged += (s,e) => {
            headerHost.Margin = new Thickness(0, 0, Math.Max(0, scroll.ActualWidth-scroll.ViewportWidth), 0);
        };
        card.Children.Add(scroll);
        Draw();
    }

    void UpdateReset()
    {
        resetButton.IsEnabled = start.Text.Length != 0 || end.Text.Length != 0 || from.HasValue || to.HasValue;
    }

    void SwitchMode(string value)
    {
        if (mode == value) return;
        mode = value; picks.Clear(); Draw();
    }

    static Grid Row(double height)
    {
        var grid = new Grid { Height = height, Background = Brushes.Transparent };
        foreach (int width in new[] { 48, 1, 114, 126, 96, 150 })
            grid.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(width, width == 1 ? GridUnitType.Star : GridUnitType.Pixel)
            });
        return grid;
    }
    static void Cell(Grid grid, int column, UIElement element)
    {
        Grid.SetColumn(element,column); grid.Children.Add(element);
    }
    static TextBlock Value(string text, bool header = false)
    {
        var label = Text(text,header ? 12 : 13,muted,header);
        label.Margin = new Thickness(0,0,9,0); label.ToolTip = text;
        return label;
    }
    static CheckBox Check(string caption)
    {
        var check = new CheckBox { Style = (Style)theme["Pick"], ToolTip = caption };
        AutomationProperties.SetName(check, caption);
        return check;
    }

    void DrawTabs()
    {
        heading.Text = mode == "휴지통" ? "휴지통" : "업무 목록";
        tabs.Children.Clear();
        foreach (string tab in new[] { "미완료", "전체", "완료" })
        {
            bool selected = tab == mode;
            int count = jobs.Count(j => !j.Trash && (tab == "전체" || (j.State == "완료") == (tab == "완료")));
            var button = Quiet(tab, () => SwitchMode(tab)); button.Height = 40;
            var label = Line();
            label.Children.Add(Text(tab + "  " + count,14,selected ? ink : muted,selected));
            button.Content = label; AutomationProperties.SetName(button,tab+" "+count+"건");
            var block = new DockPanel { Margin = new Thickness(0,0,5,0) };
            var underline = new Border { Height = 2, Margin = new Thickness(6,2,6,0),
                                         Background = selected ? ink : Brushes.Transparent };
            DockPanel.SetDock(underline,Dock.Bottom); block.Children.Add(underline); button.VerticalAlignment=VerticalAlignment.Center; block.Children.Add(button);
            tabs.Children.Add(block);
        }
        trashButton.Background = mode == "휴지통" ? Paint("#E9ECEA") : Brushes.Transparent;
    }

    void Draw()
    {
        var list = Visible();
        picks.IntersectWith(list);
        DrawTabs(); UpdateReset(); rows.Children.Clear(); headerHost.Children.Clear(); checks.Clear();
        var header = Row(37); headerHost.Children.Add(header);
        selectAll = Check("현재 목록 전체 선택");
        if (mode != "휴지통") Cell(header,0,selectAll);
        selectAll.Click += (s,e) => {
            if (picks.Count == list.Count) picks.Clear(); else picks.UnionWith(list);
            foreach (var pair in checks) pair.Value.IsChecked = picks.Contains(pair.Key);
            UpdateSelection(list.Count);
        };
        Cell(header,1,Value("업무명",true)); Cell(header,2,Value("접수일",true));
        var sortButton = Quiet("완료예정일", () => { byDue = !byDue; Draw(); });
        sortButton.Padding = new Thickness(0); sortButton.Height = 30;
        sortButton.HorizontalAlignment = HorizontalAlignment.Left;
        sortButton.Foreground = byDue ? accent : muted; sortButton.FontSize = 12;
        sortButton.Content = byDue ? "완료예정일  ↑" : "완료예정일  ↕";
        sortButton.ToolTip = byDue ? "접수일 정렬로 돌아가기" : "완료예정일이 빠른 순으로 정렬";
        Cell(header,3,sortButton); Cell(header,4,Value("상태",true)); Cell(header,5,Value("비고",true));
        foreach (var job in list) AddRow(job,list.Count);
        if (list.Count == 0)
        {
            var empty = new StackPanel { Margin = new Thickness(10,35,10,12), HorizontalAlignment = HorizontalAlignment.Center };
            empty.Children.Add(Text(from.HasValue || to.HasValue ? "해당 접수일의 업무가 없습니다." :
                mode == "휴지통" ? "휴지통이 비어 있습니다." : "표시할 업무가 없습니다.", 14, muted));
            rows.Children.Add(empty);
        }
        UpdateSelection(list.Count);
    }

    void UpdateSelection(int count)
    {
        selectAll.IsChecked = picks.Count == 0 ? false : picks.Count == count ? true : (bool?)null;
        selectAll.IsEnabled = count > 0;
        export.IsEnabled = picks.Count > 0 && mode != "휴지통";
        totals.Text = (mode == "휴지통" ? "휴지통 " : "업무 ") + count + "건" +
            (picks.Count > 0 ? "   ·   "+picks.Count+"건 선택" : "");
    }

    static Border Badge(string state)
    {
        bool waiting = state == "회신대기", done = state == "완료";
        var fg = Paint(waiting ? "#AD7B20" : done ? "#27684E" : "#4C78AC");
        var label = Line();
        label.Children.Add(new Ellipse { Width=6, Height=6, Fill=fg, Margin=new Thickness(0,0,5,0), VerticalAlignment=VerticalAlignment.Center });
        label.Children.Add(Text(state,13,ink));
        return new Border { HorizontalAlignment=HorizontalAlignment.Left, VerticalAlignment=VerticalAlignment.Center, Child=label };
    }

    void AddRow(Job job, int count)
    {
        var row = Row(49);
        rows.Children.Add(new Border { BorderBrush=Paint("#ECEEEC"), BorderThickness=new Thickness(0,0,0,1), Child=row });
        var check = Check(job.Title+" 선택"); check.IsChecked = picks.Contains(job);
        if (!job.Trash) { Cell(row,0,check); checks.Add(job,check); }
        var menu = RowMenu(job); row.ContextMenu = menu;
        Action highlight = () => row.Background = row.IsMouseOver || menu.IsOpen ? Paint("#F7F9F7") : Brushes.White;
        row.MouseEnter += (s,e) => highlight(); row.MouseLeave += (s,e) => highlight();
        menu.Opened += (s,e) => highlight(); menu.Closed += (s,e) => highlight();
        check.Click += (s,e) => {
            if (check.IsChecked == true) picks.Add(job); else picks.Remove(job); UpdateSelection(count);
        };
        highlight();
        var title = Btn(job.Title,() => Edit(job)); title.Style = (Style)theme["TitleLink"];
        var name = new TextBlock { Text=job.Title, FontSize=14, TextTrimming=TextTrimming.CharacterEllipsis };
        title.Content=name; title.ToolTip=job.Title; title.Margin=new Thickness(0,0,12,0);
        AutomationProperties.SetName(title,job.Title); Cell(row,1,title);
        Cell(row,2,Value(job.Received));
        var due = Value(String.IsNullOrEmpty(job.Due) ? "미정" : job.Due);
        if (job.State != "완료" && Date(job.Due,DateTime.Today.Year) < DateTime.Today) due.Foreground=Paint("#A33F46");
        Cell(row,3,due); Cell(row,4,Badge(job.State));
        var memo=Value(job.Memo); memo.Background=Brushes.Transparent; Cell(row,5,memo);
        if (job.Trash) row.MouseLeftButtonUp += (s,e) => { if (!e.Handled) Edit(job); };
        else memo.MouseLeftButtonDown += (s,e) => { if (e.ClickCount == 2) EditMemo(row,job,memo); };
    }

    void EditMemo(Grid row, Job job, TextBlock label)
    {
        var input=new TextBox { Text=job.Memo, Margin=new Thickness(0,0,6,0) };
        row.Children.Remove(label); Cell(row,5,input); input.Focus(); input.SelectAll();
        bool ended=false;
        Action commit=() => {
            if (ended) return;
            ended=true;
            try { var next=job.Copy(); next.Memo=input.Text; Update(job,next); }
            catch (Exception ex) { MessageBox.Show(ex.Message); Draw(); }
        };
        input.LostKeyboardFocus += (s,e) => commit();
        input.KeyDown += (s,e) => {
            if (e.Key == Key.Enter) { e.Handled=true; commit(); }
            if (e.Key == Key.Escape) { e.Handled=true; ended=true; Draw(); }
        };
    }
}
