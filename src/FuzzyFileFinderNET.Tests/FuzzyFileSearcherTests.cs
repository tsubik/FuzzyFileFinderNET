using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;


namespace VSGoToFile.Tests
{
	[TestFixture]
	public class FuzzyFileSearcherTests
	{
		[Test]
		public void is_making_the_right_pattern()
		{
			FuzzyFileFinder searcher = new FuzzyFileFinder(null, new List<string>());
			string escapePattern = string.Format("(f)([^{0}]*?)(o)([^{0}]*?)(o)", Regex.Escape(Path.DirectorySeparatorChar.ToString));
			string enterPattern = "foo";

			Assert.AreEqual(escapePattern, searcher.MakePattern(enterPattern));
		}
	}
}
