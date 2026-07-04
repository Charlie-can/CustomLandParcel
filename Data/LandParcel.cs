using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace CustomLandParcel.Data
{
    internal enum LandParcelState
    {
        Locked = 0,
        Available = 1,
        Purchased = 2
    }

    internal sealed class LandParcel
    {
        public LandParcel(Guid id, string name, IEnumerable<float2> points)
        {
            Id = id;
            Name = string.IsNullOrWhiteSpace(name) ? "Parcel" : name;
            State = LandParcelState.Available;
            Points = new List<float2>(points);
        }

        public Guid Id { get; }

        public string Name { get; set; }

        public LandParcelState State { get; set; }

        public int Price { get; set; }

        public List<float2> Points { get; }

        public bool IsPurchased => State == LandParcelState.Purchased;

        public override string ToString()
        {
            return $"{Name} ({Id:N}, state={State}, price={Price}, vertices={Points.Count})";
        }
    }
}
