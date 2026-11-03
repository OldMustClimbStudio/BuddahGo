using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace BuddahGo.Tests
{
    // A merged table-driven test runs every case before it fails, so one run still reports each failing input. The
    // Unity NUnit build (com.unity.ext.nunit 1.0.6, NUnit 3.5) has no Assert.Multiple.
    internal static class EveryCase
    {
        public static void Check<T>(IEnumerable<T> cases, Action<T> check, Func<T, string> describe = null)
        {
            var failures = new List<string>();
            foreach (T item in cases)
            {
                try { check(item); }
                catch (Exception e) when (e is AssertionException || !(e is ResultStateException))
                {
                    string label = describe != null ? describe(item) : "case " + item;
                    failures.Add(label + ": " + (e is AssertionException ? e.Message.Trim() : e.ToString()));
                }
            }
            if (failures.Count > 0)
                Assert.Fail(failures.Count + " failed:\n" + string.Join("\n", failures));
        }

        // Independent parts of one test: each part runs even when an earlier one fails.
        public static void Parts(params (string name, Action check)[] parts) => Check(parts, p => p.check(), p => p.name);
    }
}
