# /// script
# dependencies = [
#     "polars==1.38.1",
#     "numpy==2.4.2",
#     "openpyxl==3.1.5",
# ]
# ///

"""
Test Revit Element Analysis with Polars
Demonstrates data collection, analysis, and visualization.
Practical example combining all three modules: CodeExecute, Logging, Visualization
"""

import polars as pl
from Autodesk.Revit import UI
from Autodesk.Revit.DB import BuiltInParameter, ElementId, FilteredElementCollector, Wall

uiapp : UI.UIApplication = __revit__  # type: ignore  # noqa: F821
uidoc = uiapp.ActiveUIDocument
doc = uidoc.Document


def collect_wall_data():
    """Collect wall data from active document"""
    print("Collecting wall data...")

    walls = FilteredElementCollector(doc).OfClass(Wall).ToElements()

    data = []
    for wall in walls:
        # Get parameters
        length_param = wall.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH)
        height_param = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)
        area_param = wall.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED)

        data.append(
            {
                "Id": wall.Id.IntegerValue,
                "Name": wall.Name,
                "Length": length_param.AsDouble() if length_param else 0,
                "Height": height_param.AsDouble() if height_param else 0,
                "Area": area_param.AsDouble() if area_param else 0,
                "WallType": wall.WallType.Name if wall.WallType else "Unknown",
            }
        )

    print(f"Collected {len(data)} walls")
    return data


def analyze_walls(data):
    """Analyze wall data with Polars"""
    print()
    print("=== Wall Analysis ===")

    df = pl.DataFrame(data)

    # Summary statistics
    print()
    print("Summary Statistics:")
    print(
        df.select(
            [
                pl.col("Length").mean().alias("Avg Length"),
                pl.col("Height").mean().alias("Avg Height"),
                pl.col("Area").sum().alias("Total Area"),
            ]
        )
    )

    # Group by wall type
    print()
    print("By Wall Type:")
    summary = df.group_by("WallType").agg(
        [
            pl.len().alias("Count"),
            pl.col("Length").sum().alias("Total Length"),
            pl.col("Area").sum().alias("Total Area"),
        ]
    )
    print(summary)

    return df, summary


def visualize_long_walls(df, threshold_percentile=0.9):
    """Visualize walls above length threshold"""
    print()
    print("=== Visualizing Long Walls ===")

    # Calculate threshold
    threshold = df.select(pl.col("Length").quantile(threshold_percentile)).item()

    print(f"Length threshold (top {(1 - threshold_percentile) * 100:.0f}%): {threshold:.2f}")

    # Filter long walls
    long_walls = df.filter(pl.col("Length") > threshold)

    print(f"Found {len(long_walls)} long walls")

    # Visualize their curves
    for row in long_walls.iter_rows(named=True):
        wall_id = row["Id"]
        wall = doc.GetElement(ElementId(row["Id"]))

        if wall and wall.Location and hasattr(wall.Location, "Curve"):
            curve = wall.Location.Curve
            print(f"Wall {wall_id}: {row['Name']}, Length={row['Length']:.2f}")
            print(curve)  # Visualize in 3D view


def find_outliers(df):
    """Find walls with unusual dimensions"""
    print()
    print("=== Finding Outliers ===")

    # Walls with area > 95th percentile
    area_threshold = df.select(pl.col("Area").quantile(0.95)).item()
    large_walls = df.filter(pl.col("Area") > area_threshold)

    print(f"Large walls (area > {area_threshold:.2f}):")
    print(large_walls.select(["Id", "Name", "Area"]))

    # Walls with unusual height/length ratio
    df_with_ratio = df.with_columns((pl.col("Height") / pl.col("Length")).alias("HeightLengthRatio"))

    # Filter extreme ratios
    extreme_ratio = df_with_ratio.filter((pl.col("HeightLengthRatio") > 2.0) | (pl.col("HeightLengthRatio") < 0.1))

    if len(extreme_ratio) > 0:
        print()
        print("Walls with unusual Height/Length ratio:")
        print(extreme_ratio.select(["Id", "Name", "Height", "Length", "HeightLengthRatio"]))


if __name__ == "__main__":
    print("=== Revit Wall Analysis Test ===")
    print()

    try:
        # 1. Collect data
        data = collect_wall_data()

        if len(data) == 0:
            print("WARNING: No walls found in active document")
        else:
            # 2. Analyze
            df, summary = analyze_walls(data)

            # 3. Find outliers
            find_outliers(df)

            # 4. Visualize long walls
            visualize_long_walls(df, threshold_percentile=0.9)

            print()
            print("Analysis complete ✓")

    except Exception as e:
        print(f"ERROR: {e}")
        import traceback

        print(traceback.format_exc())
