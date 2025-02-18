using System;
using FMGUnity.Utility.Serials;
using UnityEngine;

namespace FMGUnity.Utility.Interfaces
{
    public interface IIdentifiable
    {
        int Id { get; } // Integer ID
        string Name { get; } // String Name
    }

    public abstract class Identifiable : IIdentifiable
    {
        public int Id { get; protected set; }
        public string Name { get; protected set; }

        protected virtual void Initialize(string typeName, params object[] attributes)
        {
            Id = IdGenerator.CalcId(typeName, attributes);
            Name = $"{typeName}-{Id}";
        }
    }
}
