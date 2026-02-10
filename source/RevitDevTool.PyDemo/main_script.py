# /// script
# dependencies = [
#     "polars==1.38.1",
#     "numpy==2.4.2",
#     "openpyxl==3.1.5",
#     "plotly==6.5.2",
#     "scipy==1.17.0",
#     "shapely==2.1.2",
#     "trimesh==4.11.1",
#     "vedo==2025.5.4",
#     "networkx==3.6.1",
# ]
# ///

from __future__ import annotations
import os
import tempfile
import Autodesk.Revit.DB as DB
import Autodesk.Revit.UI as UI
import UIFramework

from System import Action, Uri, Console
from System.Windows import Window, WindowStartupLocation
from System.Windows.Threading import DispatcherPriority
from Microsoft.Web.WebView2.Wpf import WebView2
from Microsoft.Web.WebView2.Core import CoreWebView2Environment, CoreWebView2HostResourceAccessKind


# Global Context
uiapp = __revit__  # type: UI.UIApplication  # noqa: F821
doc = uiapp.ActiveUIDocument.Document


def collect_elements_by_category_and_level():
    """Thu thập dữ liệu elements từ Revit, phân loại theo category và level"""
    
    # Lấy tất cả levels trong project
    collector_levels = DB.FilteredElementCollector(doc)\
        .OfCategory(DB.BuiltInCategory.OST_Levels)\
        .WhereElementIsNotElementType()
    
    levels_dict = {}
    for level in collector_levels:
        levels_dict[level.Id.IntegerValue] = {
            'name': level.Name,
            'elevation': level.Elevation
        }
    
    # Định nghĩa các categories quan trọng để phân tích
    categories_to_analyze = [
        (DB.BuiltInCategory.OST_Walls, "Walls"),
        (DB.BuiltInCategory.OST_Floors, "Floors"),
        (DB.BuiltInCategory.OST_Doors, "Doors"),
        (DB.BuiltInCategory.OST_Windows, "Windows"),
        (DB.BuiltInCategory.OST_Furniture, "Furniture"),
        (DB.BuiltInCategory.OST_StructuralColumns, "Structural Columns"),
        (DB.BuiltInCategory.OST_StructuralFraming, "Structural Framing"),
        (DB.BuiltInCategory.OST_Rooms, "Rooms"),
        (DB.BuiltInCategory.OST_MEPSpaces, "Spaces"),
        (DB.BuiltInCategory.OST_DuctCurves, "Ducts"),
        (DB.BuiltInCategory.OST_PipeCurves, "Pipes"),
        (DB.BuiltInCategory.OST_ElectricalFixtures, "Electrical Fixtures"),
        (DB.BuiltInCategory.OST_LightingFixtures, "Lighting Fixtures"),
        (DB.BuiltInCategory.OST_Ceilings, "Ceilings"),
        (DB.BuiltInCategory.OST_Roofs, "Roofs"),
        (DB.BuiltInCategory.OST_Stairs, "Stairs"),
    ]
    
    data_structure = {}
    total_elements = 0
    
    # Thu thập elements cho mỗi category
    for builtin_cat, cat_name in categories_to_analyze:
        try:
            collector = DB.FilteredElementCollector(doc)\
                .OfCategory(builtin_cat)\
                .WhereElementIsNotElementType()
            
            elements_list = list(collector)
            
            if not elements_list:
                continue
                
            data_structure[cat_name] = {
                'total': len(elements_list),
                'by_level': {},
                'no_level': 0
            }
            
            # Phân loại theo level
            for elem in elements_list:
                total_elements += 1
                
                # Lấy level của element
                level_id = None
                level_param = elem.get_Parameter(DB.BuiltInParameter.FAMILY_LEVEL_PARAM)
                if level_param and level_param.HasValue:
                    level_id = level_param.AsElementId().IntegerValue
                else:
                    # Thử lấy level từ các parameters khác
                    level_param = elem.LevelId
                    if level_param and level_param.IntegerValue > 0:
                        level_id = level_param.IntegerValue
                
                if level_id and level_id in levels_dict:
                    level_name = levels_dict[level_id]['name']
                    if level_name not in data_structure[cat_name]['by_level']:
                        data_structure[cat_name]['by_level'][level_name] = 0
                    data_structure[cat_name]['by_level'][level_name] += 1
                else:
                    data_structure[cat_name]['no_level'] += 1
                    
        except Exception as e:
            Console.WriteLine("Error processing category {}: {}".format(cat_name, str(e)))
            continue
    
    return {
        'data': data_structure,
        'levels': levels_dict,
        'total_elements': total_elements
    }


def create_advanced_plotly_visualizations(data_dict):
    """Tạo nhiều loại visualization phức tạp với Plotly"""
    import plotly.graph_objects as go
    from plotly.subplots import make_subplots
    import plotly.express as px
    
    data_structure = data_dict['data']
    total_elements = data_dict['total_elements']
    
    # === 1. Tạo dữ liệu cho các charts ===
    
    # Chuẩn bị dữ liệu cho grouped bar chart
    categories = []
    levels_set = set()
    
    for cat_name, cat_data in data_structure.items():
        categories.append(cat_name)
        for level_name in cat_data['by_level'].keys():
            levels_set.add(level_name)
    
    levels_list = sorted(list(levels_set))
    
    # Matrix data cho heatmap
    matrix_data = []
    for level_name in levels_list:
        row = []
        for cat_name in categories:
            count = data_structure[cat_name]['by_level'].get(level_name, 0)
            row.append(count)
        matrix_data.append(row)
    
    # === 2. Tạo subplot với nhiều visualization ===
    
    fig = make_subplots(
        rows=3, cols=2,
        subplot_titles=(
            'Elements by Category (Total Count)',
            'Elements Distribution by Level',
            'Heatmap: Category vs Level',
            'Top 5 Categories by Element Count',
            'Category Distribution (Pie Chart)',
            'Level Utilization (Elements per Level)'
        ),
        specs=[
            [{}, {}],  # Row 1: Bar charts (default xy type)
            [{"type": "heatmap", "colspan": 2}, None],  # Row 2: Heatmap
            [{}, {"type": "domain"}]  # Row 3: Bar chart (xy) and Pie chart (domain)
        ],
        vertical_spacing=0.12,
        horizontal_spacing=0.15
    )
    
    # === CHART 1: Total Elements by Category (Stacked Bar) ===
    colors = px.colors.qualitative.Set3
    
    for idx, level_name in enumerate(levels_list):
        level_counts = []
        for cat_name in categories:
            count = data_structure[cat_name]['by_level'].get(level_name, 0)
            level_counts.append(count)
        
        fig.add_trace(
            go.Bar(
                name=level_name,
                x=categories,
                y=level_counts,
                marker_color=colors[idx % len(colors)],
                hovertemplate='<b>%{x}</b><br>' +
                             'Level: ' + level_name + '<br>' +
                             'Count: %{y}<br>' +
                             '<extra></extra>'
            ),
            row=1, col=1
        )
    
    # === CHART 2: Elements by Level (Total per Level) ===
    level_totals = {}
    for cat_name, cat_data in data_structure.items():
        for level_name, count in cat_data['by_level'].items():
            if level_name not in level_totals:
                level_totals[level_name] = 0
            level_totals[level_name] += count
    
    sorted_levels = sorted(level_totals.items(), key=lambda x: x[1], reverse=True)
    level_names = [x[0] for x in sorted_levels]
    level_counts = [x[1] for x in sorted_levels]
    
    fig.add_trace(
        go.Bar(
            x=level_names,
            y=level_counts,
            marker=dict(
                color=level_counts,
                colorscale='Viridis',
                showscale=True,
                colorbar=dict(title="Count", x=1.15)
            ),
            text=level_counts,
            textposition='outside',
            hovertemplate='<b>%{x}</b><br>Total Elements: %{y}<extra></extra>'
        ),
        row=1, col=2
    )
    
    # === CHART 3: Heatmap ===
    fig.add_trace(
        go.Heatmap(
            z=matrix_data,
            x=categories,
            y=levels_list,
            colorscale='RdYlGn',
            text=matrix_data,
            texttemplate='%{text}',
            textfont={"size": 10},
            hovertemplate='Category: %{x}<br>Level: %{y}<br>Count: %{z}<extra></extra>',
            colorbar=dict(title="Element Count", x=1.02)
        ),
        row=2, col=1
    )
    
    # === CHART 4: Top 5 Categories ===
    category_totals = [(cat, data['total']) for cat, data in data_structure.items()]
    category_totals.sort(key=lambda x: x[1], reverse=True)
    top_5 = category_totals[:5]
    
    fig.add_trace(
        go.Bar(
            x=[x[1] for x in top_5],
            y=[x[0] for x in top_5],
            orientation='h',
            marker=dict(color='lightblue'),
            text=[x[1] for x in top_5],
            textposition='outside',
            hovertemplate='<b>%{y}</b><br>Total: %{x}<extra></extra>'
        ),
        row=3, col=1
    )
    
    # === CHART 5: Pie Chart - Category Distribution ===
    top_10_categories = category_totals[:10]
    
    fig.add_trace(
        go.Pie(
            labels=[x[0] for x in top_10_categories],
            values=[x[1] for x in top_10_categories],
            hole=0.4,
            marker=dict(colors=px.colors.qualitative.Pastel),
            textinfo='label+percent',
            hovertemplate='<b>%{label}</b><br>Count: %{value}<br>Percentage: %{percent}<extra></extra>'
        ),
        row=3, col=2
    )
    
    # === Update layout ===
    fig.update_layout(
        title=dict(
            text=f'<b>Revit Elements Analysis Dashboard</b><br>' +
                 f'<sub>Total Elements Analyzed: {total_elements:,}</sub>',
            x=0.5,
            xanchor='center',
            font=dict(size=24)
        ),
        showlegend=True,
        height=1400,
        width=1800,
        hovermode='closest',
        template='plotly_white',
        legend=dict(
            orientation="v",
            yanchor="top",
            y=0.99,
            xanchor="left",
            x=1.02
        )
    )
    
    # Update axes
    fig.update_xaxes(title_text="Category", row=1, col=1, tickangle=-45)
    fig.update_yaxes(title_text="Element Count", row=1, col=1)
    
    fig.update_xaxes(title_text="Level", row=1, col=2, tickangle=-45)
    fig.update_yaxes(title_text="Element Count", row=1, col=2)
    
    fig.update_xaxes(title_text="Category", row=2, col=1, tickangle=-45)
    fig.update_yaxes(title_text="Level", row=2, col=1)
    
    fig.update_xaxes(title_text="Element Count", row=3, col=1)
    
    # Tạo thêm một figure riêng cho Sunburst chart (hierarchical)
    sunburst_fig = create_sunburst_chart(data_structure)
    
    # Tạo thêm một figure riêng cho Treemap
    treemap_fig = create_treemap_chart(data_structure)
    
    return fig, sunburst_fig, treemap_fig


def create_sunburst_chart(data_structure):
    """Tạo Sunburst chart để hiển thị hierarchy"""
    import plotly.graph_objects as go
    
    labels = ["All Elements"]
    parents = [""]
    values = [0]
    colors = []
    
    # Color palette
    import plotly.express as px
    color_palette = px.colors.qualitative.Set3
    
    total_all = sum(data['total'] for data in data_structure.values())
    values[0] = total_all
    colors.append('#636EFA')
    
    # Add categories
    cat_idx = 0
    for cat_name, cat_data in data_structure.items():
        labels.append(cat_name)
        parents.append("All Elements")
        values.append(cat_data['total'])
        colors.append(color_palette[cat_idx % len(color_palette)])
        cat_idx += 1
        
        # Add levels for each category
        for level_name, count in cat_data['by_level'].items():
            labels.append(f"{level_name} ({cat_name})")
            parents.append(cat_name)
            values.append(count)
            colors.append(color_palette[cat_idx % len(color_palette)])
    
    fig = go.Figure(go.Sunburst(
        labels=labels,
        parents=parents,
        values=values,
        branchvalues="total",
        marker=dict(
            colors=colors,
            line=dict(color='white', width=2)
        ),
        hovertemplate='<b>%{label}</b><br>Count: %{value}<br>Percent of parent: %{percentParent}<br>Percent of root: %{percentRoot}<extra></extra>'
    ))
    
    fig.update_layout(
        title=dict(
            text='<b>Hierarchical View: Elements by Category and Level</b>',
            x=0.5,
            xanchor='center',
            font=dict(size=20)
        ),
        width=1000,
        height=1000,
        template='plotly_white'
    )
    
    return fig


def create_treemap_chart(data_structure):
    """Tạo Treemap chart"""
    import plotly.graph_objects as go
    
    labels = []
    parents = []
    values = []
    text_info = []
    
    # Root
    labels.append("All Elements")
    parents.append("")
    total_all = sum(data['total'] for data in data_structure.values())
    values.append(total_all)
    text_info.append(f"Total: {total_all}")
    
    # Categories and levels
    for cat_name, cat_data in data_structure.items():
        # Add category
        labels.append(cat_name)
        parents.append("All Elements")
        values.append(cat_data['total'])
        text_info.append(f"{cat_name}<br>{cat_data['total']} elements")
        
        # Add levels
        for level_name, count in cat_data['by_level'].items():
            labels.append(f"{level_name}_{cat_name}")
            parents.append(cat_name)
            values.append(count)
            text_info.append(f"{level_name}<br>{count} elements")
    
    fig = go.Figure(go.Treemap(
        labels=labels,
        parents=parents,
        values=values,
        text=text_info,
        textposition='middle center',
        marker=dict(
            colorscale='Rainbow',
            line=dict(color='white', width=2)
        ),
        hovertemplate='<b>%{label}</b><br>Count: %{value}<br>Percent of parent: %{percentParent:.1%}<extra></extra>'
    ))
    
    fig.update_layout(
        title=dict(
            text='<b>Treemap View: Elements Distribution</b>',
            x=0.5,
            xanchor='center',
            font=dict(size=20)
        ),
        width=1200,
        height=800,
        template='plotly_white'
    )
    
    return fig


def create_html_dashboard(main_fig, sunburst_fig, treemap_fig):
    """Tạo HTML dashboard với nhiều charts"""
    
    # Convert figures to HTML
    main_html = main_fig.to_html(include_plotlyjs='cdn', div_id='main-dashboard')
    sunburst_html = sunburst_fig.to_html(include_plotlyjs=False, div_id='sunburst-chart')
    treemap_html = treemap_fig.to_html(include_plotlyjs=False, div_id='treemap-chart')
    
    # Create comprehensive HTML
    html_content = f"""
<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <title>Revit Elements Analysis Dashboard</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            margin: 0;
            padding: 20px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
        }}
        
        .container {{
            max-width: 1900px;
            margin: 0 auto;
            background: white;
            border-radius: 15px;
            box-shadow: 0 10px 40px rgba(0,0,0,0.3);
            padding: 30px;
        }}
        
        h1 {{
            color: #333;
            text-align: center;
            margin-bottom: 10px;
            font-size: 2.5em;
            text-shadow: 2px 2px 4px rgba(0,0,0,0.1);
        }}
        
        .subtitle {{
            text-align: center;
            color: #666;
            margin-bottom: 30px;
            font-size: 1.2em;
        }}
        
        .nav-tabs {{
            display: flex;
            justify-content: center;
            margin-bottom: 20px;
            border-bottom: 2px solid #ddd;
        }}
        
        .tab-button {{
            padding: 12px 30px;
            margin: 0 5px;
            border: none;
            background: #f0f0f0;
            cursor: pointer;
            font-size: 16px;
            font-weight: bold;
            border-radius: 8px 8px 0 0;
            transition: all 0.3s ease;
        }}
        
        .tab-button:hover {{
            background: #e0e0e0;
            transform: translateY(-2px);
        }}
        
        .tab-button.active {{
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            box-shadow: 0 -2px 10px rgba(102, 126, 234, 0.3);
        }}
        
        .tab-content {{
            display: none;
            animation: fadeIn 0.5s;
        }}
        
        .tab-content.active {{
            display: block;
        }}
        
        @keyframes fadeIn {{
            from {{ opacity: 0; transform: translateY(10px); }}
            to {{ opacity: 1; transform: translateY(0); }}
        }}
        
        .chart-container {{
            margin: 20px 0;
            padding: 20px;
            background: #fafafa;
            border-radius: 10px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
        }}
        
        .info-box {{
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 20px;
            border-radius: 10px;
            margin-bottom: 20px;
            box-shadow: 0 4px 15px rgba(102, 126, 234, 0.3);
        }}
        
        .info-box h3 {{
            margin-top: 0;
            font-size: 1.5em;
        }}
    </style>
</head>
<body>
    <div class="container">
        <h1>🏗️ Revit Elements Analysis Dashboard</h1>
        <p class="subtitle">Comprehensive visualization of building elements by category and level</p>
        
        <div class="info-box">
            <h3>📊 Dashboard Overview</h3>
            <p>This advanced dashboard provides multiple perspectives on your Revit model's element distribution:</p>
            <ul>
                <li><strong>Main Dashboard:</strong> Multi-chart view with bar charts, heatmap, and pie chart</li>
                <li><strong>Sunburst Chart:</strong> Hierarchical view showing relationships between categories and levels</li>
                <li><strong>Treemap:</strong> Proportional visualization of element distribution</li>
            </ul>
        </div>
        
        <div class="nav-tabs">
            <button class="tab-button active" onclick="showTab('main')">Main Dashboard</button>
            <button class="tab-button" onclick="showTab('sunburst')">Sunburst View</button>
            <button class="tab-button" onclick="showTab('treemap')">Treemap View</button>
        </div>
        
        <div id="main" class="tab-content active">
            <div class="chart-container">
                {main_html}
            </div>
        </div>
        
        <div id="sunburst" class="tab-content">
            <div class="chart-container">
                {sunburst_html}
            </div>
        </div>
        
        <div id="treemap" class="tab-content">
            <div class="chart-container">
                {treemap_html}
            </div>
        </div>
    </div>
    
    <script>
        function showTab(tabName) {{
            // Hide all tabs
            var tabs = document.getElementsByClassName('tab-content');
            for (var i = 0; i < tabs.length; i++) {{
                tabs[i].classList.remove('active');
            }}
            
            // Remove active class from all buttons
            var buttons = document.getElementsByClassName('tab-button');
            for (var i = 0; i < buttons.length; i++) {{
                buttons[i].classList.remove('active');
            }}
            
            // Show selected tab
            document.getElementById(tabName).classList.add('active');
            event.target.classList.add('active');
        }}
    </script>
</body>
</html>
    """
    
    return html_content


def show_visualization_in_webview(html_content):
    """Hiển thị visualization trong WebView2 (Modeless pattern)"""
    
    try:
        # Tạo temp file cho HTML
        temp_dir = tempfile.mkdtemp()
        html_file = os.path.join(temp_dir, "dashboard.html")
        
        with open(html_file, 'w', encoding='utf-8') as f:
            f.write(html_content)
        
        Console.WriteLine("[Dashboard] HTML saved to: {}".format(html_file))
        
        # Tạo WPF Window
        window = Window()
        window.Title = "Revit Elements Analysis - Plotly Dashboard"
        window.Width = 1920
        window.Height = 1080
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner
        window.Owner = UIFramework.MainWindow.getMainWnd()
        
        # Tạo WebView2
        webview = WebView2()
        window.Content = webview
        
        # Async initialization của WebView2
        def on_window_loaded(sender, e):
            Console.WriteLine("[Dashboard] Window loaded, initializing WebView2...")
            
            # Tạo user data folder
            user_data = os.path.join(tempfile.gettempdir(), "Revit_Plotly_WV2_Cache")
            if not os.path.exists(user_data):
                os.makedirs(user_data)
            
            # CreateAsync cho environment
            env_task = CoreWebView2Environment.CreateAsync(None, user_data, None)
            
            def check_task():
                if env_task.IsCompleted:
                    try:
                        env = env_task.Result
                        
                        # Dùng Dispatcher.BeginInvoke cho WPF
                        action = Action(lambda: webview.EnsureCoreWebView2Async(env))
                        window.Dispatcher.BeginInvoke(DispatcherPriority.Normal, action)
                        
                        Console.WriteLine("[Dashboard] WebView2 Environment ready")
                    except Exception as ex:
                        Console.WriteLine("[Error] Environment task failed: {}".format(str(ex)))
            
            # Đăng ký callback khi task complete
            env_task.GetAwaiter().OnCompleted(Action(check_task))
        
        # Handler khi WebView2 core sẵn sàng
        def on_webview_ready(sender, args):
            if not args.IsSuccess:
                Console.WriteLine("[Error] WebView2 Init Failed: {}".format(
                    args.InitializationException.Message if args.InitializationException else "Unknown"
                ))
                return
            
            Console.WriteLine("[Dashboard] WebView2 Core initialized, navigating to HTML...")
            
            # Navigate đến HTML file
            webview.CoreWebView2.Navigate(html_file)
        
        # Handler khi window đóng
        def on_window_closing(sender, args):
            Console.WriteLine("[Dashboard] Window closing, disposing WebView2...")
            if webview:
                webview.Dispose()
        
        # Đăng ký events
        window.Loaded += on_window_loaded
        webview.CoreWebView2InitializationCompleted += on_webview_ready
        window.Closing += on_window_closing
        
        # Show modeless window (không block)
        window.Show()
        
        Console.WriteLine("[Dashboard] Modeless WPF window shown successfully")
        
    except Exception as ex:
        import traceback
        error_msg = "Error showing WebView: {}\n\nTraceback:\n{}".format(str(ex), traceback.format_exc())
        Console.WriteLine(error_msg)
        UI.TaskDialog.Show("Error", error_msg)


# === MAIN EXECUTION ===
def main():
    """Main execution function"""
    try:
        Console.WriteLine("Starting Revit Elements Analysis...")
        
        # 1. Thu thập dữ liệu từ Revit
        Console.WriteLine("Collecting data from Revit...")
        data_dict = collect_elements_by_category_and_level()
        
        if data_dict['total_elements'] == 0:
            UI.TaskDialog.Show("Warning", "No elements found in the current Revit document.")
            return
        
        Console.WriteLine("Total elements collected: {}".format(data_dict['total_elements']))
        
        # 2. Tạo visualizations
        Console.WriteLine("Creating Plotly visualizations...")
        main_fig, sunburst_fig, treemap_fig = create_advanced_plotly_visualizations(data_dict)
        
        # 3. Tạo HTML dashboard
        Console.WriteLine("Generating HTML dashboard...")
        html_content = create_html_dashboard(main_fig, sunburst_fig, treemap_fig)
        
        # 4. Hiển thị trong WebView
        Console.WriteLine("Launching visualization...")
        show_visualization_in_webview(html_content)
        
    except Exception as ex:
        import traceback
        error_msg = "Error: {}\n\nTraceback:\n{}".format(str(ex), traceback.format_exc())
        Console.WriteLine(error_msg)
        UI.TaskDialog.Show("Error", error_msg)


# Run the main function
if __name__ == "__main__":
    main()