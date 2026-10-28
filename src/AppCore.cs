using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace DailySprint
{
    public sealed class Habit
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string CreatedOn { get; set; }
        public string ArchivedOn { get; set; }
    }

    public sealed class Expense
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public string Notes { get; set; }
        public decimal Amount { get; set; }
        public string Date { get; set; }
        public string Time { get; set; }
    }

    public sealed class AppData
    {
        public int Version { get; set; }
        public decimal DailyBudget { get; set; }
        public string CurrencyCode { get; set; }
        public List<Habit> Habits { get; set; }
        public Dictionary<string, List<Expense>> Expenses { get; set; }
        public Dictionary<string, List<string>> Completions { get; set; }
    }

    public static class AppCore
    {
        public static readonly string[] Categories = new string[]
        {
            "Food & Dining",
            "Transit & Gas",
            "Bills & Utilities",
            "Shopping & Retail",
            "Entertainment",
            "Health & Wellness",
            "Home",
            "Other"
        };

        public static readonly string[] CurrencyCodes = new string[]
        {
            "USD", "PHP", "EUR", "GBP", "JPY", "CAD", "AUD", "SGD"
        };

        public static string DateKey(DateTime date)
        {
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        public static AppData CreateDefaultData()
        {
            string today = DateKey(DateTime.Today);
            return new AppData
            {
                Version = 1,
                DailyBudget = 50m,
                CurrencyCode = "USD",
                Habits = new List<Habit>
                {
                    new Habit { Id = Guid.NewGuid().ToString("N"), Name = "Drink 2L Water", CreatedOn = today, ArchivedOn = "" },
                    new Habit { Id = Guid.NewGuid().ToString("N"), Name = "20-minute Reading", CreatedOn = today, ArchivedOn = "" },
                    new Habit { Id = Guid.NewGuid().ToString("N"), Name = "Daily Physical Activity", CreatedOn = today, ArchivedOn = "" }
                },
                Expenses = new Dictionary<string, List<Expense>>(StringComparer.Ordinal),
                Completions = new Dictionary<string, List<string>>(StringComparer.Ordinal)
            };
        }

        public static AppData Normalize(AppData data)
        {
            if (data == null)
                throw new InvalidDataException("The selected file does not contain a DailySprint backup.");

            if (data.Version < 1 || data.Version > 1)
                throw new InvalidDataException("This backup was created by an unsupported DailySprint version.");

            if (data.DailyBudget < 0 || data.DailyBudget > 1000000000m)
                throw new InvalidDataException("The daily budget in this file is invalid.");

            if (data.Habits == null || data.Expenses == null || data.Completions == null)
                throw new InvalidDataException("The backup is incomplete or has an invalid data format.");

            if (data.Habits.Count > 200)
                throw new InvalidDataException("The backup contains more than 200 habits.");

            if (!CurrencyCodes.Contains(data.CurrencyCode))
                throw new InvalidDataException("The currency code in this file is not supported.");

            var habitIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (Habit habit in data.Habits)
            {
                if (habit == null || String.IsNullOrWhiteSpace(habit.Id) || String.IsNullOrWhiteSpace(habit.Name))
                    throw new InvalidDataException("A habit in this file is missing its name or identifier.");

                habit.Name = habit.Name.Trim();
                if (habit.Name.Length > 80 || habit.Id.Length > 64 || !habitIds.Add(habit.Id))
                    throw new InvalidDataException("The backup contains a duplicate or invalid habit.");

                if (String.IsNullOrEmpty(habit.CreatedOn))
                    habit.CreatedOn = DateKey(DateTime.Today);
                ValidateDateKey(habit.CreatedOn);
                if (!String.IsNullOrEmpty(habit.ArchivedOn))
                {
                    ValidateDateKey(habit.ArchivedOn);
                    if (String.CompareOrdinal(habit.ArchivedOn, habit.CreatedOn) < 0)
                        throw new InvalidDataException("A habit cannot be archived before it was created.");
                }
            }

            var expenseIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<Expense>> day in data.Expenses)
            {
                ValidateDateKey(day.Key);
                if (day.Value == null)
                    throw new InvalidDataException("An expense list in this backup is invalid.");
                if (day.Value.Count > 10000)
                    throw new InvalidDataException("An expense date contains too many entries.");

                foreach (Expense expense in day.Value)
                {
                    if (expense == null || String.IsNullOrWhiteSpace(expense.Id) ||
                        String.IsNullOrWhiteSpace(expense.Title) || expense.Amount <= 0 ||
                        expense.Amount > 1000000000m)
                        throw new InvalidDataException("An expense in this backup is invalid.");

                    expense.Title = expense.Title.Trim();
                    if (expense.Title.Length > 120 || expense.Id.Length > 64 ||
                        !expenseIds.Add(expense.Id))
                        throw new InvalidDataException("The backup contains a duplicate or invalid expense.");
                    if (String.IsNullOrWhiteSpace(expense.Category))
                        expense.Category = "Other";
                    expense.Notes = expense.Notes ?? "";
                    expense.Time = expense.Time ?? "";
                    if (expense.Category.Length > 80 || expense.Notes.Length > 300 || expense.Time.Length > 32)
                        throw new InvalidDataException("An expense contains a field that is too long.");
                    expense.Date = day.Key;
                }
            }

            foreach (KeyValuePair<string, List<string>> day in data.Completions)
            {
                ValidateDateKey(day.Key);
                if (day.Value == null)
                    throw new InvalidDataException("A habit history entry in this backup is invalid.");
                day.Value.RemoveAll(String.IsNullOrWhiteSpace);
                List<string> uniqueIds = day.Value.Distinct(StringComparer.Ordinal).ToList();
                day.Value.Clear();
                day.Value.AddRange(uniqueIds);
                if (day.Value.Any(id => !habitIds.Contains(id)))
                    throw new InvalidDataException("The backup contains a completion for an unknown habit.");
            }

            data.Version = 1;
            return data;
        }

        public static string Serialize(AppData data)
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = 10000000 };
            return serializer.Serialize(Normalize(data));
        }

        public static AppData Deserialize(string json)
        {
            if (String.IsNullOrWhiteSpace(json))
                throw new InvalidDataException("The selected backup file is empty.");
            var serializer = new JavaScriptSerializer { MaxJsonLength = 10000000 };
            AppData data;
            try
            {
                data = serializer.Deserialize<AppData>(json);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("The backup file could not be read as valid DailySprint data.", ex);
            }
            return Normalize(data);
        }

        public static List<Habit> GetHabitsForDay(AppData data, DateTime date)
        {
            string key = DateKey(date);
            return data.Habits
                .Where(habit => String.CompareOrdinal(habit.CreatedOn, key) <= 0 &&
                    (String.IsNullOrEmpty(habit.ArchivedOn) || String.CompareOrdinal(habit.ArchivedOn, key) >= 0))
                .ToList();
        }

        public static List<string> GetCompletedHabitIds(AppData data, DateTime date)
        {
            List<string> values;
            return data.Completions.TryGetValue(DateKey(date), out values)
                ? new List<string>(values)
                : new List<string>();
        }

        public static decimal GetSpent(AppData data, DateTime date)
        {
            List<Expense> expenses;
            return data.Expenses.TryGetValue(DateKey(date), out expenses)
                ? expenses.Sum(expense => expense.Amount)
                : 0m;
        }

        public static int GetCompletedCount(AppData data, DateTime date)
        {
            List<Habit> habits = GetHabitsForDay(data, date);
            List<string> completed = GetCompletedHabitIds(data, date);
            return habits.Count(habit => completed.Contains(habit.Id));
        }

        public static int GetCurrentStreak(AppData data, DateTime today)
        {
            DateTime cursor = today.Date;
            if (!IsDayComplete(data, cursor))
                cursor = cursor.AddDays(-1);

            int streak = 0;
            while (streak < 36500 && IsDayComplete(data, cursor))
            {
                streak++;
                cursor = cursor.AddDays(-1);
            }
            return streak;
        }

        public static bool IsDayComplete(AppData data, DateTime date)
        {
            List<Habit> habits = GetHabitsForDay(data, date);
            if (habits.Count == 0)
                return false;
            List<string> completed = GetCompletedHabitIds(data, date);
            return habits.All(habit => completed.Contains(habit.Id));
        }

        public static string FormatMoney(decimal amount, string currencyCode)
        {
            string symbol;
            switch (currencyCode)
            {
                case "PHP": symbol = "\u20B1"; break;
                case "EUR": symbol = "\u20AC"; break;
                case "GBP": symbol = "\u00A3"; break;
                case "JPY": symbol = "\u00A5"; break;
                case "CAD": symbol = "C$"; break;
                case "AUD": symbol = "A$"; break;
                case "SGD": symbol = "S$"; break;
                default: symbol = "$"; break;
            }
            return symbol + amount.ToString("N2", CultureInfo.CurrentCulture);
        }

        private static void ValidateDateKey(string key)
        {
            DateTime parsed;
            if (String.IsNullOrEmpty(key) ||
                !DateTime.TryParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out parsed) ||
                parsed < new DateTime(1753, 1, 1) ||
                parsed > new DateTime(9998, 12, 31))
                throw new InvalidDataException("The backup contains an invalid date.");
        }
    }

    public sealed class DataStore
    {
        public string FilePath { get; private set; }

        public DataStore(string filePath)
        {
            FilePath = filePath;
        }

        public static DataStore ForCurrentUser()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DailySprint");
            return new DataStore(Path.Combine(folder, "data.json"));
        }

        public AppData Load()
        {
            if (!File.Exists(FilePath))
            {
                AppData initial = AppCore.CreateDefaultData();
                Save(initial);
                return initial;
            }

            try
            {
                var file = new FileInfo(FilePath);
                if (file.Length > 10000000)
                    throw new InvalidDataException("The saved data file is larger than the supported 10 MB limit.");
                return AppCore.Deserialize(File.ReadAllText(FilePath));
            }
            catch (Exception ex)
            {
                throw new IOException(
                    "DailySprint could not load your saved data. Your original data file has not been changed.\r\n\r\n" +
                    "File: " + FilePath + "\r\n\r\nDetails: " + ex.Message, ex);
            }
        }

        public void Save(AppData data)
        {
            string folder = Path.GetDirectoryName(FilePath);
            Directory.CreateDirectory(folder);
            string temporary = FilePath + ".tmp";
            try
            {
                File.WriteAllText(temporary, AppCore.Serialize(data));
                if (File.Exists(FilePath))
                    File.Replace(temporary, FilePath, FilePath + ".bak", true);
                else
                    File.Move(temporary, FilePath);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
    }
}
