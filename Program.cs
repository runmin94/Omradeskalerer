using System.Globalization;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace Omradeskalerer;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed class MainForm : Form
{
    private static readonly Color BackgroundColor = Color.FromArgb(241, 245, 251);
    private static readonly Color CardColor = Color.White;
    private static readonly Color PrimaryColor = Color.FromArgb(19, 49, 101);
    private static readonly Color DeepBlueColor = Color.FromArgb(8, 25, 64);
    private static readonly Color AccentColor = Color.FromArgb(0, 186, 224);
    private static readonly Color OrangeColor = Color.FromArgb(255, 167, 38);
    private static readonly Color TextColor = Color.FromArgb(25, 38, 59);
    private static readonly Color MutedColor = Color.FromArgb(94, 109, 132);
    private static readonly Color ErrorColor = Color.FromArgb(211, 74, 74);
    private static readonly Color FieldColor = Color.FromArgb(247, 250, 255);
    private static readonly Color BorderColor = Color.FromArgb(218, 227, 240);

    private readonly TextBox inputMinBox = CreateNumberBox("0");
    private readonly TextBox inputMaxBox = CreateNumberBox("32767");
    private readonly TextBox outputMinBox = CreateNumberBox("4");
    private readonly TextBox outputMaxBox = CreateNumberBox("20");
    private readonly TextBox valueBox = CreateNumberBox("0");
    private readonly ComboBox inputPresetBox = CreatePresetBox();
    private readonly ComboBox outputPresetBox = CreatePresetBox();
    private readonly Label resultLabel = new();
    private readonly Label inputValueLabel = new();
    private readonly Label positionLabel = new();
    private readonly Label formulaLabel = new();
    private readonly Label statusLabel = new();
    private readonly CheckBox clampCheckBox = new();
    private readonly RangeProgressBar rangeProgressBar = new();
    private readonly ToolTip toolTip = new();
    private double? currentResult;
    private bool isUpdatingPresets;

    private static readonly RangePreset[] InputPresets =
    [
        new("0–65535 (uint)", 0, 65535),
        new("0–32767 (int)", 0, 32767),
        new("0–4095 (12-bit)", 0, 4095),
        new("0–1023 (10-bit)", 0, 1023),
        new("0-511 (8-bit)", 0, 511),
        new("0–100", 0, 100),
        new("Egendefinert", null, null),
    ];

    private static readonly RangePreset[] OutputPresets =
    [
        new("0-5", 0,5),
        new("0–10", 0, 10),
        new("0–20", 0, 20),
        new("4–20", 4, 20),
        new("0–100", 0, 100),
        new("Egendefinert", null, null),
    ];

    public MainForm()
    {
        Text = "Områdeskalerer";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(760, 960);
        MinimumSize = new Size(700, 900);
        BackColor = BackgroundColor;
        Font = new Font("Segoe UI", 10F);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.Sizable;
        DoubleBuffered = true;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        ConfigurePresetBox(inputPresetBox, InputPresets);
        ConfigurePresetBox(outputPresetBox, OutputPresets);
        SyncPresetSelections();
        Controls.Add(BuildMainLayout());

        foreach (TextBox box in new[] { inputMinBox, inputMaxBox, outputMinBox, outputMaxBox, valueBox })
        {
            box.TextChanged += (_, _) =>
            {
                Recalculate();
                if (!isUpdatingPresets && box != valueBox)
                {
                    SyncPresetSelections();
                }
            };
            box.Enter += (_, _) => box.SelectAll();
        }

        inputPresetBox.SelectedIndexChanged += (_, _) =>
            ApplySelectedPreset(inputPresetBox, inputMinBox, inputMaxBox);
        outputPresetBox.SelectedIndexChanged += (_, _) =>
            ApplySelectedPreset(outputPresetBox, outputMinBox, outputMaxBox);
        rangeProgressBar.RatioChanged += (_, _) => UpdateValueFromSlider();
        valueBox.KeyPress += ValueBox_KeyPress;
        valueBox.Leave += (_, _) => NormalizeValueForCurrentInputRange();
        clampCheckBox.CheckedChanged += (_, _) => Recalculate();
        Shown += (_, _) =>
        {
            valueBox.Focus();
            valueBox.SelectAll();
            Recalculate();
        };
    }

    private Control BuildMainLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(28),
            BackColor = BackgroundColor,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildInputCard(), 0, 1);
        root.Controls.Add(BuildResultCard(), 0, 2);
        return root;
    }

    private Control BuildHeader()
    {
        var panel = new GradientPanel
        {
            Dock = DockStyle.Top,
            Height = 112,
            Margin = new Padding(0, 0, 0, 18),
            Color1 = DeepBlueColor,
            Color2 = Color.FromArgb(23, 98, 178),
            CornerRadius = 24,
        };

        var iconBox = new PictureBox
        {
            Image = Icon?.ToBitmap(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(64, 64),
            Location = new Point(22, 22),
            BackColor = Color.Transparent,
        };

        var title = new Label
        {
            AutoSize = true,
            Text = "Områdeskalerer",
            Font = new Font("Segoe UI Semibold", 21F),
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Location = new Point(101, 22),
        };

        var subtitle = new Label
        {
            AutoSize = true,
            Text = "Lineær skalering fra ett tallområde til et annet",
            Font = new Font("Segoe UI", 9.8F),
            ForeColor = Color.FromArgb(210, 229, 250),
            BackColor = Color.Transparent,
            Location = new Point(104, 64),
        };

        var badge = new RoundedPanel
        {
            Size = new Size(94, 34),
            Location = new Point(610, 38),
            BackColor = Color.FromArgb(37, 111, 190),
            BorderColor = Color.FromArgb(91, 166, 234),
            CornerRadius = 17,
        };
        badge.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "LINEÆR",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Semibold", 8.3F),
            ForeColor = Color.White,
            BackColor = Color.Transparent,
        });
        panel.Resize += (_, _) =>
            badge.Location = new Point(panel.ClientSize.Width - badge.Width - 24, 38);

        panel.Controls.Add(iconBox);
        panel.Controls.Add(title);
        panel.Controls.Add(subtitle);
        panel.Controls.Add(badge);
        return panel;
    }

    private Control BuildInputCard()
    {
        var card = CreateCard();
        card.AutoSize = true;
        card.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        card.Margin = new Padding(0, 0, 0, 16);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 6,
            Padding = new Padding(24, 21, 24, 20),
            BackColor = Color.Transparent,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        var inputHeading = CreateRangeHeading("INN", "Inngangsområde", "Råverdi eller signal", AccentColor);
        var outputHeading = CreateRangeHeading("UT", "Utgangsområde", "Ønsket skalert verdi", OrangeColor);
        layout.Controls.Add(inputHeading, 0, 0);
        layout.Controls.Add(outputHeading, 1, 0);

        layout.Controls.Add(CreateField("Vanlig område", inputPresetBox), 0, 1);
        layout.Controls.Add(CreateField("Vanlig område", outputPresetBox), 1, 1);
        layout.Controls.Add(CreateField("Minimum", inputMinBox), 0, 2);
        layout.Controls.Add(CreateField("Minimum", outputMinBox), 1, 2);
        layout.Controls.Add(CreateField("Maksimum", inputMaxBox), 0, 3);
        layout.Controls.Add(CreateField("Maksimum", outputMaxBox), 1, 3);

        var valueField = CreateField("Verdi som skal skaleres", valueBox);
        valueField.Margin = new Padding(0, 10, 8, 0);
        layout.Controls.Add(valueField, 0, 4);

        clampCheckBox.Text = "Begrens resultatet til utgangsområdet";
        clampCheckBox.AutoSize = true;
        clampCheckBox.ForeColor = TextColor;
        clampCheckBox.BackColor = Color.Transparent;
        clampCheckBox.FlatStyle = FlatStyle.Flat;
        clampCheckBox.Margin = new Padding(14, 38, 0, 0);
        layout.Controls.Add(clampCheckBox, 1, 4);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 18, 0, 0),
            BackColor = Color.Transparent,
        };
        var resetButton = CreateButton("Nullstill", false);
        resetButton.Click += (_, _) => ResetValues();
        var swapButton = CreateButton("Bytt områder", false);
        swapButton.Click += (_, _) => SwapRanges();
        buttons.Controls.Add(resetButton);
        buttons.Controls.Add(swapButton);
        layout.Controls.Add(buttons, 0, 5);
        layout.SetColumnSpan(buttons, 2);

        card.Controls.Add(layout);
        return card;
    }

    private Control BuildResultCard()
    {
        var card = new GradientPanel
        {
            Dock = DockStyle.Fill,
            MinimumSize = new Size(0, 310),
            Color1 = DeepBlueColor,
            Color2 = Color.FromArgb(17, 69, 132),
            CornerRadius = 24,
        };
        card.Margin = new Padding(0);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(26, 22, 26, 22),
            BackColor = Color.Transparent,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var summary = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
        };
        summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));
        summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
        summary.Controls.Add(CreateResultMetric("SKALERT RESULTAT", resultLabel, false), 0, 0);
        summary.Controls.Add(CreateResultMetric("VERDI SOM SKAL SKALERES", inputValueLabel, true), 1, 0);
        layout.Controls.Add(summary, 0, 0);

        resultLabel.AutoSize = true;
        resultLabel.Font = new Font("Segoe UI Semibold", 38F);
        resultLabel.ForeColor = Color.White;
        resultLabel.BackColor = Color.Transparent;
        resultLabel.Margin = new Padding(0, 3, 0, 0);

        inputValueLabel.AutoSize = true;
        inputValueLabel.Font = new Font("Segoe UI Semibold", 24F);
        inputValueLabel.ForeColor = OrangeColor;
        inputValueLabel.BackColor = Color.Transparent;
        inputValueLabel.TextAlign = ContentAlignment.MiddleRight;
        inputValueLabel.Margin = new Padding(0, 8, 0, 0);

        positionLabel.AutoSize = true;
        positionLabel.Font = new Font("Segoe UI", 9.8F);
        positionLabel.ForeColor = Color.FromArgb(191, 219, 246);
        positionLabel.BackColor = Color.Transparent;
        positionLabel.Margin = new Padding(2, 0, 0, 12);
        layout.Controls.Add(positionLabel, 0, 1);

        rangeProgressBar.Dock = DockStyle.Top;
        rangeProgressBar.Height = 18;
        rangeProgressBar.Margin = new Padding(0, 0, 0, 18);
        rangeProgressBar.TrackColor = Color.FromArgb(45, 85, 140);
        rangeProgressBar.FillColor = AccentColor;
        rangeProgressBar.MarkerColor = OrangeColor;
        rangeProgressBar.AccessibleName = "Velg verdi i inngangsområdet";
        rangeProgressBar.AccessibleRole = AccessibleRole.Slider;
        toolTip.SetToolTip(rangeProgressBar, "Klikk eller dra for å endre inngangsverdien");
        layout.Controls.Add(rangeProgressBar, 0, 2);

        var divider = new Panel
        {
            Dock = DockStyle.Top,
            Height = 1,
            BackColor = Color.FromArgb(63, 111, 166),
            Margin = new Padding(0, 0, 0, 15),
        };
        layout.Controls.Add(divider, 0, 3);

        formulaLabel.AutoSize = true;
        formulaLabel.MaximumSize = new Size(650, 0);
        formulaLabel.Font = new Font("Consolas", 9.3F);
        formulaLabel.ForeColor = Color.FromArgb(197, 220, 244);
        formulaLabel.BackColor = Color.Transparent;
        formulaLabel.Margin = new Padding(0, 0, 0, 10);
        layout.Controls.Add(formulaLabel, 0, 4);

        statusLabel.AutoSize = true;
        statusLabel.MaximumSize = new Size(650, 0);
        statusLabel.Font = new Font("Segoe UI", 9.5F);
        statusLabel.ForeColor = Color.FromArgb(255, 155, 155);
        statusLabel.BackColor = Color.Transparent;
        layout.Controls.Add(statusLabel, 0, 5);

        var copyButton = CreateButton("Kopier resultat", true);
        copyButton.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
        copyButton.Click += (_, _) => CopyResult();
        layout.Controls.Add(copyButton, 0, 6);

        card.Controls.Add(layout);
        return card;
    }

    private void Recalculate()
    {
        statusLabel.Text = string.Empty;
        currentResult = null;

        if (!TryRead(inputMinBox, out double inputMin) ||
            !TryRead(inputMaxBox, out double inputMax) ||
            !TryRead(outputMinBox, out double outputMin) ||
            !TryRead(outputMaxBox, out double outputMax) ||
            !TryRead(valueBox, out double value))
        {
            ShowInvalid("Fyll inn gyldige tall i alle feltene.");
            return;
        }

        if (inputMin == inputMax)
        {
            ShowInvalid("Minimum og maksimum i inngangsområdet må være forskjellige.");
            return;
        }

        double ratio = (value - inputMin) / (inputMax - inputMin);
        double result = outputMin + ratio * (outputMax - outputMin);
        bool wasClamped = false;

        if (clampCheckBox.Checked)
        {
            double lower = Math.Min(outputMin, outputMax);
            double upper = Math.Max(outputMin, outputMax);
            double clamped = Math.Clamp(result, lower, upper);
            wasClamped = clamped != result;
            result = clamped;
        }

        if (IsBitRange(outputMin, outputMax))
        {
            result = Math.Round(result);
        }

        currentResult = result;
        resultLabel.Text = FormatRangeValue(result, outputMin, outputMax, false);
        inputValueLabel.Text = FormatRangeValue(value, inputMin, inputMax, true);
        positionLabel.Text = $"{FormatPercentage(ratio * 100)} % av inngangsområdet";
        rangeProgressBar.Ratio = ratio;
        rangeProgressBar.IsValid = true;
        formulaLabel.Text =
            $"{FormatNumber(outputMin)} + (({FormatNumber(value)} - {FormatNumber(inputMin)}) / " +
            $"({FormatNumber(inputMax)} - {FormatNumber(inputMin)})) x " +
            $"({FormatNumber(outputMax)} - {FormatNumber(outputMin)})";

        if (wasClamped)
        {
            statusLabel.ForeColor = Color.FromArgb(191, 219, 246);
            statusLabel.Text = "Resultatet ble begrenset til utgangsområdet.";
        }
        else
        {
            statusLabel.Text = string.Empty;
        }
    }

    private void ShowInvalid(string message)
    {
        resultLabel.Text = "-";
        inputValueLabel.Text = "-";
        positionLabel.Text = string.Empty;
        rangeProgressBar.IsValid = false;
        formulaLabel.Text = "Formel: ut_min + ((verdi - inn_min) / (inn_maks - inn_min)) x (ut_maks - ut_min)";
        statusLabel.ForeColor = Color.FromArgb(255, 155, 155);
        statusLabel.Text = message;
    }

    private void UpdateValueFromSlider()
    {
        if (!TryRead(inputMinBox, out double inputMin) ||
            !TryRead(inputMaxBox, out double inputMax) ||
            inputMin == inputMax)
        {
            return;
        }

        double value = inputMin + rangeProgressBar.Ratio * (inputMax - inputMin);
        valueBox.Text = IsBitRange(inputMin, inputMax)
            ? Math.Round(value).ToString("0", CultureInfo.CurrentCulture)
            : FormatEditableNumber(value);
    }

    private void ValueBox_KeyPress(object? sender, KeyPressEventArgs eventArgs)
    {
        if (!IsCurrentInputBitRange())
        {
            return;
        }

        if (eventArgs.KeyChar is ',' or '.')
        {
            eventArgs.Handled = true;
        }
    }

    private void NormalizeValueForCurrentInputRange()
    {
        if (!TryRead(inputMinBox, out double inputMin) ||
            !TryRead(inputMaxBox, out double inputMax) ||
            !TryRead(valueBox, out double value))
        {
            return;
        }

        string normalized = IsBitRange(inputMin, inputMax)
            ? Math.Round(value).ToString("0", CultureInfo.CurrentCulture)
            : FormatEditableNumber(value);

        if (valueBox.Text != normalized)
        {
            valueBox.Text = normalized;
            valueBox.SelectionStart = valueBox.TextLength;
        }
    }

    private bool IsCurrentInputBitRange()
    {
        return TryRead(inputMinBox, out double inputMin) &&
               TryRead(inputMaxBox, out double inputMax) &&
               IsBitRange(inputMin, inputMax);
    }

    private void ResetValues()
    {
        inputMinBox.Text = "0";
        inputMaxBox.Text = "32767";
        outputMinBox.Text = "4";
        outputMaxBox.Text = "20";
        valueBox.Text = "0";
        clampCheckBox.Checked = false;
        SyncPresetSelections();
        valueBox.Focus();
        valueBox.SelectAll();
    }

    private void SwapRanges()
    {
        double? swappedValue = currentResult;

        (inputMinBox.Text, outputMinBox.Text) = (outputMinBox.Text, inputMinBox.Text);
        (inputMaxBox.Text, outputMaxBox.Text) = (outputMaxBox.Text, inputMaxBox.Text);

        if (swappedValue is not null)
        {
            valueBox.Text =
                TryRead(inputMinBox, out double newInputMin) &&
                TryRead(inputMaxBox, out double newInputMax) &&
                IsBitRange(newInputMin, newInputMax)
                    ? Math.Round(swappedValue.Value).ToString("0", CultureInfo.CurrentCulture)
                    : FormatEditableNumber(swappedValue.Value);
        }

        SyncPresetSelections();
        valueBox.Focus();
        valueBox.SelectAll();
    }

    private void ApplySelectedPreset(ComboBox presetBox, TextBox minimumBox, TextBox maximumBox)
    {
        if (isUpdatingPresets || presetBox.SelectedItem is not RangePreset preset || preset.IsCustom)
        {
            return;
        }

        isUpdatingPresets = true;
        try
        {
            minimumBox.Text = FormatNumber(preset.Minimum!.Value);
            maximumBox.Text = FormatNumber(preset.Maximum!.Value);
        }
        finally
        {
            isUpdatingPresets = false;
        }

        Recalculate();

        if (presetBox == inputPresetBox)
        {
            NormalizeValueForCurrentInputRange();
        }
    }

    private void SyncPresetSelections()
    {
        isUpdatingPresets = true;
        try
        {
            SyncPresetSelection(inputPresetBox, InputPresets, inputMinBox, inputMaxBox);
            SyncPresetSelection(outputPresetBox, OutputPresets, outputMinBox, outputMaxBox);
        }
        finally
        {
            isUpdatingPresets = false;
        }
    }

    private static void SyncPresetSelection(
        ComboBox presetBox,
        RangePreset[] presets,
        TextBox minimumBox,
        TextBox maximumBox)
    {
        if (!TryRead(minimumBox, out double minimum) || !TryRead(maximumBox, out double maximum))
        {
            presetBox.SelectedItem = presets.First(preset => preset.IsCustom);
            return;
        }

        presetBox.SelectedItem = presets.FirstOrDefault(
            preset => !preset.IsCustom &&
                      preset.Minimum == minimum &&
                      preset.Maximum == maximum)
            ?? presets.First(preset => preset.IsCustom);
    }

    private void CopyResult()
    {
        if (currentResult is null)
        {
            return;
        }

        try
        {
            Clipboard.SetText(FormatResult(currentResult.Value));
            statusLabel.ForeColor = Color.FromArgb(191, 219, 246);
            statusLabel.Text = "Resultatet er kopiert.";
        }
        catch (ExternalException)
        {
            statusLabel.ForeColor = Color.FromArgb(255, 155, 155);
            statusLabel.Text = "Kunne ikke kopiere resultatet akkurat nå.";
        }
    }

    private static bool TryRead(TextBox box, out double value)
    {
        string text = box.Text.Trim();
        NumberStyles styles = NumberStyles.Float | NumberStyles.AllowThousands;

        if (double.TryParse(text, styles, CultureInfo.CurrentCulture, out value))
        {
            return double.IsFinite(value);
        }

        string normalized = text.Replace(',', '.');
        return double.TryParse(normalized, styles, CultureInfo.InvariantCulture, out value) &&
               double.IsFinite(value);
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.##########", CultureInfo.CurrentCulture);
    }

    private static string FormatResult(double value)
    {
        return value.ToString("0.###", CultureInfo.CurrentCulture);
    }

    private static string FormatEditableNumber(double value)
    {
        return value.ToString("0.00", CultureInfo.CurrentCulture);
    }

    private static string FormatRangeValue(double value, double minimum, double maximum, bool isInput)
    {
        if (IsBitRange(minimum, maximum))
        {
            return Math.Round(value).ToString("0", CultureInfo.CurrentCulture);
        }

        return isInput ? FormatEditableNumber(value) : FormatResult(value);
    }

    private static string FormatPercentage(double value)
    {
        return value.ToString("0.##", CultureInfo.CurrentCulture);
    }

    private static bool IsBitRange(double minimum, double maximum)
    {
        double lower = Math.Min(minimum, maximum);
        double upper = Math.Max(minimum, maximum);

        return lower == 0 &&
               upper is 511 or 1023 or 4095 or 32767 or 65535;
    }

    private static RoundedPanel CreateCard()
    {
        return new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = CardColor,
            BorderColor = BorderColor,
            CornerRadius = 22,
        };
    }

    private static Label CreateSectionHeading(string text, Color? color = null)
    {
        return new Label
        {
            AutoSize = true,
            Text = text,
            Font = new Font("Segoe UI Semibold", 11F),
            ForeColor = color ?? TextColor,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 8, 8),
        };
    }

    private static Control CreateResultMetric(string headingText, Label valueLabel, bool rightAlign)
    {
        var metric = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = rightAlign ? new Padding(12, 0, 0, 0) : new Padding(0),
        };
        metric.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        metric.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var heading = CreateSectionHeading(headingText, Color.FromArgb(183, 214, 246));
        heading.Font = new Font("Segoe UI Semibold", 8.5F);
        heading.Anchor = rightAlign ? AnchorStyles.Top | AnchorStyles.Right : AnchorStyles.Top | AnchorStyles.Left;
        heading.TextAlign = rightAlign ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft;

        valueLabel.Text = "-";
        valueLabel.Anchor = rightAlign ? AnchorStyles.Top | AnchorStyles.Right : AnchorStyles.Top | AnchorStyles.Left;

        metric.Controls.Add(heading, 0, 0);
        metric.Controls.Add(valueLabel, 0, 1);
        return metric;
    }

    private static Control CreateRangeHeading(string badgeText, string titleText, string subtitleText, Color badgeColor)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 59,
            Margin = new Padding(0, 0, 12, 10),
            BackColor = Color.Transparent,
        };

        bool isOrange = badgeColor.ToArgb() == OrangeColor.ToArgb();
        var badge = new RoundedPanel
        {
            Size = new Size(46, 27),
            Location = new Point(0, 2),
            BackColor = isOrange
                ? Color.FromArgb(255, 245, 224)
                : Color.FromArgb(224, 248, 253),
            BorderColor = badgeColor,
            CornerRadius = 13,
        };
        badge.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = badgeText,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Semibold", 8F),
            ForeColor = isOrange ? Color.FromArgb(164, 91, 0) : Color.FromArgb(0, 117, 146),
            BackColor = Color.Transparent,
        });

        panel.Controls.Add(badge);
        panel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = titleText,
            Font = new Font("Segoe UI Semibold", 11.2F),
            ForeColor = TextColor,
            BackColor = Color.Transparent,
            Location = new Point(53, 0),
        });
        panel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = subtitleText,
            Font = new Font("Segoe UI", 8.2F),
            ForeColor = MutedColor,
            BackColor = Color.Transparent,
            Location = new Point(54, 27),
        });
        return panel;
    }

    private static Control CreateField(string labelText, Control inputControl)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 14, 10),
            BackColor = Color.Transparent,
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var label = new Label
        {
            AutoSize = true,
            Text = labelText,
            Font = new Font("Segoe UI", 8.8F),
            ForeColor = MutedColor,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 3),
        };
        var shell = new RoundedPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            BackColor = FieldColor,
            BorderColor = BorderColor,
            CornerRadius = 10,
            Padding = new Padding(11, 7, 8, 5),
            Margin = new Padding(0),
        };
        inputControl.Dock = DockStyle.Fill;
        inputControl.BackColor = FieldColor;
        inputControl.GotFocus += (_, _) =>
        {
            shell.BorderColor = AccentColor;
            shell.Invalidate();
        };
        inputControl.LostFocus += (_, _) =>
        {
            shell.BorderColor = BorderColor;
            shell.Invalidate();
        };
        shell.Controls.Add(inputControl);
        panel.Controls.Add(label, 0, 0);
        panel.Controls.Add(shell, 0, 1);
        return panel;
    }

    private static TextBox CreateNumberBox(string value)
    {
        return new TextBox
        {
            Text = value,
            Dock = DockStyle.Top,
            Font = new Font("Segoe UI Semibold", 11F),
            ForeColor = TextColor,
            BackColor = FieldColor,
            BorderStyle = BorderStyle.None,
            AutoSize = false,
            Height = 25,
            Margin = new Padding(0),
        };
    }

    private static ComboBox CreatePresetBox()
    {
        return new ComboBox
        {
            Dock = DockStyle.Top,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 10F),
            ForeColor = TextColor,
            BackColor = FieldColor,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0),
        };
    }

    private static void ConfigurePresetBox(ComboBox presetBox, RangePreset[] presets)
    {
        presetBox.Items.AddRange(presets);
    }

    private static Button CreateButton(string text, bool primary)
    {
        var button = new Button
        {
            AutoSize = true,
            Text = text,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 9.3F),
            Cursor = Cursors.Hand,
            Padding = new Padding(15, 7, 15, 7),
            Margin = new Padding(0, 0, 8, 0),
            UseVisualStyleBackColor = false,
        };

        if (primary)
        {
            button.BackColor = AccentColor;
            button.ForeColor = DeepBlueColor;
            button.FlatAppearance.BorderColor = AccentColor;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(68, 211, 238);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 157, 193);
        }
        else
        {
            button.BackColor = FieldColor;
            button.ForeColor = PrimaryColor;
            button.FlatAppearance.BorderColor = BorderColor;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(233, 241, 251);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(220, 232, 246);
        }

        button.Resize += (_, _) =>
        {
            button.Region?.Dispose();
            using GraphicsPath path = UiGeometry.CreateRoundedRectangle(button.ClientRectangle, 12);
            button.Region = new Region(path);
        };
        return button;
    }

    private sealed record RangePreset(string Name, double? Minimum, double? Maximum)
    {
        public bool IsCustom => Minimum is null || Maximum is null;

        public override string ToString() => Name;
    }
}

internal static class UiGeometry
{
    public static GraphicsPath CreateRoundedRectangle(Rectangle rectangle, int radius)
    {
        var path = new GraphicsPath();
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
        {
            return path;
        }

        int diameter = Math.Min(radius * 2, Math.Min(rectangle.Width, rectangle.Height));
        var arc = new Rectangle(rectangle.Location, new Size(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = rectangle.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rectangle.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rectangle.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal class RoundedPanel : Panel
{
    private int cornerRadius = 18;
    private Color borderColor = Color.Transparent;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius
    {
        get => cornerRadius;
        set
        {
            cornerRadius = value;
            UpdateRegion();
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderColor
    {
        get => borderColor;
        set
        {
            borderColor = value;
            Invalidate();
        }
    }

    public RoundedPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
    }

    protected override void OnResize(EventArgs eventArgs)
    {
        base.OnResize(eventArgs);
        UpdateRegion();
    }

    protected override void OnPaintBackground(PaintEventArgs eventArgs)
    {
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using GraphicsPath path = UiGeometry.CreateRoundedRectangle(ClientRectangle, CornerRadius);
        using var brush = new SolidBrush(BackColor);
        eventArgs.Graphics.FillPath(brush, path);
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        if (BorderColor == Color.Transparent)
        {
            return;
        }

        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var borderRectangle = Rectangle.Inflate(ClientRectangle, -1, -1);
        using GraphicsPath path = UiGeometry.CreateRoundedRectangle(borderRectangle, Math.Max(1, CornerRadius - 1));
        using var pen = new Pen(BorderColor, 1F);
        eventArgs.Graphics.DrawPath(pen, path);
    }

    private void UpdateRegion()
    {
        if (ClientRectangle.Width <= 0 || ClientRectangle.Height <= 0)
        {
            return;
        }

        Region?.Dispose();
        using GraphicsPath path = UiGeometry.CreateRoundedRectangle(ClientRectangle, CornerRadius);
        Region = new Region(path);
    }
}

internal sealed class GradientPanel : Panel
{
    private int cornerRadius = 18;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color Color1 { get; set; } = Color.Navy;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color Color2 { get; set; } = Color.RoyalBlue;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius
    {
        get => cornerRadius;
        set
        {
            cornerRadius = value;
            UpdateRegion();
            Invalidate();
        }
    }

    public GradientPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
    }

    protected override void OnResize(EventArgs eventArgs)
    {
        base.OnResize(eventArgs);
        UpdateRegion();
    }

    protected override void OnPaintBackground(PaintEventArgs eventArgs)
    {
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using GraphicsPath path = UiGeometry.CreateRoundedRectangle(ClientRectangle, CornerRadius);
        using var brush = new LinearGradientBrush(ClientRectangle, Color1, Color2, 18F);
        eventArgs.Graphics.FillPath(brush, path);
    }

    private void UpdateRegion()
    {
        if (ClientRectangle.Width <= 0 || ClientRectangle.Height <= 0)
        {
            return;
        }

        Region?.Dispose();
        using GraphicsPath path = UiGeometry.CreateRoundedRectangle(ClientRectangle, CornerRadius);
        Region = new Region(path);
    }
}

internal sealed class RangeProgressBar : Control
{
    private double ratio;
    private bool isValid;
    private bool isDragging;

    public event EventHandler? RatioChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color TrackColor { get; set; } = Color.LightGray;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color FillColor { get; set; } = Color.Cyan;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color MarkerColor { get; set; } = Color.Orange;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double Ratio
    {
        get => ratio;
        set
        {
            ratio = value;
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsValid
    {
        get => isValid;
        set
        {
            isValid = value;
            Invalidate();
        }
    }

    public RangeProgressBar()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.UserPaint,
            true);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        int markerDiameter = Math.Min(16, Height);
        int trackHeight = Math.Min(8, Math.Max(4, Height / 2));
        Rectangle trackRectangle = GetTrackRectangle(markerDiameter, trackHeight);
        using GraphicsPath trackPath = UiGeometry.CreateRoundedRectangle(trackRectangle, trackHeight / 2);
        using var trackBrush = new SolidBrush(TrackColor);
        eventArgs.Graphics.FillPath(trackBrush, trackPath);

        if (!IsValid)
        {
            return;
        }

        double visibleRatio = Math.Clamp(Ratio, 0D, 1D);
        int markerCenter = trackRectangle.Left + (int)Math.Round(visibleRatio * trackRectangle.Width);
        int fillWidth = Math.Max(0, markerCenter - trackRectangle.Left);

        if (fillWidth > 1)
        {
            var fillRectangle = new Rectangle(trackRectangle.Left, trackRectangle.Top, fillWidth, trackRectangle.Height);
            using GraphicsPath fillPath = UiGeometry.CreateRoundedRectangle(fillRectangle, trackHeight / 2);
            using var fillBrush = new SolidBrush(FillColor);
            eventArgs.Graphics.FillPath(fillBrush, fillPath);
        }

        var markerRectangle = new Rectangle(
            Math.Clamp(markerCenter - markerDiameter / 2, 0, Math.Max(0, Width - markerDiameter)),
            (Height - markerDiameter) / 2,
            markerDiameter,
            markerDiameter);
        using var markerBrush = new SolidBrush(MarkerColor);
        eventArgs.Graphics.FillEllipse(markerBrush, markerRectangle);
        using var markerPen = new Pen(Color.White, 2F);
        eventArgs.Graphics.DrawEllipse(markerPen, markerRectangle);
    }

    protected override void OnMouseDown(MouseEventArgs eventArgs)
    {
        base.OnMouseDown(eventArgs);
        if (eventArgs.Button != MouseButtons.Left || !IsValid)
        {
            return;
        }

        Focus();
        isDragging = true;
        Capture = true;
        UpdateRatioFromMouse(eventArgs.X);
    }

    protected override void OnMouseMove(MouseEventArgs eventArgs)
    {
        base.OnMouseMove(eventArgs);
        if (isDragging)
        {
            UpdateRatioFromMouse(eventArgs.X);
        }
    }

    protected override void OnMouseUp(MouseEventArgs eventArgs)
    {
        base.OnMouseUp(eventArgs);
        if (eventArgs.Button != MouseButtons.Left || !isDragging)
        {
            return;
        }

        UpdateRatioFromMouse(eventArgs.X);
        isDragging = false;
        Capture = false;
    }

    protected override void OnMouseCaptureChanged(EventArgs eventArgs)
    {
        base.OnMouseCaptureChanged(eventArgs);
        if (!Capture)
        {
            isDragging = false;
        }
    }

    private void UpdateRatioFromMouse(int mouseX)
    {
        int markerDiameter = Math.Min(16, Height);
        Rectangle trackRectangle = GetTrackRectangle(markerDiameter, Math.Min(8, Math.Max(4, Height / 2)));
        double newRatio = Math.Clamp(
            (mouseX - trackRectangle.Left) / (double)Math.Max(1, trackRectangle.Width),
            0D,
            1D);

        if (Math.Abs(newRatio - ratio) < 0.000000001)
        {
            return;
        }

        ratio = newRatio;
        Invalidate();
        RatioChanged?.Invoke(this, EventArgs.Empty);
    }

    private Rectangle GetTrackRectangle(int markerDiameter, int trackHeight)
    {
        int markerRadius = markerDiameter / 2;
        return new Rectangle(
            markerRadius,
            (Height - trackHeight) / 2,
            Math.Max(1, Width - markerDiameter),
            trackHeight);
    }
}
