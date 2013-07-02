using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FuzzyFileFinderNET.Extensions;

namespace VSGoToFile
{
	public class FuzzyFileFinder
	{
		public class CharacterRun
		{
			public string String { get; set; }
			public bool Inside { get; set; }

			public override string ToString()
			{
				if (Inside)
				{
					return string.Format("({0})", String);
				}
				else
				{
					return String;
				}
			}
		}

		public class FileSystemEntry
		{
			public Directory Parent { get; private set; }
			public string Name { get; private set; }

			public FileSystemEntry(Directory parent, string name)
			{
				Parent = parent;
				Name = name;
			}

			public string Path
			{
				get
				{
					return System.IO.Path.Combine(Parent.Name, Name);
				}
			}

		}

		public class Directory
		{
			public string Name { get; private set; }
			public bool IsRoot { get; private set; }
			public List<Directory> SubDirectories { get; set; }

			public Directory(string name, bool isRoot=false)
			{
				Name = name;
				IsRoot = isRoot;
				SubDirectories = new List<Directory>();
			}
		}

		private struct MatchResult
		{
			public float Score;
			public string Result;
			public bool Missed;
		}

		public struct MatchFileResult
		{
			public string Path;
			public string Abbr;
			public string Directory;
			public string Name;
			public string HighlightedDirectory;
			public string HighlightedName;
			public string HighlightedPath;
			public float Score;

			public override string ToString()
			{
				return HighlightedPath;
			}
		}

		//the roots directory trees to search
		public List<Directory> Roots { get; private set; }
		//list of files beneath all roots
		public List<FileSystemEntry> Files { get; private set; }
		public int Ceiling { get; private set; }
		public string SharedPrefix { get; private set; }
		public Regex SharedPrefixRegex { get; private set; }
		public List<string> Ignores { get; private set; }
		private char FILE_SEPARATOR = System.IO.Path.DirectorySeparatorChar;

		public FuzzyFileFinder(List<string> directories, List<string> fullFileNames, int ceiling = 10000, List<string> ignores = null)
		{
			Roots = new List<Directory>();
			Files = new List<FileSystemEntry>();

			if (fullFileNames != null && fullFileNames.Count > 0)
			{
				LoadDirectoryTreeAndFiles(fullFileNames);
			}  

			if (directories == null && fullFileNames == null)
			{
				directories = new List<string>();
				if (directories.Count == 0)
				{
					directories.Add(".");
				}
			}

			if(directories != null)
			{
				var rootDirNames = directories
					.Select(dir => Path.GetFullPath(dir))
					.Where(dir => File.GetAttributes(dir).HasFlag(FileAttributes.Directory))
					.Distinct();

				Roots.Concat(rootDirNames.Select(dir => new Directory(dir, true)).ToList());
			}
			SharedPrefix = DetermineSharedPrefix();
			SharedPrefixRegex = new Regex("^" + Regex.Escape(SharedPrefix) + (String.IsNullOrEmpty(SharedPrefix) ? "" : Regex.Escape(FILE_SEPARATOR.ToString())));

			Ceiling = ceiling;
			Ignores = ignores;

			if (!(fullFileNames != null && fullFileNames.Count > 0))
			{
				ReScan();
			}
		}

		#region Load directories tree if only fullpath filenames was provided

		private void LoadDirectoryTreeAndFiles(List<string> fullFileNames)
		{
			Roots = new List<Directory>();
			Files = new List<FileSystemEntry>();
			fullFileNames = fullFileNames.OrderBy(x => x).ToList();

			fullFileNames.ForEach(fileName =>
			{
				var segments = fileName.Split(FILE_SEPARATOR);
				Directory currentDirectory = null;
				//last element will be filename
				for (int i = 0; i < segments.Length - 1; i++)
				{
					currentDirectory = MakeDirectoryTree(currentDirectory, segments[i]);
				}
				Files.Add(new FileSystemEntry(currentDirectory, segments.Last()));
			});
		}

		private Directory MakeDirectoryTree(Directory parent, string dir)
		{
			if (parent == null)
			{
				var rootDir = Roots.Find(x => x.Name.ToLower() == dir);
				if (rootDir == null)
				{
					rootDir = new Directory(dir, true);
					Roots.Add(rootDir);
				}
				return rootDir;
			}
			string fullName = string.Empty;
			if (parent.Name.LastIndexOf(Path.VolumeSeparatorChar) == parent.Name.Length-1)
			{
				fullName = parent.Name + FILE_SEPARATOR;
			}
			else
			{
				fullName = Path.Combine(parent.Name, dir);
			}
			var subDir = parent.SubDirectories.Find(x => x.Name.ToLower() == fullName);
			if (subDir == null)
			{
				subDir = new Directory(fullName, false);
				parent.SubDirectories.Add(subDir);
			}
			return subDir;
		}

		#endregion

		#region Load directories tree from the file system

		public void ReScan()
		{
			Files.Clear();
			Roots.ForEach( root => FollowTree(root));
		}

		// Recursively scans +directory+ and all files and subdirectories
		// beneath it, depth-first.
		private void FollowTree(Directory dir)
		{
			foreach (var entry in new DirectoryInfo(dir.Name).EnumerateFileSystemInfos())
			{
				if (IsIgnored(entry.Name)) continue;
				if (Files.Count > Ceiling) throw new TooManyEntries();

				if (entry.Attributes.HasFlag(FileAttributes.Directory))
				{
					FollowTree(new Directory(entry.FullName));
				}
				else if (!IsIgnored(SharedPrefixRegex.Replace(entry.FullName, "")))
				{
					Files.Add(new FileSystemEntry(dir, entry.Name)); 
				}
			}
		}

		#endregion

		//TODO Implement this function 
		private bool IsIgnored(string name)
		{
			return false;
		}

		// Takes the given +pattern+ (which must be a string) and searches
		// all files beneath +root+, yielding each match.
		//
		// +pattern+ is interpreted thus:
		//
		// * "foo" : look for any file with the characters 'f', 'o', and 'o'
		//   in its basename (discounting directory names). The characters
		//   must be in that order.
		// * "foo/bar" : look for any file with the characters 'b', 'a',
		//   and 'r' in its basename (discounting directory names). Also,
		//   any successful match must also have at least one directory
		//   element matching the characters 'f', 'o', and 'o' (in that
		//   order.
		// * "foo/bar/baz" : same as "foo/bar", but matching two
		//   directory elements in addition to a file name of "baz".
		//
		// Each yielded match will be a hash containing the following keys:
		//
		// * :path refers to the full path to the file
		// * :directory refers to the directory of the file
		// * :name refers to the name of the file (without directory)
		// * :highlighted_directory refers to the directory of the file with
		//   matches highlighted in parentheses.
		// * :highlighted_name refers to the name of the file with matches
		//   highlighted in parentheses
		// * :highlighted_path refers to the full path of the file with
		//   matches highlighted in parentheses
		// * :abbr refers to an abbreviated form of :highlighted_path, where
		//   path segments without matches are compressed to just their first
		//   character.
		// * :score refers to a value between 0 and 1 indicating how closely
		//   the file matches the given pattern. A score of 1 means the
		//   pattern matches the file exactly.

		public void Search(string pattern, Action<MatchFileResult> block)
		{
			pattern.Replace(" ", "");
			var pathParts = pattern.Split(FILE_SEPARATOR).ToList();
			if(pathParts.Last() == FILE_SEPARATOR.ToString())
				pathParts.Add("");

			var fileNamePart = pathParts.Pop();
			string pathRegexRaw;
			Regex pathRegex = null;

			if(pathParts.Any())
			{
				pathRegexRaw = MakePathPattern(pathParts);
				pathRegex = new Regex(pathRegexRaw, RegexOptions.IgnoreCase);
			}

			string fileRegexRaw = "^(.*?)" + MakePattern(pattern) + "(.*)$";
			Regex fileRegex = new Regex(fileRegexRaw, RegexOptions.IgnoreCase);
			Dictionary<Directory, MatchResult> pathMatches = new Dictionary<Directory,MatchResult>();

			Files.ForEach(file =>
			{
				var pathMatch = MatchPath(file.Parent, pathMatches, pathRegex, pathParts.Count);
				if (!pathMatch.Missed)
				{
					MatchFile(file, fileRegex, pathMatch, block); 
				}
			});
		}

		public List<MatchFileResult> Find(string pattern, int max = Int32.MaxValue)
		{
			List<MatchFileResult> results = new List<MatchFileResult>();
			try
			{
				Search(pattern, (match) =>
				{
					results.Add(match);
					if(results.Count >= max)
						throw new EndBlockException();
				});
			}
			catch(EndBlockException)
			{

			}
			return results;
		}

		public class EndBlockException : Exception { }
		public class TooManyEntries : Exception { }

		// Takes the given pattern string "foo" and converts it to a new
		// string in unix systems "(f)([^/]*?)(o)([^/]*?)(o)" that can be used to create
		// a regular expression.
		public string MakePattern(string pattern)
		{
			var patterns = pattern.ToCharArray().Select(x => x.ToString()).ToList();
			if (patterns.Count == 0) patterns.Add("");
			StringBuilder strBuilder = new StringBuilder(); 
			patterns.ForEach(character =>
			{
				if (strBuilder.Length > 0)
				{
					strBuilder.Append("([^" + Regex.Escape(FILE_SEPARATOR.ToString()) + "]*?)");
				}
				strBuilder.Append("(" + Regex.Escape(character) + ")");
			});
			return strBuilder.ToString();
		}

		public string MakePathPattern(List<string> pathParts)
		{
			return "^(.*?)" + pathParts.Select(part => MakePattern(part)).Aggregate((str1, str2) => str1 + "(.*?" + Regex.Escape(FILE_SEPARATOR.ToString()) + ".*?)" + str2) + "(.*?)$";
		}

		// Match the given path against the regex, caching the result in +path_matches+.
		// If +path+ is already cached in the path_matches cache, just return the cached
		// value.
		private MatchResult MatchPath(Directory path, Dictionary<Directory, MatchResult> pathMatches, Regex pathRegex, int pathSegments)
		{
			if (pathMatches.ContainsKey(path))
				return pathMatches[path];

			var nameWithSlash = path.Name + FILE_SEPARATOR; //add a trailing slash for matching the prefix
			var matchableName = SharedPrefixRegex.Replace(nameWithSlash, "");
			matchableName = matchableName.Remove(matchableName.Length-1,1);
			
			if(pathRegex != null)
			{
				var match = matchableName.Match(pathRegex);
				pathMatches.Add(path, match.Length > 0 ? BuildMatchResult(match, pathSegments) : new MatchResult { Score = 1, Result = matchableName, Missed = true });
			}
			else
			{
				pathMatches.Add(path, new MatchResult { Score = 1, Result = matchableName });
			}
			return pathMatches[path];
		}

		// Match +file+ against +file_regex+. If it matches, yield the match
		//metadata to the block.
		private void MatchFile(FileSystemEntry file, Regex fileRegex, MatchResult pathMatch, Action<MatchFileResult> block)
		{
			var fileMatch = file.Name.Match(fileRegex);
			if (fileMatch.Length > 0)
			{
				var matchResult = BuildMatchResult(fileMatch, 1);
				var fullMatchResult = pathMatch.Result == string.Empty ? matchResult.Result : Path.Combine(pathMatch.Result, matchResult.Result);
				var shortendedPath = Regex.Replace(pathMatch.Result, @"[^\/]+", new MatchEvaluator((match) => match.Value.IndexOf('(') != -1 ? match.Value : match.Value.Substring(0, 1)));
				var abbr = shortendedPath == string.Empty ? matchResult.Result : Path.Combine(shortendedPath, matchResult.Result);

				var result = new MatchFileResult
				{
					Path = file.Path,
					Abbr = abbr,
					Directory = file.Parent.Name,
					HighlightedDirectory = pathMatch.Result,
					HighlightedName = matchResult.Result,
					HighlightedPath = fullMatchResult,
					Score = pathMatch.Score * matchResult.Score
				};
				block(result);
			}
		}

		private string DetermineSharedPrefix()
		{
			if (Roots.Count == 0)
				return string.Empty;
			//the common case: if there is only a single root, then the entire
			//name of the root is the shared prefix.
			if (Roots.Count == 1)
			{
				return Roots.First().Name;
			}

			List<string[]> splitRoots = Roots.Select(root => root.Name.Split(FILE_SEPARATOR)).ToList();
			int segments = splitRoots.Max(x => x.Length);

			string[] master = splitRoots.Pop();

			for (int segment = 0; segment < segments; segment++)
			{
				if (!splitRoots.All(x => x[segment] == master[segment]))
				{
					return String.Join(FILE_SEPARATOR.ToString(), master.Take(segment));
				}
			}

			// shouldn't ever get here, since we uniq the root list before
			// calling this method, but if we do, somehow...
			return Roots.First().Name;
		}

		// Given a MatchData object +match+ and a number of "inside"
		// segments to support, compute both the match score and  the
		// highlighted match string. The "inside segments" refers to how
		// many patterns were matched in this one match. For a file name,
		// this will always be one. For directories, it will be one for
		// each directory segment in the original pattern.
		private MatchResult BuildMatchResult(Match match, int insideSegments)
		{
			var runs = new List<CharacterRun>();
			int insideChars = 0;
			int totalChars = 0;

			foreach (Capture capture in match.Captures)
			{
				if (capture.Length > 0)
				{
					// odd-numbered captures are matches inside the pattern.
					// even-numbered captures are matches between the pattern's elements.

					bool inside = capture.Index % 2 != 0;

					totalChars += Regex.Replace(capture.Value, "(" + Regex.Escape(FILE_SEPARATOR.ToString()) + ")", "", RegexOptions.IgnoreCase).Length;
					if (inside) insideChars += capture.Value.Length;

					var lastCharacterRun = runs.LastOrDefault();
					if (lastCharacterRun != null && lastCharacterRun.Inside == inside)
						lastCharacterRun.String += capture.Value;
					else
						runs.Add(new CharacterRun { Inside = inside, String = capture.Value });
				}
			}

			// Determine the score of this match.
			// 1. fewer "inside runs" (runs corresponding to the original pattern)
			//    is better.
			// 2. better coverage of the actual path name is better

			var insideRuns = runs.Select(x => x.Inside);
			var runRatio = insideRuns.Count() == 0 ? 1 : insideSegments / (float)insideRuns.Count();
			var charRatio = totalChars == 0 ? 1 : (float)insideChars / totalChars;

			var score = runRatio * charRatio;

			return new MatchResult { Score = score, Result = String.Join("", runs.Select(x => x.ToString())) };
		}
	}
}
