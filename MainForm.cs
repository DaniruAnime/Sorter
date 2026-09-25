using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Sorter
{
    public sealed class MainForm : Form
    {
        private readonly DataGridView inputGrid = new DataGridView();
        private readonly FlowLayoutPanel visualizationPanel = new FlowLayoutPanel();
        private readonly DataGridView resultGrid = new DataGridView();
        private readonly Label fastestLabel = new Label();
        private readonly Label countLabel = new Label();
        private readonly TextBox googleUrlBox = new TextBox();
        private readonly ComboBox directionBox = new ComboBox();

        private readonly CheckBox bubbleCheck = new CheckBox();
        private readonly CheckBox insertionCheck = new CheckBox();
        private readonly CheckBox shakerCheck = new CheckBox();
        private readonly CheckBox quickCheck = new CheckBox();
        private readonly CheckBox bogoCheck = new CheckBox();

        private readonly Dictionary<SortAlgorithm, VisualizerControl> visualizers = new Dictionary<SortAlgorithm, VisualizerControl>();
        private CancellationTokenSource cancellation;
        private Task calculationTask;
        private int calculationVersion;

        private const int MaxInputCount = 2000;
        private const int BogoMaxCount = 20;

        public MainForm()
        {
            Text = "Визуализация алгоритмов сортировки — Mono WinForms";
            StartPosition = FormStartPosition.CenterScreen;
            Width = 1250;
            Height = 850;
            MinimumSize = new Size(1050, 700);

            BuildMenu();
            BuildLayout();
            FormClosing += OnFormClosing;
            FillSampleData();
        }

        private void BuildMenu()
        {
            MenuStrip menu = new MenuStrip();

            ToolStripMenuItem file = new ToolStripMenuItem("Файл");
            ToolStripMenuItem importExcel = new ToolStripMenuItem("Загрузить Excel (.xlsx)");
            importExcel.Click += delegate { LoadExcel(); };
            ToolStripMenuItem importGoogle = new ToolStripMenuItem("Загрузить Google Table");
            importGoogle.Click += delegate { LoadGoogle(); };
            ToolStripMenuItem exit = new ToolStripMenuItem("Выход");
            exit.Click += delegate { Close(); };
            file.DropDownItems.Add(importExcel);
            file.DropDownItems.Add(importGoogle);
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add(exit);

            ToolStripMenuItem data = new ToolStripMenuItem("Данные");
            ToolStripMenuItem generate = new ToolStripMenuItem("Сгенерировать данные");
            generate.Click += delegate { GenerateData(); };
            ToolStripMenuItem clear = new ToolStripMenuItem("Очистить");
            clear.Click += delegate { ClearAll(); };
            data.DropDownItems.Add(generate);
            data.DropDownItems.Add(clear);

            ToolStripMenuItem sort = new ToolStripMenuItem("Сортировка");
            ToolStripMenuItem calculate = new ToolStripMenuItem("Рассчитать");
            calculate.ShortcutKeys = Keys.F5;
            calculate.Click += delegate { StartCalculation(); };
            ToolStripMenuItem stop = new ToolStripMenuItem("Остановить");
            stop.ShortcutKeys = Keys.Escape;
            stop.Click += delegate { StopCalculation(); };
            sort.DropDownItems.Add(calculate);
            sort.DropDownItems.Add(stop);

            ToolStripMenuItem help = new ToolStripMenuItem("Справка");
            ToolStripMenuItem about = new ToolStripMenuItem("О программе");
            about.Click += delegate
            {
                MessageBox.Show(this,
                    "Лабораторная работа: визуализация сортировок.\n" +
                    "WinForms под Mono / Arch Linux.\n\n" +
                    "Алгоритмы: Bubble, Insertion, Shaker, Quick, BOGO.",
                    "О программе", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            help.DropDownItems.Add(about);

            menu.Items.Add(file);
            menu.Items.Add(data);
            menu.Items.Add(sort);
            menu.Items.Add(help);
            MainMenuStrip = menu;
            Controls.Add(menu);
        }

        private void BuildLayout()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(8);
            root.ColumnCount = 2;
            root.RowCount = 1;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            root.Controls.Add(BuildOptionsPanel(), 0, 0);
            root.Controls.Add(BuildContentPanel(), 1, 0);
            Controls.Add(root);
        }

        private Control BuildOptionsPanel()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.AutoScroll = true;

            GroupBox algorithms = new GroupBox();
            algorithms.Text = "Алгоритмы (можно несколько)";
            algorithms.Dock = DockStyle.Top;
            algorithms.Height = 190;

            bubbleCheck.Text = "Пузырьковая";
            insertionCheck.Text = "Вставками";
            shakerCheck.Text = "Шейкерная";
            quickCheck.Text = "Быстрая";
            bogoCheck.Text = "BOGO";
            CheckBox[] checks = { bubbleCheck, insertionCheck, shakerCheck, quickCheck, bogoCheck };
            for (int checkIndex = 0; checkIndex < checks.Length; ++checkIndex)
            {
                checks[checkIndex].AutoSize = true;
                checks[checkIndex].Location = new Point(15, 28 + checkIndex * 30);
                algorithms.Controls.Add(checks[checkIndex]);
            }

            GroupBox direction = new GroupBox();
            direction.Text = "Направление";
            direction.Dock = DockStyle.Top;
            direction.Height = 85;
            directionBox.DropDownStyle = ComboBoxStyle.DropDownList;
            directionBox.Items.Add("По возрастанию");
            directionBox.Items.Add("По убыванию");
            directionBox.SelectedIndex = 0;
            directionBox.Location = new Point(15, 30);
            directionBox.Width = 205;
            direction.Controls.Add(directionBox);

            GroupBox google = new GroupBox();
            google.Text = "Google Sheets";
            google.Dock = DockStyle.Top;
            google.Height = 125;
            Label googleLabel = new Label();
            googleLabel.Text = "URL таблицы:";
            googleLabel.AutoSize = true;
            googleLabel.Location = new Point(12, 28);
            googleUrlBox.Location = new Point(12, 50);
            googleUrlBox.Width = 220;
            googleUrlBox.Text = "https://docs.google.com/spreadsheets/d/.../edit?gid=0";
            google.Controls.Add(googleLabel);
            google.Controls.Add(googleUrlBox);

            Label hint = new Label();
            hint.Dock = DockStyle.Top;
            hint.AutoSize = false;
            hint.Height = 150;
            hint.Text = "Подсказка:\n" +
                        "• В DataGridView вводятся числовые значения.\n" +
                        "• Excel: первый лист, все числовые ячейки.\n" +
                        "• Google Sheets: лист по gid из URL.\n" +
                        "• BOGO разрешён для 2–20 элементов. Ограничения по времени нет; при необходимости используйте «Остановить».";
            hint.Padding = new Padding(8);

            panel.Controls.Add(hint);
            panel.Controls.Add(google);
            panel.Controls.Add(direction);
            panel.Controls.Add(algorithms);
            return panel;
        }

        private Control BuildContentPanel()
        {
            TableLayoutPanel content = new TableLayoutPanel();
            content.Dock = DockStyle.Fill;
            content.RowCount = 4;
            content.ColumnCount = 1;
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

            GroupBox inputGroup = new GroupBox();
            inputGroup.Text = "Входные данные";
            inputGroup.Dock = DockStyle.Fill;

            inputGrid.Dock = DockStyle.Fill;
            inputGrid.AllowUserToAddRows = true;
            inputGrid.AllowUserToDeleteRows = true;
            inputGrid.RowHeadersVisible = true;
            inputGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            inputGrid.ColumnCount = 1;
            inputGrid.Columns[0].Name = "Значение";
            inputGrid.EditMode = DataGridViewEditMode.EditOnEnter;
            inputGrid.CellValidating += InputGrid_CellValidating;
            inputGrid.RowsAdded += delegate { UpdateCountLabel(); };
            inputGrid.RowsRemoved += delegate { UpdateCountLabel(); };
            inputGroup.Controls.Add(inputGrid);

            GroupBox visualizationGroup = new GroupBox();
            visualizationGroup.Text = "Одновременная визуализация выбранных алгоритмов";
            visualizationGroup.Dock = DockStyle.Fill;
            visualizationPanel.Dock = DockStyle.Fill;
            visualizationPanel.FlowDirection = FlowDirection.TopDown;
            visualizationPanel.WrapContents = false;
            visualizationPanel.AutoScroll = true;
            visualizationPanel.Resize += delegate { ResizeVisualizerCards(); };
            visualizationGroup.Controls.Add(visualizationPanel);

            GroupBox resultGroup = new GroupBox();
            resultGroup.Text = "Результаты";
            resultGroup.Dock = DockStyle.Fill;
            resultGrid.Dock = DockStyle.Fill;
            resultGrid.ReadOnly = true;
            resultGrid.AllowUserToAddRows = false;
            resultGrid.RowHeadersVisible = false;
            resultGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            resultGrid.ColumnCount = 4;
            resultGrid.Columns[0].Name = "Алгоритм";
            resultGrid.Columns[1].Name = "Время";
            resultGrid.Columns[2].Name = "Итерации";
            resultGrid.Columns[3].Name = "Статус";
            resultGroup.Controls.Add(resultGrid);

            Panel statusPanel = new Panel();
            statusPanel.Dock = DockStyle.Fill;
            countLabel.AutoSize = true;
            countLabel.Location = new Point(5, 7);
            fastestLabel.AutoSize = true;
            fastestLabel.Location = new Point(190, 7);
            fastestLabel.Text = "Самый быстрый: —";
            statusPanel.Controls.Add(countLabel);
            statusPanel.Controls.Add(fastestLabel);

            content.Controls.Add(inputGroup, 0, 0);
            content.Controls.Add(visualizationGroup, 0, 1);
            content.Controls.Add(resultGroup, 0, 2);
            content.Controls.Add(statusPanel, 0, 3);
            return content;
        }

        private void InputGrid_CellValidating(object sender, DataGridViewCellValidatingEventArgs eventArgs)
        {
            if (eventArgs.RowIndex < 0 || eventArgs.ColumnIndex != 0) return;
            if (inputGrid.Rows[eventArgs.RowIndex].IsNewRow) return;

            string text = Convert.ToString(eventArgs.FormattedValue);
            if (string.IsNullOrWhiteSpace(text)) return;

            double value;
            if (!TryParseDouble(text, out value) || double.IsNaN(value) || double.IsInfinity(value))
            {
                eventArgs.Cancel = true;
                MessageBox.Show(this, "В ячейке " + (eventArgs.RowIndex + 1) + " должно быть корректное конечное число.",
                    "Некорректный ввод", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void FillSampleData()
        {
            double[] sample = { 12, 5, 8, 3, 17, 1, 10, 7, 4, 15 };
            SetInputData(sample);
        }

        private void SetInputData(IEnumerable<double> data)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            List<double> values = data.ToList();
            if (values.Count > MaxInputCount)
                throw new InvalidOperationException("Количество элементов не должно превышать " + MaxInputCount + ".");

            foreach (double value in values)
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                    throw new InvalidDataException("В наборе обнаружено некорректное нечисловое значение.");
            }

            ClearInputGridRows();
            foreach (double value in values)
            {
                int rowIndex = inputGrid.Rows.Add();
                inputGrid.Rows[rowIndex].Cells[0].Value = value.ToString(CultureInfo.CurrentCulture);
            }
            UpdateCountLabel();
        }

        private List<double> ReadInputData()
        {
            List<double> result = new List<double>();
            for (int rowIndex = 0; rowIndex < inputGrid.Rows.Count; ++rowIndex)
            {
                DataGridViewRow row = inputGrid.Rows[rowIndex];
                if (row.IsNewRow) continue;
                string text = Convert.ToString(row.Cells[0].Value);
                if (string.IsNullOrWhiteSpace(text)) continue;

                double value;
                if (!TryParseDouble(text, out value) || double.IsNaN(value) || double.IsInfinity(value))
                    throw new FormatException("Строка " + (rowIndex + 1) + ": некорректное числовое значение.");
                result.Add(value);
            }

            if (result.Count < 2)
                throw new InvalidOperationException("Для сортировки нужно минимум 2 числовых значения.");
            if (result.Count > MaxInputCount)
                throw new InvalidOperationException("Количество элементов не должно превышать " + MaxInputCount + ".");
            return result;
        }

        private List<SortAlgorithm> SelectedAlgorithms()
        {
            List<SortAlgorithm> result = new List<SortAlgorithm>();
            if (bubbleCheck.Checked) result.Add(SortAlgorithm.Bubble);
            if (insertionCheck.Checked) result.Add(SortAlgorithm.Insertion);
            if (shakerCheck.Checked) result.Add(SortAlgorithm.Shaker);
            if (quickCheck.Checked) result.Add(SortAlgorithm.Quick);
            if (bogoCheck.Checked) result.Add(SortAlgorithm.Bogo);
            return result;
        }

        private void StartCalculation()
        {
            if (calculationTask != null && !calculationTask.IsCompleted)
            {
                MessageBox.Show(this, "Расчёт уже выполняется. Сначала остановите его.", "Расчёт", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            calculationTask = null;

            List<SortAlgorithm> selected = SelectedAlgorithms();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Выберите хотя бы один алгоритм через CheckBox.", "Нет алгоритмов", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            List<double> data;
            try
            {
                data = ReadInputData();
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Ошибка входных данных", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (bogoCheck.Checked && data.Count > BogoMaxCount)
            {
                MessageBox.Show(this,
                    "При выборе BOGO количество элементов не должно превышать " + BogoMaxCount + ".",
                    "Недопустимый размер для BOGO", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool ascending = directionBox.SelectedIndex == 0;
            CancellationTokenSource calculationCancellation = new CancellationTokenSource();
            cancellation = calculationCancellation;
            CancellationToken calculationToken = calculationCancellation.Token;
            int currentCalculationVersion = ++calculationVersion;

            resultGrid.Rows.Clear();
            fastestLabel.Text = "Самый быстрый: выполняется…";
            BuildVisualizers(selected);

            foreach (SortAlgorithm algorithm in selected)
            {
                VisualizerControl visualizer = visualizers[algorithm];
                visualizer.SetData(data.ToArray());
                visualizer.SetStatus("Ожидание", "—");
                resultGrid.Rows.Add(GetAlgorithmName(algorithm), "—", "—", "Ожидание");
            }

            Dictionary<SortAlgorithm, VisualizerControl> calculationVisualizers =
                new Dictionary<SortAlgorithm, VisualizerControl>(visualizers);

            calculationTask = Task.Factory.StartNew(delegate
            {
                List<SortResult> results = new List<SortResult>();

                try
                {
                    foreach (SortAlgorithm algorithm in selected)
                    {
                        if (calculationToken.IsCancellationRequested)
                            break;

                        VisualizerControl visualizer = null;
                        bool visualizerExists = calculationVisualizers.TryGetValue(algorithm, out visualizer);

                        PostToUi(currentCalculationVersion, delegate
                        {
                            if (visualizerExists && visualizer != null)
                                visualizer.SetStatus("Выполняется", "—");
                            UpdateResultStatus(algorithm, "Выполняется");
                        });

                        Stopwatch updateWatch = Stopwatch.StartNew();
                        bool firstFrame = true;

                        SortResult result = SortAlgorithms.Run(algorithm, data.ToArray(), ascending,
                            delegate(double[] snapshot, int firstIndex, int secondIndex, string operation)
                            {
                                if (!firstFrame && updateWatch.ElapsedMilliseconds < 15)
                                    return;

                                firstFrame = false;
                                updateWatch.Restart();

                                if (visualizerExists && visualizer != null)
                                    visualizer.EnqueueFrame(snapshot, firstIndex, secondIndex, operation);
                            }, calculationToken);

                        results.Add(result);

                        if (visualizerExists && visualizer != null)
                            visualizer.SetFinalState(result.SortedData, result.Message, FormatTime(result.Elapsed));

                        PostToUi(currentCalculationVersion, delegate
                        {
                            UpdateResultRow(algorithm, result);
                        });
                    }

                    PostToUi(currentCalculationVersion, delegate
                    {
                        if (calculationToken.IsCancellationRequested)
                        {
                            fastestLabel.Text = "Самый быстрый: расчёт остановлен";
                            return;
                        }

                        ShowFastest(results);
                    });
                }
                catch (Exception exception)
                {
                    PostToUi(currentCalculationVersion, delegate
                    {
                        fastestLabel.Text = "Ошибка расчёта: " + exception.Message;
                    });
                }
                finally
                {
                    PostCalculationStateToUi(calculationCancellation);
                    calculationCancellation.Dispose();
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void UpdateResultStatus(SortAlgorithm algorithm, string status)
        {
            foreach (DataGridViewRow row in resultGrid.Rows)
            {
                if (Convert.ToString(row.Cells[0].Value) == GetAlgorithmName(algorithm))
                {
                    row.Cells[3].Value = status;
                    break;
                }
            }
        }

        private void PostToUi(int expectedCalculationVersion, Action action)
        {
            if (IsDisposed || !IsHandleCreated || expectedCalculationVersion != calculationVersion)
                return;

            try
            {
                BeginInvoke(new Action(delegate
                {
                    if (IsDisposed || expectedCalculationVersion != calculationVersion)
                        return;
                    action();
                }));
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void PostCalculationStateToUi(CancellationTokenSource finishedCancellation)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            try
            {
                BeginInvoke(new Action(delegate
                {
                    if (!ReferenceEquals(cancellation, finishedCancellation))
                        return;

                    cancellation = null;
                    calculationTask = null;
                }));
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void UpdateResultRow(SortAlgorithm algorithm, SortResult result)
        {
            foreach (DataGridViewRow row in resultGrid.Rows)
            {
                if (Convert.ToString(row.Cells[0].Value) == GetAlgorithmName(algorithm))
                {
                    row.Cells[1].Value = FormatTime(result.Elapsed);
                    row.Cells[2].Value = result.Iterations.ToString("N0", CultureInfo.CurrentCulture);
                    row.Cells[3].Value = result.Message;
                    break;
                }
            }
        }

        private void ShowFastest(List<SortResult> results)
        {
            List<SortResult> completed = results
                .Where(resultItem => resultItem.Completed && !resultItem.Cancelled)
                .ToList();
            if (completed.Count == 0)
            {
                fastestLabel.Text = "Самый быстрый: нет завершённых алгоритмов";
                return;
            }

            SortResult fastest = completed.OrderBy(resultItem => resultItem.Elapsed).First();
            fastestLabel.Text = "Самый быстрый в этом запуске: " + GetAlgorithmName(fastest.Algorithm) +
                                " (" + FormatTime(fastest.Elapsed) + ")";
        }

        private void StopCalculation()
        {
            CancellationTokenSource currentCancellation = cancellation;
            if (currentCancellation != null)
                currentCancellation.Cancel();
        }

        private void BuildVisualizers(List<SortAlgorithm> algorithms)
        {
            foreach (Control control in visualizationPanel.Controls)
                control.Dispose();
            visualizationPanel.Controls.Clear();
            visualizers.Clear();

            foreach (SortAlgorithm algorithm in algorithms)
            {
                VisualizerControl control = new VisualizerControl(GetAlgorithmName(algorithm));
                control.Width = Math.Max(500, visualizationPanel.ClientSize.Width - 30);
                visualizationPanel.Controls.Add(control);
                visualizers[algorithm] = control;
            }
        }

        private void ResizeVisualizerCards()
        {
            int width = Math.Max(500, visualizationPanel.ClientSize.Width - 30);
            foreach (Control control in visualizationPanel.Controls)
                control.Width = width;
        }

        private void LoadExcel()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Excel Workbook (*.xlsx)|*.xlsx|Все файлы (*.*)|*.*";
                dialog.Title = "Выберите XLSX-файл";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    SetInputData(DataIO.ReadXlsx(dialog.FileName));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Не удалось загрузить XLSX:\n" + ex.Message,
                        "Ошибка импорта", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void LoadGoogle()
        {
            try
            {
                SetInputData(DataIO.ReadGoogleSheet(googleUrlBox.Text));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Не удалось загрузить Google Sheets:\n" + ex.Message +
                    "\n\nТаблица должна быть доступна на чтение по ссылке.",
                    "Ошибка импорта", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void GenerateData()
        {
            using (GeneratorDialog dialog = new GeneratorDialog())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                GeneratorSettings settings = dialog.Settings;
                Random random = new Random();
                double[] generated = new double[settings.Count];
                for (int generatedIndex = 0; generatedIndex < generated.Length; ++generatedIndex)
                    generated[generatedIndex] = settings.Min + random.NextDouble() * (settings.Max - settings.Min);

                for (int generatedIndex = 0; generatedIndex < generated.Length; ++generatedIndex)
                    generated[generatedIndex] = Math.Round(generated[generatedIndex], 2);

                SetInputData(generated);
            }
        }

        private void ClearInputGridRows()
        {
            inputGrid.CancelEdit();
            inputGrid.ClearSelection();
            inputGrid.CurrentCell = null;

            bool allowUserToAddRows = inputGrid.AllowUserToAddRows;
            try
            {
                inputGrid.AllowUserToAddRows = false;
                inputGrid.Rows.Clear();
            }
            finally
            {
                inputGrid.AllowUserToAddRows = allowUserToAddRows;
            }
        }

        private void ClearAll()
        {
            ++calculationVersion;
            StopCalculation();
            ClearInputGridRows();
            resultGrid.Rows.Clear();
            fastestLabel.Text = "Самый быстрый: —";

            foreach (Control visualizerControl in visualizationPanel.Controls)
                visualizerControl.Dispose();
            visualizationPanel.Controls.Clear();
            visualizers.Clear();
            UpdateCountLabel();
        }

        private void OnFormClosing(object sender, FormClosingEventArgs eventArgs)
        {
            ++calculationVersion;
            StopCalculation();
        }

        private void UpdateCountLabel()
        {
            int count = 0;
            foreach (DataGridViewRow row in inputGrid.Rows)
            {
                if (!row.IsNewRow && !string.IsNullOrWhiteSpace(Convert.ToString(row.Cells[0].Value)))
                    ++count;
            }
            countLabel.Text = "Элементов: " + count;
        }

        private static string GetAlgorithmName(SortAlgorithm algorithm)
        {
            switch (algorithm)
            {
                case SortAlgorithm.Bubble: return "Пузырьковая";
                case SortAlgorithm.Insertion: return "Вставками";
                case SortAlgorithm.Shaker: return "Шейкерная";
                case SortAlgorithm.Quick: return "Быстрая";
                case SortAlgorithm.Bogo: return "BOGO";
                default: return algorithm.ToString();
            }
        }

        private static string FormatTime(TimeSpan time)
        {
            if (time.TotalMilliseconds < 1.0)
                return (time.TotalMilliseconds * 1000.0).ToString("F2", CultureInfo.CurrentCulture) + " мкс";
            if (time.TotalSeconds < 1.0)
                return time.TotalMilliseconds.ToString("F3", CultureInfo.CurrentCulture) + " мс";
            return time.TotalSeconds.ToString("F3", CultureInfo.CurrentCulture) + " с";
        }

        private static bool TryParseDouble(string text, out double value)
        {
            return double.TryParse(text.Trim(), NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.CurrentCulture, out value)
                || double.TryParse(text.Trim(), NumberStyles.Float | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture, out value)
                || double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out value);
        }
    }
}
