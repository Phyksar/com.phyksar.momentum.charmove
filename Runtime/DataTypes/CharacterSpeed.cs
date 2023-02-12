using System;
using UnityEngine;

namespace Momentum.DataTypes
{
    [Serializable]
    public struct CharacterSpeed
    {
        [Min(0.0f)]
        public float maxSpeed;

        [Min(0.0f)]
        public float acceleration;

        [Min(0.0f)]
        public float friction;
    }
}
