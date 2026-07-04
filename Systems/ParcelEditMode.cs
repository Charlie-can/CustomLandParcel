namespace CustomLandParcel.Systems
{
    internal enum ParcelEditMode
    {
        Idle,
        Drawing,
        HoverVertex,
        HoverEdge,
        HoverParcel,
        DragVertex,
        DragParcel
    }

    internal enum ParcelEditHitKind
    {
        None,
        Vertex,
        Edge,
        Parcel
    }
}
