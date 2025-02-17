using System;
using FMGUnity.Utility.Serials;
using UnityEngine;

namespace FMGUnity.Utility.Interfaces
{
    public interface IIdentifiable
    {
        public SerialGuid Id { get; }
    }
}
