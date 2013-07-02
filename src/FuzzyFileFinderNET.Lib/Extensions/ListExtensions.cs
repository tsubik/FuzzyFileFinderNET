using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FuzzyFileFinderNET.Extensions
{
	public static class ListExtensions
	{
		public static T Pop<T>(this List<T> list)
		{
			T obj = list.LastOrDefault();
			list.RemoveAt(list.Count - 1);
			return obj;  
		}
	}
}
