namespace RentAgency.Web.Features.Rentals.Domain;

public class RentalTerms
{
    public DateOnly Start { get; set; }

    public DateOnly End { get; set; }

    public decimal MonthlyPrice { get; set; }

    public decimal Deposit { get; set; }

    public RentalTerms()
    {
    }

    public RentalTerms(DateOnly start, DateOnly end, decimal monthlyPrice, decimal deposit)
    {
        Start = start;
        End = end;
        MonthlyPrice = monthlyPrice;
        Deposit = deposit;
    }
}
