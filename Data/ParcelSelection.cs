using System;

namespace CustomLandParcel.Data
{
    internal struct ParcelSelection
    {
        public ParcelSelection(Guid parcelId, int vertexIndex)
        {
            ParcelId = parcelId;
            VertexIndex = vertexIndex;
        }

        public Guid ParcelId { get; set; }

        public int VertexIndex { get; set; }

        public static ParcelSelection Empty => new ParcelSelection(Guid.Empty, -1);
    }
}
