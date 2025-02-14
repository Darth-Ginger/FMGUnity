using System;
using UnityEngine;

namespace FMGUnity.Utility.Interfaces
{
    public interface IIdentifiable
    {
        public Guid Id { get; }
    }
}
