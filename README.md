# unity-junk
Misc Unity junk

Right now this only contains PersistentProfiler. PersistentProfiler is a class for measuring the real time duration of various operations on any thread. Unlike the Unity Profiler, which provides per-frame metrics, PersistentProfiler is designed to provide long-term metrics. For example if you have AI pathing routines you could wrap them in PersistentProfiler calls, run your game for 10 minutes, then see how often those routines were called and how much time they took total.

PersistentProfiler measures real time, not CPU time. So if you profile downloading an asset bundle that takes 10 seconds to complete the recorded time will be 10 seconds, regardless of how much time was CPU. This is by design. 

### Usage

Use **BeginTiming** and **EndTiming** to wrap sections of code you want to measure, passing in a unique name for that section.

**Print** can be used that prints out a table of results, sorted from most to least time taken. **GetPrintStatsAction** returns an action that can be used to hook into an event handler, so you can do something like OnPrintInfo += GetPrintStatsAction

**Clear** resets the Profiler. 


### Details

**The profiler always no-ops unless ENABLE_CUSTOM_PROFILER is defined.** For production builds you probably want to leave this undefined.

Our version of this uses our own logger that has various reporting types and level  - I've replaced those calls with Debug.Log, but if you have something more sophisticated you may want to replace those with your own logging system.

The PersistentProfiler uses milliseconds and it's basis for measurement. If you want to profile very small fast sections of code you can swap **ElapsedMilliseconds** to **ElapsedTicks**.  
