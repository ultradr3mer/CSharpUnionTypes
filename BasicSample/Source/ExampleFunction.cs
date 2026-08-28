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
}