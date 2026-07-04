using Colossal.UI.Binding;
using CustomLandParcel.Data;
using CustomLandParcel.Geometry;

namespace CustomLandParcel.UI
{
    internal static class ParcelUIWriter
    {
        public static void WriteParcels(IJsonWriter writer, ParcelStore store)
        {
            writer.ArrayBegin((uint)store.Parcels.Count);
            for (var i = 0; i < store.Parcels.Count; i++)
            {
                var parcel = store.Parcels[i];
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
                writer.PropertyName("min");
                writer.Write(min);
                writer.PropertyName("max");
                writer.Write(max);
                writer.PropertyName("points");
                writer.ArrayBegin((uint)parcel.Points.Count);
                for (var pointIndex = 0; pointIndex < parcel.Points.Count; pointIndex++)
                {
                    writer.Write(parcel.Points[pointIndex]);
                }

                writer.ArrayEnd();
                writer.TypeEnd();
            }

            writer.ArrayEnd();
        }
    }
}
