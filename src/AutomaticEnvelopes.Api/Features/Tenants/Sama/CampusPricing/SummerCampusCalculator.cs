namespace AutomaticEnvelopes.Api.Features.Tenants.Sama.CampusPricing;

public static class SummerCampusCalculator
{
    public static ParticipantArgs? GetSiblingWithLeastWeeks(CampusFamilyArguments args)
    {
        if (args.FamilyDiscountType != "Germa" || args.Participants.Count <= 1)
        {
            return null;
        }

        var campusParticipants = args.Participants.Where(p => p.CampusWeeks > 0).ToList();

        return campusParticipants.Count > 1
            ? campusParticipants.OrderBy(p => p.CampusWeeks).First()
            : null;
    }

    public static ParticipantPricingResult Calculate(ParticipantArgs p, CampusFamilyArguments args, ParticipantArgs? siblingWithLeastWeeks)
    {
        decimal campusBasePrice = GetCampusBasePrice(p.CampusWeeks, args.IsSocio);
        decimal tecniBasePrice = GetTecnificacioBasePrice(p.TecnificacioWeeks, args.IsSocio);

        if (args.IsAfterMay1st)
        {
            campusBasePrice *= 1.10m;
            tecniBasePrice *= 1.10m;
        }

        decimal discountMultiplier = 0m;

        if (p.CampusWeeks > 0)
        {
            if (args.FamilyDiscountType == "Familia Nombrosa" || args.FamilyDiscountType == "Familia Monoparental")
            {
                discountMultiplier = 0.15m;
            }
            else if (args.FamilyDiscountType == "Germa" && p == siblingWithLeastWeeks)
            {
                discountMultiplier = 0.10m;
            }
        }

        var campusDiscountAmount = campusBasePrice * discountMultiplier;
        var finalCampusPrice = campusBasePrice - campusDiscountAmount;

        var servicesCost = (p.MenjadorDays * 10m) + (p.TardaDays * 6m);
        var excursionsCost = (decimal)p.ExcursionsCost;

        var participantTotal = finalCampusPrice + tecniBasePrice + servicesCost + excursionsCost;

        return new ParticipantPricingResult(
            Name: p.Name,
            CampusWeeks: p.CampusWeeks,
            SeptemberDays: 0,
            TecnificacioWeeks: p.TecnificacioWeeks,
            CampusBasePrice: campusBasePrice,
            TecnificacioBasePrice: tecniBasePrice,
            DiscountApplied: campusDiscountAmount,
            ServicesCost: servicesCost,
            ExcursionsCost: excursionsCost,
            Total: participantTotal
        );
    }

    private static decimal GetCampusBasePrice(int weeks, bool isSocio)
    {
        var clampedWeeks = Math.Clamp(weeks, 0, 6);
        if (clampedWeeks == 0) return 0m;

        if (isSocio)
        {
            return clampedWeeks switch { 1 => 63.00m, 2 => 125.00m, 3 => 188.00m, 4 => 244.00m, 5 => 290.00m, 6 => 322.00m, _ => 0m };
        }
        else
        {
            return clampedWeeks switch { 1 => 69.00m, 2 => 138.00m, 3 => 206.00m, 4 => 268.00m, 5 => 320.00m, 6 => 354.00m, _ => 0m };
        }
    }

    private static decimal GetTecnificacioBasePrice(int weeks, bool isSocio)
    {
        var clampedWeeks = Math.Clamp(weeks, 0, 4);
        if (clampedWeeks == 0) return 0m;

        if (isSocio)
        {
            return clampedWeeks switch { 1 => 50.00m, 2 => 95.00m, 3 => 140.00m, 4 => 185.00m, _ => 0m };
        }
        else
        {
            return clampedWeeks switch { 1 => 55.00m, 2 => 105.00m, 3 => 154.00m, 4 => 204.00m, _ => 0m };
        }
    }
}