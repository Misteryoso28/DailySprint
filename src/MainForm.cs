using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DailySprint
{
    public sealed class MainForm : Form
    {
        private readonly DataStore store;
        private AppData data;
        private readonly Panel contentHost;
        private readonly FlowLayoutPanel content;
        private readonly Label pageTitle;
        private readonly Label pageSubtitle;
        private readonly Label statusLabel;
        private readonly Label streakLabel;
        private readonly DateTimePicker datePicker;
        private readonly Dictionary<string, Button> navigation = new Dictionary<string, Button>();
        private string currentPage = "Today";
        private DateTime selectedDate = DateTime.Today;
        private string expenseSearch = "";
        private Panel expenseResults;

        private static readonly Color Canvas = Color.FromArgb(15, 23, 42);
        private static readonly Color Sidebar = Color.FromArgb(11, 18, 35);
        private static readonly Color Card = Color.FromArgb(24, 34, 55);
        private static readonly Color CardLight = Color.FromArgb(30, 42, 66);
        private static readonly Color Border = Color.FromArgb(45, 59, 83);
        private static readonly Color TextMain = Color.FromArgb(241, 245, 249);
        private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);
        private static readonly Color Indigo = Color.FromArgb(99, 102, 241);
        private static readonly Color Green = Color.FromArgb(52, 211, 153);
        private static readonly Color Red = Color.FromArgb(248, 113, 113);
        private static readonly Color Amber = Color.FromArgb(251, 191, 36);

        public MainForm(DataStore dataStore, AppData initialData)
        {
            store = dataStore;
            data = AppCore.Normalize(initialData);
            Text = "DailySprint";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 640);
            Size = new Size(1120, 780);
            BackColor = Canvas;
            ForeColor = TextMain;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            var shell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Canvas,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 218));
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(shell);

            var sidebar = new Panel { Dock = DockStyle.Fill, BackColor = Sidebar, Padding = new Padding(18, 20, 14, 14) };
            shell.Controls.Add(sidebar, 0, 0);

            var brand = new Label
            {
                Text = "⚡  DailySprint",
                Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold),
                ForeColor = TextMain,
                AutoSize = true,
                Location = new Point(15, 18)
            };
            sidebar.Controls.Add(brand);
            var sideDescription = new Label
            {
                Text = "YOUR DAILY COMPANION",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = TextMuted,
                AutoSize = true,
                Location = new Point(17, 58)
            };
            sidebar.Controls.Add(sideDescription);

            string[] pages = { "Today", "Expenses", "Habits", "Summary" };
            string[] glyphs = { "◷", "$", "✓", "▤" };
            for (int i = 0; i < pages.Length; i++)
            {
                string page = pages[i];
                var button = CreateNavButton(glyphs[i] + "    " + page, page);
                button.Location = new Point(8, 100 + (i * 52));
                sidebar.Controls.Add(button);
                navigation.Add(page, button);
            }

            streakLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 64,
                ForeColor = Amber,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 0, 0, 0)
            };
            sidebar.Controls.Add(streakLabel);

            var workspace = new Panel { Dock = DockStyle.Fill, BackColor = Canvas };
            shell.Controls.Add(workspace, 1, 0);

            var header = new Panel { Dock = DockStyle.Top, Height = 104, BackColor = Canvas, Padding = new Padding(28, 18, 28, 12) };
            workspace.Controls.Add(header);
            pageTitle = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 20F, FontStyle.Bold),
                ForeColor = TextMain,
                Location = new Point(25, 16)
            };
            header.Controls.Add(pageTitle);
            pageSubtitle = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                ForeColor = TextMuted,
                Location = new Point(28, 57)
            };
            header.Controls.Add(pageSubtitle);

            var addExpenseButton = CreateButton("+  Add expense", Indigo, TextMain);
            addExpenseButton.Size = new Size(142, 38);
            addExpenseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            addExpenseButton.Location = new Point(header.ClientSize.Width - 300, 30);
            addExpenseButton.Click += delegate { ShowExpenseDialog(null); };
            header.Controls.Add(addExpenseButton);

            datePicker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "ddd, MMM d, yyyy",
                Width = 150,
                Height = 34,
                Font = new Font("Segoe UI", 9F),
                CalendarMonthBackground = Card,
                Value = selectedDate,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(header.ClientSize.Width - 150, 32)
            };
            datePicker.ValueChanged += delegate
            {
                selectedDate = datePicker.Value.Date;
                RenderCurrentPage();
            };
            header.Controls.Add(datePicker);
            header.Resize += delegate
            {
                datePicker.Left = header.ClientSize.Width - 158;
                addExpenseButton.Left = header.ClientSize.Width - 316;
            };

            contentHost = new Panel { Dock = DockStyle.Fill, BackColor = Canvas, Padding = Padding.Empty };
            workspace.Controls.Add(contentHost);
            contentHost.BringToFront();

            content = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = false,
                FlowDirection = FlowDirection.TopDown,
                Padding = new Padding(26, 8, 26, 24),
                BackColor = Canvas
            };
            contentHost.Controls.Add(content);
            content.Resize += delegate { ResizeCards(); };

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 27, BackColor = Canvas, Padding = new Padding(28, 0, 28, 4) };
            workspace.Controls.Add(footer);
            statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8F),
                ForeColor = TextMuted,
                Text = "Your data is saved privately on this computer."
            };
            footer.Controls.Add(statusLabel);
            footer.BringToFront();

            contentHost.BringToFront();
            header.BringToFront();
            footer.BringToFront();
            foreach (KeyValuePair<string, Button> item in navigation)
                item.Value.Click += delegate { Navigate(item.Key); };

            Navigate("Today");
        }

        private Button CreateNavButton(string text, string page)
        {
            var button = new Button
            {
                Text = text,
                Tag = page,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
                ForeColor = TextMuted,
                BackColor = Sidebar,
                Size = new Size(177, 42),
                Cursor = Cursors.Hand,
                Padding = new Padding(10, 0, 0, 0)
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Card;
            return button;
        }

        private void Navigate(string page)
        {
            currentPage = page;
            foreach (KeyValuePair<string, Button> item in navigation)
            {
                bool selected = item.Key == page;
                item.Value.BackColor = selected ? CardLight : Sidebar;
                item.Value.ForeColor = selected ? TextMain : TextMuted;
            }
            RenderCurrentPage();
        }

        private void RenderCurrentPage()
        {
            if (IsDisposed)
                return;

            pageTitle.Text = currentPage == "Today" ? "Daily Overview" : currentPage;
            pageSubtitle.Text = currentPage == "Summary"
                ? "A clear look at your habits and spending."
                : "Your plan and progress for " + selectedDate.ToString("dddd, MMMM d", System.Globalization.CultureInfo.CurrentCulture) + ".";
            streakLabel.Text = "  CURRENT STREAK     " + AppCore.GetCurrentStreak(data, DateTime.Today) + " day(s)";
            expenseResults = null;
            content.SuspendLayout();
            content.Controls.Clear();

            if (currentPage == "Today")
                RenderDashboard();
            else if (currentPage == "Expenses")
                RenderExpenses();
            else if (currentPage == "Habits")
                RenderHabits();
            else
                RenderSummary();

            ResizeCards();
            content.ResumeLayout();
        }

        private void RenderDashboard()
        {
            decimal spent = AppCore.GetSpent(data, selectedDate);
            decimal remaining = data.DailyBudget - spent;
            decimal percentage = data.DailyBudget <= 0
                ? (spent > 0 ? 1 : 0)
                : Math.Min(1, spent / data.DailyBudget);
            int complete = AppCore.GetCompletedCount(data, selectedDate);
            List<Habit> habits = AppCore.GetHabitsForDay(data, selectedDate);

            Panel budget = AddCard(182);
            AddEyebrow(budget, "DAILY SPENDING PLAN", 22, 18);
            AddText(budget, AppCore.FormatMoney(spent, data.CurrencyCode), 22, 43, 26, FontStyle.Bold, TextMain);
            AddText(budget, "of " + AppCore.FormatMoney(data.DailyBudget, data.CurrencyCode), 194, 54, 11, FontStyle.Regular, TextMuted);
            AddPill(budget, remaining >= 0 ? "On track" : "Over budget", remaining >= 0 ? Green : Red, budget.Width - 124, 22);
            var bar = new Panel { BackColor = Border, Location = new Point(22, 91), Size = new Size(Math.Max(100, budget.Width - 44), 10), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            var fill = new Panel { BackColor = remaining >= 0 ? Indigo : Red, Dock = DockStyle.Left, Width = (int)(bar.Width * (double)percentage) };
            bar.Controls.Add(fill);
            budget.Controls.Add(bar);
            AddText(budget,
                remaining >= 0
                    ? AppCore.FormatMoney(remaining, data.CurrencyCode) + " remaining"
                    : AppCore.FormatMoney(Math.Abs(remaining), data.CurrencyCode) + " over your limit",
                22, 113, 12, FontStyle.Regular, TextMuted);
            AddText(budget, (int)Math.Round(percentage * 100) + "% used", budget.Width - 112, 113, 12, FontStyle.Bold, TextMuted);
            var settings = CreateButton("Budget & currency", CardLight, TextMain);
            settings.Size = new Size(154, 34);
            settings.Location = new Point(budget.Width - 177, 141);
            settings.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            settings.Click += delegate { ShowSettingsDialog(); };
            budget.Controls.Add(settings);

            Panel habitCard = AddCard(Math.Max(230, 74 + habits.Count * 48));
            AddSectionTitle(habitCard, "Today's habits", complete + " / " + habits.Count + " complete", 18);
            if (habits.Count == 0)
                AddEmpty(habitCard, "No habits are scheduled for this date. Add one to get started.", 18, 66);
            for (int i = 0; i < habits.Count; i++)
            {
                Habit habit = habits[i];
                bool done = AppCore.GetCompletedHabitIds(data, selectedDate).Contains(habit.Id);
                int y = 61 + i * 48;
                var toggle = CreateButton((done ? "✓" : "○") + "   " + habit.Name, done ? Color.FromArgb(23, 55, 52) : CardLight, done ? Green : TextMain);
                toggle.TextAlign = ContentAlignment.MiddleLeft;
                toggle.Size = new Size(habitCard.Width - 38, 40);
                toggle.Location = new Point(18, y);
                toggle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                toggle.Click += delegate { ToggleHabit(habit.Id); };
                habitCard.Controls.Add(toggle);
            }

            List<Expense> expenses = GetExpenses(selectedDate);
            Panel spendingCard = AddCard(82 + Math.Min(4, expenses.Count) * 47);
            AddSectionTitle(spendingCard, "Recent expenses", AppCore.FormatMoney(spent, data.CurrencyCode) + " total", 18);
            if (expenses.Count == 0)
                AddEmpty(spendingCard, "No expenses recorded. Use “Add expense” to log a purchase.", 18, 65);
            else
            {
                foreach (Expense expense in expenses.OrderByDescending(item => item.Time).Take(4))
                    AddExpenseRow(spendingCard, expense, 61);
                RepositionExpenseRows(spendingCard, 61);
            }
        }

        private void RenderExpenses()
        {
            List<Expense> expenses = GetExpenses(selectedDate);
            Panel totals = AddCard(116);
            AddEyebrow(totals, "TOTAL SPENDING", 20, 18);
            AddText(totals, AppCore.FormatMoney(AppCore.GetSpent(data, selectedDate), data.CurrencyCode), 20, 43, 25, FontStyle.Bold, TextMain);
            AddText(totals, expenses.Count + (expenses.Count == 1 ? " expense" : " expenses") + " on this date", 21, 80, 11, FontStyle.Regular, TextMuted);

            Panel list = AddCard(126);
            AddSectionTitle(list, "Transactions", "Add expenses for any date", 18);
            var search = new TextBox
            {
                Text = expenseSearch,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = CardLight,
                ForeColor = TextMain,
                Font = new Font("Segoe UI", 9F),
                Location = new Point(18, 57),
                Size = new Size(320, 31),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            list.Controls.Add(search);
            expenseResults = new Panel
            {
                Location = new Point(18, 98),
                Size = new Size(list.Width - 36, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            list.Controls.Add(expenseResults);
            search.TextChanged += delegate
            {
                expenseSearch = search.Text;
                RenderExpenseRows();
            };
            RenderExpenseRows();
        }

        private void RenderExpenseRows()
        {
            if (expenseResults == null || expenseResults.IsDisposed)
                return;
            expenseResults.Controls.Clear();
            List<Expense> expenses = GetExpenses(selectedDate);
            if (!String.IsNullOrWhiteSpace(expenseSearch))
                expenses = expenses.Where(item =>
                    item.Title.IndexOf(expenseSearch, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                    item.Category.IndexOf(expenseSearch, StringComparison.CurrentCultureIgnoreCase) >= 0)
                    .ToList();

            if (expenses.Count == 0)
            {
                expenseResults.Height = 45;
                expenseResults.Parent.Height = 153;
                var empty = new Label
                {
                    Text = String.IsNullOrWhiteSpace(expenseSearch)
                        ? "No expenses for this date yet."
                        : "No expenses match your search.",
                    ForeColor = TextMuted,
                    AutoSize = true,
                    Location = new Point(0, 10)
                };
                expenseResults.Controls.Add(empty);
                ResizeCards();
                return;
            }

            int index = 0;
            foreach (Expense expense in expenses.OrderByDescending(item => item.Time))
            {
                var row = MakeExpenseRow(expense, index * 64, expenseResults.Width);
                row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                expenseResults.Controls.Add(row);
                index++;
            }
            expenseResults.Height = index * 64;
            expenseResults.Parent.Height = 106 + expenseResults.Height;
            ResizeCards();
        }

        private void RenderHabits()
        {
            List<Habit> habits = AppCore.GetHabitsForDay(data, selectedDate);
            int completed = AppCore.GetCompletedCount(data, selectedDate);
            Panel progress = AddCard(112);
            AddEyebrow(progress, "DAILY PROGRESS", 20, 18);
            AddText(progress, completed + " of " + habits.Count + " habits complete", 20, 43, 20, FontStyle.Bold, TextMain);
            AddText(progress, AppCore.GetCurrentStreak(data, DateTime.Today) + " day current streak", 21, 78, 11, FontStyle.Regular, Amber);

            Panel list = AddCard(90 + Math.Max(1, habits.Count) * 74);
            AddSectionTitle(list, "Your habits", "Checked habits are saved for the selected date", 18);
            if (habits.Count == 0)
                AddEmpty(list, "No habits are scheduled for this date.", 18, 68);
            else
            {
                List<string> completedIds = AppCore.GetCompletedHabitIds(data, selectedDate);
                for (int i = 0; i < habits.Count; i++)
                {
                    Habit habit = habits[i];
                    bool done = completedIds.Contains(habit.Id);
                    var item = new Panel
                    {
                        BackColor = CardLight,
                        Location = new Point(18, 60 + i * 68),
                        Size = new Size(list.Width - 36, 58),
                        Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                    };
                    var checkbox = CreateButton(done ? "✓" : "○", done ? Color.FromArgb(23, 55, 52) : Card, done ? Green : TextMuted);
                    checkbox.Size = new Size(38, 38);
                    checkbox.Location = new Point(10, 10);
                    checkbox.Click += delegate { ToggleHabit(habit.Id); };
                    item.Controls.Add(checkbox);
                    var name = new Label
                    {
                        Text = habit.Name,
                        ForeColor = done ? Green : TextMain,
                        Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
                        AutoSize = true,
                        Location = new Point(60, 19)
                    };
                    item.Controls.Add(name);
                    var remove = CreateButton("Remove", Card, Red);
                    remove.Size = new Size(76, 32);
                    remove.Location = new Point(item.Width - 88, 13);
                    remove.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                    remove.Click += delegate { ArchiveHabit(habit); };
                    item.Controls.Add(remove);
                    list.Controls.Add(item);
                }
            }
            var add = CreateButton("+  Create a habit", Indigo, TextMain);
            add.Size = new Size(168, 38);
            add.Click += delegate { ShowHabitDialog(); };
            AddButtonCard(add);
        }

        private void RenderSummary()
        {
            decimal spent = AppCore.GetSpent(data, selectedDate);
            List<Habit> habits = AppCore.GetHabitsForDay(data, selectedDate);
            int completed = AppCore.GetCompletedCount(data, selectedDate);
            Panel digest = AddCard(184);
            AddEyebrow(digest, "DAILY DIGEST", 20, 18);
            AddText(digest, selectedDate.ToString("dddd, MMMM d, yyyy"), 20, 42, 18, FontStyle.Bold, TextMain);
            AddText(digest, "Habits complete", 20, 91, 10, FontStyle.Regular, TextMuted);
            AddText(digest, completed + " / " + habits.Count, 20, 111, 19, FontStyle.Bold, Green);
            AddText(digest, "Total spent", 246, 91, 10, FontStyle.Regular, TextMuted);
            AddText(digest, AppCore.FormatMoney(spent, data.CurrencyCode), 246, 111, 19, FontStyle.Bold, Indigo);
            string performance = spent <= data.DailyBudget ? "Within daily budget" : "Over daily budget";
            AddText(digest, performance + "  ·  " + AppCore.GetCurrentStreak(data, DateTime.Today) + " day streak",
                20, 151, 11, FontStyle.Regular, spent <= data.DailyBudget ? Green : Red);

            Panel week = AddCard(88 + 7 * 47);
            AddSectionTitle(week, "Selected day and previous 6 days", "Spending and habit completion", 18);
            for (int i = 0; i < 7; i++)
            {
                DateTime day = selectedDate.Date.AddDays(-i);
                decimal daySpent = AppCore.GetSpent(data, day);
                List<Habit> dayHabits = AppCore.GetHabitsForDay(data, day);
                int dayComplete = AppCore.GetCompletedCount(data, day);
                string label = day.ToString("ddd, MMM d");
                string habitsText = dayComplete + "/" + dayHabits.Count + " habits";
                AddText(week, label, 20, 59 + i * 47, 10, FontStyle.Bold, TextMain);
                AddText(week, habitsText, 137, 59 + i * 47, 10, FontStyle.Regular, TextMuted);
                AddText(week, AppCore.FormatMoney(daySpent, data.CurrencyCode), week.Width - 140, 59 + i * 47, 10, FontStyle.Bold, daySpent > data.DailyBudget ? Red : Indigo);
                var divider = new Panel
                {
                    BackColor = Border,
                    Location = new Point(18, 81 + i * 47),
                    Size = new Size(week.Width - 36, 1),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };
                week.Controls.Add(divider);
            }

            Panel exports = AddCard(150);
            AddSectionTitle(exports, "Your data", "Keep a backup or use your records in a spreadsheet", 18);
            var csv = CreateButton("Export all records to CSV", CardLight, TextMain);
            csv.Size = new Size(195, 38);
            csv.Location = new Point(18, 61);
            csv.Click += delegate { ExportCsv(); };
            exports.Controls.Add(csv);
            var backup = CreateButton("Save backup (JSON)", CardLight, TextMain);
            backup.Size = new Size(170, 38);
            backup.Location = new Point(225, 61);
            backup.Click += delegate { ExportBackup(); };
            exports.Controls.Add(backup);
            var restore = CreateButton("Restore backup", CardLight, TextMain);
            restore.Size = new Size(132, 38);
            restore.Location = new Point(407, 61);
            restore.Click += delegate { ImportBackup(); };
            exports.Controls.Add(restore);
            var reset = CreateButton("Reset this date", Card, Red);
            reset.Size = new Size(132, 32);
            reset.Location = new Point(18, 105);
            reset.Click += delegate { ResetSelectedDate(); };
            exports.Controls.Add(reset);
        }

        private void AddExpenseRow(Panel parent, Expense expense, int y)
        {
            Panel row = MakeExpenseRow(expense, y, parent.Width - 36);
            row.Left = 18;
            row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            row.Tag = "expense-row";
            parent.Controls.Add(row);
        }

        private void RepositionExpenseRows(Panel parent, int startY)
        {
            int i = 0;
            foreach (Control row in parent.Controls)
            {
                if (row.Tag as string == "expense-row")
                {
                    row.Top = startY + i * 47;
                    i++;
                }
            }
        }

        private Panel MakeExpenseRow(Expense expense, int y, int width)
        {
            var row = new Panel { BackColor = CardLight, Location = new Point(0, y), Size = new Size(Math.Max(250, width), 56) };
            var title = new Label
            {
                Text = expense.Title,
                ForeColor = TextMain,
                Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
                AutoEllipsis = true,
                Location = new Point(11, 7),
                Size = new Size(Math.Max(100, width - 230), 20)
            };
            row.Controls.Add(title);
            var detail = new Label
            {
                Text = expense.Category + (String.IsNullOrEmpty(expense.Time) ? "" : "  ·  " + expense.Time),
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 8F),
                AutoEllipsis = true,
                Location = new Point(11, 30),
                Size = new Size(Math.Max(100, width - 230), 17)
            };
            row.Controls.Add(detail);
            var amount = new Label
            {
                Text = AppCore.FormatMoney(expense.Amount, data.CurrencyCode),
                ForeColor = TextMain,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight,
                Location = new Point(width - 224, 15),
                Size = new Size(75, 24)
            };
            row.Controls.Add(amount);
            var edit = CreateButton("Edit", Card, TextMuted);
            edit.Size = new Size(47, 30);
            edit.Location = new Point(width - 135, 13);
            edit.Click += delegate { ShowExpenseDialog(expense); };
            row.Controls.Add(edit);
            var delete = CreateButton("×", Card, Red);
            delete.Size = new Size(38, 30);
            delete.Location = new Point(width - 82, 13);
            delete.Click += delegate { DeleteExpense(expense); };
            row.Controls.Add(delete);
            return row;
        }

        private void ShowExpenseDialog(Expense existing)
        {
            using (var dialog = new Form())
            using (var fields = new TableLayoutPanel())
            {
                dialog.Text = existing == null ? "Add expense" : "Edit expense";
                dialog.BackColor = Canvas;
                dialog.ForeColor = TextMain;
                dialog.Font = Font;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ClientSize = new Size(430, 420);
                fields.Dock = DockStyle.Fill;
                fields.Padding = new Padding(22);
                fields.ColumnCount = 2;
                fields.RowCount = 7;
                fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
                fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                for (int i = 0; i < 6; i++)
                    fields.RowStyles.Add(new RowStyle(SizeType.Absolute, i == 5 ? 66 : 48));
                fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                dialog.Controls.Add(fields);

                var title = new TextBox { Text = existing == null ? "" : existing.Title, MaxLength = 120 };
                var amount = new NumericUpDown { DecimalPlaces = 2, Minimum = 0.01m, Maximum = 1000000000m, ThousandsSeparator = true, Value = existing == null ? 1m : existing.Amount };
                var category = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                category.Items.AddRange(AppCore.Categories);
                category.SelectedItem = existing == null ? AppCore.Categories[0] : existing.Category;
                if (category.SelectedIndex < 0)
                    category.SelectedItem = "Other";
                var day = new DateTimePicker
                {
                    Format = DateTimePickerFormat.Custom,
                    CustomFormat = "dddd, MMM d, yyyy",
                    Value = existing == null
                        ? selectedDate
                        : DateTime.ParseExact(existing.Date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
                };
                var notes = new TextBox { Text = existing == null ? "" : existing.Notes, MaxLength = 300, Multiline = true, Height = 54, ScrollBars = ScrollBars.Vertical };

                AddField(fields, "What was it?", title, 0);
                AddField(fields, "Amount", amount, 1);
                AddField(fields, "Category", category, 2);
                AddField(fields, "Date", day, 3);
                AddField(fields, "Notes", notes, 4);

                var buttons = new FlowLayoutPanel
                {
                    FlowDirection = FlowDirection.RightToLeft,
                    Dock = DockStyle.Fill,
                    WrapContents = false,
                    Padding = new Padding(0, 8, 0, 0)
                };
                var save = CreateButton(existing == null ? "Save expense" : "Save changes", Indigo, TextMain);
                save.Size = new Size(128, 36);
                save.Click += delegate
                {
                    if (String.IsNullOrWhiteSpace(title.Text))
                    {
                        MessageBox.Show(dialog, "Enter a description for this expense.", "A description is required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    string key = AppCore.DateKey(day.Value);
                    Expense saved = existing ?? new Expense { Id = Guid.NewGuid().ToString("N") };
                    saved.Title = title.Text.Trim();
                    saved.Amount = amount.Value;
                    saved.Category = category.SelectedItem.ToString();
                    saved.Notes = notes.Text.Trim();
                    saved.Date = key;
                    saved.Time = DateTime.Now.ToString("h:mm tt");

                    if (existing != null && existing.Date != key)
                        RemoveExpense(existing);
                    List<Expense> list;
                    if (!data.Expenses.TryGetValue(key, out list))
                    {
                        list = new List<Expense>();
                        data.Expenses[key] = list;
                    }
                    if (existing == null || !list.Any(item => item.Id == saved.Id))
                        list.Add(saved);
                    else
                    {
                        int index = list.FindIndex(item => item.Id == saved.Id);
                        list[index] = saved;
                    }
                    if (Persist())
                        dialog.DialogResult = DialogResult.OK;
                    else
                        return;
                };
                var cancel = CreateButton("Cancel", CardLight, TextMuted);
                cancel.Size = new Size(82, 36);
                cancel.DialogResult = DialogResult.Cancel;
                buttons.Controls.Add(save);
                buttons.Controls.Add(cancel);
                fields.Controls.Add(buttons, 1, 6);
                dialog.AcceptButton = save;
                dialog.CancelButton = cancel;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    ShowStatus(existing == null ? "Expense saved." : "Expense updated.");
            }
        }

        private void ShowHabitDialog()
        {
            using (var dialog = new Form())
            using (var fields = new TableLayoutPanel())
            {
                dialog.Text = "Create a habit";
                dialog.BackColor = Canvas;
                dialog.ForeColor = TextMain;
                dialog.Font = Font;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ClientSize = new Size(420, 190);
                fields.Dock = DockStyle.Fill;
                fields.Padding = new Padding(22);
                fields.ColumnCount = 2;
                fields.RowCount = 3;
                fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
                fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
                fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
                dialog.Controls.Add(fields);

                var name = new TextBox { MaxLength = 80 };
                AddField(fields, "Habit name", name, 0);
                var help = new Label
                {
                    Text = "This habit will appear starting on " + selectedDate.ToString("MMM d, yyyy") + ".",
                    AutoSize = true,
                    ForeColor = TextMuted,
                    Padding = new Padding(0, 8, 0, 0)
                };
                fields.Controls.Add(help, 1, 1);
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
                var save = CreateButton("Create habit", Indigo, TextMain);
                save.Size = new Size(116, 35);
                save.Click += delegate
                {
                    if (String.IsNullOrWhiteSpace(name.Text))
                    {
                        MessageBox.Show(dialog, "Enter a name for this habit.", "A name is required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    if (data.Habits.Count >= 200)
                    {
                        MessageBox.Show(dialog, "You can have up to 200 habits.", "Habit limit", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    data.Habits.Add(new Habit
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Name = name.Text.Trim(),
                        CreatedOn = AppCore.DateKey(selectedDate),
                        ArchivedOn = ""
                    });
                    if (Persist())
                        dialog.DialogResult = DialogResult.OK;
                };
                var cancel = CreateButton("Cancel", CardLight, TextMuted);
                cancel.Size = new Size(80, 35);
                cancel.DialogResult = DialogResult.Cancel;
                buttons.Controls.Add(save);
                buttons.Controls.Add(cancel);
                fields.Controls.Add(buttons, 1, 2);
                dialog.AcceptButton = save;
                dialog.CancelButton = cancel;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    ShowStatus("Habit created.");
            }
        }

        private void ShowSettingsDialog()
        {
            using (var dialog = new Form())
            using (var fields = new TableLayoutPanel())
            {
                dialog.Text = "Budget & currency";
                dialog.BackColor = Canvas;
                dialog.ForeColor = TextMain;
                dialog.Font = Font;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ClientSize = new Size(430, 215);
                fields.Dock = DockStyle.Fill;
                fields.Padding = new Padding(22);
                fields.ColumnCount = 2;
                fields.RowCount = 4;
                fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
                fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
                fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
                fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
                dialog.Controls.Add(fields);
                var budget = new NumericUpDown
                {
                    DecimalPlaces = 2,
                    Minimum = 0,
                    Maximum = 1000000000m,
                    ThousandsSeparator = true,
                    Value = data.DailyBudget
                };
                var currency = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                currency.Items.AddRange(AppCore.CurrencyCodes);
                currency.SelectedItem = data.CurrencyCode;
                AddField(fields, "Daily budget", budget, 0);
                AddField(fields, "Currency", currency, 1);
                var note = new Label
                {
                    Text = "Your data is stored locally in your Windows user profile.",
                    ForeColor = TextMuted,
                    AutoSize = true,
                    Padding = new Padding(0, 7, 0, 0)
                };
                fields.Controls.Add(note, 1, 2);

                var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
                var save = CreateButton("Save settings", Indigo, TextMain);
                save.Size = new Size(125, 35);
                save.Click += delegate
                {
                    data.DailyBudget = budget.Value;
                    data.CurrencyCode = currency.SelectedItem.ToString();
                    if (Persist())
                        dialog.DialogResult = DialogResult.OK;
                };
                var cancel = CreateButton("Cancel", CardLight, TextMuted);
                cancel.Size = new Size(80, 35);
                cancel.DialogResult = DialogResult.Cancel;
                buttons.Controls.Add(save);
                buttons.Controls.Add(cancel);
                fields.Controls.Add(buttons, 1, 3);
                dialog.AcceptButton = save;
                dialog.CancelButton = cancel;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    ShowStatus("Settings saved.");
            }
        }

        private void AddField(TableLayoutPanel layout, string labelText, Control editor, int row)
        {
            var label = new Label
            {
                Text = labelText,
                ForeColor = TextMuted,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Padding = new Padding(0, 8, 0, 0)
            };
            editor.Dock = DockStyle.Fill;
            editor.Margin = new Padding(3, 5, 3, 5);
            editor.Font = new Font("Segoe UI", 9F);
            if (editor is TextBox)
            {
                editor.BackColor = CardLight;
                editor.ForeColor = TextMain;
            }
            else if (editor is ComboBox)
            {
                editor.BackColor = CardLight;
                editor.ForeColor = TextMain;
            }
            else if (editor is NumericUpDown)
            {
                editor.BackColor = CardLight;
                editor.ForeColor = TextMain;
            }
            else if (editor is DateTimePicker)
            {
                editor.BackColor = CardLight;
                editor.ForeColor = TextMain;
            }
            layout.Controls.Add(label, 0, row);
            layout.Controls.Add(editor, 1, row);
        }

        private void ToggleHabit(string id)
        {
            string day = AppCore.DateKey(selectedDate);
            List<string> completed;
            if (!data.Completions.TryGetValue(day, out completed))
            {
                completed = new List<string>();
                data.Completions[day] = completed;
            }
            if (completed.Contains(id))
                completed.Remove(id);
            else
                completed.Add(id);
            Persist();
        }

        private void ArchiveHabit(Habit habit)
        {
            DialogResult answer = MessageBox.Show(this,
                "Remove \"" + habit.Name + "\" from this date onward? Earlier history will be kept.",
                "Remove habit", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
                return;
            habit.ArchivedOn = AppCore.DateKey(selectedDate);
            Persist();
            ShowStatus("Habit removed. Earlier history is preserved.");
        }

        private void RemoveExpense(Expense expense)
        {
            List<Expense> list;
            if (data.Expenses.TryGetValue(expense.Date, out list))
            {
                list.RemoveAll(item => item.Id == expense.Id);
                if (list.Count == 0)
                    data.Expenses.Remove(expense.Date);
            }
        }

        private void DeleteExpense(Expense expense)
        {
            DialogResult answer = MessageBox.Show(this, "Delete this expense entry?", "Delete expense",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
                return;
            RemoveExpense(expense);
            Persist();
            ShowStatus("Expense deleted.");
        }

        private bool Persist()
        {
            try
            {
                store.Save(data);
                statusLabel.Text = "Saved on this computer at " + DateTime.Now.ToString("h:mm tt") + ".";
                RenderCurrentPage();
                return true;
            }
            catch (Exception ex)
            {
                string details = ex.Message;
                try
                {
                    data = store.Load();
                    RenderCurrentPage();
                }
                catch (Exception reloadError)
                {
                    details += "\r\n\r\nThe last saved data could not be reloaded: " + reloadError.Message;
                }
                statusLabel.Text = "Changes were not saved.";
                MessageBox.Show(this, "Your changes could not be saved.\r\n\r\n" + details,
                    "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private List<Expense> GetExpenses(DateTime date)
        {
            List<Expense> expenses;
            return data.Expenses.TryGetValue(AppCore.DateKey(date), out expenses)
                ? new List<Expense>(expenses)
                : new List<Expense>();
        }

        private void ExportCsv()
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "Export DailySprint records";
                dialog.Filter = "CSV spreadsheet (*.csv)|*.csv";
                dialog.FileName = "DailySprint-" + DateTime.Today.ToString("yyyy-MM-dd") + ".csv";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    var builder = new StringBuilder();
                    builder.AppendLine("Type,Date,Name,Amount,Category,Status,Notes");
                    foreach (KeyValuePair<string, List<Expense>> day in data.Expenses.OrderBy(item => item.Key))
                    {
                        foreach (Expense expense in day.Value)
                            builder.AppendLine(String.Join(",", new[]
                            {
                                CsvField("Expense"), CsvField(day.Key), CsvField(expense.Title),
                                CsvField(expense.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                                CsvField(expense.Category), CsvField(""), CsvField(expense.Notes)
                            }));
                    }
                    foreach (string day in data.Completions.Keys.Union(data.Habits.Select(h => h.CreatedOn)).Distinct().OrderBy(value => value))
                    {
                        DateTime date;
                        if (!DateTime.TryParse(day, out date))
                            continue;
                        List<string> done = AppCore.GetCompletedHabitIds(data, date);
                        foreach (Habit habit in AppCore.GetHabitsForDay(data, date))
                            builder.AppendLine(String.Join(",", new[]
                            {
                                CsvField("Habit"), CsvField(day), CsvField(habit.Name),
                                CsvField(""), CsvField(""), CsvField(done.Contains(habit.Id) ? "Completed" : "Not completed"),
                                CsvField("")
                            }));
                    }
                    File.WriteAllText(dialog.FileName, "\uFEFF" + builder.ToString(), Encoding.UTF8);
                    ShowStatus("CSV exported.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "The CSV file could not be written.\r\n\r\n" + ex.Message,
                        "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static string CsvField(string value)
        {
            string safe = value ?? "";
            if (safe.Length > 0 && "=+-@".IndexOf(safe[0]) >= 0)
                safe = "'" + safe;
            return "\"" + safe.Replace("\"", "\"\"") + "\"";
        }

        private void ExportBackup()
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "Save a DailySprint backup";
                dialog.Filter = "DailySprint backup (*.json)|*.json";
                dialog.FileName = "DailySprint-backup-" + DateTime.Today.ToString("yyyy-MM-dd") + ".json";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    File.WriteAllText(dialog.FileName, AppCore.Serialize(data), Encoding.UTF8);
                    ShowStatus("Backup saved.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "The backup could not be written.\r\n\r\n" + ex.Message,
                        "Backup failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ImportBackup()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Restore a DailySprint backup";
                dialog.Filter = "DailySprint backup (*.json)|*.json|All files (*.*)|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                if (MessageBox.Show(this, "Restoring this backup will replace the data currently on this computer. Continue?",
                    "Replace current data", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
                try
                {
                    AppData imported = AppCore.Deserialize(File.ReadAllText(dialog.FileName));
                    AppData previous = data;
                    data = imported;
                    try
                    {
                        store.Save(data);
                    }
                    catch
                    {
                        data = previous;
                        throw;
                    }
                    RenderCurrentPage();
                    ShowStatus("Backup restored.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "The backup could not be restored. Your existing data was not replaced.\r\n\r\n" + ex.Message,
                        "Restore failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ResetSelectedDate()
        {
            DialogResult answer = MessageBox.Show(this,
                "This removes the expenses and habit checkmarks for " + selectedDate.ToString("MMMM d, yyyy") + ". Habit definitions and other dates will remain.",
                "Reset selected date", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes)
                return;
            string key = AppCore.DateKey(selectedDate);
            data.Expenses.Remove(key);
            data.Completions.Remove(key);
            Persist();
            ShowStatus("Entries reset for " + selectedDate.ToString("MMM d") + ".");
        }

        private void ShowStatus(string message)
        {
            statusLabel.Text = message;
        }

        private Panel AddCard(int height)
        {
            var card = new Panel
            {
                BackColor = Card,
                BorderStyle = BorderStyle.FixedSingle,
                Height = height,
                Width = Math.Max(360, content.ClientSize.Width - content.Padding.Horizontal - 28),
                Margin = new Padding(0, 0, 0, 14),
                Padding = Padding.Empty
            };
            content.Controls.Add(card);
            return card;
        }

        private void AddButtonCard(Button button)
        {
            Panel card = AddCard(58);
            button.Location = new Point(18, 10);
            card.Controls.Add(button);
        }

        private void ResizeCards()
        {
            int width = Math.Max(360, content.ClientSize.Width - content.Padding.Horizontal - 28);
            foreach (Control control in content.Controls)
            {
                control.Width = width;
                foreach (Control child in control.Controls)
                {
                    Panel panel = child as Panel;
                    if (panel != null && (panel.Tag as string) == "expense-row")
                        panel.Width = width - 36;
                }
            }
        }

        private void AddEyebrow(Control parent, string text, int x, int y)
        {
            AddText(parent, text, x, y, 9, FontStyle.Bold, TextMuted);
        }

        private void AddSectionTitle(Panel parent, string title, string detail, int x)
        {
            AddText(parent, title, x, 18, 14, FontStyle.Bold, TextMain);
            AddText(parent, detail, parent.Width - 250, 21, 9, FontStyle.Regular, TextMuted);
        }

        private void AddText(Control parent, string text, int x, int y, float size, FontStyle style, Color color)
        {
            var label = new Label
            {
                Text = text,
                ForeColor = color,
                Font = new Font("Segoe UI", size, style),
                AutoSize = true,
                Location = new Point(x, y)
            };
            parent.Controls.Add(label);
        }

        private void AddEmpty(Control parent, string text, int x, int y)
        {
            var label = new Label
            {
                Text = text,
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 9F),
                AutoSize = true,
                Location = new Point(x, y)
            };
            parent.Controls.Add(label);
        }

        private void AddPill(Panel parent, string text, Color color, int x, int y)
        {
            var pill = new Label
            {
                Text = text,
                ForeColor = color,
                BackColor = color == Green ? Color.FromArgb(23, 55, 52) : Color.FromArgb(73, 38, 45),
                Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(94, 26),
                Location = new Point(x, y)
            };
            parent.Controls.Add(pill);
        }

        private Button CreateButton(string text, Color background, Color foreground)
        {
            var button = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = background,
                ForeColor = foreground,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderColor = Border;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(background, 0.08F);
            return button;
        }
    }
}
