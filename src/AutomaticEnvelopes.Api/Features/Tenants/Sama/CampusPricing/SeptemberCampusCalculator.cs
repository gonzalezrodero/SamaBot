namespace AutomaticEnvelopes.Api.Features.Tenants.Sama.CampusPricing;

public static class SeptemberCampusCalculator
{
    public static ParticipantPricingResult Calculate(ParticipantArgs p, CampusFamilyArguments args)
    {
        // 125€ per 10 dies, 13.50€ per dia esporàdic
        decimal campusBasePrice = p.SeptemberDays == 10 ? 125.00m : p.SeptemberDays * 13.50m;

        decimal discountAmount = 0m;

        // El descompte del 7% només s'aplica si fan el campus sencer (10 dies) i són socis
        if (args.IsSocio && p.SeptemberDays == 10)
        {
            discountAmount = campusBasePrice * 0.07m;
        }

        decimal finalCampusPrice = campusBasePrice - discountAmount;

        // Servei de menjador a 10€ el dia
        decimal servicesCost = p.MenjadorDays * 10m;
        decimal excursionsCost = (decimal)p.ExcursionsCost;

        decimal total = finalCampusPrice + servicesCost + excursionsCost;

        return new ParticipantPricingResult(
            Name: p.Name,
            CampusWeeks: 0,
            SeptemberDays: p.SeptemberDays,
            TecnificacioWeeks: 0,
            CampusBasePrice: campusBasePrice,
            TecnificacioBasePrice: 0m,
            DiscountApplied: discountAmount,
            ServicesCost: servicesCost,
            ExcursionsCost: excursionsCost,
            Total: total
        );
    }
}