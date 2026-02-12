# /// script
# dependencies = [
#     "shapely==2.1.2",
#     "numpy==2.4.2",
#     "openpyxl==3.1.5",
#     "pydantic==2.12.5",
#     "scikit-learn==1.8.0"
#     "polars==1.38.1"
# ]
# ///

from Autodesk.Revit import DB
from sklearn.cluster import KMeans
import numpy as np

def cluster_revit_points(points, n_clusters=3):
    """
    Clusters a list of Revit XYZ points into n_clusters using KMeans.

    :param points: List of DB.XYZ points
    :param n_clusters: Number of clusters for KMeans
    :return: List of cluster labels corresponding to each point
    """
    # Convert Revit XYZ points to numpy array
    data = np.array([[pt.X, pt.Y, pt.Z] for pt in points])

    # Perform KMeans clustering
    kmeans = KMeans(n_clusters=n_clusters)
    kmeans.fit(data)

    return kmeans.labels_.tolist()

def main():
    # Example Revit points
    revit_points = [
        DB.XYZ(1, 2, 3),
        DB.XYZ(1, 2, 4),
        DB.XYZ(10, 10, 10),
        DB.XYZ(10, 11, 10),
        DB.XYZ(50, 50, 50),
        DB.XYZ(51, 50, 50)
    ]

    n_clusters = 3
    labels = cluster_revit_points(revit_points, n_clusters)

    for pt, label in zip(revit_points, labels):
        print(f"Point ({pt.X}, {pt.Y}, {pt.Z}) is in cluster {label}")

if __name__ == "__main__":
    main()
