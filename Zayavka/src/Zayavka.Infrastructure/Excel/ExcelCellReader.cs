using System.Globalization;
using ClosedXML.Excel;

namespace Zayavka.Infrastructure.Excel;

/// <summary>
/// Хелперы чтения значений ячеек ClosedXML в текст.
/// Повторяют логику исходного проекта на Free Pascal:
/// дата → <c>dd.MM.yyyy</c>, число → целое или с дробной частью,
/// время → <c>HH:mm</c>.
/// </summary>
public static class ExcelCellReader
{
    /// <summary>Читает значение ячейки как текст (дата, число или строка).</summary>
    public static string ReadText(IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return string.Empty;
        }

        switch (cell.DataType)
        {
            case XLDataType.DateTime:
                return cell.GetDateTime().ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

            case XLDataType.Number:
            {
                var value = cell.GetDouble();
                if (Math.Abs(value) < 1E15 && Math.Abs(value - Math.Round(value)) < 1E-9)
                {
                    return ((long)Math.Round(value)).ToString(CultureInfo.InvariantCulture);
                }

                return value.ToString(CultureInfo.InvariantCulture);
            }

            case XLDataType.Boolean:
                return cell.GetBoolean() ? "TRUE" : "FALSE";

            default:
                return cell.GetString().Trim();
        }
    }

    /// <summary>Читает значение ячейки как время в формате <c>HH:mm</c>.</summary>
    public static string ReadTime(IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return string.Empty;
        }

        if (cell.DataType == XLDataType.DateTime)
        {
            return cell.GetDateTime().ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        if (cell.DataType == XLDataType.Number)
        {
            return FractionToTime(cell.GetDouble());
        }

        return NormalizeTime(cell.GetString());
    }

    /// <summary>Преобразует долю суток Excel (например, 0.7083) в <c>HH:mm</c>.</summary>
    public static string FractionToTime(double value)
    {
        var fraction = value - Math.Floor(value);
        var time = TimeSpan.FromDays(fraction);
        return $"{time.Hours:00}:{time.Minutes:00}";
    }

    /// <summary>Приводит строку («17:00», «17.00», «0,7083») к виду <c>HH:mm</c>.</summary>
    public static string NormalizeTime(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return string.Empty;
        }

        if (text.Contains(':'))
        {
            var parts = text.Split(':');
            if (parts.Length >= 2 &&
                int.TryParse(parts[0], out var hour) &&
                int.TryParse(parts[1], out var minute))
            {
                return $"{hour:00}:{minute:00}";
            }

            return text;
        }

        if (double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric))
        {
            return FractionToTime(numeric);
        }

        return text;
    }
}
