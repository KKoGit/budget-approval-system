using BudgetApproval.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BudgetApproval.Infrastructure.Persistence;

/// <summary>
/// Seeds a fictional agency, the Northbridge Regional Services Agency. All names, amounts and
/// e-mail addresses are invented.
/// <para>
/// Requests are created by calling the same domain methods the API uses (Create → Submit → Approve …),
/// so the seed data obeys every business rule and arrives with a genuine audit trail. Dates are relative
/// to "now" so the approval queue always shows realistic waiting times.
/// </para>
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(BudgetDbContext db, TimeProvider clock, CancellationToken ct = default)
    {
        if (await db.Departments.AnyAsync(ct)) return;

        var now = clock.GetUtcNow().UtcDateTime;
        DateTime DaysAgo(double days) => now.AddDays(-days);

        // ---------------------------------------------------------------- departments
        var its = new Department("ITS", "Information Technology Services");
        var fac = new Department("FAC", "Facilities & Fleet");
        var outreach = new Department("OUT", "Public Outreach");
        var hcm = new Department("HCM", "Human Capital Management");
        var rsa = new Department("RSA", "Research & Analytics");
        var bud = new Department("BUD", "Budget Office");
        db.Departments.AddRange(its, fac, outreach, hcm, rsa, bud);
        await db.SaveChangesAsync(ct);

        // ---------------------------------------------------------------- users
        var maya = new AppUser("Maya Chen", "maya.chen@northbridge.example", its.Id, UserRole.Requester);
        var luis = new AppUser("Luis Ortega", "luis.ortega@northbridge.example", fac.Id, UserRole.Requester);
        var priya = new AppUser("Priya Natarajan", "priya.natarajan@northbridge.example", outreach.Id, UserRole.Requester);
        var samuel = new AppUser("Samuel Okafor", "samuel.okafor@northbridge.example", hcm.Id, UserRole.Requester);
        // Dana heads Research & Analytics and holds delegated approval authority: she can raise requests AND approve
        // other departments' requests, which is exactly the situation the self-approval rule exists for.
        var dana = new AppUser("Dana Whitfield", "dana.whitfield@northbridge.example", rsa.Id, UserRole.Requester | UserRole.Approver);
        var marcus = new AppUser("Marcus Bell", "marcus.bell@northbridge.example", bud.Id, UserRole.Approver);
        db.Users.AddRange(maya, luis, priya, samuel, dana, marcus);
        await db.SaveChangesAsync(ct);

        // ---------------------------------------------------------------- allocations
        var allocations = new Dictionary<(int, int), DepartmentAllocation>();
        void Allocate(Department d, int fy, decimal amount) => allocations[(d.Id, fy)] = new DepartmentAllocation(d.Id, fy, amount);

        Allocate(its, 2026, 1_200_000m); Allocate(its, 2027, 1_300_000m);
        Allocate(fac, 2026, 500_000m);   Allocate(fac, 2027, 550_000m);
        Allocate(outreach, 2026, 250_000m); Allocate(outreach, 2027, 260_000m);
        Allocate(hcm, 2026, 400_000m);   Allocate(hcm, 2027, 420_000m);
        Allocate(rsa, 2026, 350_000m);   Allocate(rsa, 2027, 380_000m);
        db.Allocations.AddRange(allocations.Values);

        // ---------------------------------------------------------------- requests
        var requests = new List<BudgetRequest>();

        BudgetRequest Draft(AppUser who, int fy, BudgetCategory cat, string title, string why, decimal amount, double createdDaysAgo)
        {
            var r = BudgetRequest.Create(who.DepartmentId, fy, cat, title, why, amount, who.AsActor(), DaysAgo(createdDaysAgo));
            requests.Add(r);
            return r;
        }

        BudgetRequest Submitted(AppUser who, int fy, BudgetCategory cat, string title, string why, decimal amount, double createdDaysAgo, double submittedDaysAgo)
        {
            var r = Draft(who, fy, cat, title, why, amount, createdDaysAgo);
            r.Submit(who.AsActor(), DaysAgo(submittedDaysAgo));
            return r;
        }

        void Approve(BudgetRequest r, AppUser approver, double daysAgo, decimal? amount = null, string? comment = null)
        {
            var approved = amount ?? r.RequestedAmount;
            r.Approve(approver.AsActor(), approved, comment, DaysAgo(daysAgo));
            allocations[(r.DepartmentId, r.FiscalYear)].CommitApproval(approved);
        }

        // FY2026 — the year now closing. Mostly decided.
        Approve(Submitted(maya, 2026, BudgetCategory.Equipment, "Laptop refresh for field inspectors",
            "Replaces 140 inspector laptops that are past their five-year support window and can no longer receive security patches.",
            420_000m, 210, 205), marcus, 190);

        Approve(Submitted(maya, 2026, BudgetCategory.Software, "Security awareness training platform",
            "Annual phishing simulation and training licenses for all 1,100 staff, required by the agency's information security policy.",
            85_000m, 180, 178), marcus, 170, 60_000m,
            "Approved for the core license tier only. The advanced analytics add-on can be requested in FY2027.");

        var cooling = Submitted(maya, 2026, BudgetCategory.ProfessionalServices, "Data center cooling assessment",
            "Independent engineering assessment of cooling capacity in the primary server room ahead of planned equipment growth.",
            150_000m, 160, 158);
        cooling.Reject(dana.AsActor(), "Facilities & Fleet already has this assessment in its FY2026 HVAC scope. Coordinate with Luis Ortega instead.", DaysAgo(150));

        Approve(Submitted(luis, 2026, BudgetCategory.Equipment, "Electric vehicle fleet pilot",
            "Six electric pool vehicles and two charging stations for the downtown campus, to test total cost of ownership against gasoline vehicles.",
            310_000m, 200, 198), marcus, 185);

        Approve(Submitted(luis, 2026, BudgetCategory.Facilities, "HVAC controls upgrade, Building C",
            "Replaces the building management controller in Building C, which fails several times a month and has no spare parts available.",
            150_000m, 150, 149), dana, 140);

        // Deliberately larger than what Facilities has left this year (40,000): shows the allocation ceiling rule.
        Submitted(luis, 2026, BudgetCategory.Facilities, "Emergency roof repair, Warehouse 2",
            "Storm damage in August left three active leaks above the records storage area. Temporary tarps will not last through winter.",
            75_000m, 9, 8);

        Approve(Submitted(priya, 2026, BudgetCategory.Travel, "Regional community listening sessions",
            "Eight evening sessions across the region to gather public input on service hours, including venue hire and staff mileage.",
            38_500m, 175, 172), marcus, 165);

        var multilingual = Submitted(priya, 2026, BudgetCategory.ProfessionalServices, "Multilingual outreach materials",
            "Translation and printing of service guides in the five most-requested languages identified in the 2025 community survey.",
            62_000m, 30, 28);
        multilingual.ReturnForRevision(marcus.AsActor(),
            "Please separate translation costs from printing costs and attach at least two vendor quotes for each.", DaysAgo(24));

        Approve(Submitted(samuel, 2026, BudgetCategory.Training, "New supervisor leadership cohort",
            "A six-month leadership program for 24 newly promoted supervisors, delivered by an external provider.",
            96_000m, 190, 188), dana, 180);

        Submitted(samuel, 2026, BudgetCategory.ProfessionalServices, "Recruitment marketing campaign",
            "Targeted job advertising for hard-to-fill engineering and data roles, which have been vacant for an average of 94 days.",
            54_000m, 14, 12);

        Approve(Submitted(dana, 2026, BudgetCategory.Software, "Statistical software licenses",
            "Renewal of twelve statistical analysis licenses used for the agency's quarterly performance reporting.",
            48_000m, 220, 218), marcus, 210);

        // Dana's own pending request: when signed in as Dana, the queue shows it but blocks her from deciding it.
        Submitted(dana, 2026, BudgetCategory.Software, "Survey data collection platform",
            "Replaces three separate survey tools with one platform that meets accessibility and data-residency requirements.",
            120_000m, 6, 5);

        // FY2027 — planning cycle for the year starting 1 October.
        Submitted(maya, 2027, BudgetCategory.Equipment, "Network core switch replacement",
            "The core switches reach end of vendor support in March 2027. Replacement must be procured early to allow a phased cut-over.",
            640_000m, 20, 18);

        Draft(maya, 2027, BudgetCategory.ProfessionalServices, "Service desk contract renewal",
            "Three-year renewal of the outsourced after-hours service desk. Current contract expires 31 December 2026.",
            210_000m, 4);

        Draft(luis, 2027, BudgetCategory.Facilities, "Parking lot resurfacing, north campus",
            "Resurfacing and re-striping of the north campus staff lot, which has failed its last two safety inspections.",
            180_000m, 3);

        Submitted(priya, 2027, BudgetCategory.ProfessionalServices, "Annual public report design",
            "Design and accessible layout of the FY2026 annual report for print and web publication.",
            28_000m, 11, 10);

        Submitted(samuel, 2027, BudgetCategory.Personnel, "HR data analyst position (1 FTE)",
            "A permanent analyst to own workforce reporting, currently spread across three generalists at the cost of other work.",
            118_000m, 7, 3);

        Draft(dana, 2027, BudgetCategory.Travel, "Research conference attendance",
            "Registration and travel for four analysts to present agency research at two national public-administration conferences.",
            22_500m, 2);

        db.BudgetRequests.AddRange(requests);
        await db.SaveChangesAsync(ct);
    }
}
