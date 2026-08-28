using System.Linq;

namespace BasicSample;

public static class ExampleFunction
{
	public static IEnumerable<int> NonZeroEnumerable(IEnumerable<int> | int input)
	{
		IEnumerable<int> normalized = input.Match(
			enumerable => enumerable,
			i => new int[] { i });

		return normalized.Where(item => item > 0);
	}

	public static string Describe3(string | int | bool input)
	{
		return input.Match(
			s => $"string: {s}",
			i => $"int: {i}",
			b => $"bool: {b}");
	}

	public static string Describe4(string | int | bool | double input)
	{
		return input.Match(
			s => $"string: {s}",
			i => $"int: {i}",
			b => $"bool: {b}",
			d => $"double: {d}");
	}
}