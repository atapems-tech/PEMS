using PEMS.Licensing.Evaluation;

namespace PEMS.Web.Licensing;

public sealed record LicensePageModel(LicenseStatus Status, bool Enforce);
