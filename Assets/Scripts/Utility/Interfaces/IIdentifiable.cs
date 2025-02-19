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

    [Serializable]
    public abstract class Identifiable : IIdentifiable
    {
        [SerializeField] protected string _name;
        public int Id { get; protected set; }
        public string Name => _name;

        protected virtual void Initialize(string typeName, params object[] attributes)
        {
            Id = IdGenerator.CalcId(typeName, attributes);
            _name = $"{typeName}-{Id}";
        }
    }
}
