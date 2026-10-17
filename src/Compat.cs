// WinSpeed targets .NET Framework 4.6.2 so it runs on every Windows 10 release (1607 and later ship 4.6.2;
// Windows 11 ships 4.8) with nothing to install. C# tuples need System.ValueTuple, which only became part of
// the framework in 4.7, so we carry the small subset we use instead of shipping System.ValueTuple.dll.
#if !NET47_OR_GREATER
using System.Collections.Generic;

namespace System
{
    struct ValueTuple<T1, T2> : IEquatable<ValueTuple<T1, T2>>
    {
        public T1 Item1;
        public T2 Item2;
        public ValueTuple(T1 item1, T2 item2) { Item1 = item1; Item2 = item2; }

        public bool Equals(ValueTuple<T1, T2> o) =>
            EqualityComparer<T1>.Default.Equals(Item1, o.Item1) && EqualityComparer<T2>.Default.Equals(Item2, o.Item2);
        public override bool Equals(object obj) => obj is ValueTuple<T1, T2> o && Equals(o);
        public override int GetHashCode() =>
            unchecked(EqualityComparer<T1>.Default.GetHashCode(Item1) * 397 ^ EqualityComparer<T2>.Default.GetHashCode(Item2));
        public override string ToString() => "(" + Item1 + ", " + Item2 + ")";
    }

    struct ValueTuple<T1, T2, T3> : IEquatable<ValueTuple<T1, T2, T3>>
    {
        public T1 Item1;
        public T2 Item2;
        public T3 Item3;
        public ValueTuple(T1 item1, T2 item2, T3 item3) { Item1 = item1; Item2 = item2; Item3 = item3; }

        public bool Equals(ValueTuple<T1, T2, T3> o) =>
            EqualityComparer<T1>.Default.Equals(Item1, o.Item1) && EqualityComparer<T2>.Default.Equals(Item2, o.Item2)
            && EqualityComparer<T3>.Default.Equals(Item3, o.Item3);
        public override bool Equals(object obj) => obj is ValueTuple<T1, T2, T3> o && Equals(o);
        public override int GetHashCode() => unchecked((EqualityComparer<T1>.Default.GetHashCode(Item1) * 397
            ^ EqualityComparer<T2>.Default.GetHashCode(Item2)) * 397 ^ EqualityComparer<T3>.Default.GetHashCode(Item3));
        public override string ToString() => "(" + Item1 + ", " + Item2 + ", " + Item3 + ")";
    }
}

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Event | AttributeTargets.Field | AttributeTargets.Parameter
        | AttributeTargets.Property | AttributeTargets.ReturnValue | AttributeTargets.Struct)]
    sealed class TupleElementNamesAttribute : Attribute
    {
        public TupleElementNamesAttribute(string[] transformNames) { TransformNames = transformNames; }
        public IList<string> TransformNames { get; }
    }
}
#endif
