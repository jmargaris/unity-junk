using System.Threading;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// A class that allows for timing the real-time duration of code blocks after the lifetime of an app.
/// Use BeginTiming and EndTiming to measure blocks, then print out the results when you feel like it
/// </summary>
public class PersistentProfiler {
  //mapps a thread to a timing dict - which maps the strings from BeginTiming to start and end times, kind of
  private static Dictionary<int, TimingDictionary> s_threadIDToTimingDictionary = new Dictionary<int, TimingDictionary>();

  //maps a thread to a timing total dict - which maps the strings from BeginTiming to total calls, total time taken, etc 
  private static Dictionary<int, TimingTotalDictionary> s_threadIDToTimingTotalDictionary = new Dictionary<int, TimingTotalDictionary>();

  //these are just scratch things that get re-used to not go crazy with memory while printing stats 
  private static Dictionary<string, TimingCount> s_totalDict = new Dictionary<string, TimingCount>();
  private static List<TimingCount> s_allCounts = new List<TimingCount>(1024);

  private static System.Diagnostics.Stopwatch s_stopwatch = new System.Diagnostics.Stopwatch();


  /// <summary>
  /// Begins timing a section of code  - s should be a unique name
  /// </summary>
	[Conditional("ENABLE_CUSTOM_PROFILER")]
  public static void BeginTiming(string s) {
    if (!s_stopwatch.IsRunning) {
      s_stopwatch.Start();
    }

    //mark the start time for this call
    TimingList l = GetTimingList(s);
    l.Add(s_stopwatch.ElapsedMilliseconds);
  }

  /// <summary>
  /// End timing a section of code  - s should be a unique name that was used in BeginTiming
  /// </summary>
	[Conditional("ENABLE_CUSTOM_PROFILER")]
  public static void EndTiming(string s) {

    long timeNow = s_stopwatch.ElapsedMilliseconds;
    TimingList l = GetTimingList(s);
    if (l.Count == 0) {
      UnityEngine.Debug.LogError("Mismatched profiler timing for: " + s);
      return;
    }

    //pop off start time of the most recent start tracking of s
    //calc the difference from then till now and record it
    long lastTimingStart = l[l.Count - 1];
    l.RemoveAt(l.Count - 1);
    long diff = timeNow - lastTimingStart;
    AddTotalTiming(s, diff);
  }



  /// <summary>
  /// A variation on end timing that will immediately print out the timing information for symbol s
  /// </summary>
  [Conditional("ENABLE_CUSTOM_PROFILER")]
  public static void EndTimingAndReport(string s) {



    //TODO should we use ticks instead of ms?
    long timeNow = s_stopwatch.ElapsedMilliseconds;
    TimingList l = GetTimingList(s);
    if (l.Count == 0) {
      UnityEngine.Debug.LogError("Mismatched profiler timing for: " + s);
      return;
    }
    long lastTimingStart = l[l.Count - 1];
    l.RemoveAt(l.Count - 1);
    long diff = timeNow - lastTimingStart;
    TimingCount t = AddTotalTiming(s, diff);
    UnityEngine.Debug.Log(string.Format("[{0}] took [{1}ms ({2}s)] time for [{3}] calls (Avg [{4}])", s, t.totalTime, (float)t.totalTime / 1000, t.calls, t.totalTime / t.calls));
  }



  /// <summary>
  /// Prints out the poorly formatted table of timings to a string builder
  /// </summary>
  [Conditional("ENABLE_CUSTOM_PROFILER")]
  public static void Print(StringBuilder b) {
    b.Append("\nPersistentProfiler----\n");
    s_totalDict.Clear();
    lock (s_threadIDToTimingTotalDictionary) {
      foreach (KeyValuePair<int, TimingTotalDictionary> pair in s_threadIDToTimingTotalDictionary) {
        foreach (KeyValuePair<string, TimingCount> pair2 in pair.Value) {
          TimingCount count = null;
          s_totalDict.TryGetValue(pair2.Key, out count);
          if (count == null) {
            count = new TimingCount();
            s_totalDict[pair2.Key] = count;
            count.name = pair2.Key;
          }
          count.totalTime += pair2.Value.totalTime;
          count.calls += pair2.Value.calls;
        }
      }
    }

    s_allCounts.Clear();
    s_allCounts.Capacity = Mathf.Max(s_allCounts.Capacity, s_totalDict.Count);
    foreach (KeyValuePair<string, TimingCount> pair in s_totalDict) {
      s_allCounts.Add(pair.Value);
    }

    s_allCounts.Sort(SortTimingCounts);
    for (int i = s_allCounts.Count - 1; i >= 0; i--) {
      b.AppendLine(string.Format("{0}ms / {1}s] [{2}] Calls [{3}] Avg [{4}] ",
        s_allCounts[i].totalTime, (float)s_allCounts[i].totalTime / 1000, s_allCounts[i].name, s_allCounts[i].calls, s_allCounts[i].totalTime / s_allCounts[i].calls));
    }
  }


  /// <summary>
  /// Resets 
  /// </summary>
  [Conditional("ENABLE_CUSTOM_PROFILER")]
  public static void Clear() {
    s_threadIDToTimingDictionary.Clear();
    s_threadIDToTimingTotalDictionary.Clear();
    s_stopwatch.Reset();
    s_stopwatch.Stop();
    s_stopwatch.Start();
  }

  //These functions can be used to hook up the PrintStats to some event handler - 
  //like OnPrintButtonPressed += PersistentProfiler.GetPrintStatsAction()

  [Conditional("ENABLE_CUSTOM_PROFILER")]
  static public void PrintStats() {
    HandleOnPrintStats();
  }

  public static System.Action GetPrintStatsAction() {
    return HandleOnPrintStats;
  }


  private static void HandleOnPrintStats() {
    System.Text.StringBuilder b = new System.Text.StringBuilder();
    Print(b);
    UnityEngine.Debug.Log(b.ToString());
  }

  private static int SortTimingCounts(TimingCount a, TimingCount b) {
    if (a.totalTime == b.totalTime) return 0;
    else if (a.totalTime < b.totalTime) return -1;
    return 1;
  }


  /// <summary>
  /// Records a call count / time elapsed to our class that records that stuff
  /// </summary>
  private static TimingCount AddTotalTiming(string s, long diff) {
    int threadId = Thread.CurrentThread.ManagedThreadId;
    TimingTotalDictionary dictionary = null;
    lock (s_threadIDToTimingTotalDictionary) {
      s_threadIDToTimingTotalDictionary.TryGetValue(threadId, out dictionary);
      if (dictionary == null) {
        dictionary = new TimingTotalDictionary();
        s_threadIDToTimingTotalDictionary[threadId] = dictionary;
      }
    }
    TimingCount timingCount = null;
    dictionary.TryGetValue(s, out timingCount);
    if (timingCount == null) {
      timingCount = new TimingCount();
      timingCount.name = s;
      dictionary[s] = timingCount;
    }
    timingCount.totalTime += diff;
    timingCount.calls += 1;
    return timingCount;
  }

  private static TimingDictionary GetTimingDictionary() {
    int threadId = Thread.CurrentThread.ManagedThreadId;
    TimingDictionary dictionary = null;
    lock (s_threadIDToTimingDictionary) {
      s_threadIDToTimingDictionary.TryGetValue(threadId, out dictionary);
      if (dictionary == null) {
        dictionary = new TimingDictionary();
        s_threadIDToTimingDictionary[threadId] = dictionary;
      }
    }
    return dictionary;
  }

  private static TimingList GetTimingList(string s) {
    TimingDictionary dictionary = GetTimingDictionary();
    TimingList list = null;
    dictionary.TryGetValue(s, out list);
    if (list == null) {
      list = new TimingList();
      dictionary[s] = list;
    }
    return list;

  }


  //I think this is a list because in theory (and maybe in reality) we can handle cases where we call multiple begin timings
  //on the same symbol and on the same thread then undwind with back to back end timing calls
  private class TimingList : List<long> { }

  private class TimingDictionary : Dictionary<string, TimingList> { }

  private class TimingCount {
    public long totalTime = 0;
    public int calls = 0;
    public string name;
  }

  private class TimingTotalDictionary : Dictionary<string, TimingCount> { }


}

