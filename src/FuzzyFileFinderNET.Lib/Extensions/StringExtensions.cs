using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FuzzyFileFinderNET.Extensions
{
	public static class StringExtensions
	{
		public static Match Match(this string value, Regex regex)
		{
			return regex.Match(value);
		}

		public static string[] Split(this string value, char separator)
		{
			return value.Split(new char[] { separator });
		}

	}
}
