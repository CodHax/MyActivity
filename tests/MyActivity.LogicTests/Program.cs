using MyActivity.Helpers;

// Tiny dependency-free test runner. Exit code 0 = all passed.
var failures = 0;
var total = 0;

void Check(string name, bool condition, string? detail = null)
{
    total++;
    if (condition) { Console.WriteLine($"  PASS  {name}"); return; }
    failures++;
    Console.WriteLine($"  FAIL  {name} {detail}");
}
void Near(string name, decimal actual, decimal expected, decimal tol = 0.02m) =>
    Check(name, Math.Abs(actual - expected) <= tol, $"(got {actual}, expected {expected})");

string Calc(params string[] keys)
{
    var c = new BasicCalculator();
    foreach (var k in keys) c.Press(k);
    return c.Display;
}

Console.WriteLine("Password hashing");
var hash = PasswordHasher.Hash("Secret123");
Check("hash is not the plain password", !hash.Contains("Secret123"));
Check("correct password verifies", PasswordHasher.Verify("Secret123", hash));
Check("wrong password rejected", !PasswordHasher.Verify("secret123", hash));
Check("two hashes of same password differ (random salt)", hash != PasswordHasher.Hash("Secret123"));
Check("garbage hash does not throw / verifies false", !PasswordHasher.Verify("x", "not-a-hash"));
Check("fresh hash needs no rehash", !PasswordHasher.NeedsRehash(hash));

Console.WriteLine("Validators");
Check("empty email", Validators.ValidateEmail("") == "Please enter your email.");
Check("bad email", Validators.ValidateEmail("abc@x") == "Invalid email address.");
Check("good email", Validators.ValidateEmail("a.b@company.com") is null);
Check("empty employee id", Validators.ValidateEmployeeId(" ") is not null);
Check("good employee id", Validators.ValidateEmployeeId("EMP-1024") is null);
Check("weak password (short)", Validators.ValidatePassword("a1") is not null);
Check("weak password (no digit)", Validators.ValidatePassword("abcdefgh") is not null);
Check("good password", Validators.ValidatePassword("abcdefg1") is null);
Check("confirm mismatch", Validators.ValidateConfirmPassword("a", "b") == "Passwords do not match.");

Console.WriteLine("Working days");
Check("Mon-Fri default: Monday works", WorkingDays.IsWorkingDay(WorkingDays.DefaultMask, DayOfWeek.Monday));
Check("Mon-Fri default: Sunday off", !WorkingDays.IsWorkingDay(WorkingDays.DefaultMask, DayOfWeek.Sunday));
Check("add Saturday", WorkingDays.IsWorkingDay(WorkingDays.Set(WorkingDays.DefaultMask, DayOfWeek.Saturday, true), DayOfWeek.Saturday));

Console.WriteLine("EMI");
var emi = FinancialCalculators.Emi(1_000_000m, 8.5m, 240);
Near("EMI 10L @8.5% 20y", emi.Emi, 8678.23m);
Near("total payment = EMI x n", emi.TotalPayment, emi.Emi * 240, 0.001m);
Near("interest = total - principal", emi.TotalInterest, emi.TotalPayment - 1_000_000m, 0.001m);
var zero = FinancialCalculators.Emi(120_000m, 0m, 12);
Near("0% rate EMI = P/n", zero.Emi, 10_000m);
Check("tenure years->months", FinancialCalculators.ToMonths(20, true) == 240 && FinancialCalculators.ToMonths(18, false) == 18);

Console.WriteLine("SIP");
var sip = FinancialCalculators.MonthlySip(5000m, 12m, 120);
Near("SIP invested", sip.TotalInvested, 600_000m, 0m);
Near("SIP maturity 5k @12% 10y", sip.MaturityValue, 1_161_695.38m);
Near("SIP returns = maturity - invested", sip.EstimatedReturns, 1_161_695.38m - 600_000m);
Near("0% SIP = just contributions", FinancialCalculators.MonthlySip(1000m, 0m, 12).MaturityValue, 12_000m);
var lump = FinancialCalculators.LumpSum(100_000m, 12m, 120);
Near("Lump sum 1L @12% 10y", lump.MaturityValue, 310_584.82m);

Console.WriteLine("SWP");
var swp = FinancialCalculators.Swp(1_000_000m, 8m, 8_000m, 120);
Near("SWP withdrawn", swp.TotalWithdrawn, 960_000m);
Near("SWP remaining", swp.RemainingAmount, 756_071.95m);
Check("SWP not exhausted", !swp.CorpusExhausted);
var swp2 = FinancialCalculators.Swp(500_000m, 6m, 10_000m, 120);
Check("SWP corpus exhausted", swp2.CorpusExhausted && swp2.RemainingAmount == 0m);
Check("SWP exhausted after 58 months", swp2.MonthsWithdrawn == 58, $"(got {swp2.MonthsWithdrawn})");
Near("SWP growth = remaining + withdrawn - initial", swp.EstimatedReturns, swp.RemainingAmount + swp.TotalWithdrawn - 1_000_000m, 0.001m);

Console.WriteLine("Calculator input validation");
Check("amount required", FinancialCalculators.ValidateAmount(null, "the loan amount") is not null);
Check("amount must be > 0", FinancialCalculators.ValidateAmount(0, "the loan amount") is not null);
Check("rate > 100 rejected", FinancialCalculators.ValidateRate(101, true) is not null);
Check("tenure > 50y rejected", FinancialCalculators.ValidateTenure(51, true) is not null);
Check("tenure fractional months rejected", FinancialCalculators.ValidateTenure(2.5m, false) is not null);
Check("tenure 1.5 years ok (18 months)", FinancialCalculators.ValidateTenure(1.5m, true) is null);

Console.WriteLine("Basic calculator");
Check("2 + 3 =", Calc("2", "+", "3", "=") == "5");
Check("12 - 5 =", Calc("1", "2", "-", "5", "=") == "7");
Check("6 x 7 =", Calc("6", "*", "7", "=") == "42");
Check("10 / 4 =", Calc("1", "0", "/", "4", "=") == "2.5");
Check("0.1 + 0.2 = 0.3 (decimal, not binary float)", Calc("0", ".", "1", "+", "0", ".", "2", "=") == "0.3");
Check("chain 2 + 3 x 4 = (immediate execution)", Calc("2", "+", "3", "*", "4", "=") == "20");
Check("divide by zero", Calc("5", "/", "0", "=") == "Cannot divide by zero");
Check("clear after error", Calc("5", "/", "0", "=", "C") == "0");
Check("digit after error starts fresh", Calc("5", "/", "0", "=", "7") == "7");
Check("backspace", Calc("1", "2", "3", "B") == "12");
Check("backspace to zero", Calc("7", "B") == "0");
Check("negate", Calc("5", "N") == "-5");
Check("decimal once only", Calc("1", ".", "2", ".", "3") == "1.23");
Check("200 + 10% = 220", Calc("2", "0", "0", "+", "1", "0", "%", "=") == "220");
Check("200 x 10% = 20", Calc("2", "0", "0", "*", "1", "0", "%", "=") == "20");
Check("50 % = 0.5", Calc("5", "0", "%") == "0.5");
Check("= repeated operand: 5 + = gives 10", Calc("5", "+", "=") == "10");
Check("operator can be changed before 2nd operand", Calc("9", "+", "-", "4", "=") == "5");
Check("new digit after = starts new calc", Calc("2", "+", "3", "=", "9") == "9");
Check("leading zeros collapse", Calc("0", "0", "7") == "7");
Check("max 15 digits", Calc(new string('9', 20).Select(c => c.ToString()).ToArray()).Length == 15);

Console.WriteLine("Money");
Check("ToMinor rounds half up", MoneyFormat.ToMinor(10.005m) == 1001);
Check("FromMinor", MoneyFormat.FromMinor(150050) == 1500.50m);
Check("Parse with grouping", MoneyFormat.Parse("1,500.50") == 1500.50m);
Check("Parse invalid", MoneyFormat.Parse("abc") is null);
Check("Parse empty", MoneyFormat.Parse("") is null);
Check("3rd decimal detected", MoneyFormat.HasMoreThanTwoDecimals(1.234m) && !MoneyFormat.HasMoreThanTwoDecimals(1.23m));
Console.WriteLine($"  (info) Format(1234567.5) = {MoneyFormat.Format(1234567.5m)}   [lakh grouping needs ICU; fine on real devices]");

Console.WriteLine();
Console.WriteLine(failures == 0 ? $"ALL {total} CHECKS PASSED" : $"{failures} OF {total} CHECKS FAILED");
return failures == 0 ? 0 : 1;
