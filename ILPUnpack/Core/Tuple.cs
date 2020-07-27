namespace ILPUnpack.Core {
	public sealed class Tuple<T1, T2> {
		public T1 Item1 { get; set; }

		public T2 Item2 { get; set; }

		public override bool Equals(object obj) {
			if (obj is not Tuple<T1, T2> other)
				return false;
			return Item1.Equals(other.Item1) && Item2.Equals(other.Item2);

		}
		public override int GetHashCode() => Item1.GetHashCode() + Item2.GetHashCode();

		public override string ToString() => "(" + Item1 + ", " + Item2 + ")";
	}
}
