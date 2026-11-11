using System.Globalization;

namespace Examples.Srp.Before;

/// <summary>
/// BEFORE: one class ingests, checks credit and writes the invoice. The risk team owns the credit
/// check and the finance team owns the invoice, but both go through the same rounding helper.
/// </summary>
public sealed class OrderPipeline(int amountDecimals)
{
    public PipelineResult Process(string csvLine, decimal creditLimit)
    {
        var fields = csvLine.Split(',');
        var customer = fields[0];
        var amount = Round(decimal.Parse(fields[1], NumberStyles.Number, CultureInfo.InvariantCulture));
        if (amount > creditLimit)
        {
            return new PipelineResult(false, null);
        }
        return new PipelineResult(true, $"INVOICE {customer} {amount.ToString(CultureInfo.InvariantCulture)}");
    }

    // Shared by the credit check and the invoice. Finance asks for whole units on invoices,
    // someone sets amountDecimals to 0, and the credit check now compares a rounded amount.
    private decimal Round(decimal value) => Math.Round(value, amountDecimals, MidpointRounding.AwayFromZero);
}

public sealed record PipelineResult(bool Accepted, string? Invoice);
