namespace UnionTypes;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// A discriminated union of exactly two variants <typeparamref name="T1"/> and <typeparamref name="T2"/>.
/// </summary>
/// <typeparam name="T1">The first variant type.</typeparam>
/// <typeparam name="T2">The second variant type.</typeparam>
/// <remarks>
/// <para>
/// The active variant is tracked by an internal tag so that <see langword="null"/> values
/// are handled correctly (a typed <c>null</c> for <typeparamref name="T1"/> is distinct from
/// a typed <c>null</c> for <typeparamref name="T2"/>).
/// </para>
/// <para>
/// Identical variant types (<c>Union&lt;T, T&gt;</c>) are rejected by the constructor because
/// the active variant could not be determined unambiguously. Overlapping types such as
/// <c>Union&lt;object, string&gt;</c> are allowed but may be ambiguous on assignment; the
/// implicit operators prefer <typeparamref name="T2"/> when both could apply.
/// </para>
/// </remarks>
public readonly struct Union<T1, T2> : IEquatable<Union<T1, T2>>
{
    private const byte TagDefault = 0;
    private const byte TagT1 = 1;
    private const byte TagT2 = 2;

    private readonly object? _value;
    private readonly byte _tag;

    static Union()
    {
        if (typeof(T1) == typeof(T2))
        {
            throw new InvalidOperationException(
                $"Union<{typeof(T1).Name}, {typeof(T2).Name}>: identical variant types are not supported.");
        }
    }

    /// <summary>
    /// Initializes a new instance holding a value of the first variant.
    /// </summary>
    /// <param name="value">The value of type <typeparamref name="T1"/>.</param>
    public Union(T1 value)
    {
        _value = value;
        _tag = TagT1;
    }

    /// <summary>
    /// Initializes a new instance holding a value of the second variant.
    /// </summary>
    /// <param name="value">The value of type <typeparamref name="T2"/>.</param>
    public Union(T2 value)
    {
        _value = value;
        _tag = TagT2;
    }

    /// <summary>
    /// Gets a value indicating whether the active variant is <typeparamref name="T1"/>.
    /// </summary>
    public bool IsT1 => _tag == TagT1;

    /// <summary>
    /// Gets a value indicating whether the active variant is <typeparamref name="T2"/>.
    /// </summary>
    public bool IsT2 => _tag == TagT2;

    /// <summary>
    /// Attempts to retrieve the value as <typeparamref name="T1"/>.
    /// </summary>
    /// <param name="value">When this method returns <see langword="true"/>, the value of the first variant; otherwise the default value of <typeparamref name="T1"/>.</param>
    /// <returns><see langword="true"/> if the active variant is <typeparamref name="T1"/>.</returns>
    public bool TryGetT1([MaybeNullWhen(false)] out T1 value)
    {
        if (_tag == TagT1)
        {
            value = (T1)(_value!);
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>
    /// Attempts to retrieve the value as <typeparamref name="T2"/>.
    /// </summary>
    /// <param name="value">When this method returns <see langword="true"/>, the value of the second variant; otherwise the default value of <typeparamref name="T2"/>.</param>
    /// <returns><see langword="true"/> if the active variant is <typeparamref name="T2"/>.</returns>
    public bool TryGetT2([MaybeNullWhen(false)] out T2 value)
    {
        if (_tag == TagT2)
        {
            value = (T2)(_value!);
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>
    /// Matches the active variant against the supplied delegates and returns the result.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="onT1">Invoked when the active variant is <typeparamref name="T1"/>.</param>
    /// <param name="onT2">Invoked when the active variant is <typeparamref name="T2"/>.</param>
    /// <returns>The value produced by the matching delegate.</returns>
    /// <exception cref="InvalidOperationException">The union is uninitialized (neither variant active).</exception>
    public TResult Match<TResult>(Func<T1, TResult> onT1, Func<T2, TResult> onT2)
    {
        return _tag switch
        {
            TagT1 => onT1((T1)(_value!)),
            TagT2 => onT2((T2)(_value!)),
            _ => throw new InvalidOperationException("Union is uninitialized."),
        };
    }

    /// <summary>
    /// Dispatches the active variant to the supplied actions.
    /// </summary>
    /// <param name="onT1">Invoked when the active variant is <typeparamref name="T1"/>.</param>
    /// <param name="onT2">Invoked when the active variant is <typeparamref name="T2"/>.</param>
    /// <exception cref="InvalidOperationException">The union is uninitialized (neither variant active).</exception>
    public void Switch(Action<T1> onT1, Action<T2> onT2)
    {
        switch (_tag)
        {
            case TagT1:
                onT1((T1)(_value!));
                return;
            case TagT2:
                onT2((T2)(_value!));
                return;
            default:
                throw new InvalidOperationException("Union is uninitialized.");
        }
    }

    /// <summary>
    /// Implicitly converts a value of <typeparamref name="T1"/> into a union holding the first variant.
    /// </summary>
    public static implicit operator Union<T1, T2>(T1 value) => new(value);

    /// <summary>
    /// Implicitly converts a value of <typeparamref name="T2"/> into a union holding the second variant.
    /// </summary>
    public static implicit operator Union<T1, T2>(T2 value) => new(value);

    /// <inheritdoc/>
    public bool Equals(Union<T1, T2> other)
    {
        if (_tag != other._tag)
        {
            return false;
        }

        if (_tag == TagDefault)
        {
            return true;
        }

        return Equals(_value, other._value);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Union<T1, T2> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(_tag, _value);
    }

    /// <inheritdoc/>
    public override string? ToString()
    {
        return _tag switch
        {
            TagT1 => $"T1({_value})",
            TagT2 => $"T2({_value})",
            _ => "Union(uninitialized)",
        };
    }

    /// <summary>Equality operator.</summary>
    public static bool operator ==(Union<T1, T2> left, Union<T1, T2> right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(Union<T1, T2> left, Union<T1, T2> right) => !left.Equals(right);
}
