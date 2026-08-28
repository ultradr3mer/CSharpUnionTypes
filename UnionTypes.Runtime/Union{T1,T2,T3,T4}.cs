namespace UnionTypes;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// A discriminated union of four variants.
/// </summary>
public readonly struct Union<T1, T2, T3, T4> : IEquatable<Union<T1, T2, T3, T4>>
{
    private const byte TagDefault = 0;
    private const byte TagT1 = 1;
    private const byte TagT2 = 2;
    private const byte TagT3 = 3;
    private const byte TagT4 = 4;

    private readonly object? _value;
    private readonly byte _tag;

    static Union()
    {
        var t1 = typeof(T1); var t2 = typeof(T2); var t3 = typeof(T3); var t4 = typeof(T4);
        if (t1 == t2 || t1 == t3 || t1 == t4 || t2 == t3 || t2 == t4 || t3 == t4)
        {
            throw new InvalidOperationException(
                $"Union<{t1.Name}, {t2.Name}, {t3.Name}, {t4.Name}>: identical variant types are not supported.");
        }
    }

    public Union(T1 value) { _value = value; _tag = TagT1; }
    public Union(T2 value) { _value = value; _tag = TagT2; }
    public Union(T3 value) { _value = value; _tag = TagT3; }
    public Union(T4 value) { _value = value; _tag = TagT4; }

    public bool IsT1 => _tag == TagT1;
    public bool IsT2 => _tag == TagT2;
    public bool IsT3 => _tag == TagT3;
    public bool IsT4 => _tag == TagT4;

    public bool TryGetT1([MaybeNullWhen(false)] out T1 value)
    {
        if (_tag == TagT1) { value = (T1)(_value!); return true; }
        value = default!; return false;
    }

    public bool TryGetT2([MaybeNullWhen(false)] out T2 value)
    {
        if (_tag == TagT2) { value = (T2)(_value!); return true; }
        value = default!; return false;
    }

    public bool TryGetT3([MaybeNullWhen(false)] out T3 value)
    {
        if (_tag == TagT3) { value = (T3)(_value!); return true; }
        value = default!; return false;
    }

    public bool TryGetT4([MaybeNullWhen(false)] out T4 value)
    {
        if (_tag == TagT4) { value = (T4)(_value!); return true; }
        value = default!; return false;
    }

    public TResult Match<TResult>(
        Func<T1, TResult> onT1, Func<T2, TResult> onT2,
        Func<T3, TResult> onT3, Func<T4, TResult> onT4)
    {
        return _tag switch
        {
            TagT1 => onT1((T1)(_value!)),
            TagT2 => onT2((T2)(_value!)),
            TagT3 => onT3((T3)(_value!)),
            TagT4 => onT4((T4)(_value!)),
            _ => throw new InvalidOperationException("Union is uninitialized."),
        };
    }

    public void Switch(
        Action<T1> onT1, Action<T2> onT2,
        Action<T3> onT3, Action<T4> onT4)
    {
        switch (_tag)
        {
            case TagT1: onT1((T1)(_value!)); return;
            case TagT2: onT2((T2)(_value!)); return;
            case TagT3: onT3((T3)(_value!)); return;
            case TagT4: onT4((T4)(_value!)); return;
            default: throw new InvalidOperationException("Union is uninitialized.");
        }
    }

    public static implicit operator Union<T1, T2, T3, T4>(T1 value) => new(value);
    public static implicit operator Union<T1, T2, T3, T4>(T2 value) => new(value);
    public static implicit operator Union<T1, T2, T3, T4>(T3 value) => new(value);
    public static implicit operator Union<T1, T2, T3, T4>(T4 value) => new(value);

    public bool Equals(Union<T1, T2, T3, T4> other)
    {
        if (_tag != other._tag) return false;
        if (_tag == TagDefault) return true;
        return Equals(_value, other._value);
    }

    public override bool Equals(object? obj) => obj is Union<T1, T2, T3, T4> other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(_tag, _value);

    public override string? ToString() => _tag switch
    {
        TagT1 => $"T1({_value})",
        TagT2 => $"T2({_value})",
        TagT3 => $"T3({_value})",
        TagT4 => $"T4({_value})",
        _ => "Union(uninitialized)",
    };

    public static bool operator ==(Union<T1, T2, T3, T4> left, Union<T1, T2, T3, T4> right) => left.Equals(right);
    public static bool operator !=(Union<T1, T2, T3, T4> left, Union<T1, T2, T3, T4> right) => !left.Equals(right);
}