using System;
using System.IO;
using System.Linq;
using DailySprint;

internal static class AppCoreTests
{
    private static int assertions;

    private static void Assert(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new Exception("Test failed: " + message);
    }

    private static void Main()
    {
        DateTime day = new DateTime(2026, 1, 6);
        AppData data = AppCore.CreateDefaultData();
        Habit habit = data.Habits[0];
        data.Habits = new System.Collections.Generic.List<Habit>
        {
            new Habit { Id = habit.Id, Name = habit.Name, CreatedOn = AppCore.DateKey(day.AddDays(-2)), ArchivedOn = "" }
        };
        habit = data.Habits[0];
        data.Completions[AppCore.DateKey(day.AddDays(-1))] = new System.Collections.Generic.List<string> { habit.Id };
        data.Completions[AppCore.DateKey(day)] = new System.Collections.Generic.List<string> { habit.Id };

        Assert(AppCore.GetCurrentStreak(data, day) == 2, "streak counts consecutive completed days through today");
        data.Completions.Remove(AppCore.DateKey(day));
        Assert(AppCore.GetCurrentStreak(data, day) == 1, "streak continues through yesterday when today is incomplete");
        Assert(AppCore.GetCurrentStreak(data, day.AddDays(-3)) == 0, "streak does not bridge an incomplete day");

        data.Expenses[AppCore.DateKey(day)] = new System.Collections.Generic.List<Expense>
        {
            new Expense { Id = "e1", Title = "Lunch", Amount = 12.50m, Category = "Food & Dining", Date = AppCore.DateKey(day) },
            new Expense { Id = "e2", Title = "Bus", Amount = 2.25m, Category = "Transit & Gas", Date = AppCore.DateKey(day) }
        };
        Assert(AppCore.GetSpent(data, day) == 14.75m, "daily totals use exact decimal arithmetic");
        Assert(AppCore.GetCompletedCount(data, day.AddDays(-1)) == 1, "habit completion is kept per date");
        Assert(AppCore.FormatMoney(1234.5m, "PHP").StartsWith("\u20B1"), "currency setting formats Philippine pesos");
        habit.ArchivedOn = AppCore.DateKey(day);
        Assert(AppCore.GetHabitsForDay(data, day).Count == 1, "a habit remains available on its archive date");
        Assert(AppCore.GetHabitsForDay(data, day.AddDays(-1)).Count == 1, "archiving preserves earlier habit history");
        Assert(AppCore.GetHabitsForDay(data, day.AddDays(1)).Count == 0, "an archived habit is not scheduled later");
        habit.ArchivedOn = "";

        string serialized = AppCore.Serialize(data);
        AppData restored = AppCore.Deserialize(serialized);
        Assert(AppCore.GetSpent(restored, day) == 14.75m, "JSON backup round-trips expense data");
        Assert(restored.Completions[AppCore.DateKey(day.AddDays(-1))].Contains(habit.Id), "JSON backup round-trips habit history");
        bool rejectsUnknownHabit = false;
        AppData invalid = AppCore.Deserialize(serialized);
        invalid.Completions[AppCore.DateKey(day)] = new System.Collections.Generic.List<string> { "missing-habit" };
        try { AppCore.Serialize(invalid); }
        catch (InvalidDataException) { rejectsUnknownHabit = true; }
        Assert(rejectsUnknownHabit, "backups cannot attach completions to unknown habits");
        bool rejectsUnsupportedDate = false;
        try
        {
            AppData invalidDate = AppCore.Deserialize(serialized);
            invalidDate.Completions["0001-01-01"] = new System.Collections.Generic.List<string>();
            AppCore.Serialize(invalidDate);
        }
        catch (InvalidDataException) { rejectsUnsupportedDate = true; }
        Assert(rejectsUnsupportedDate, "imported dates stay within the desktop date picker range");

        string folder = Path.Combine(Path.GetTempPath(), "DailySprint-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new DataStore(Path.Combine(folder, "data.json"));
            AppData initial = store.Load();
            Assert(initial.Habits.Count == 3, "new data store creates starter habits");
            initial.DailyBudget = 85m;
            store.Save(initial);
            Assert(store.Load().DailyBudget == 85m, "data store persists settings");
            initial.DailyBudget = 90m;
            store.Save(initial);
            Assert(store.Load().DailyBudget == 90m, "repeated saves replace the prior backup safely");
            File.WriteAllText(store.FilePath, "not valid json");
            bool failedClearly = false;
            try { store.Load(); }
            catch (IOException ex) { failedClearly = ex.Message.Contains("original data file has not been changed"); }
            Assert(failedClearly, "corrupt local data is reported without silently resetting it");
            Assert(File.ReadAllText(store.FilePath) == "not valid json", "failed load leaves original data untouched");
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        Console.WriteLine("Passed " + assertions + " DailySprint core assertions.");
    }
}
