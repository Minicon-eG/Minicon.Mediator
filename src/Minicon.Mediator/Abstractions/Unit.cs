namespace Minicon.Mediator;

/// <summary>
/// Represents an absent value, used as the response type for requests that do not return a value.
/// </summary>
public readonly struct Unit : IEquatable<Unit>, IComparable<Unit>, IComparable
{
	/// <summary>The single <see cref="Unit"/> value.</summary>
	public static readonly Unit Value;

	/// <summary>A pre-completed <see cref="Task{Unit}"/> returning <see cref="Value"/>.</summary>
	public static readonly Task<Unit> Task = System.Threading.Tasks.Task.FromResult(Value);

	/// <inheritdoc />
	public int CompareTo(Unit other) => 0;

	int IComparable.CompareTo(object? obj) => 0;

	/// <inheritdoc />
	public override int GetHashCode() => 0;

	/// <inheritdoc />
	public bool Equals(Unit other) => true;

	/// <inheritdoc />
	public override bool Equals(object? obj) => obj is Unit;

	/// <inheritdoc />
	public override string ToString() => "()";

	public static bool operator ==(Unit left, Unit right) => true;
	public static bool operator !=(Unit left, Unit right) => false;
	public static bool operator <(Unit left, Unit right) => false;
	public static bool operator <=(Unit left, Unit right) => true;
	public static bool operator >(Unit left, Unit right) => false;
	public static bool operator >=(Unit left, Unit right) => true;
}
