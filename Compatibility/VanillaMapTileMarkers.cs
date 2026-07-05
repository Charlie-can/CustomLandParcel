using Unity.Entities;

namespace CustomLandParcel.Compatibility
{
    internal struct VanillaMapTileBlocker : IComponentData
    {
    }

    internal struct VanillaMapTileUnlockedByParcel : IComponentData
    {
    }

    internal struct VanillaMapTileLockedByParcel : IComponentData
    {
    }

    internal struct VanillaMapTileHiddenByParcelSetting : IComponentData
    {
    }
}
