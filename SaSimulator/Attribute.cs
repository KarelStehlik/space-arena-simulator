using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SaSimulator
{
    internal struct Attribute<T> where T: IMultiplyOperators<T,float,T>
    {
        private T baseValue;
        private float increase;
        public T BaseValue { readonly get { return baseValue; } set { baseValue = value; Update(); } }
        public float Increase { readonly get { return increase; } set { increase = value; Update(); } }

        private void Update()
        {
            Value = baseValue * increase;
        }
        public T Value {  get; private set; }
        public static implicit operator T(Attribute<T> a) => a.Value;
        public Attribute(T value)
        {
            baseValue = value;
            increase = 1;
            Value = baseValue * increase;
        }
    }
}
