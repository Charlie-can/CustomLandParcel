using Colossal.UI.Binding;
using CustomLandParcel.Data;
using CustomLandParcel.Geometry;
using Unity.Mathematics;

namespace CustomLandParcel.UI
{
    internal static class ParcelUIWriter
    {
        public static void WriteParcels(IJsonWriter writer, ParcelStore store)
        {
            writer.ArrayBegin((uint)store.Parcels.Count);
            foreach (var parcel in store.Parcels)
            {
                PolygonMath.TryGetBounds(parcel.Points, out var min, out var max);
                writer.TypeBegin("customLandParcel.Parcel");
                writer.PropertyName("id");
                writer.Write(parcel.Id.ToString("N"));
                writer.PropertyName("name");
                writer.Write(parcel.Name);
                writer.PropertyName("state");
                writer.Write(parcel.State.ToString());
                writer.PropertyName("price");
                writer.Write(parcel.Price);
                writer.PropertyName("area");
                writer.Write(PolygonMath.Area(parcel.Points));
                writer.PropertyName("selected");
                writer.Write(parcel.Id == store.SelectedParcelId);
                writer.PropertyName("boundaryRed");
                writer.Write(parcel.BoundaryRed);
                writer.PropertyName("boundaryGreen");
                writer.Write(parcel.BoundaryGreen);
                writer.PropertyName("boundaryBlue");
                writer.Write(parcel.BoundaryBlue);
                writer.PropertyName("boundaryOpacity");
                writer.Write(parcel.BoundaryOpacity);
                writer.PropertyName("fillOpacity");
                writer.Write(parcel.FillOpacity);
                writer.PropertyName("boundaryWidth");
                writer.Write(parcel.BoundaryWidth);
                writer.PropertyName("min");
                writer.Write(min);
                writer.PropertyName("max");
                writer.Write(max);
                writer.PropertyName("points");
                writer.ArrayBegin((uint)parcel.Points.Count);
                foreach (var t in parcel.Points)
                {
                    writer.Write(t);
                }

                writer.ArrayEnd();
                writer.TypeEnd();
            }

            writer.ArrayEnd();
        }
    }
}
