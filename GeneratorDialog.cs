using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace Sorter
{
    public sealed class GeneratorSettings
    {
        public int Count;
        public double Min;
        public double Max;
    }

    public sealed class GeneratorDialog : Form
    {
        private readonly TextBox countBox = new TextBox();
        private readonly TextBox minBox = new TextBox();
        private readonly TextBox maxBox = new TextBox();
        public GeneratorSettings Settings { get; private set; }

        public GeneratorDialog()
        {
            Text = "Генерация данных";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(390, 190);

            TableLayoutPanel table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.Padding = new Padding(14);
            table.ColumnCount = 2;
            table.RowCount = 4;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

            table.Controls.Add(new Label { Text = "Количество элементов:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            table.Controls.Add(countBox, 1, 0);
            table.Controls.Add(new Label { Text = "Минимальное значение:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            table.Controls.Add(minBox, 1, 1);
            table.Controls.Add(new Label { Text = "Максимальное значение:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
            table.Controls.Add(maxBox, 1, 2);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.WrapContents = false;
            Button cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Width = 90 };
            Button ok = new Button { Text = "Сгенерировать", Width = 120 };
            ok.Click += OnOk;
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            table.Controls.Add(buttons, 0, 3);
            table.SetColumnSpan(buttons, 2);

            Controls.Add(table);
            AcceptButton = ok;
            CancelButton = cancel;

            countBox.Text = "30";
            minBox.Text = "0";
            maxBox.Text = "100";
        }

        private void OnOk(object sender, EventArgs eventArgs)
        {
            int count;
            double min;
            double max;
            if (!int.TryParse(countBox.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out count))
            {
                MessageBox.Show(this, "Количество должно быть целым числом.", "Некорректный ввод", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                countBox.Focus();
                return;
            }
            if (!TryParseDouble(minBox.Text, out min) || !TryParseDouble(maxBox.Text, out max)
                || double.IsNaN(min) || double.IsNaN(max)
                || double.IsInfinity(min) || double.IsInfinity(max))
            {
                MessageBox.Show(this, "Минимум и максимум должны быть конечными числами.", "Некорректный ввод", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (count < 2 || count > 2000)
            {
                MessageBox.Show(this, "Количество элементов должно быть от 2 до 2000.", "Недопустимое значение", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (min >= max)
            {
                MessageBox.Show(this, "Минимальное значение должно быть меньше максимального.", "Недопустимое значение", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Settings = new GeneratorSettings { Count = count, Min = min, Max = max };
            DialogResult = DialogResult.OK;
            Close();
        }

        private static bool TryParseDouble(string text, out double value)
        {
            return double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value)
                || double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
