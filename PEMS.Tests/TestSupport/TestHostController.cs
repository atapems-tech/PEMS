using Microsoft.AspNetCore.Mvc;

namespace PEMS.Tests.TestSupport;

/// <summary>A controller with no actions that view-rendering tests use as the host (the DCC controller was used for this before SCRUM-838 deleted it).</summary>
public sealed class TestHostController : Controller
{
}
