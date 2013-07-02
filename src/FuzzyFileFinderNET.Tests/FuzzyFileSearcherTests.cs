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
		private FuzzyFileFinder _finder;

		[SetUp]
		public void SetUp()
		{
			_finder = new FuzzyFileFinder(null, new List<string>());
		}

		[Test]
		public void is_making_the_right_file_pattern()
		{
			string escapePattern = string.Format("(f)([^{0}]*?)(o)([^{0}]*?)(o)", Regex.Escape(Path.DirectorySeparatorChar.ToString()));
			string enterPattern = "foo";

			Assert.AreEqual(escapePattern, _finder.MakePattern(enterPattern));
		}

		[Test]
		public void is_making_the_right_path_pattern()
		{
			string escapePattern = string.Format("^(.*?)(c)([^{0}]*?)(o)([^{0}]*?)(n)([^{0}]*?)(n)([^{0}]*?)(t)(.*?{0}.*?)(a)([^{0}]*?)(d)([^{0}]*?)(m)(.*?{0}.*?)(h)([^{0}]*?)(o)([^{0}]*?)(m)(.*?)$", Regex.Escape(Path.DirectorySeparatorChar.ToString()));
			List<string> enterPathParts = new List<string> { "connt", "adm", "hom" };

			Assert.AreEqual(escapePattern, _finder.MakePathPattern(enterPathParts));
		}
	}
}
